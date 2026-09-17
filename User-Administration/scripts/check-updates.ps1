# Check for updates to dependencies
Write-Host "Checking for updates to dependencies..." -ForegroundColor Yellow

# Check frontend dependencies
Write-Host "Frontend dependencies:" -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
npm outdated --long
cd ../../

# Check backend dependencies (if .NET is available)
Write-Host "`nBackend dependencies:" -ForegroundColor Yellow
if (Get-Command dotnet -ErrorAction SilentlyContinue)
{
    cd backend/src/Nexorsys.Identity.API
    dotnet list package --deprecated
    cd ../../../
}
else
{
    Write-Host "Backend dependencies: Cannot check ( .NET SDK not available)" -ForegroundColor Red
}

Write-Host "`nUpdate check completed!" -ForegroundColor Green