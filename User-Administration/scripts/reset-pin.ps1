# Reset user PIN
# Requires PostgreSQL command line tools (psql) and BCrypt.Net-Next

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

# Get user identifier
$userId = Read-Host "Enter user ID (GUID) or SamAccountName"
$pin = Read-Host "Enter new PIN" -AsSecureString
$pinValue = [System.Net.NetworkCredential]::new('', $pin).Password
if ([string]::IsNullOrWhiteSpace($pinValue)) { throw "A new PIN is required." }

# Hash the PIN
$hash = [BCrypt.Net.BCrypt]::HashPassword($pinValue, [BCrypt.Net.BCrypt]::GenerateSalt(12))

# Update PIN in database
Write-Host "`nUpdating PIN for user..." -ForegroundColor Yellow

$updateSql = @"
UPDATE user_pins 
SET pin_hash = '$hash', updated_at = NOW()
WHERE user_id = (SELECT id FROM users WHERE id::text = '$userId' OR sam_account_name = '$userId')
AND is_active = true;

INSERT INTO audit_logs (id, user_id, action, resource_type, resource_id, new_values, ip_address, created_at)
SELECT uuid_generate_v4(), id, 'pin_reset', 'user_pin', id, '{"credential": "updated; value omitted"}', NULL, NOW()
FROM users
WHERE id::text = '$userId' OR sam_account_name = '$userId';
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $updateSql

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to reset PIN." -ForegroundColor Red
    exit 1
}

Write-Host "PIN reset successfully!" -ForegroundColor Green
