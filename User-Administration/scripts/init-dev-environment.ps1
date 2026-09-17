# Initialize development environment
# Run this script to set up the complete development environment

Write-Host "Initializing PINÈDE IDENTITY development environment..." -ForegroundColor Green

# Check prerequisites
Write-Host "Checking prerequisites..." -ForegroundColor Yellow

# Check PostgreSQL
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "WARNING: PostgreSQL command line tools (psql) are not available." -ForegroundColor Yellow
    Write-Host "You will need to install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
}
else
{
    Write-Host "PostgreSQL: OK" -ForegroundColor Green
}

# Check .NET
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    Write-Host "WARNING: .NET SDK not found." -ForegroundColor Yellow
    Write-Host "Backend development requires .NET 8 SDK." -ForegroundColor Yellow
}
else
{
    Write-Host "NET: OK" -ForegroundColor Green
}

# Check Node.js
if (-not (Get-Command npm -ErrorAction SilentlyContinue))
{
    Write-Host "WARNING: Node.js not found." -ForegroundColor Yellow
    Write-Host "Frontend development requires Node.js 18+." -ForegroundColor Yellow
}
else
{
    Write-Host "Node.js: OK" -ForegroundColor Green
}

# Setup database
Write-Host "`nSetting up database..." -ForegroundColor Yellow
& .\setup-database.ps1

# Setup frontend
Write-Host "`nSetting up frontend..." -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
npm install
npm run typecheck
cd ../../

# Seed database (optional)
$seed = Read-Host "Seed database with initial data? (y/n)"
if ($seed -eq 'y' -or $seed -eq 'Y')
{
    & .\seed-database.ps1
}

Write-Host "`nDevelopment environment initialized!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Start frontend: cd frontend/nexorsys-identity-ui && npm run dev"
Write-Host "2. Start backend (when .NET available): cd backend/src/Nexorsys.Identity.API && dotnet run"
Write-Host "3. Access frontend at http://localhost:3000"
Write-Host "4. Access backend API at https://localhost:7000 or http://localhost:5000"