# Export users to CSV
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$env:PGPASSWORD = $postgresPassword
$outputFile = "users_export_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Exporting users to CSV file..." -ForegroundColor Yellow

$exportQuery = @"
COPY (
    SELECT 
        ad_guid,
        sam_account_name,
        display_name,
        email,
        department,
        title,
        employee_id,
        badge_uid,
        is_active,
        is_local_profile,
        created_at,
        updated_at
    FROM users
    ORDER BY created_at DESC
) TO STDOUT WITH CSV HEADER;
"@

# Use psql to export and save to file
& psql -U $postgresUser -h localhost -d $databaseName -c $exportQuery -t -A | Out-File -FilePath $outputFile -Encoding UTF8

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to export users." -ForegroundColor Red
    exit 1
}

Write-Host "Users exported successfully!" -ForegroundColor Green
Write-Host "File: $outputFile" -ForegroundColor Yellow
