# Clean up old deployment packages
$daysToKeep = 30
$packagePattern = "deployment_package_*.zip"

Write-Host "Cleaning up old deployment packages..." -ForegroundColor Yellow

$oldPackages = Get-ChildItem -Path . -Filter $packagePattern -File | Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$daysToKeep) }

if ($oldPackages.Count -eq 0)
{
    Write-Host "No old deployment packages found." -ForegroundColor Yellow
    exit 0
}

foreach ($package in $oldPackages)
{
    Write-Host "Deleting: $($package.Name)" -ForegroundColor Red
    Remove-Item $package.FullName
}

Write-Host "Cleaned up $($oldPackages.Count) old deployment package(s)." -ForegroundColor Green