# Clear application cache
# Clears Redis cache if available, otherwise clears local files

$cacheDir = "cache"
$redisHost = "localhost"
$redisPort = 6379

Write-Host "Clearing application cache..." -ForegroundColor Yellow

# Clear Redis cache if available
if (Get-Command redis-cli -ErrorAction SilentlyContinue)
{
    Write-Host "Clearing Redis cache..." -ForegroundColor Yellow
    & redis-cli -h $redisHost -p $redisPort FLUSHDB
    Write-Host "Redis cache cleared." -ForegroundColor Green
}
else
{
    Write-Host "Redis not available. Clearing local cache files..." -ForegroundColor Yellow
    if (Test-Path $cacheDir)
    {
        Remove-Item -Recurse -Force $cacheDir
        Write-Host "Local cache directory removed." -ForegroundColor Green
    }
    else
    {
        Write-Host "No cache directory found." -ForegroundColor Yellow
    }
}

Write-Host "Cache cleared successfully!" -ForegroundColor Green