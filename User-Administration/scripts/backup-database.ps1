# Backup database
# Requires PostgreSQL command line tools (pg_dump)

$postgresUser = "postgres"
if ([string]::IsNullOrWhiteSpace($env:PGPASSWORD)) { throw 'Supply the PostgreSQL password through the process environment before running this script.' }
$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
$backupDir = Join-Path $PSScriptRoot "..\backups"
if (-not (Test-Path $backupDir)) { New-Item -ItemType Directory -Path $backupDir | Out-Null }
$backupFile = Join-Path $backupDir "db_backup_$(Get-Date -Format 'yyyyMMdd_HHmmss').sql"

# Common PostgreSQL binary paths
$pgPaths = @(
    "C:\Program Files\PostgreSQL\18\bin",
    "C:\Program Files\PostgreSQL\17\bin",
    "C:\Program Files\PostgreSQL\16\bin",
    "C:\Program Files\PostgreSQL\15\bin",
    "C:\Program Files\PostgreSQL\14\bin"
)

$pgDumpPath = "pg_dump"

# Check if pg_dump is in PATH
if (-not (Get-Command $pgDumpPath -ErrorAction SilentlyContinue)) {
    # Search in common paths
    foreach ($path in $pgPaths) {
        $fullPath = Join-Path $path "pg_dump.exe"
        if (Test-Path $fullPath) {
            $pgDumpPath = $fullPath
            break
        }
    }
}

# Final check
if (-not (Get-Command $pgDumpPath -ErrorAction SilentlyContinue) -and -not (Test-Path $pgDumpPath)) {
    Write-Host "PostgreSQL dump tool (pg_dump) is not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure pg_dump is in your PATH." -ForegroundColor Yellow
    exit 1
}

Write-Host "Backing up database '$databaseName' to $backupFile..." -ForegroundColor Yellow
& $pgDumpPath -U $postgresUser -h localhost -d `"$databaseName`" --clean -f $backupFile

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed to backup database." -ForegroundColor Red
    exit 1
}

Write-Host "Database backup completed successfully!" -ForegroundColor Green
Write-Host "Backup file: $backupFile" -ForegroundColor Yellow
