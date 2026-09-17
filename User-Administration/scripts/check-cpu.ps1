# Check CPU usage
Write-Host "Checking CPU usage..." -ForegroundColor Yellow

$cpu = Get-WmiObject -Class Win32_PerfFormattedData_PerfOS_Processor | Select-Object Name, @{Name="PercentProcessorTime";Expression={[math]::Round($_.PercentProcessorTime,2)}} | Format-Table -AutoSize

Write-Host $cpu