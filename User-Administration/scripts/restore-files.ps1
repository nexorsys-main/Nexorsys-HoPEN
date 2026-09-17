# Restore files from backup
$backupFile = Read-Host "Enter backup archive path (ZIP file)"
$extractDir = "restore_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host "Restoring files from backup..." -ForegroundColor Yellow

# Extract archive
Write-Host "Extracting archive..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($backupFile, $extractDir)

# Copy files back to project
$sourceDirs = @(
    "db",
    "backend/src/Nexorsys.Identity.Core",
    "backend/src/Nexorsys.Identity.Infrastructure",
    "backend/src/Nexorsys.Identity.API",
    "frontend/nexorsys-identity-ui/src",
    "frontend/nexorsys-identity-ui/public",
    "AGENTS.md",
    "README.md",
    ".env.example"
)

foreach ($dir in $sourceDirs)
{
    $sourcePath = Join-Path $extractDir $dir
    if (Test-Path $sourcePath)
    {
        Copy-Item -Path $sourcePath -Destination . -Recurse -Force
        Write-Host "Restored: $dir" -ForegroundColor Green
    }
}

Write-Host "File restoration completed successfully!" -ForegroundColor Green