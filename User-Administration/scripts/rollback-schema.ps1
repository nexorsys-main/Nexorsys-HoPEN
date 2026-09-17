# Rollback database schema (re-run init_db.sql which drops and recreates)
# WARNING: This will delete all data!
throw "Disabled: destructive schema rollback via init_db.sql is not supported. Use the documented, reviewed EF migration rollback procedure on a verified backup."

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$scriptPath = Join-Path $PSScriptRoot "..\db\init_db.sql"

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Confirm with user
$confirmation = Read-Host "WARNING: This will delete all data and recreate the schema. Are you sure? (y/n)"
if ($confirmation -ne 'y' -and $confirmation -ne 'Y')
{
    exit 0
}

Write-Host "Rolling back database schema..." -ForegroundColor Yellow

# Run the initialization script (which drops and recreates tables)
& psql -U $postgresUser -h localhost -d $databaseName -f $scriptPath

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to rollback database schema." -ForegroundColor Red
    exit 1
}

Write-Host "Database schema rolled back successfully!" -ForegroundColor Green
