# List recent audit logs
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$env:PGPASSWORD = $postgresPassword
$daysBack = 7

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Listing recent audit logs (last $daysBack days):" -ForegroundColor Yellow

$listAuditLogsQuery = @"
SELECT 
    id::text as audit_log_id,
    user_id::text as user_id,
    action,
    resource_type,
    resource_id::text,
    ip_address,
    created_at
FROM audit_logs
WHERE created_at >= NOW() - INTERVAL '$daysBack days'
ORDER BY created_at DESC
LIMIT 50;
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $listAuditLogsQuery
