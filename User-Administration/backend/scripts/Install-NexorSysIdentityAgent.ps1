[CmdletBinding(SupportsShouldProcess = $true)]
param(
	[Parameter(Mandatory)] [string] $PublishedAgentExe,
	[Parameter(Mandatory)] [uri] $IdentityApiBaseUrl,
	[Parameter(Mandatory)] [ValidatePattern('^(?:[A-Fa-f0-9]{40})$')] [string] $AgentCertificateThumbprint
)

$ErrorActionPreference = 'Stop'
$serviceName = 'NexorSysIdentityAgent'
$registryPath = 'HKLM:\SOFTWARE\NexorSys\Identity\Agent'

if ($env:OS -ne 'Windows_NT') { throw 'The NexorSys Identity Agent installer requires Windows.' }
$principal = [Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Run elevated as an administrator.' }

$resolvedExe = (Resolve-Path -LiteralPath $PublishedAgentExe).Path
if ([IO.Path]::GetFileName($resolvedExe) -ne 'Nexorsys.WindowsAgent.exe') { throw 'Expected the published Nexorsys.WindowsAgent.exe.' }
$untrustedSids = @('S-1-1-0', 'S-1-5-11', 'S-1-5-32-545', 'S-1-5-4')
$writeRights = [Security.AccessControl.FileSystemRights]::WriteData -bor [Security.AccessControl.FileSystemRights]::AppendData -bor
	[Security.AccessControl.FileSystemRights]::WriteAttributes -bor [Security.AccessControl.FileSystemRights]::WriteExtendedAttributes -bor
	[Security.AccessControl.FileSystemRights]::Delete -bor [Security.AccessControl.FileSystemRights]::DeleteSubdirectoriesAndFiles -bor
	[Security.AccessControl.FileSystemRights]::ChangePermissions -bor [Security.AccessControl.FileSystemRights]::TakeOwnership -bor
	[Security.AccessControl.FileSystemRights]::Modify -bor [Security.AccessControl.FileSystemRights]::FullControl
foreach ($protectedPath in @((Split-Path -Parent $resolvedExe), $resolvedExe)) {
	$acl = Get-Acl -LiteralPath $protectedPath
	foreach ($rule in $acl.Access) {
		if ($rule.AccessControlType -ne [Security.AccessControl.AccessControlType]::Allow) { continue }
		try { $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value } catch { continue }
		if ($sid -in $untrustedSids -and (([int]$rule.FileSystemRights -band [int]$writeRights) -ne 0)) {
			throw "Agent binary or containing directory is writable by an untrusted principal ($($rule.IdentityReference)); install it in a protected location."
		}
	}
}
$signature = Get-AuthenticodeSignature -LiteralPath $resolvedExe
if ($signature.Status -ne [Management.Automation.SignatureStatus]::Valid) { throw 'Agent binary must have a valid Authenticode signature before installation.' }
if ($IdentityApiBaseUrl.Scheme -ne 'https' -or $IdentityApiBaseUrl.IsLoopback -or $IdentityApiBaseUrl.UserInfo -or $IdentityApiBaseUrl.Query -or $IdentityApiBaseUrl.Fragment) {
	throw 'Identity API URL must be a non-loopback HTTPS URL without user info, query, or fragment.'
}

# Certificate enrollment and private-key ACL provisioning are intentionally external PKI steps.
# Agent startup independently verifies the thumbprint, chain, EKU, private-key use, and key DACL.
$serviceAccount = "NT SERVICE\$serviceName"
$thumbprint = $AgentCertificateThumbprint.ToUpperInvariant()
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $service) {
	if ($PSCmdlet.ShouldProcess($resolvedExe, "Create restricted service $serviceName")) {
		& sc.exe create $serviceName "binPath= `"$resolvedExe`"" "start= auto" "obj= $serviceAccount" "DisplayName= NexorSys Identity Agent" | Out-Null
		if ($LASTEXITCODE -ne 0) { throw "sc.exe create failed with exit code $LASTEXITCODE." }
	}
}

if ($PSCmdlet.ShouldProcess($serviceName, 'Apply restricted service SID, recovery, and control permissions')) {
	& sc.exe sidtype $serviceName restricted | Out-Null
	if ($LASTEXITCODE -ne 0) { throw "sc.exe sidtype failed with exit code $LASTEXITCODE." }
	& sc.exe config $serviceName "start= delayed-auto" "obj= $serviceAccount" | Out-Null
	if ($LASTEXITCODE -ne 0) { throw "sc.exe config failed with exit code $LASTEXITCODE." }
	& sc.exe failure $serviceName "reset= 86400" "actions= restart/5000/restart/15000/restart/60000" | Out-Null
	if ($LASTEXITCODE -ne 0) { throw "sc.exe failure failed with exit code $LASTEXITCODE." }
	& sc.exe sdset $serviceName 'D:(A;;CCLCSWRPWPDTLOCRRC;;;SY)(A;;CCDCLCSWRPWPDTLOCRSDRCWDWO;;;BA)' | Out-Null
	if ($LASTEXITCODE -ne 0) { throw "sc.exe sdset failed with exit code $LASTEXITCODE." }
}

if ($PSCmdlet.ShouldProcess($registryPath, 'Write protected non-secret Agent configuration')) {
	New-Item -Path $registryPath -Force | Out-Null
	Set-ItemProperty -LiteralPath $registryPath -Name IdentityApiBaseUrl -Value $IdentityApiBaseUrl.AbsoluteUri
	Set-ItemProperty -LiteralPath $registryPath -Name AgentCertificateThumbprint -Value $thumbprint
	$key = Get-Item -LiteralPath $registryPath
	$acl = $key.GetAccessControl()
	$acl.SetAccessRuleProtection($true, $false)
	$acl.Access | ForEach-Object { [void]$acl.RemoveAccessRule($_) }
	$inheritance = [Security.AccessControl.InheritanceFlags]::None
	$propagation = [Security.AccessControl.PropagationFlags]::None
	$allow = [Security.AccessControl.AccessControlType]::Allow
	foreach ($identity in @('NT AUTHORITY\SYSTEM', 'BUILTIN\Administrators')) {
		$rule = [Security.AccessControl.RegistryAccessRule]::new($identity, [Security.AccessControl.RegistryRights]::FullControl, $inheritance, $propagation, $allow)
		[void]$acl.AddAccessRule($rule)
	}
	$readRule = [Security.AccessControl.RegistryAccessRule]::new($serviceAccount, [Security.AccessControl.RegistryRights]::ReadKey, $inheritance, $propagation, $allow)
	[void]$acl.AddAccessRule($readRule)
	$key.SetAccessControl($acl)
}

Write-Output "Configured $serviceName as $serviceAccount."
Write-Output 'The service was not started: first provision and verify the LocalMachine\My Agent certificate and service-only private-key ACL.'
