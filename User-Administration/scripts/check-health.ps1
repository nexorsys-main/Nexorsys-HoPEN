# Check application health
# Requires curl

$apiUrl = Read-Host "Enter API base URL (or press Enter for https://localhost:5001)"
if ([string]::IsNullOrEmpty($apiUrl)) { $apiUrl = "https://localhost:5001" }

Write-Host "Checking application health at $apiUrl..." -ForegroundColor Yellow

try {
    # Check API health endpoint
    $healthResponse = Invoke-RestMethod -Uri "$apiUrl/health" -Method Get -TimeoutSec 5
    Write-Host "API Health:" -ForegroundColor Green
    $healthResponse | ConvertTo-Json | Write-Host
    
    # Check if frontend is accessible
    $frontendResponse = Invoke-WebRequest -Uri "$apiUrl" -TimeoutSec 5 -UseBasicParsing
    if ($frontendResponse.StatusCode -eq 200) {
        Write-Host "Frontend is accessible" -ForegroundColor Green
    }
} catch {
    Write-Host "Health check failed: $_" -ForegroundColor Red
}
