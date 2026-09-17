#requires -Version 5.1
# PINÈDE IDENTITY - Development Environment Setup
# Run this script to set up the development environment

Write-Host "Setting up PINÈDE IDENTITY development environment..." -ForegroundColor Green

# Check if PostgreSQL is running
Write-Host "Checking PostgreSQL..." -ForegroundColor Yellow
$postgresCheck = Get-Service | Where-Object { $_.Name -like "*postgres*" }
if ($postgresCheck) {
    Write-Host "PostgreSQL service found." -ForegroundColor Green
} else {
    Write-Host "PostgreSQL service not found. Please ensure PostgreSQL is installed and running." -ForegroundColor Red
    Write-Host "You can download PostgreSQL from https://www.postgresql.org/download/" -ForegroundColor Yellow
}

# Check if .NET 8 is available
Write-Host "`nChecking .NET 8 SDK..." -ForegroundColor Yellow
try {
    dotnet --version
    Write-Host ".NET SDK is installed." -ForegroundColor Green
} catch {
    Write-Host ".NET 8 SDK is NOT installed. Backend development requires .NET 8." -ForegroundColor Red
    Write-Host "Download from https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
}

# Check if Node.js is available
Write-Host "`nChecking Node.js..." -ForegroundColor Yellow
$node = Get-Command npm -ErrorAction SilentlyContinue
if ($node) {
    $nodeVersion = npm --version
    Write-Host "Node.js $nodeVersion is installed." -ForegroundColor Green
} else {
    Write-Host "Node.js is NOT installed." -ForegroundColor Red
    Write-Host "Download from https://nodejs.org" -ForegroundColor Yellow
}

# Database setup
Write-Host "`nSetting up database..." -ForegroundColor Yellow
Write-Host "1. Create the database (if not exists):"
Write-Host "   CREATE DATABASE NexorSys_Dev;" -ForegroundColor Gray
Write-Host "`n2. Run the initialization script:"
Write-Host "   psql -U postgres -d NexorSys_Dev -f db/init_db.sql" -ForegroundColor Gray

# Frontend setup
Write-Host "`nSetting up frontend..." -ForegroundColor Yellow
cd frontend/nexorsys-identity-ui
Write-Host "Installing npm packages..." -ForegroundColor Gray
npm install | Out-Null
Write-Host "Frontend packages installed." -ForegroundColor Green

# Create .env file
$envFile = ".env"
if (!(Test-Path $envFile)) {
    Write-Host "Creating .env file from example..." -ForegroundColor Gray
    Copy-Item ".env.example" -Destination $envFile
    Write-Host "Created $envFile. Update it with your configuration." -ForegroundColor Green
} else {
    Write-Host ".env file already exists." -ForegroundColor Yellow
}

# Build frontend
Write-Host "Building frontend..." -ForegroundColor Gray
npm run build | Out-Null
Write-Host "Frontend build completed." -ForegroundColor Green

# Return to project root
cd ../../..

Write-Host "`nSetup complete!" -ForegroundColor Green
Write-Host "`nNext steps:" -ForegroundColor Yellow
Write-Host "1. Start frontend development server: npm run dev (in frontend/nexorsys-identity-ui)"
Write-Host "2. Start backend API when .NET SDK is available: dotnet run (in backend/src/Nexorsys.Identity.API)"
Write-Host "3. Access frontend at http://localhost:3000"
Write-Host "4. Access backend API at https://localhost:7000 or http://localhost:5000"
