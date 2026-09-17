[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [switch]$AllowDirtyEngineeringBuild
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedRoot = [IO.Path]::GetFullPath($repoRoot)
function Get-RelativePathCompat([string]$BasePath, [string]$FullPath) {
    $baseUri = [System.Uri]::new([IO.Path]::GetFullPath($BasePath).TrimEnd('\/') + '/')
    $fullUri = [System.Uri]::new([IO.Path]::GetFullPath($FullPath))
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($fullUri).ToString()).Replace('/', '\')
}
$resolvedOutput = if ([IO.Path]::IsPathRooted($OutputDirectory)) {
    [IO.Path]::GetFullPath($OutputDirectory)
} else {
    [IO.Path]::GetFullPath((Join-Path $resolvedRoot $OutputDirectory))
}
if ($resolvedOutput -eq $resolvedRoot -or $resolvedOutput -eq [IO.Path]::GetPathRoot($resolvedOutput)) {
    throw "OutputDirectory must be a dedicated artifact directory, not the repository or a filesystem root."
}
if (Test-Path $resolvedOutput) {
    if ((Get-ChildItem -LiteralPath $resolvedOutput -Force | Measure-Object).Count -gt 0) {
        throw "OutputDirectory must be empty; existing files will not be overwritten."
    }
} else { New-Item -ItemType Directory -Path $resolvedOutput | Out-Null }

$gitStatus = & git -C $resolvedRoot status --porcelain
$treeClean = [string]::IsNullOrWhiteSpace(($gitStatus -join "`n"))
if (-not $treeClean -and -not $AllowDirtyEngineeringBuild) {
    throw "Release packaging requires a clean Git worktree. Use AllowDirtyEngineeringBuild only for a non-release engineering check."
}
$commit = (& git -C $resolvedRoot rev-parse --short=12 HEAD).Trim()
$version = "1.5.3"
$previousSourceDateEpoch = $env:SOURCE_DATE_EPOCH
$sourceDateEpoch = 0L
if (-not [long]::TryParse($env:SOURCE_DATE_EPOCH, [ref]$sourceDateEpoch)) {
    $commitEpoch = (& git -C $resolvedRoot show -s --format=%ct HEAD).Trim()
    if (-not [long]::TryParse($commitEpoch, [ref]$sourceDateEpoch)) { throw "Unable to determine a reproducible source timestamp." }
}
if ($sourceDateEpoch -lt 315532800 -or $sourceDateEpoch -gt 4354819198) {
    throw "SOURCE_DATE_EPOCH must map to a UTC timestamp representable by ZIP (1980-01-01 through 2107-12-31)."
}
$buildTimestamp = [DateTimeOffset]::FromUnixTimeSeconds($sourceDateEpoch).UtcDateTime
$env:SOURCE_DATE_EPOCH = $sourceDateEpoch.ToString([Globalization.CultureInfo]::InvariantCulture)
$stage = Join-Path ([IO.Path]::GetTempPath()) "nexorsys-release-$([guid]::NewGuid().ToString('N'))"
$null = New-Item -ItemType Directory -Path $stage

try {
    # A release script must be runnable from a clean checkout. Do not rely on a
    # caller having populated obj/project.assets.json before EF or publish runs.
    & dotnet restore (Join-Path $resolvedRoot "backend/Nexorsys.Identity.Solution.sln") --nologo
    if ($LASTEXITCODE -ne 0) { throw "Solution restore failed." }

    $apiOut = Join-Path $stage "identity-api"
    $agentOut = Join-Path $stage "windows-agent"
    $kioskOut = Join-Path $stage "windows-kiosk"
    $webOut = Join-Path $stage "control-center"
    $dbOut = Join-Path $stage "database"
    $docsOut = Join-Path $stage "documentation"
    New-Item -ItemType Directory -Path $apiOut,$agentOut,$kioskOut,$webOut,$dbOut,$docsOut | Out-Null

    $previousDatabaseSetting = $env:NEXORSYS_DATABASE
    $previousJwtKey = $env:Jwt__Key
    try {
        if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DATABASE)) { $env:NEXORSYS_DATABASE = "Host=localhost;Database=nexorsys_release_script_generation;Username=unused;Password=unused" }
        if ([string]::IsNullOrWhiteSpace($env:Jwt__Key)) {
            $randomKey = [byte[]]::new(48)
            $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
            $rng.GetBytes($randomKey)
            $rng.Dispose()
            $env:Jwt__Key = [Convert]::ToBase64String($randomKey)
        }
        & dotnet ef migrations script --idempotent --project (Join-Path $resolvedRoot "backend/src/Nexorsys.Identity.Infrastructure/Nexorsys.Identity.Infrastructure.csproj") `
            --startup-project (Join-Path $resolvedRoot "backend/src/Nexorsys.Identity.API/Nexorsys.Identity.API.csproj") `
            --configuration Release --output (Join-Path $dbOut "migrations-idempotent.sql")
        if ($LASTEXITCODE -ne 0) { throw "Idempotent EF migration script generation failed." }
    } finally {
        $env:NEXORSYS_DATABASE = $previousDatabaseSetting
        $env:Jwt__Key = $previousJwtKey
    }

    $pathMap = "$resolvedRoot=/src"
    & dotnet publish (Join-Path $resolvedRoot "backend/src/Nexorsys.Identity.API/Nexorsys.Identity.API.csproj") -c Release -o $apiOut -p:UseAppHost=false "-p:PathMap=$pathMap" -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "Identity API publish failed." }
    & dotnet publish (Join-Path $resolvedRoot "backend/src/Nexorsys.WindowsAgent/Nexorsys.WindowsAgent.csproj") -c Release -r win-x64 --self-contained false -o $agentOut "-p:PathMap=$pathMap" -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "Windows Agent publish failed." }
    & dotnet publish (Join-Path $resolvedRoot "kiosk-nfc/Nexorsys.NFC.APP.csproj") -c Release -r win-x64 --self-contained false -o $kioskOut "-p:PathMap=$pathMap" -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "Windows NFC Kiosk publish failed." }

    $frontend = Join-Path $resolvedRoot "frontend/nexorsys-identity-ui"
    Push-Location $frontend
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw "Frontend dependency installation failed." }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw "Frontend production build failed." }
    } finally { Pop-Location }
    Copy-Item -Path (Join-Path $frontend "dist\*") -Destination $webOut -Recurse -Force

    foreach ($directory in @($apiOut,$agentOut,$kioskOut,$webOut)) {
        Get-ChildItem -LiteralPath $directory -Recurse -File | Where-Object { $_.Name -match '\.(pdb|map|env)$' -or $_.Name -in 'custom_settings.json','appsettings.Development.json' } |
            Remove-Item -Force
    }
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "docs/production-database-and-key-management.md") -Destination $docsOut
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "docs/phase5-authorization-matrix.md") -Destination $docsOut
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "docs/phase5-security-operations.md") -Destination $docsOut
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "docs/phase6-product-capability-gaps.md") -Destination $docsOut
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "docs/NEXORSYS_COMMERCIAL_README.md") -Destination (Join-Path $docsOut "README.md")
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "NEXORSYS_PHASE5_FINAL_SECURITY_REPORT.md") -Destination $docsOut
    Copy-Item -LiteralPath (Join-Path $resolvedRoot "NEXORSYS_PHASE6_SOFTWARE_CLOSURE_REPORT.md") -Destination $docsOut
    if (Test-Path (Join-Path $resolvedRoot "NEXORSYS_FINAL_COMMERCIAL_READINESS_REPORT.md")) { Copy-Item -LiteralPath (Join-Path $resolvedRoot "NEXORSYS_FINAL_COMMERCIAL_READINESS_REPORT.md") -Destination $docsOut }
    if (Test-Path (Join-Path $resolvedRoot "NEXORSYS_PRODUCT_OVERVIEW.md")) { Copy-Item -LiteralPath (Join-Path $resolvedRoot "NEXORSYS_PRODUCT_OVERVIEW.md") -Destination $docsOut }
    if (Test-Path (Join-Path $resolvedRoot "SECURITY_VALIDATION_CHECKLIST.md")) { Copy-Item -LiteralPath (Join-Path $resolvedRoot "SECURITY_VALIDATION_CHECKLIST.md") -Destination $docsOut }
    & dotnet run --configuration Release --project (Join-Path $resolvedRoot "backend/tools/Nexorsys.RouteInventory/Nexorsys.RouteInventory.csproj") -- --output (Join-Path $docsOut "api-route-inventory.json")
    if ($LASTEXITCODE -ne 0) { throw "API route inventory generation failed." }
    Set-Content -LiteralPath (Join-Path $stage "SIGNING_REQUIRED.txt") -Encoding utf8 -Value @(
        "This engineering package is unsigned.",
        "Before commercial distribution, sign every executable, library, and installer with the customer-approved production certificate and timestamp service.",
        "The native Credential Provider MSI is intentionally not included: required native installer build/signing tools and Winlogon validation are not available in this pipeline."
    )

    $forbiddenFileNames = @("custom_settings.json", ".env", "appsettings.Development.json")
    $secretPatterns = @(
        '-----BEGIN (RSA|EC|OPENSSH|PRIVATE) KEY-----',
        '(?im)(password|passwd|secret|api[_-]?key|clientsecret)\s*[:=]\s*["''][^"'']{8,}',
        'eyJ[A-Za-z0-9_-]{15,}\.[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}',
        'DevelopmentOwnerBypass',
        'Administrateur Nexorsys',
        'dev_key'
    )
    foreach ($file in Get-ChildItem -LiteralPath $stage -Recurse -File) {
        if ($forbiddenFileNames -contains $file.Name) { throw "Forbidden local configuration artifact in package: $($file.Name)" }
        $bytes = [IO.File]::ReadAllBytes($file.FullName)
        $utf8 = [Text.Encoding]::UTF8.GetString($bytes)
        $utf16 = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($pattern in $secretPatterns) {
            if ($utf8 -match $pattern -or $utf16 -match $pattern) { throw "Potential credential material detected in release artifact: $(Get-RelativePathCompat $stage $file.FullName)" }
        }
        if ($utf8 -match '(?i)[A-Z]:\\Users\\[^\\\s]+\\|D:\\Innovera' -or $utf16 -match '(?i)[A-Z]:\\Users\\[^\\\s]+\\|D:\\Innovera') {
            throw "Personal or customer-specific absolute path detected in release artifact: $(Get-RelativePathCompat $stage $file.FullName)"
        }
    }

    $packages = [Collections.Generic.List[object]]::new()
    $lockPath = Join-Path $frontend "package-lock.json"
    $npmEntries = & node -e "const p=require(process.argv[1]); for (const [k,v] of Object.entries(p.packages||{})) if(v.version) console.log(JSON.stringify([k,v.version]));" $lockPath
    if ($LASTEXITCODE -ne 0) { throw "Unable to read npm lockfile for SBOM generation." }
    foreach ($entryJson in $npmEntries) {
        $entry = $entryJson | ConvertFrom-Json
        $pkgName = [string]$entry[0] -replace '^node_modules/',''
        $pkgVersion = [string]$entry[1]
        if ($pkgName -and $pkgVersion) {
            $packages.Add([ordered]@{ SPDXID = "SPDXRef-Npm-$($packages.Count)"; name = $pkgName; versionInfo = $pkgVersion; downloadLocation = "NOASSERTION"; filesAnalyzed = $false; licenseConcluded = "NOASSERTION"; licenseDeclared = "NOASSERTION" })
        }
    }
    foreach ($projectFile in (Get-ChildItem (Join-Path $resolvedRoot "backend/src") -Filter "*.csproj" -Recurse | Sort-Object FullName -CaseSensitive)) {
        [xml]$projectXml = Get-Content -LiteralPath $projectFile.FullName -Raw
        foreach ($reference in $projectXml.SelectNodes("//PackageReference")) {
            $name = $reference.Include
            $packageVersion = $reference.Version
            if (-not $packageVersion -and $reference.ChildNodes) { $packageVersion = $reference.ChildNodes | Where-Object LocalName -eq "Version" | Select-Object -First 1 -ExpandProperty InnerText }
            if ($name -and $packageVersion) {
                $packages.Add([ordered]@{ SPDXID = "SPDXRef-DotNet-$($packages.Count)"; name = $name; versionInfo = $packageVersion; downloadLocation = "NOASSERTION"; filesAnalyzed = $false; licenseConcluded = "NOASSERTION"; licenseDeclared = "NOASSERTION" })
            }
        }
    }
    $sbom = [ordered]@{
        spdxVersion = "SPDX-2.3"; dataLicense = "CC0-1.0"; SPDXID = "SPDXRef-DOCUMENT"
        name = "NexorSys Identity + Kiosk $version ($commit)"
        documentNamespace = "urn:nexorsys:spdx:${version}:${commit}"
        creationInfo = [ordered]@{ creators = @("Tool: NexorSys build-release.ps1"); created = $buildTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ") }
        packages = $packages
    }
    $sbom | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath (Join-Path $stage "SBOM.spdx.json") -Encoding utf8

    $artifactFiles = Get-ChildItem -LiteralPath $stage -Recurse -File | Sort-Object FullName
    $manifest = [ordered]@{
        product = "NexorSys Identity + Kiosk"; version = $version; sourceCommit = $commit; sourceTreeClean = $treeClean
        buildUtc = $buildTimestamp.ToString("yyyy-MM-ddTHH:mm:ssZ")
        signed = $false; commercialRelease = $false
        excluded = @("tests", "SQL backups", "local configuration and secrets", "source maps", "old MSI artifacts", "developer build directories")
        artifacts = @($artifactFiles | ForEach-Object { [ordered]@{ path = ((Get-RelativePathCompat $stage $_.FullName) -replace '\\','/'); sha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant() } })
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $stage "release-manifest.json") -Encoding utf8
    $checksumLines = Get-ChildItem -LiteralPath $stage -Recurse -File | Where-Object Name -ne "SHA256SUMS.txt" | Sort-Object FullName | ForEach-Object {
        "{0}  {1}" -f (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash.ToLowerInvariant(), ((Get-RelativePathCompat $stage $_.FullName) -replace '\\','/')
    }
    $checksumLines | Set-Content -LiteralPath (Join-Path $stage "SHA256SUMS.txt") -Encoding ascii

    $zipPath = Join-Path $resolvedOutput "NexorSys-Identity-Kiosk-$version-$commit.zip"
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zipStream = [IO.File]::Open($zipPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $archive = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create, $false)
        try {
            $stagePrefix = $stage.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
            $zipEntries = Get-ChildItem -LiteralPath $stage -Recurse -File | ForEach-Object {
                [pscustomobject]@{ File = $_; Name = ((Get-RelativePathCompat $stage $_.FullName) -replace '\\','/') }
            } | Sort-Object Name -CaseSensitive
            foreach ($zipEntry in $zipEntries) {
                if ([IO.Path]::IsPathRooted($zipEntry.Name) -or $zipEntry.Name -match '(^|/)\.\.(/|$)') {
                    throw "Refusing to create an archive entry outside the staging root: $($zipEntry.Name)"
                }
                $entry = $archive.CreateEntry($zipEntry.Name, [IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new($buildTimestamp)
                $entry.ExternalAttributes = 0
                $entryStream = $entry.Open()
                try {
                    $inputStream = [IO.File]::OpenRead($zipEntry.File.FullName)
                    try { $inputStream.CopyTo($entryStream) } finally { $inputStream.Dispose() }
                } finally { $entryStream.Dispose() }
            }
        } finally { $archive.Dispose() }
    } finally { $zipStream.Dispose() }
    Write-Output "Created unsigned engineering package: $zipPath"
    Write-Output "Source worktree clean: $treeClean; commercial release: false; signature: absent."
}
finally {
    if (Test-Path -LiteralPath $stage) { # Remove-Item -LiteralPath $stage -Recurse -Force 
    Write-Output "Preserved stage at: $stage" }
    if ($null -eq $previousSourceDateEpoch) { Remove-Item Env:\SOURCE_DATE_EPOCH -ErrorAction SilentlyContinue }
    else { $env:SOURCE_DATE_EPOCH = $previousSourceDateEpoch }
}
