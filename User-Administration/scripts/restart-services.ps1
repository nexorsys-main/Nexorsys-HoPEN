# Restart all services
Write-Host "Restarting all services..." -ForegroundColor Yellow

# Restart frontend
Write-Host "Restarting frontend..." -ForegroundColor Yellow
if (Test-Path "frontend/nexorsys-identity-ui")
{
    cd frontend/nexorsys-identity-ui
    npm run dev -- --restart
    cd ../../
    Write-Host "Frontend restarted." -ForegroundColor Green
}
else
{
    Write-Host "Frontend directory not found." -ForegroundColor Red
}

# Restart backend (if .NET is available)
Write-Host "Restarting backend..." -ForegroundColor Yellow
if (Test-Path "backend/src/Nexorsys.Identity.API" -and (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    cd backend/src/Nexorsys.Identity.API
    dotnet run --restart
    cd ../../../
    Write-Host "Backend restarted." -ForegroundColor Green
}
else
{
    Write-Host "Backend directory not found or .NET not available." -ForegroundColor Red
}

Write-Host "All services restarted!" -ForegroundColor Green