# Sign-App.ps1
# This script creates a self-signed certificate and signs the application binaries.

Write-Host "Creating self-signed certificate..." -ForegroundColor Cyan

$certName = "NexorsysDevelopmentCert"
$cert = New-SelfSignedCertificate -Type Custom -Subject "CN=$certName" -KeyUsage DigitalSignature -FriendlyName "Nexorsys Dev Cert" -CertStoreLocation "Cert:\CurrentUser\My" -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3")

# Export and Import to Trusted Root (requires Admin, but let's try CurrentUser first)
# Actually, for SAC, it might need to be in Trusted Root.

Write-Host "Signing binaries..." -ForegroundColor Yellow
$filesToSign = Get-ChildItem -Path ".\bin\Debug\net8.0-windows\*" -Include *.exe, *.dll

foreach ($file in $filesToSign) {
    Write-Host "Signing $($file.Name)..."
    Set-AuthenticodeSignature -FilePath $file.FullName -Certificate $cert
}

Write-Host "Signing completed." -ForegroundColor Green
