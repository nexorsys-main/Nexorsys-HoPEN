# Seed database with initial data
# Requires PostgreSQL command line tools (psql)
if ($env:NEXORSYS_ALLOW_DEMO_SEED -ne "1" -or $env:NEXORSYS_DEMO_DATABASE -ne "NexorSys_Dev") {
    throw "Demo seed is restricted to the explicitly named NexorSys_Dev database."
}

$postgresUser = "postgres"
$postgresPassword = $env:NEXORSYS_DB_PASSWORD
if ([string]::IsNullOrWhiteSpace($postgresPassword)) { throw "Set NEXORSYS_DB_PASSWORD through the deployment secret store." }
$databaseName = $env:NEXORSYS_DEMO_DATABASE
$demoDomain = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DEMO_DOMAIN)) { "example.invalid" } else { $env:NEXORSYS_DEMO_DOMAIN }
$adminEmail = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DEMO_ADMIN_EMAIL)) { "admin@$demoDomain" } else { $env:NEXORSYS_DEMO_ADMIN_EMAIL }
$identityUrl = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DEMO_IDENTITY_URL)) { "https://identity.$demoDomain" } else { $env:NEXORSYS_DEMO_IDENTITY_URL }
$kioskUrl = if ([string]::IsNullOrWhiteSpace($env:NEXORSYS_DEMO_KIOSK_URL)) { "https://kiosk.$demoDomain" } else { $env:NEXORSYS_DEMO_KIOSK_URL }

# Check if psql is available
if (-not (Get-Command psql -ErrorAction SilentlyContinue))
{
    Write-Host "PostgreSQL command line tools (psql) are not available." -ForegroundColor Red
    Write-Host "Please install PostgreSQL and ensure psql is in your PATH." -ForegroundColor Yellow
    exit 1
}

# Insert admin user
Write-Host "Inserting admin user..." -ForegroundColor Yellow
$adminSql = @"
INSERT INTO users (id, ad_guid, sam_account_name, distinguished_name, display_name, first_name, last_name, email, department, title, is_active, is_local_profile) 
VALUES (uuid_generate_v4(), 'admin-guid', 'administrator', 'CN=Administrator,CN=Users,DC=example,DC=invalid', 'System Administrator', 'System', 'Administrator', '$adminEmail', 'IT', 'System Admin', true, true)
ON CONFLICT (ad_guid) DO NOTHING;
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $adminSql

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to insert admin user." -ForegroundColor Red
    exit 1
}

# Insert default applications
Write-Host "Inserting default applications..." -ForegroundColor Yellow
$appsSql = @"
INSERT INTO applications (id, name, description, client_id, client_secret, redirect_uris, is_active) VALUES
('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11', 'NexorSys Identity UI', 'Admin dashboard and user management', 'nexorsys_identity_ui', NULL, ARRAY['$identityUrl/dashboard'], true),
('a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a12', 'NexorSys Kiosk', 'Kiosk application integration', 'nexorsys_kiosk', NULL, ARRAY['$kioskUrl/callback'], true)
ON CONFLICT (id) DO NOTHING;
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $appsSql

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to insert applications." -ForegroundColor Red
    exit 1
}

# Insert admin permission
Write-Host "Inserting admin permission..." -ForegroundColor Yellow
$permissionSql = @"
INSERT INTO user_permissions (id, user_id, application_id, permission_level, granted_by) 
SELECT uuid_generate_v4(), u.id, a.id, 'admin', u.id
FROM users u, applications a
WHERE u.ad_guid = 'admin-guid' AND a.client_id = 'nexorsys_identity_ui'
ON CONFLICT (id) DO NOTHING;
"@

& psql -U $postgresUser -h localhost -d $databaseName -c $permissionSql

if ($LASTEXITCODE -ne 0)
{
    Write-Host "Failed to insert admin permission." -ForegroundColor Red
    exit 1
}

Write-Host "Database seeding completed successfully!" -ForegroundColor Green
