<#
.SYNOPSIS
Inventories all PINÈDE Identity components on the local machine.
#>

Requires -RunAsAdministrator

$ReportPathText = ".\Nexorsys-Inventory-Report.txt"
$ReportPathJson = ".\Nexorsys-Inventory-Report.json"

$inventory = @{
    Timestamp = (Get-Date -Format "s")
    Processes = @()
    Services = @()
    CredentialProviders = @()
    RegistryKeys = @()
    Directories = @()
    Certificates = @()
    ScheduledTasks = @()
    StartupEntries = @()
    FirewallRules = @()
}

Write-Host "Starting PINÈDE Discovery..." -ForegroundColor Cyan

# 1. Processes
Write-Host "Scanning processes..."
$inventory.Processes = @(Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "Nexorsys" } | Select-Object Id, ProcessName, Path)

# 2. Services
Write-Host "Scanning services..."
$inventory.Services = @(Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -match "Nexorsys" } | Select-Object Name, DisplayName, Status)

# 3. Credential Providers
Write-Host "Scanning Credential Providers..."
$cpPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers"
if (Test-Path $cpPath) {
    $cps = Get-ChildItem -Path $cpPath
    foreach ($cp in $cps) {
        $val = (Get-ItemProperty -Path $cp.PSPath -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
        if ($val -match "Nexorsys") {
            $inventory.CredentialProviders += @{
                CLSID = $cp.PSChildName
                Name = $val
            }
        }
    }
}

# 4. Registry Keys
Write-Host "Scanning Registry Keys..."
$regPaths = @(
    "HKLM:\SOFTWARE\Nexorsys",
    "HKLM:\SOFTWARE\NexorsysIdentity",
    "HKCU:\SOFTWARE\Nexorsys",
    "HKLM:\SOFTWARE\Innovera\Nexorsys"
)
foreach ($rp in $regPaths) {
    if (Test-Path $rp) {
        $inventory.RegistryKeys += $rp
    }
}

# 5. Directories
Write-Host "Scanning Directories..."
$dirPaths = @(
    "$env:ProgramFiles\Nexorsys",
    "$env:ProgramData\Nexorsys",
    "$env:LOCALAPPDATA\Nexorsys",
    "$env:APPDATA\Nexorsys"
)
foreach ($dp in $dirPaths) {
    if (Test-Path $dp) {
        $inventory.Directories += $dp
    }
}

# Output
$inventory | ConvertTo-Json -Depth 4 | Out-File -FilePath $ReportPathJson -Encoding UTF8
$inventoryText = $inventory | Out-String
$inventoryText | Out-File -FilePath $ReportPathText -Encoding UTF8

Write-Host "Discovery complete! Reports generated:" -ForegroundColor Green
Write-Host " -> $ReportPathJson"
Write-Host " -> $ReportPathText"
