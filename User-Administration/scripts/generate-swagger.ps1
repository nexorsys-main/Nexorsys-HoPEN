# Generate API documentation (Swagger)
# Requires .NET Swagger tools

$apiProject = "backend/src/Nexorsys.Identity.API/Nexorsys.Identity.API.csproj"

# Check if dotnet is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue))
{
    Write-Host ".NET SDK not available. Cannot generate Swagger documentation." -ForegroundColor Red
    exit 1
}

# Check if Swagger tools are installed
$swaggerToolsInstalled = Get-ChildItem -Path Env: | Where-Object { $_.Name -eq "SwaggerTools" } -ErrorAction SilentlyContinue
if (-not $swaggerToolsInstalled)
{
    Write-Host "Installing Swagger tools..." -ForegroundColor Yellow
    dotnet new tool-manifest -g
    dotnet tool install --global Swashbuckle.AspNetCore.SwaggerGen
}

Write-Host "Generating Swagger documentation..." -ForegroundColor Yellow

# Generate Swagger JSON
$swaggerOutput = "swagger.json"
& dotnet swagger tofile --output $swaggerOutput --outputDir . $apiProject

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to generate Swagger documentation." -ForegroundColor Red
    exit 1
}

Write-Host "Swagger documentation generated successfully!" -ForegroundColor Green
Write-Host "File: $swaggerOutput" -ForegroundColor Yellow

# Optionally generate HTML version
$generateHtml = Read-Host "Generate HTML version? (y/n)"
if ($generateHtml -eq 'y' -or $generateHtml -eq 'Y')
{
    Write-Host "Generating HTML documentation..." -ForegroundColor Yellow
    & dotnet swagger tofile --output "swagger.html" --outputDir . $apiProject --flavor "web"
    
    if ($LASTEXITCODE -ne 0)
    {
        Write-Host "Failed to generate HTML documentation." -ForegroundColor Red
    }
    else
    {
        Write-Host "HTML documentation generated successfully!" -ForegroundColor Green
        Write-Host "File: swagger.html" -ForegroundColor Yellow
    }
}