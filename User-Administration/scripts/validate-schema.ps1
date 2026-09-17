# Validate database schema
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DATABASE_NAME)) { "NexorSys_Dev" } else { $env:NEXORSYS_DATABASE_NAME }

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Validating database schema..." -ForegroundColor Yellow

# Check if all tables exist
$tables = @(
    "users",
    "user_pins",
    "applications",
    "user_permissions",
    "audit_logs",
    "workflows",
    "kiosk_sessions"
)

foreach ($table in $tables)
{
    $checkQuery = "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = '$table');"
    $result = & psql -U $postgresUser -h localhost -d $databaseName -t -c $checkQuery
    
    if ($result -imatch "t")
    {
        Write-Host "Table ${table}: OK" -ForegroundColor Green
    }
    else
    {
        Write-Host "Table ${table}: MISSING" -ForegroundColor Red
    }
}

# Check relationships
$relationships = @(
    "users.ad_guid",
    "user_pins.badge_uid",
    "user_pins.user_id",
    "user_permissions.user_id",
    "user_permissions.application_id",
    "audit_logs.user_id",
    "workflows.user_id",
    "kiosk_sessions.user_id"
)

foreach ($relationship in $relationships)
{
    $checkQuery = "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = '$($relationship.Split('.')[0])' AND column_name = '$($relationship.Split('.')[1])');"
    $result = & psql -U $postgresUser -h localhost -d $databaseName -t -c $checkQuery
    
    if ($result -imatch "t")
    {
        Write-Host "Column ${relationship}: OK" -ForegroundColor Green
    }
    else
    {
        Write-Host "Column ${relationship}: MISSING" -ForegroundColor Red
    }
}

Write-Host "`nSchema validation completed!" -ForegroundColor Green
