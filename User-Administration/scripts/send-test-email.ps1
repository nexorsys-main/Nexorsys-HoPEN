# Send test email
# Requires SMTP access

$to = Read-Host "Enter recipient email address"
$subject = "NexorSys Identity - Test Email"
$body = @"
This is a test email from PINÈDE IDENTITY system.

Test time: $(Get-Date)
"@

$smtpServer = $env:NEXORSYS_SMTP_SERVER
$smtpPort = 587
$from = $env:NEXORSYS_SMTP_FROM
if ([string]::IsNullOrWhiteSpace($smtpServer) -or [string]::IsNullOrWhiteSpace($from)) {
    throw "SMTP is not configured. Set NEXORSYS_SMTP_SERVER and NEXORSYS_SMTP_FROM through the deployment secret store."
}
$password = Read-Host "Enter SMTP password or app password" -AsSecureString
$passwordString = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))

Write-Host "Sending test email..." -ForegroundColor Yellow

try {
    $smtp = New-Object Net.Mail.SmtpClient($smtpServer, $smtpPort)
    $smtp.EnableSSL = $true
    $smtp.Credentials = New-Object System.Net.NetworkCredential($from, $passwordString)
    $smtp.Send($from, $to, $subject, $body)
    
    Write-Host "Email sent successfully!" -ForegroundColor Green
} catch {
    Write-Host "Failed to send email: $_" -ForegroundColor Red
}
