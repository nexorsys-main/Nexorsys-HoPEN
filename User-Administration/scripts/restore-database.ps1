# Restore database from backup
# Requires PostgreSQL command line tools (psql)

$postgresUser = "postgres"
if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) { throw 'Supply the PostgreSQL password through the process environment before running this script.' }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$backupDir = Join-Path $PSScriptRoot "..\backups"

# Try to find the latest backup if no file is specified
$backupFile = $args[0]
if ([string]::IsNullOrEmpty($backupFile)) {
    if (Test-Path $backupDir) {
        $latestBackup = Get-ChildItem -Path $backupDir -Filter "db_backup_*.sql" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
        if ($latestBackup) {
            $backupFile = $latestBackup.FullName
            Write-Host "Using latest backup: $($latestBackup.Name)" -ForegroundColor Cyan
        }
    }
}

# Common PostgreSQL binary paths
$pgPaths = @(
    "C:\Program Files\PostgreSQL\18\bin",
    "C:\Program Files\PostgreSQL\17\bin",
    "C:\Program Files\PostgreSQL\16\bin",
    "C:\Program Files\PostgreSQL\15\bin",
    "C:\Program Files\PostgreSQL\14\bin"
)

$psqlPath = "psql"

# Check if psql is in PATH
if (-not (Get-Command $psqlPath -ErrorAction SilentlyContinue)) {
    # Search in common paths
    foreach ($path in $pgPaths) {
        $fullPath = Join-Path $path "psql.exe"
        if (Test-Path $fullPath) {
            $psqlPath = $fullPath
            break
        }
    }
}

# Final check
if (-not (Get-Command $psqlPath -ErrorAction SilentlyContinue) -and -not (Test-Path $psqlPath)) {
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Check if backup file exists
if ([string]::IsNullOrEmpty($backupFile) -or -not (Test-Path $backupFile)) {
    Write-Host "Backup file not found or not specified." -ForegroundColor Red
    exit 1
}

Write-Host "Restoring database '$databaseName' from $backupFile..." -ForegroundColor Yellow

# Drop existing database if it exists (using postgres db as target for drop)
& $psqlPath -U $postgresUser -h localhost -d postgres -c "DROP DATABASE IF EXISTS `"$databaseName`" WITH (FORCE);"

# Create database
& $psqlPath -U $postgresUser -h localhost -d postgres -c "CREATE DATABASE `"$databaseName`";"

# Restore from backup
& $psqlPath -U $postgresUser -h localhost -d `"$databaseName`" -f $backupFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to restore database." -ForegroundColor Red
    exit 1
}

Write-Host "Database restoration completed successfully!" -ForegroundColor Green
