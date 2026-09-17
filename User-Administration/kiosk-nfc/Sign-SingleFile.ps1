# Sign-SingleFile.ps1
$certName = "NexorsysDevelopmentCert"
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*CN=$certName*" } | Select-Object -First 1

if ($cert) {
    $file = ".\bin\Release\net8.0-windows\win-x64\publish\Nexorsys.NFC.APP.exe"
    Write-Host "Signing $file..."
    Set-AuthenticodeSignature -FilePath $file -Certificate $cert
} else {
    Write-Host "Cert not found."
}
