# Generate system report
$reportFile = "system_report_$(Get-Date -Format 'yyyyMMdd_HHmmss').txt"

$report = @"
PINÈDE IDENTITY - System Report
Generated on: $(Get-Date)
================================

System Information:
------------------
"@

# Add system info
$computerSystem = Get-WmiObject -Class Win32_ComputerSystem
$report += "`nManufacturer: $($computerSystem.Manufacturer)"
$report += "`nModel: $($computerSystem.Model)"
$report += "`nTotal Physical Memory: $([math]::Round($computerSystem.TotalPhysicalMemory/1GB,2)) GB"

# Add OS info
$os = Get-WmiObject -Class Win32_OperatingSystem
$report += "`nOperating System: $($os.Caption) $($os.Version)"
$report += "`nSystem Uptime: $([math]::Round((Get-Date) - $os.LastBootUpTime, 2))"

# Add disk info
$disks = Get-WmiObject -Class Win32_LogicalDisk | Where-Object {$_.DriveType -eq 3}
foreach ($disk in $disks)
{
    $report += "`nDisk $($disk.DeviceID): $([math]::Round($disk.FreeSpace/1GB,2)) GB free of $([math]::Round($disk.Size/1GB,2)) GB ($([math]::Round(($disk.FreeSpace/$disk.Size)*100,2))% free)"
}

# Add service status
$services = @("postgres", "nexorsys-identity-api", "nexorsys-identity-ui")
foreach ($service in $services)
{
    $serviceStatus = Get-Service -Name $service -ErrorAction SilentlyContinue
    if ($serviceStatus)
    {
        $report += "`nService ${service}: $($serviceStatus.Status)"
    }
    else
    {
        $report += "`nService ${service}: NOT INSTALLED"
    }
}

$report += "`n`nReport generated at: $(Get-Date)"

$report | Out-File -FilePath $reportFile -Encoding UTF8

Write-Host "System report generated successfully!" -ForegroundColor Green
Write-Host "File: $reportFile" -ForegroundColor Yellow
