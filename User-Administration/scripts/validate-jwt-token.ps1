# Validate JWT token
# Requires System.IdentityModel.Tokens.Jwt

$token = Read-Host "Enter JWT token to validate"
$jwtKey = Read-Host "Enter JWT Secret Key" -AsSecureString
$jwtKeyString = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($jwtKey))
$issuer = "NexorSys.Identity"
$audience = "NexorSys.Identity.Clients"

try {
    $tokenHandler = New-Object System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
    $key = [Text.Encoding]::ASCII.GetBytes($jwtKeyString)
    
    $validationParameters = New-Object System.IdentityModel.Tokens.TokenValidationParameters
    $validationParameters.ValidateIssuerSigningKey = $true
    $validationParameters.IssuerSigningKey = New-Object System.Security.Cryptography.SymmetricSecurityKey($key)
    $validationParameters.ValidateIssuer = $true
    $validationParameters.ValidateAudience = $true
    $validationParameters.ValidIssuer = $issuer
    $validationParameters.ValidAudience = $audience
    $validationParameters.ClockSkew = [TimeSpan]::Zero
    
    $principal = $tokenHandler.ValidateToken($token, $validationParameters, [ref]$null)
    
    Write-Host "`nToken is VALID" -ForegroundColor Green
    Write-Host "User ID: $($principal.FindFirst("nameidentifier").Value)" -ForegroundColor Yellow
    Write-Host "User Name: $($principal.Identity.Name)" -ForegroundColor Yellow
    Write-Host "Expiration: $($principal.Claims | Where-Object { $_.Type -eq "exp" } | ForEach-Object { [DateTime]::FromBinary([Int64]$_.Value) })" -ForegroundColor Yellow
} catch {
    Write-Host "Token is INVALID: $_" -ForegroundColor Red
}
