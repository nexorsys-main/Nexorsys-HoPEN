# Clean up temporary files
$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "Cleaning up temporary files..." -ForegroundColor Yellow

# Clean frontend temp files
Write-Host "Cleaning frontend temporary files..." -ForegroundColor Yellow
$frontendDir = Join-Path $ProjectRoot "frontend/nexorsys-identity-ui"
if (Test-Path $frontendDir) {
    Push-Location $frontendDir
    Remove-Item -Recurse -Force node_modules 2>$null
    Remove-Item -Recurse -Force dist 2>$null
    Remove-Item -Recurse -Force .cache 2>$null
    Pop-Location
}

# Clean backend temp files
Write-Host "Cleaning backend temporary files..." -ForegroundColor Yellow
$backendApiDir = Join-Path $ProjectRoot "backend/src/Nexorsys.Identity.API"
if (Test-Path "$backendApiDir/bin") { Remove-Item -Recurse -Force "$backendApiDir/bin" 2>$null }
if (Test-Path "$backendApiDir/obj") { Remove-Item -Recurse -Force "$backendApiDir/obj" 2>$null }

# Clean solution temp files
$backendDir = Join-Path $ProjectRoot "backend"
if (Test-Path "$backendDir/bin") { Remove-Item -Recurse -Force "$backendDir/bin" 2>$null }
if (Test-Path "$backendDir/obj") { Remove-Item -Recurse -Force "$backendDir/obj" 2>$null }

Write-Host "Temporary files cleaned up!" -ForegroundColor Green