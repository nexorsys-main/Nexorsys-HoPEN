# Test database connection
$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }

Write-Host "Testing database connection..." -ForegroundColor Yellow

if (-not (Get-Command psql -ErrorAction SilentlyContinue)) {
    throw "PostgreSQL command line tools (psql) are not available."
}

$previousPgPassword = $env:PGPASSWORD
try {
    $env:PGPASSWORD = $postgresPassword
    & psql -X -v ON_ERROR_STOP=1 -U $postgresUser -h localhost -d $databaseName -c 'SELECT 1;' | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "psql returned exit code $LASTEXITCODE." }
    Write-Host "Database connection successful!" -ForegroundColor Green
} catch {
    Write-Host "Database connection failed. Check the configured host, database and secret provider." -ForegroundColor Red
    exit 1
} finally {
    $env:PGPASSWORD = $previousPgPassword
}
