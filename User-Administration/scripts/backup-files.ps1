# Backup important files
$backupDir = "backup_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
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

Write-Host "Creating backup..." -ForegroundColor Yellow

# Create backup directory
New-Item -ItemType Directory -Path $backupDir | Out-Null

# Copy files to backup directory
foreach ($dir in $sourceDirs)
{
    if (Test-Path $dir)
    {
        Copy-Item -Path $dir -Destination $backupDir -Recurse -Force
        Write-Host "Copied: $dir" -ForegroundColor Green
    }
    else
    {
        Write-Host "Not found: $dir" -ForegroundColor Yellow
    }
}

# Create a compressed archive
$archiveFile = "$backupDir.zip"
Write-Host "Creating compressed archive..." -ForegroundColor Yellow
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($backupDir, $archiveFile)

Write-Host "Backup completed successfully!" -ForegroundColor Green
Write-Host "Backup location: $backupDir and $archiveFile" -ForegroundColor Yellow