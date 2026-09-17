# PINÈDE IDENTITY - Development Environment Runner (PowerShell)
# This script starts both the frontend and backend with hot-reload enabled.

Write-Host "Starting PINÈDE IDENTITY development environment..." -ForegroundColor Cyan

# Define paths
$RootPath = Resolve-Path "$PSScriptRoot\.."
$FrontendPath = Join-Path $RootPath "frontend\nexorsys-identity-ui"
$BackendPath = Join-Path $RootPath "backend\src\Nexorsys.Identity.API"

# Start Frontend
Write-Host "Starting frontend development server (Vite) in background..." -ForegroundColor Yellow
Start-Process powershell -ArgumentList "-Command", "cd '$FrontendPath'; npm run dev" -WindowStyle Hidden

# Start Backend
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    Write-Host "Starting backend API (dotnet watch) in background..." -ForegroundColor Yellow
    Start-Process powershell -ArgumentList "-Command", "cd '$BackendPath'; dotnet watch run" -WindowStyle Hidden
} else {
    Write-Warning ".NET SDK not found. Backend API will not start."
}

Write-Host "`nDevelopment environment launched in BACKGROUND." -ForegroundColor Green
Write-Host "Frontend: http://localhost:3000"
Write-Host "Backend API: http://localhost:5000"
Write-Host "`nUse '.\scripts\stop-dev.ps1' to stop the services." -ForegroundColor Cyan
