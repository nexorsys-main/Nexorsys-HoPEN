param(
    [string]$OutputDirectory = (Join-Path $env:LOCALAPPDATA ('NexorSys\Builds\CredentialProvider-' + [guid]::NewGuid().ToString('N'))),
    [string]$SigningCertificateThumbprint,
    [uri]$TimestampServer
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$buildDir = [IO.Path]::GetFullPath($OutputDirectory)
$resolvedProject = [IO.Path]::GetFullPath($projectRoot)
if ($buildDir -eq [IO.Path]::GetPathRoot($buildDir) -or $buildDir -eq $resolvedProject -or $buildDir.StartsWith($resolvedProject + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputDirectory must be a dedicated directory outside the source tree.'
}
if (Test-Path -LiteralPath $buildDir) {
    if ((Get-ChildItem -LiteralPath $buildDir -Force | Measure-Object).Count -gt 0) { throw 'OutputDirectory must be empty; existing artifacts will not be overwritten.' }
} else { New-Item -ItemType Directory -Path $buildDir | Out-Null }
$msiPath = Join-Path $buildDir 'NexorSysIdentityCredentialProvider.msi'

if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) { throw 'CMake is required to build the native Credential Provider.' }
if (-not (Get-Command wix -ErrorAction SilentlyContinue)) { throw 'WiX CLI is required to build the MSI.' }

cmake -S $projectRoot -B $buildDir -A x64
if ($LASTEXITCODE -ne 0) { throw "CMake configure failed with exit code $LASTEXITCODE." }
cmake --build $buildDir --config Release
if ($LASTEXITCODE -ne 0) { throw "Credential Provider build failed with exit code $LASTEXITCODE." }

$signingRequested = -not [string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or $null -ne $TimestampServer
if ($signingRequested -and ([string]::IsNullOrWhiteSpace($SigningCertificateThumbprint) -or $null -eq $TimestampServer)) {
    throw 'Both SigningCertificateThumbprint and TimestampServer are required to sign this build.'
}
$dllPath = Join-Path $buildDir 'Release/NexorsysCredentialProvider.dll'
if ($signingRequested) {
    & (Join-Path $projectRoot '../scripts/Sign-NexorSysArtifact.ps1') -Path $dllPath -CertificateThumbprint $SigningCertificateThumbprint -TimestampServer $TimestampServer
    if ($LASTEXITCODE -ne 0) { throw 'Credential Provider DLL signing failed.' }
}

$dllPath = Join-Path $buildDir 'Release/NexorsysCredentialProvider.dll'
wix build -arch x64 -d "CredentialProviderPath=$dllPath" (Join-Path $projectRoot 'Setup.wxs') -out $msiPath
if ($LASTEXITCODE -ne 0) { throw "WiX build failed with exit code $LASTEXITCODE." }

if ($signingRequested) {
    & (Join-Path $projectRoot '../scripts/Sign-NexorSysArtifact.ps1') -Path $msiPath -CertificateThumbprint $SigningCertificateThumbprint -TimestampServer $TimestampServer
    if ($LASTEXITCODE -ne 0) { throw 'Credential Provider MSI signing failed.' }
}

if ($signingRequested) { Write-Host "Built signed MSI artifact: $msiPath" }
else { Write-Host "Built unsigned engineering package: $msiPath" }
Write-Warning 'Signing is not equivalent to release validation. Real PKI, Winlogon, install/upgrade/uninstall/rollback, and Windows security validation remain required.'
