# Generate JWT token for testing
# Requires System.IdentityModel.Tokens.Jwt (from Microsoft.AspNetCore.Authentication.JwtBearer)

$userId = Read-Host "Enter User ID (GUID)"
$samAccountName = Read-Host "Enter SamAccountName"
$jwtKey = Read-Host "Enter JWT Secret Key" -AsSecureString
$jwtKeyString = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($jwtKey))
$issuer = "NexorSys.Identity"
$audience = "NexorSys.Identity.Clients"

try {
    $tokenHandler = New-Object System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler
    $key = [Text.Encoding]::ASCII.GetBytes($jwtKeyString)
    
    $tokenDescriptor = New-Object System.IdentityModel.Tokens.Jwt.SecurityTokenDescriptor
    $tokenDescriptor.Subject = New-Object System.Security.Claims.ClaimsIdentity @(
        New-Object System.Security.Claims.Claim("nameidentifier", $userId),
        New-Object System.Security.Claims.Claim("name", $samAccountName),
        New-Object System.Security.Claims.Claim("user_id", $userId)
    )
    $tokenDescriptor.Expires = (Get-Date).AddHours(1)
    $tokenDescriptor.Issuer = $issuer
    $tokenDescriptor.Audience = $audience
    $tokenDescriptor.SigningCredentials = New-Object System.IdentityModel.Tokens.Jwt.SigningCredentials -ArgumentList @(New-Object System.Security.Cryptography.SymmetricSecurityKey($key), "HS256")
    
    $token = $tokenHandler.CreateToken($tokenDescriptor)
    $tokenString = $tokenHandler.WriteToken($token)
    
    Write-Host "`nGenerated JWT Token:" -ForegroundColor Green
    Write-Host $tokenString
} catch {
    Write-Host "Error generating token: $_" -ForegroundColor Red
}
