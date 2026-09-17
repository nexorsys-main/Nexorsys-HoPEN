<#
.SYNOPSIS
Safely backs up PINÈDE configurations, certificates, and registry settings.
#>

Requires -RunAsAdministrator

$Timestamp = Get-Date -Format "yyyyMMdd-HHmm"
$BackupRoot = "C:\NexorsysBackup\$Timestamp"
$RegBackupPath = "$BackupRoot\Registry"
$ConfigBackupPath = "$BackupRoot\Config"
$CertBackupPath = "$BackupRoot\Certificates"

New-Item -ItemType Directory -Force -Path $RegBackupPath | Out-Null
New-Item -ItemType Directory -Force -Path $ConfigBackupPath | Out-Null
New-Item -ItemType Directory -Force -Path $CertBackupPath | Out-Null

Write-Host "Starting Safe Backup to $BackupRoot..." -ForegroundColor Cyan

# 1. Backup Configuration Files
Write-Host "Backing up Configuration Files..."
$ConfigDirs = @(
    "$env:ProgramFiles\Nexorsys",
    "$env:ProgramData\Nexorsys"
)

foreach ($dir in $ConfigDirs) {
    if (Test-Path $dir) {
        $files = Get-ChildItem -Path $dir -Recurse -Include "custom_settings.json", "appsettings.json", "*.config" -ErrorAction SilentlyContinue
        foreach ($file in $files) {
            $dest = Join-Path $ConfigBackupPath $file.Name
            Copy-Item -Path $file.FullName -Destination $dest -Force
            Write-Host "  -> Backed up $($file.Name)"
        }
    }
}

# 2. Backup Registry
Write-Host "Backing up Registry Keys..."
$regPaths = @(
    "HKLM\SOFTWARE\Nexorsys",
    "HKLM\SOFTWARE\NexorsysIdentity",
    "HKLM\SOFTWARE\Innovera\Nexorsys"
)
foreach ($rp in $regPaths) {
    $cleanName = $rp -replace "\\", "_"
    $destReg = Join-Path $RegBackupPath "$cleanName.reg"
    
    # Check if exists in registry first
    $psPath = $rp -replace "HKLM", "HKLM:" -replace "HKCU", "HKCU:"
    if (Test-Path $psPath) {
        # Export using reg.exe for native format
        Start-Process -FilePath "reg.exe" -ArgumentList "export `"$rp`" `"$destReg`" /y" -Wait -WindowStyle Hidden
        Write-Host "  -> Exported $rp"
    }
}

# 3. Backup Certificates
Write-Host "Backing up Certificates..."
$certs = Get-ChildItem -Path Cert:\LocalMachine\My | Where-Object { $_.Subject -match "Nexorsys" }
$backupPassword = $env:NEXORSYS_BACKUP_PASSWORD
if ([string]::IsNullOrWhiteSpace($backupPassword)) { throw "Set NEXORSYS_BACKUP_PASSWORD through the deployment secret store." }
$Password = ConvertTo-SecureString $backupPassword -AsPlainText -Force

foreach ($cert in $certs) {
    $destCert = Join-Path $CertBackupPath "$($cert.Thumbprint).pfx"
    Export-PfxCertificate -Cert $cert -FilePath $destCert -Password $Password | Out-Null
    Write-Host "  -> Exported certificate $($cert.Thumbprint) to encrypted backup."
}

Write-Host "Backup completed successfully!" -ForegroundColor Green
Write-Host "Backup Location: $BackupRoot"
