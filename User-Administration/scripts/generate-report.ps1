# Generate system usage report
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$env:PGPASSWORD = $postgresPassword
$outputFile = "usage_report_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Generating system usage report..." -ForegroundColor Yellow

$report = @"
PINÈDE IDENTITY - System Usage Report
Generated on: $(Get-Date)
=====================================

1. Database Statistics
-------------------------
Database: $databaseName

"@

# Get database size
$dbSizeQuery = "SELECT pg_size_pretty(pg_database_size('$databaseName));"
$dbSize = & psql -U $postgresUser -h localhost -d postgres -t -c $dbSizeQuery
$report += "`nTotal Database Size: $dbSize`n"

# Get user statistics
$usersQuery = @"
SELECT 
    COUNT(*) as total_users,
    COUNT(CASE WHEN is_active THEN 1 END) as active_users,
    COUNT(CASE WHEN is_local_profile THEN 1 END) as local_profiles,
    COUNT(CASE WHEN badge_uid IS NOT NULL THEN 1 END) as users_with_badge
FROM users;
"@

$usersResult = & psql -U $postgresUser -h localhost -d $databaseName -t -c $usersQuery
$report += "User Statistics:`n"
$report += $usersResult

# Get PIN statistics
$pinsQuery = @"
SELECT 
    COUNT(*) as total_pins,
    COUNT(CASE WHEN is_active THEN 1 END) as active_pins
FROM user_pins;
"@

$pinsResult = & psql -U $postgresUser -h localhost -d $databaseName -t -c $pinsQuery
$report += "`nPIN Statistics:`n"
$report += $pinsResult

# Get workflow statistics
$workflowsQuery = @"
SELECT 
    COUNT(*) as total_workflows,
    COUNT(CASE WHEN status = 'pending' THEN 1 END) as pending_workflows,
    COUNT(CASE WHEN status = 'approved' THEN 1 END) as approved_workflows,
    COUNT(CASE WHEN status = 'rejected' THEN 1 END) as rejected_workflows
FROM workflows
WHERE created_at >= NOW() - INTERVAL '30 days';
"@

$workflowsResult = & psql -U $postgresUser -h localhost -d $databaseName -t -c $workflowsQuery
$report += "`nWorkflow Statistics (last 30 days):`n"
$report += $workflowsResult

# Get audit log statistics
$auditLogsQuery = @"
SELECT 
    COUNT(*) as total_logs,
    COUNT(CASE WHEN action = 'user_created' THEN 1 END) as user_creations,
    COUNT(CASE WHEN action = 'pin_changed' THEN 1 END) as pin_changes,
    COUNT(CASE WHEN action = 'login' THEN 1 END) as logins
FROM audit_logs
WHERE created_at >= NOW() - INTERVAL '7 days';
"@

$auditLogsResult = & psql -U $postgresUser -h localhost -d $databaseName -t -c $auditLogsQuery
$report += "`nAudit Log Statistics (last 7 days):`n"
$report += $auditLogsResult

$report += "`n`nReport generated at: $(Get-Date)" + "`n"

$report | Out-File -FilePath $outputFile -Encoding UTF8

Write-Host "Report generated successfully!" -ForegroundColor Green
Write-Host "File: $outputFile" -ForegroundColor Yellow
