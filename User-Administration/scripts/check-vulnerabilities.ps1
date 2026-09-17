# Check for security vulnerabilities
Write-Host "Checking for security vulnerabilities..." -ForegroundColor Yellow

# Check frontend dependencies
Write-Host "Checking frontend dependencies..." -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
npm audit
cd ../../

# Check backend dependencies (if .NET is available)
Write-Host "Checking backend dependencies..." -ForegroundColor Yellow
if (Get-Command dotnet -ErrorAction SilentlyContinue)
{
    cd backend
    # Note: dotnet audit is not available in .NET 8 by default, but we can check for known vulnerabilities
    Write-Host "Backend dependency security check: Use NuGet vulnerability database manually." -ForegroundColor Yellow
    cd ..
}
else
{
    Write-Host "Backend dependency security check skipped. .NET SDK not available." -ForegroundColor Red
}

Write-Host "Security check completed!" -ForegroundColor Green