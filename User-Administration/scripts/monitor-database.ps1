# Monitor database size
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$env:PGPASSWORD = $postgresPassword

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Database size information:" -ForegroundColor Yellow

# Get database size
$dbSizeQuery = "SELECT pg_size_pretty(pg_database_size('$databaseName'));"
$dbSize = & psql -U $postgresUser -h localhost -d postgres -t -c $dbSizeQuery

# Get table sizes
$tableSizesQuery = "SELECT table_name, pg_size_pretty(pg_total_relation_size(table_name)) as size FROM information_schema.tables WHERE table_schema = 'public' ORDER BY pg_total_relation_size(table_name) DESC;"
$tableSizes = & psql -U $postgresUser -h localhost -d $databaseName -t -c $tableSizesQuery

Write-Host "Database: $databaseName" -ForegroundColor Green
Write-Host "Total size: $dbSize" -ForegroundColor Yellow
Write-Host "`nTable sizes:" -ForegroundColor Yellow
Write-Host $tableSizes
