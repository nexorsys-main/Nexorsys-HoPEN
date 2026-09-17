# Clean up old database backups
# Keeps backups for the last 30 days

$backupDir = "."
$daysToKeep = 30

Write-Host "Cleaning up database backups older than $daysToKeep days..." -ForegroundColor Yellow

# Get all backup files (assuming .sql extension)
$backupFiles = Get-ChildItem -Path $backupDir -Filter "*.sql" -File | Where-Object { $_.Name -like "db_backup_*" }

$deletedCount = 0;
foreach ($file in $backupFiles)
{
    if ($file.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep))
    {
        Write-Host "Deleting old backup: $($file.Name)" -ForegroundColor Red
        Remove-Item $file.FullName
        $deletedCount++
    }
}

Write-Host "Cleaned up $deletedCount old backup(s)." -ForegroundColor Green