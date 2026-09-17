<#
.SYNOPSIS
Validates that no PINÈDE components remain on the machine.
#>

Requires -RunAsAdministrator

$ReportPathText = ".\Removal-Validation-Report.txt"
$isClean = $true
$issues = @()

Write-Host "Starting Validation Check..." -ForegroundColor Cyan

# 1. Processes
$proc = Get-Process | Where-Object { $_.Name -match "Nexorsys" }
if ($proc) { $isClean = $false; $issues += "Processes still running: $($proc.Name)" }

# 2. Services
$serv = Get-Service | Where-Object { $_.Name -match "Nexorsys" }
if ($serv) { $isClean = $false; $issues += "Services still installed: $($serv.Name)" }

# 3. Credential Providers
$cpPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers"
if (Test-Path $cpPath) {
    $cps = Get-ChildItem -Path $cpPath
    foreach ($cp in $cps) {
        $val = (Get-ItemProperty -Path $cp.PSPath -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
        if ($val -match "Nexorsys") {
            $isClean = $false
            $issues += "Credential Provider CLSID still registered: $($cp.PSChildName)"
        }
    }
}

# 4. Registry Keys
$regPaths = @("HKLM:\SOFTWARE\Nexorsys", "HKLM:\SOFTWARE\NexorsysIdentity", "HKCU:\SOFTWARE\Nexorsys", "HKLM:\SOFTWARE\Innovera\Nexorsys")
foreach ($rp in $regPaths) {
    if (Test-Path $rp) { $isClean = $false; $issues += "Registry Tree exists: $rp" }
}

# 5. Directories
$dirPaths = @("$env:ProgramFiles\Nexorsys", "$env:ProgramData\Nexorsys", "$env:LOCALAPPDATA\Nexorsys", "$env:APPDATA\Nexorsys")
foreach ($dp in $dirPaths) {
    if (Test-Path $dp) { $isClean = $false; $issues += "Directory exists: $dp" }
}

# 6. Certificates
$certs = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Subject -match "Nexorsys" }
if ($certs) { $isClean = $false; $issues += "Certificates remain: $($certs.Thumbprint)" }

# Output Results
if ($isClean) {
    $msg = "VALIDATION PASSED: Machine is completely clean."
    Write-Host $msg -ForegroundColor Green
    $msg | Out-File -FilePath $ReportPathText -Encoding UTF8
} else {
    $msg = "VALIDATION FAILED: Remnants detected!`n" + ($issues -join "`n")
    Write-Host $msg -ForegroundColor Red
    $msg | Out-File -FilePath $ReportPathText -Encoding UTF8
}

Write-Host "Report saved to $ReportPathText"
