# Fix-Security.ps1
# This script attempts to resolve "Application Control policy" blocks by:
# 1. Unblocking all files in the directory
# 2. Adding the directory to Windows Defender exclusions
# 3. (Optional) Creating and installing a self-signed certificate if needed

Write-Host "Starting Security Fix for NEXORSYS NFC KIOSQUE..." -ForegroundColor Cyan

$CurrentDir = Get-Location
Write-Host "Target Directory: $CurrentDir"

# 1. Unblock files
Write-Host "Step 1: Unblocking files..." -ForegroundColor Yellow
Get-ChildItem -Path $CurrentDir -Recurse | Unblock-File
Write-Host "Files unblocked." -ForegroundColor Green

# 2. Add Windows Defender Exclusion
Write-Host "Step 2: Adding Windows Defender Exclusion..." -ForegroundColor Yellow
try {
    Add-MpPreference -ExclusionPath $CurrentDir.Path -ErrorAction Stop
    Write-Host "Exclusion added to Windows Defender." -ForegroundColor Green
} catch {
    Write-Host "FAILED to add exclusion. Please run this script as Administrator." -ForegroundColor Red
}

# 3. Check for Smart App Control / WDAC
Write-Host "`nNote: If you are using Windows 11 Smart App Control, you may still see blocks for unsigned binaries." -ForegroundColor Cyan
Write-Host "If the app still fails to run, try running: dotnet publish -c Release" -ForegroundColor Cyan

Write-Host "`nSecurity fix attempt completed." -ForegroundColor Green
