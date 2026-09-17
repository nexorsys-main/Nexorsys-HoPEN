# Monitor API performance
# Requires curl and jq (if available)

$apiUrl = Read-Host "Enter API base URL (or press Enter for https://localhost:5001)"
if ([string]::IsNullOrEmpty($apiUrl)) { $apiUrl = "https://localhost:5001" }
$endpoint = Read-Host "Enter API endpoint to test (or press Enter for /api/users)"
if ([string]::IsNullOrEmpty($endpoint)) { $endpoint = "/api/users" }

Write-Host "Testing API performance at $apiUrl$endpoint..." -ForegroundColor Yellow

$stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
try {
    $response = Invoke-RestMethod -Uri "$apiUrl$endpoint" -Method Get -TimeoutSec 10
    $elapsed = $stopwatch.Elapsed.TotalMilliseconds
    
    Write-Host "Response time: $elapsed ms" -ForegroundColor Green
    Write-Host "Status: Success" -ForegroundColor Green
    Write-Host "First 5 results:" -ForegroundColor Yellow
    $response | Select-Object -First 5 | ConvertTo-Json | Write-Host
} catch {
    $elapsed = $stopwatch.Elapsed.TotalMilliseconds
    Write-Host "Response time: $elapsed ms" -ForegroundColor Red
    Write-Host "Status: Failed - $_" -ForegroundColor Red
}
