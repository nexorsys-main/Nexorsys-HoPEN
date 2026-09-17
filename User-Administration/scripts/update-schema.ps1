# Update database schema
# Runs the init_db.sql script to update the schema

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

Write-Host "Updating database schema..." -ForegroundColor Yellow

# Run the initialization script
& psql -U $postgresUser -h localhost -d $databaseName -f $scriptPath

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to update database schema." -ForegroundColor Red
    exit 1
}

Write-Host "Database schema updated successfully!" -ForegroundColor Green
