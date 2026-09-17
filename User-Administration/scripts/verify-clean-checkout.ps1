[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$OutputDirectory,
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output -eq [IO.Path]::GetPathRoot($output) -or $output -eq $repoRoot -or
    $output.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be a dedicated artifact directory outside the checkout.'
}
if (Test-Path -LiteralPath $output) {
    if ((Get-ChildItem -LiteralPath $output -Force | Measure-Object).Count -gt 0) {
        throw 'OutputDirectory must be empty; existing files will not be overwritten.'
    }
} else { New-Item -ItemType Directory -Path $output | Out-Null }

$status = & git -C $repoRoot status --porcelain
if (-not [string]::IsNullOrWhiteSpace(($status -join "`n"))) {
    throw 'Clean-checkout verification requires a clean Git tree; no engineering override is permitted.'
}

Push-Location $repoRoot
try {
    & dotnet restore backend/Nexorsys.Identity.Solution.sln
    if ($LASTEXITCODE -ne 0) { throw 'Solution restore failed.' }
    & dotnet build backend/Nexorsys.Identity.Solution.sln -c $Configuration --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed.' }
    & (Join-Path $repoRoot 'scripts/test-all.ps1') -Configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw 'Integrated PostgreSQL, security and Windows tests failed.' }
    & dotnet build kiosk-nfc/Nexorsys.NFC.APP.csproj -c $Configuration --no-restore -warnaserror
    if ($LASTEXITCODE -ne 0) { throw 'Kiosk build failed.' }

    $frontend = Join-Path $repoRoot 'frontend/nexorsys-identity-ui'
    Push-Location $frontend
    try {
        & npm ci
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
        & npm test -- --run
        if ($LASTEXITCODE -ne 0) { throw 'Frontend tests failed.' }
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw 'Frontend production build failed.' }
        & npm audit --omit=dev --audit-level=high
        if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency audit failed.' }
    } finally { Pop-Location }

    & (Join-Path $repoRoot 'scripts/build-release.ps1') -OutputDirectory $output
    if ($LASTEXITCODE -ne 0) { throw 'Release package build or embedded artifact scan failed.' }

    $zip = Get-ChildItem -LiteralPath $output -Filter '*.zip' -File | Select-Object -First 1
    if (-not $zip) { throw 'Release package was not produced.' }
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip.FullName)
    try {
        foreach ($required in @('SBOM.spdx.json', 'release-manifest.json', 'SHA256SUMS.txt',
                'documentation/api-route-inventory.json', 'documentation/phase6-product-capability-gaps.md',
                'documentation/NEXORSYS_PHASE5_FINAL_SECURITY_REPORT.md',
                'documentation/NEXORSYS_PHASE6_SOFTWARE_CLOSURE_REPORT.md')) {
            if (-not $archive.GetEntry($required)) { throw "Required release package entry missing: $required" }
        }
        $manifestReader = [IO.StreamReader]::new($archive.GetEntry('release-manifest.json').Open())
        try { $manifest = $manifestReader.ReadToEnd() | ConvertFrom-Json }
        finally { $manifestReader.Dispose() }
        if (-not $manifest.sourceTreeClean -or $manifest.commercialRelease -or $manifest.signed) {
            throw 'Clean checkout package metadata is inconsistent; commercial/signed status must not be fabricated.'
        }
        $checksumReader = [IO.StreamReader]::new($archive.GetEntry('SHA256SUMS.txt').Open())
        try { $checksums = @($checksumReader.ReadToEnd() -split "`r?`n" | Where-Object { $_ }) }
        finally { $checksumReader.Dispose() }
        foreach ($line in $checksums) {
            $parts = $line -split '  ', 2
            $entry = $archive.GetEntry($parts[1].Replace('\', '/'))
            if (-not $entry) { throw "Checksum entry missing from archive: $($parts[1])" }
            $stream = $entry.Open()
            try {
                $sha = [Security.Cryptography.SHA256]::Create()
                $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
                $sha.Dispose()
            } finally { $stream.Dispose() }
            if ($actual -ne $parts[0]) { throw "Checksum verification failed: $($parts[1])" }
        }
    } finally { $archive.Dispose() }

    Write-Output "CLEAN CHECKOUT VERIFIED: $($zip.FullName)"
    Write-Output "SHA256: $((Get-FileHash -Algorithm SHA256 -LiteralPath $zip.FullName).Hash)"
} finally { Pop-Location }
