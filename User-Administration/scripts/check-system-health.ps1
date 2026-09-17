# Monitor system health
Write-Host "Monitoring system health..." -ForegroundColor Yellow

$healthReport = @()

# Check disk space
$diskSpace = Get-WmiObject -Class Win32_LogicalDisk | Where-Object {$_.DeviceID -eq "C:"} | Select @{Name="Size(GB)";Expression={[math]::Round($_.Size/1GB,2)}}, @{Name="FreeSpace(GB)";Expression={[math]::Round($_.FreeSpace/1GB,2)}}, @{Name="PercentFree";Expression={[math]::Round(($_.FreeSpace/$_.Size)*100,2)}}
$healthReport += "Disk Space: $($diskSpace.PercentFree)% free ($($diskSpace.'FreeSpace(GB)') GB free of $($diskSpace.'Size(GB)') GB)"

# Check memory
$memory = Get-WmiObject -Class Win32_OperatingSystem | Select @{Name="TotalMemory(GB)";Expression={[math]::Round($_.TotalVisibleMemorySize/1MB,2)}}, @{Name="FreeMemory(GB)";Expression={[math]::Round($_.FreePhysicalMemory/1MB,2)}}, @{Name="PercentFree";Expression={[math]::Round(($_.FreePhysicalMemory/$_.TotalVisibleMemorySize)*100,2)}}
$healthReport += "Memory: $($memory.PercentFree)% free ($($memory.'FreeMemory(GB)') GB free of $($memory.'TotalMemory(GB)') GB)"

# Check CPU
$cpu = Get-WmiObject -Class Win32_PerfFormattedData_PerfOS_Processor | Where-Object {$_.Name -eq "_Total"} | Select @{Name="PercentProcessorTime";Expression={[math]::Round($_.PercentProcessorTime,2)}}
$healthReport += "CPU: $($cpu.PercentProcessorTime)% usage"

# Check if services are running
$services = @("postgres", "nexorsys-identity-api", "nexorsys-identity-ui")
foreach ($service in $services)
{
    $serviceStatus = Get-Service -Name $service -ErrorAction SilentlyContinue
    if ($serviceStatus)
    {
        $healthReport += "${service}: $($serviceStatus.Status)"
    }
    else
    {
        $healthReport += "${service}: NOT RUNNING (not installed)"
    }
}

# Output report
Write-Host "`n=== SYSTEM HEALTH REPORT ===" -ForegroundColor Green
foreach ($line in $healthReport)
{
    Write-Host $line
}

# Check if any issues
$issues = $healthReport | Where-Object { $_ -like "*NOT RUNNING*" -or $_ -like "*0% free*" -or $_ -like "*100% used*" }
if ($issues.Count -gt 0)
{
    Write-Host "`nWARNING: System health issues detected!" -ForegroundColor Red
}
else
{
    Write-Host "`nSystem health is good." -ForegroundColor Green
}
