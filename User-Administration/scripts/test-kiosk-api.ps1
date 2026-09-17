# Test Kiosk API endpoints
# Requires curl

$baseUri = Read-Host "Enter API base URL (or press Enter for https://localhost:5001)"
if ([string]::IsNullOrEmpty($baseUri)) { $baseUri = "https://localhost:5001" }
$nfcUid = Read-Host "Enter a test credential identifier (or press Enter to skip)"
if ([string]::IsNullOrEmpty($nfcUid)) { Write-Host "No credential identifier supplied; stopping without sending a request."; exit 0 }

Write-Host "`n=== Testing Identify Endpoint ===" -ForegroundColor Yellow
$identifyResult = Invoke-RestMethod -Uri "$baseUri/api/kiosk/identify" -Method Post -Body (@{ nfcUid = $nfcUid } | ConvertTo-Json) -ContentType "application/json"
Write-Host "Response:" -ForegroundColor Green
$identifyResult | ConvertTo-Json | Write-Host

if ($identifyResult.isIdentified)
{
    $sessionId = $identifyResult.sessionId
    
    Write-Host "`n=== Testing Validate PIN Endpoint ===" -ForegroundColor Yellow
    $pin = Read-Host "Enter PIN to validate (or press Enter to skip)"
    if (-not [string]::IsNullOrEmpty($pin))
    {
        $validateResult = Invoke-RestMethod -Uri "$baseUri/api/kiosk/validate-pin" -Method Post -Body (@{ sessionId = $sessionId; pin = $pin } | ConvertTo-Json) -ContentType "application/json"
        Write-Host "Response:" -ForegroundColor Green
        $validateResult | ConvertTo-Json | Write-Host
    }
}

Write-Host "`n=== Testing Start Session ===" -ForegroundColor Yellow
$startResult = Invoke-RestMethod -Uri "$baseUri/api/kiosk/session/start" -Method Post -Body (@{ sessionId = $sessionId } | ConvertTo-Json) -ContentType "application/json"
Write-Host "Response:" -ForegroundColor Green
$startResult | ConvertTo-Json | Write-Host

Write-Host "`n=== Testing End Session ===" -ForegroundColor Yellow
$endResult = Invoke-RestMethod -Uri "$baseUri/api/kiosk/session/end" -Method Post -Body (@{ sessionId = $sessionId } | ConvertTo-Json) -ContentType "application/json"
Write-Host "Response:" -ForegroundColor Green
$endResult | ConvertTo-Json | Write-Host

Write-Host "`nAll tests completed!" -ForegroundColor Green
