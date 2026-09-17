# PINÈDE IDENTITY - Safe Cleanup Script
# This script removes ONLY temporary build artifacts (bin, obj, dist).
# It does NOT delete source code, node_modules, or configuration files.

Write-Host "Cleaning temporary build files..." -ForegroundColor Cyan

$FoldersToClean = @(
    "backend/src/Nexorsys.Identity.API/bin",
    "backend/src/Nexorsys.Identity.API/obj",
    "backend/src/Nexorsys.Identity.Infrastructure/bin",
    "backend/src/Nexorsys.Identity.Infrastructure/obj",
    "backend/src/Nexorsys.Identity.Core/bin",
    "backend/src/Nexorsys.Identity.Core/obj",
    "frontend/nexorsys-identity-ui/dist"
)

foreach ($Folder in $FoldersToClean) {
    if (Test-Path $Folder) {
        Write-Host "Removing: $Folder" -ForegroundColor Yellow
        Remove-Item -Path $Folder -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "Cleanup complete! All temporary build files removed." -ForegroundColor Green
