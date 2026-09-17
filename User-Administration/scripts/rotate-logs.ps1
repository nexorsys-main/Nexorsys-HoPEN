# Rotate logs
# Archives old logs and creates new ones

$logPath = "logs"
$daysToKeep = 30
$archiveDir = "logs_archive"

Write-Host "Rotating logs..." -ForegroundColor Yellow

# Create archive directory if it doesn't exist
if (-not (Test-Path $archiveDir))
{
    New-Item -ItemType Directory -Path $archiveDir | Out-Null
}

# Get old log files
$oldLogFiles = Get-ChildItem -Path $logPath -Filter "*.log" -File | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep) }

if ($oldLogFiles.Count -eq 0)
{
    Write-Host "No old log files to archive." -ForegroundColor Yellow
    exit 0
}

# Archive old log files
foreach ($file in $oldLogFiles)
{
    $archiveFile = Join-Path $archiveDir $file.Name
    Move-Item $file.FullName $archiveFile
    Write-Host "Archived: $($file.Name)" -ForegroundColor Green
}

Write-Host "Log rotation completed successfully!" -ForegroundColor Green
Write-Host "Archived $($oldLogFiles.Count) old log file(s)." -ForegroundColor Yellow