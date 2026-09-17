# Test API endpoints
$apiUrl = Read-Host "Enter API base URL (or press Enter for https://localhost:5001)"
if ([string]::IsNullOrEmpty($apiUrl)) { $apiUrl = "https://localhost:5001" }

Write-Host "Testing API endpoints at $apiUrl..." -ForegroundColor Yellow

# Test health endpoint
Write-Host "`n--- Health Check ---" -ForegroundColor Green
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get -TimeoutSec 5
    Write-Host "Status: Healthy" -ForegroundColor Green
} catch {
    Write-Host "Status: Unhealthy - $_" -ForegroundColor Red
}

# Test users endpoint
Write-Host "`n--- Users Endpoint ---" -ForegroundColor Green
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/users" -Method Get -TimeoutSec 5
    Write-Host "Count: $($response.Count)" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

# Test kiosk identify endpoint
Write-Host "`n--- Kiosk Identify ---" -ForegroundColor Green
try {
    $response = Invoke-RestMethod -Uri "$apiUrl/api/kiosk/identify" -Method Post -Body (@{ nfcUid = "TEST-$(Get-Date -Format 'yyyyMMdd')" } | ConvertTo-Json) -ContentType "application/json" -TimeoutSec 5
    Write-Host "Session created: $($response.sessionId)" -ForegroundColor Green
} catch {
    Write-Host "Error: $_" -ForegroundColor Red
}

Write-Host "`nAPI testing completed!" -ForegroundColor Green
