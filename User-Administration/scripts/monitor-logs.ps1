# Monitor application logs
# Requires access to log files

$logPath = "logs"
$daysBack = 1

# Check if log directory exists
if (-not (Test-Path $logPath))
{
    Write-Host "Log directory not found: $logPath" -ForegroundColor Red
    exit 1
}

Write-Host "Monitoring recent logs (last $daysBack days)..." -ForegroundColor Yellow

# Get log files
$logFiles = Get-ChildItem -Path $logPath -Filter "*.log" -File | Where-Object { $_.LastWriteTime -ge (Get-Date).AddDays(-$daysBack) }

if ($logFiles.Count -eq 0)
{
    Write-Host "No log files found in the last $daysBack days." -ForegroundColor Yellow
    exit 0
}

foreach ($file in $logFiles)
{
    Write-Host "`n=== $file.FullName ===" -ForegroundColor Green
    Get-Content $file.FullName | Select-Object -Last 50
}

Write-Host "`n`nLog monitoring completed!" -ForegroundColor Green