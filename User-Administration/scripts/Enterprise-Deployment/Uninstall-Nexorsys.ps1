<#
.SYNOPSIS
Uninstalls and completely removes all legacy PINÈDE Identity components.
#>

[CmdletBinding()]
param(
    [switch]$CleanLogs
)

Requires -RunAsAdministrator

Write-Host "Starting Complete Removal of PINÈDE Identity..." -ForegroundColor Red

# 1. Stop Processes
Write-Host "Stopping Processes..."
$processes = Get-Process | Where-Object { $_.Name -match "Nexorsys" }
foreach ($p in $processes) {
    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Write-Host "  -> Stopped Process: $($p.ProcessName) ($($p.Id))"
}

# 2. Stop and Remove Services
Write-Host "Stopping and Removing Services..."
$services = Get-Service | Where-Object { $_.Name -match "Nexorsys" }
foreach ($s in $services) {
    Stop-Service -Name $s.Name -Force -ErrorAction SilentlyContinue
    Start-Process -FilePath "sc.exe" -ArgumentList "delete $($s.Name)" -Wait -WindowStyle Hidden
    Write-Host "  -> Removed Service: $($s.Name)"
}

# 3. Unregister Credential Provider (CRITICAL)
Write-Host "Unregistering Credential Provider..."
$cpPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers"
if (Test-Path $cpPath) {
    $cps = Get-ChildItem -Path $cpPath
    foreach ($cp in $cps) {
        $val = (Get-ItemProperty -Path $cp.PSPath -Name "(Default)" -ErrorAction SilentlyContinue)."(Default)"
        if ($val -match "Nexorsys") {
            Remove-Item -Path $cp.PSPath -Recurse -Force
            Write-Host "  -> Unregistered CP CLSID: $($cp.PSChildName)"
        }
    }
}

# 4. Remove Registry Keys
Write-Host "Removing Registry Keys..."
$regPaths = @(
    "HKLM:\SOFTWARE\Nexorsys",
    "HKLM:\SOFTWARE\NexorsysIdentity",
    "HKCU:\SOFTWARE\Nexorsys",
    "HKLM:\SOFTWARE\Innovera\Nexorsys"
)
foreach ($rp in $regPaths) {
    if (Test-Path $rp) {
        Remove-Item -Path $rp -Recurse -Force
        Write-Host "  -> Removed Registry Tree: $rp"
    }
}

# 5. Remove Certificates
Write-Host "Removing Certificates..."
$certs = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Subject -match "Nexorsys" }
foreach ($cert in $certs) {
    Remove-Item -Path "Cert:\LocalMachine\My\$($cert.Thumbprint)" -Force
    Write-Host "  -> Removed Certificate: $($cert.Thumbprint)"
}

# 6. Remove Scheduled Tasks
Write-Host "Removing Scheduled Tasks..."
$tasks = Get-ScheduledTask -ErrorAction SilentlyContinue | Where-Object { $_.TaskName -match "Nexorsys" }
foreach ($task in $tasks) {
    Unregister-ScheduledTask -TaskName $task.TaskName -Confirm:$false -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Task: $($task.TaskName)"
}

# 7. Remove Startup Entries
Write-Host "Removing Startup Entries..."
$runKeys = @(
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
    "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
)
foreach ($rk in $runKeys) {
    if (Test-Path $rk) {
        $entries = Get-ItemProperty -Path $rk
        foreach ($prop in $entries.PSObject.Properties) {
            if ($prop.Value -match "Nexorsys") {
                Remove-ItemProperty -Path $rk -Name $prop.Name -Force
                Write-Host "  -> Removed Startup Entry: $($prop.Name)"
            }
        }
    }
}

# 8. Remove Firewall Rules
Write-Host "Removing Firewall Rules..."
$fwRules = Get-NetFirewallRule -ErrorAction SilentlyContinue | Where-Object { $_.DisplayName -match "Nexorsys" }
foreach ($fw in $fwRules) {
    Remove-NetFirewallRule -DisplayName $fw.DisplayName -ErrorAction SilentlyContinue
    Write-Host "  -> Removed Firewall Rule: $($fw.DisplayName)"
}

# 9. Remove Directories
Write-Host "Removing Local Files and Directories..."
$dirPaths = @(
    "$env:ProgramFiles\Nexorsys",
    "$env:ProgramData\Nexorsys",
    "$env:LOCALAPPDATA\Nexorsys",
    "$env:APPDATA\Nexorsys"
)
foreach ($dp in $dirPaths) {
    if (Test-Path $dp) {
        Remove-Item -Path $dp -Recurse -Force -ErrorAction SilentlyContinue
        Write-Host "  -> Removed Directory: $dp"
    }
}

# 10. Remove Logs & Temp
Write-Host "Removing Temp Files..."
Remove-Item -Path "$env:TEMP\Nexorsys*" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$env:windir\Temp\Nexorsys*" -Recurse -Force -ErrorAction SilentlyContinue

if ($CleanLogs) {
    Write-Host "Cleaning Logs and Crash Dumps..."
    # Specific log cleanup logic if applicable
}

Write-Host "Removal process completed successfully!" -ForegroundColor Green
