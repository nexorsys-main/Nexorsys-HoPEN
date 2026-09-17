# Extract deployment package
$zipFile = Read-Host "Enter ZIP file path"
$extractDir = "extracted_$(Get-Date -Format 'yyyyMMdd_HHmmss')"

Write-Host "Extracting deployment package..." -ForegroundColor Yellow

# Check if file exists
if (-not (Test-Path $zipFile))
{
    Write-Host "File not found: $zipFile" -ForegroundColor Red
    exit 1
}

# Extract archive
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $extractDir)

Write-Host "Package extracted successfully!" -ForegroundColor Green
Write-Host "Location: $extractDir" -ForegroundColor Yellow