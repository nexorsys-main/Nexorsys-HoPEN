# Load PINÈDE IDENTITY database schema
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$env:PGPASSWORD = $postgresPassword
$scriptPath = Join-Path $PSScriptRoot "..\db\init_db.sql"

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Create database if it doesn't exist
Write-Host "Creating database '$databaseName' if it doesn't exist..." -ForegroundColor Yellow
& psql -U $postgresUser -h localhost -c "CREATE DATABASE $databaseName;" 2>$null

if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne 1)
{
    Write-Host "Failed to create database. Check if the database already exists or if credentials are correct." -ForegroundColor Red
    exit 1
}

# Run the initialization script
Write-Host "Running database initialization script..." -ForegroundColor Yellow
& psql -U $postgresUser -h localhost -d $databaseName -f $scriptPath

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to run initialization script." -ForegroundColor Red
    exit 1
}

Write-Host "Database setup completed successfully!" -ForegroundColor Green
