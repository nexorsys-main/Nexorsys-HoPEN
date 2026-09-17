$dbPassword = $env:PGPASSWORD
if ([string]::IsNullOrWhiteSpace($dbPassword)) {
    throw 'Set PGPASSWORD in the process environment before running this migration helper.'
}
$psqlPath = "C:\Program Files\PostgreSQL\18\bin\psql.exe";

$sql = @"
ALTER TABLE users ADD COLUMN IF NOT EXISTS phone_number VARCHAR(20);
ALTER TABLE users ADD COLUMN IF NOT EXISTS password_hash VARCHAR(255);
"@

$databaseName = if ($env:NEXORSYS_DATABASE_NAME) { $env:NEXORSYS_DATABASE_NAME } else { "NexorSys_Dev" }
& $psqlPath -U postgres -h localhost -d $databaseName -c $sql
