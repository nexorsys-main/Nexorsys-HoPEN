# Trust-Cert.ps1
# This script adds the Nexorsys Dev Cert to the Trusted Root.

$certName = "NexorsysDevelopmentCert"
$cert = Get-ChildItem -Path Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*CN=$certName*" } | Select-Object -First 1

if ($cert) {
    Write-Host "Found certificate. Attempting to add to Trusted Root..." -ForegroundColor Yellow
    try {
        $rootStore = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
        $rootStore.Open("ReadWrite")
        $rootStore.Add($cert)
        $rootStore.Close()
        Write-Host "Certificate added to Trusted Root (CurrentUser)." -ForegroundColor Green
    } catch {
        Write-Host "FAILED to add to Trusted Root: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "Certificate not found." -ForegroundColor Red
}
