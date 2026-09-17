# PINÈDE IDENTITY - Stop Development Environment
# This script kills the hidden frontend and backend processes.

Write-Host "Stopping PINÈDE IDENTITY development environment..." -ForegroundColor Cyan

# Kill Vite/Node (Frontend)
Write-Host "Stopping Vite (Node)..." -ForegroundColor Yellow
Get-Process node -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -like "*vite*" } | Stop-Process -Force

# Kill .NET (Backend)
Write-Host "Stopping Backend API (dotnet)..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process Nexorsys.Identity.API -ErrorAction SilentlyContinue | Stop-Process -Force

Write-Host "All development services stopped." -ForegroundColor Green
