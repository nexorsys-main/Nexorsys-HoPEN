# Update all dependencies
Write-Host "Updating dependencies..." -ForegroundColor Yellow

# Update frontend dependencies
Write-Host "Updating frontend dependencies..." -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
npm update
cd ../../

# Update backend dependencies (if .NET is available)
Write-Host "Updating backend dependencies..." -ForegroundColor Yellow
if (Get-Command dotnet -ErrorAction SilentlyContinue)
{
    cd backend
    dotnet restore
    cd ..
    Write-Host "Backend dependencies updated." -ForegroundColor Green
}
else
{
    Write-Host "Backend dependencies NOT updated. .NET SDK not available." -ForegroundColor Red
}

Write-Host "All dependencies updated!" -ForegroundColor Green