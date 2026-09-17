# Send notifications
# Requires configured notification services

$notificationType = Read-Host "Enter notification type (email/sms/console)"
$recipients = Read-Host "Enter recipient(s) (comma-separated)"
$message = Read-Host "Enter message"

Write-Host "Sending $notificationType notification to $recipients..." -ForegroundColor Yellow

switch ($notificationType.ToLower())
{
    "email" {
        # Simple email using built-in SMTP client
        $subject = "NexorSys Identity notification - $(Get-Date)"
        $smtpServer = $env:NEXORSYS_SMTP_SERVER
        $smtpFrom = $env:NEXORSYS_SMTP_FROM
        if ([string]::IsNullOrWhiteSpace($smtpServer) -or [string]::IsNullOrWhiteSpace($smtpFrom)) {
            Write-Host "SMTP is not configured. Set NEXORSYS_SMTP_SERVER and NEXORSYS_SMTP_FROM through the deployment secret store." -ForegroundColor Red
            break
        }
        
        try {
            Send-MailMessage -To $recipients -Subject $subject -Body $message -From $smtpFrom -SmtpServer $smtpServer -Port 587 -UseSSL -Credential (Get-Credential) | Out-Null
            Write-Host "Email sent successfully!" -ForegroundColor Green
        } catch {
            Write-Host "Failed to send email: $_" -ForegroundColor Red
        }
    }
    "sms" {
        Write-Host "SMS notification not implemented in this script." -ForegroundColor Yellow
    }
    "console" {
        Write-Host "Console notification:" -ForegroundColor Green
        Write-Host $message
    }
    default {
        Write-Host "Unknown notification type." -ForegroundColor Red
    }
}
