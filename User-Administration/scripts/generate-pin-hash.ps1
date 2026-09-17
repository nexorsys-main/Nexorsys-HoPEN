# Generate BCrypt hash for a PIN
# Requires BCrypt.Net-Next library (install via NuGet or use online tool)

$pin = Read-Host -Prompt "Enter PIN to hash (will not be displayed)" -AsSecureString
$pinString = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($pin))

# Install BCrypt package if not available
if (-not (Get-Module -ListAvailable -Name BCrypt))
{
    Write-Host "Installing BCrypt module..."
    Install-Package BCrypt.Net-Next -Force -Scope CurrentUser | Out-Null
}

# Hash the PIN
$hash = [BCrypt.Net.BCrypt]::HashPassword($pinString, [BCrypt.Net.BCrypt]::GenerateSalt(12))
Write-Host "`nBCrypt Hash:" -ForegroundColor Green
Write-Host $hash
