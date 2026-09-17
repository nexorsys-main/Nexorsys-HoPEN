[CmdletBinding()]
param(
    [Parameter(Mandatory, ParameterSetName = 'Range')]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$BaseCommit,

    [Parameter(Mandatory, ParameterSetName = 'Range')]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$HeadCommit,

    [Parameter(Mandatory, ParameterSetName = 'AllHistory')]
    [switch]$AllHistory,

    [Parameter(Mandatory, ParameterSetName = 'WorkingTree')]
    [switch]$WorkingTreeOnly
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$gitleaks = Get-Command gitleaks -ErrorAction Stop
$ignoreFile = Join-Path $repoRoot '.gitleaksignore'

if ($PSCmdlet.ParameterSetName -eq 'WorkingTree') {
    # Scan the checked-out files independently of Git history. This catches a
    # newly introduced secret before it is committed, while --redact keeps
    # scanner output safe to copy into CI logs.
    & $gitleaks.Source dir --redact --no-banner --gitleaks-ignore-path $ignoreFile $repoRoot
} elseif ($PSCmdlet.ParameterSetName -eq 'AllHistory') {
    # Scan every reachable ref. Redaction is mandatory because scanner output is
    # often copied into CI logs and incident records.
    & $gitleaks.Source git --redact --gitleaks-ignore-path $ignoreFile --log-opts='--all' $repoRoot
} else {
    foreach ($commit in @($BaseCommit, $HeadCommit)) {
        & git -C $repoRoot cat-file -e "$commit^{commit}"
        if ($LASTEXITCODE -ne 0) { throw "Commit range endpoint is not available locally." }
    }

    $range = "$BaseCommit..$HeadCommit"
        & $gitleaks.Source git --redact --gitleaks-ignore-path $ignoreFile --log-opts=$range $repoRoot
}
if ($LASTEXITCODE -ne 0) { throw "Gitleaks detected a secret in the supplied commit range or failed to scan it." }
