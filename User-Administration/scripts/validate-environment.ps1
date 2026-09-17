# Validate environment configuration
Write-Host "Validating environment configuration..." -ForegroundColor Yellow

$validationErrors = @()

# Check if required ports are available
$requiredPorts = @(3000, 5000, 7000)
foreach ($port in $requiredPorts)
{
    $portInUse = Get-NetTCPConnection -LocalPort $port -ErrorAction SilentlyContinue
    if ($portInUse)
    {
        $validationErrors += "Port $port is already in use."
    }
}

# Check if environment variables are set
$requiredEnvVars = @("ConnectionStrings__DefaultConnection", "Jwt__Key", "Jwt__Issuer", "Jwt__Audience")
foreach ($envVar in $requiredEnvVars)
{
    if (-not (Test-Path env:$envVar))
    {
        $validationErrors += "Environment variable $envVar is not set."
    }
}

# Check if necessary directories exist
$requiredDirs = @(
    "db",
    "backend/src/Nexorsys.Identity.Core",
    "backend/src/Nexorsys.Identity.Infrastructure",
    "backend/src/Nexorsys.Identity.API",
    "frontend/nexorsys-identity-ui/src"
)

foreach ($dir in $requiredDirs)
{
    if (-not (Test-Path $dir))
    {
        $validationErrors += "Directory $dir is missing."
    }
}

# Output results
if ($validationErrors.Count -eq 0)
{
    Write-Host "Environment configuration is valid!" -ForegroundColor Green
}
else
{
    Write-Host "Environment configuration has errors:" -ForegroundColor Red
    foreach ($error in $validationErrors)
    {
        Write-Host "  - $error" -ForegroundColor Red
    }
    exit 1
}