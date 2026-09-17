# List all users in database
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

Write-Host "Listing all users in database '$databaseName':" -ForegroundColor Yellow

$listUsersQuery = @"
SELECT 
    id::text as user_id,
    ad_guid,
    sam_account_name,
    display_name,
    email,
    department,
    is_active,
    is_local_profile,
    created_at
FROM users
ORDER BY created_at DESC;
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $listUsersQuery
