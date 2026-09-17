# Generate test user with hashed PIN
# Requires PostgreSQL command line tools (psql) and BCrypt.Net-Next

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DEMO_DATABASE)) { "NexorSys_Dev" } else { $env:NEXORSYS_DEMO_DATABASE }
$directoryDomain = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DIRECTORY_DOMAIN)) { "example.invalid" } else { $env:NEXORSYS_DIRECTORY_DOMAIN }

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Check if BCrypt is available
$pin = Read-Host "Enter test user PIN (required; input is hidden)" -AsSecureString
$pinValue = [System.Net.NetworkCredential]::new('', $pin).Password
if ([string]::IsNullOrEmpty($pinValue)) { throw "A test PIN is required." }

# Hash the PIN
$hash = [BCrypt.Net.BCrypt]::HashPassword($pinValue, [BCrypt.Net.BCrypt]::GenerateSalt(12))

# Generate user details
$userId = [Guid]::NewGuid()
$samAccountName = Read-Host "Enter SamAccountName (or press Enter for default: testuser)"
if ([string]::IsNullOrEmpty($samAccountName)) { $samAccountName = "testuser" }
$email = Read-Host "Enter email (or press Enter for default: testuser@example.invalid)"
if ([string]::IsNullOrEmpty($email)) { $email = "testuser@example.invalid" }

# Insert test user into database
Write-Host "`nInserting test user into database..." -ForegroundColor Yellow

$insertSql = @"
INSERT INTO users (id, ad_guid, sam_account_name, distinguished_name, display_name, first_name, last_name, email, department, title, is_active, is_local_profile) 
VALUES ('$userId', '$($userId.ToString())', '$samAccountName', 'CN=$samAccountName,CN=Users,DC=$($directoryDomain.Replace('.', ',DC='))', '$samAccountName', '$($samAccountName.Split('.')[0])', '$($samAccountName.Split('.')[1])', '$email', 'IT', 'Test User', true, true);

INSERT INTO user_pins (id, user_id, badge_uid, pin_hash, is_active, created_by) 
VALUES (uuid_generate_v4(), '$userId', 'TEST-$(Get-Date -Format 'yyyyMMdd')', '$hash', true, (SELECT id FROM users WHERE ad_guid = 'admin-guid'));
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $insertSql

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to insert test user." -ForegroundColor Red
    exit 1
}

Write-Host "Test user created successfully!" -ForegroundColor Green
Write-Host "User ID: $userId" -ForegroundColor Yellow
Write-Host "Badge UID: TEST-$(Get-Date -Format 'yyyyMMdd')" -ForegroundColor Yellow
