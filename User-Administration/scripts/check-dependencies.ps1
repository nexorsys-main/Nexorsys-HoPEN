# Check application dependencies
Write-Host "Checking application dependencies..." -ForegroundColor Yellow

# Check frontend dependencies
Write-Host "Frontend dependencies:" -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
npm list --depth=0
cd ../../

# Check backend dependencies (if .NET is available)
Write-Host "`nBackend dependencies:" -ForegroundColor Yellow
if (Get-Command dotnet -ErrorAction SilentlyContinue)
{
    cd backend/src/Nexorsys.Identity.API
    dotnet list package
    cd ../../../
}
else
{
    Write-Host "Backend dependencies: Cannot check ( .NET SDK not available)" -ForegroundColor Red
}

Write-Host "`nDependency check completed!" -ForegroundColor Green