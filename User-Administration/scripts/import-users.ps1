# Import users from CSV
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DATABASE_NAME)) { "NexorSys_Dev" } else { $env:NEXORSYS_DATABASE_NAME }
$inputFile = Read-Host "Enter CSV file path"

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Check if file exists
if (-not (Test-Path $inputFile))
{
    Write-Host "File not found: $inputFile" -ForegroundColor Red
    exit 1
}

Write-Host "Importing users from CSV file..." -ForegroundColor Yellow

$importQuery = @"
COPY users (
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
) FROM STDIN WITH (FORMAT CSV, HEADER true);
"@

# Use psql to import
Get-Content -LiteralPath $inputFile -Raw | & psql -U $postgresUser -h localhost -d $databaseName -c $importQuery

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to import users." -ForegroundColor Red
    exit 1
}

Write-Host "Users imported successfully!" -ForegroundColor Green
