[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [Parameter(Mandatory = $true)][string]$CertificateThumbprint,
    [Parameter(Mandatory = $true)][uri]$TimestampServer
)

$ErrorActionPreference = 'Stop'
$resolvedPath = (Resolve-Path -LiteralPath $Path).Path
$files = if ((Get-Item -LiteralPath $resolvedPath).PSIsContainer) {
    Get-ChildItem -LiteralPath $resolvedPath -Recurse -File | Where-Object Extension -in '.exe', '.dll', '.msi'
} else { Get-Item -LiteralPath $resolvedPath }
if (-not $files -or @($files).Count -eq 0) { throw 'No executable, library, or MSI artifact found to sign.' }
if ($TimestampServer.Scheme -ne 'https') { throw 'A customer-approved HTTPS RFC3161 timestamp URL is required.' }

$thumbprint = $CertificateThumbprint.Replace(' ', '').ToUpperInvariant()
if ($thumbprint -notmatch '^[0-9A-F]{40,64}$') { throw 'Signing certificate thumbprint format is invalid.' }
$certificate = Get-ChildItem Cert:\CurrentUser\My, Cert:\LocalMachine\My -ErrorAction SilentlyContinue |
    Where-Object { $_.Thumbprint.Replace(' ', '').ToUpperInvariant() -eq $thumbprint -and $_.HasPrivateKey -and $_.NotAfter -gt [DateTime]::UtcNow } |
    Select-Object -First 1
if (-not $certificate) { throw 'A current signing certificate with accessible private key was not found in an approved certificate store.' }
$ekuExtension = $certificate.Extensions | Where-Object { $_.Oid.Value -eq '2.5.29.37' } | Select-Object -First 1
$eku = if ($ekuExtension) { [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension]::new($ekuExtension, $ekuExtension.Critical) } else { $null }
$hasCodeSigningEku = $eku -and ($eku.EnhancedKeyUsages | Where-Object { $_.Value -eq '1.3.6.1.5.5.7.3.3' })
if (-not $hasCodeSigningEku) {
    throw 'The signing certificate does not assert the Code Signing EKU.'
}

$signTool = Get-Command signtool.exe -ErrorAction SilentlyContinue
if (-not $signTool) { throw 'Windows SDK signtool.exe is required; no artifact was signed.' }
foreach ($file in $files) {
    if ($file.Extension -notin '.exe', '.dll', '.msi') { throw "Unsupported signing target: $($file.Extension)" }
    & $signTool.Source sign /sha1 $thumbprint /fd SHA256 /tr $TimestampServer.AbsoluteUri /td SHA256 /v $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Signing failed for $($file.Name)." }
    & $signTool.Source verify /pa /all /v $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Signature verification failed for $($file.Name)." }
    Write-Output ("SIGNED {0} SHA256={1}" -f $file.Name, (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash.ToLowerInvariant())
}
