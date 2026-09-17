# Synchronize users from Active Directory (simulated)
# NOTE: This is a simulation. Replace with actual LDAP calls when AD is available.

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

Write-Host "Synchronizing users from Active Directory..." -ForegroundColor Yellow

# Simulate AD query - in reality, you'd query AD for all users
$adUsers = @(
    @{ SamAccountName = "jdoe"; DisplayName = "Example User One"; Email = "user.one@example.invalid"; Department = "IT"; BadgeUid = "DEMO-001" },
    @{ SamAccountName = "jsmith"; DisplayName = "Example User Two"; Email = "user.two@example.invalid"; Department = "HR"; BadgeUid = "DEMO-002" },
    @{ SamAccountName = "bjohnson"; DisplayName = "Example User Three"; Email = "user.three@example.invalid"; Department = "Finance"; BadgeUid = "DEMO-003" }
)

foreach ($adUser in $adUsers)
{
    # Check if user exists in local DB by SamAccountName
    $checkQuery = "SELECT id FROM users WHERE sam_account_name = '$($adUser.SamAccountName)';"
    $result = & psql -U $postgresUser -h localhost -d $databaseName -t -c $checkQuery
    
    if ($result -imatch "row")
    {
        # User exists, update if needed
        Write-Host "Updating user: $($adUser.SamAccountName)" -ForegroundColor Yellow
        $updateQuery = @"
UPDATE users 
SET display_name = '$($adUser.DisplayName)', 
    email = '$($adUser.Email)', 
    department = '$($adUser.Department)', 
    updated_at = NOW()
WHERE sam_account_name = '$($adUser.SamAccountName)';
"@
        & psql -U $postgresUser -h localhost -d $databaseName -c $updateQuery
    }
    else
    {
        # User doesn't exist, create new user
        Write-Host "Creating user: $($adUser.SamAccountName)" -ForegroundColor Green
        $insertUserQuery = @"
INSERT INTO users (id, ad_guid, sam_account_name, distinguished_name, display_name, email, department, is_active, is_local_profile, created_at, updated_at)
VALUES (uuid_generate_v4(), uuid_generate_v4(), '$($adUser.SamAccountName)', 'CN=$($adUser.SamAccountName),CN=Users,DC=$($directoryDomain.Replace('.', ',DC='))', '$($adUser.DisplayName)', '$($adUser.Email)', '$($adUser.Department)', true, false, NOW(), NOW());
"@
        & psql -U $postgresUser -h localhost -d $databaseName -c $insertUserQuery
        
        # Create user pin if badge UID exists
        if (-not [string]::IsNullOrEmpty($adUser.BadgeUid))
        {
            $insertPinQuery = @"
INSERT INTO user_pins (id, user_id, badge_uid, pin_hash, is_active, created_at, updated_at)
SELECT uuid_generate_v4(), u.id, '$($adUser.BadgeUid)', '', true, NOW(), NOW()
FROM users u
WHERE u.sam_account_name = '$($adUser.SamAccountName)';
"@
            & psql -U $postgresUser -h localhost -d $databaseName -c $insertPinQuery
        }
    }
}

Write-Host "User synchronization completed!" -ForegroundColor Green
