[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$PostgresImage = "postgres:16.9-alpine"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repoRoot "backend/Nexorsys.Identity.Solution.sln"
$dockerPath = (Get-Command docker -ErrorAction Stop).Source
$containerName = "nexorsys-ci-pg-$([guid]::NewGuid().ToString('N').Substring(0, 12))"
$passwordBytes = New-Object byte[] 32
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$rng.GetBytes($passwordBytes)
$rng.Dispose()
$password = [BitConverter]::ToString($passwordBytes).Replace('-', '')
$started = $false

try {
    & $dockerPath run --rm -d --name $containerName `
        -e "POSTGRES_PASSWORD=$password" `
        -e "POSTGRES_DB=nexorsys_ci" `
        -p "127.0.0.1::5432" $PostgresImage | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Could not start disposable PostgreSQL test container." }
    $started = $true

    $port = $null
    for ($attempt = 0; $attempt -lt 90; $attempt++) {
        $portMapping = & $dockerPath port $containerName "5432/tcp" 2>$null
        if ($portMapping -match ":(\d+)$") {
            $port = $Matches[1]
            & $dockerPath exec $containerName pg_isready -U postgres -d nexorsys_ci 2>$null | Out-Null
            if ($LASTEXITCODE -eq 0) { break }
        }
        Start-Sleep -Seconds 1
    }
    if (-not $port -or $LASTEXITCODE -ne 0) { throw "Disposable PostgreSQL did not become ready within 90 seconds." }

    $env:NEXORSYS_TEST_POSTGRES = "Host=127.0.0.1;Port=$port;Database=nexorsys_ci;Username=postgres;Password=$password;Include Error Detail=false"
    & dotnet test $solution --configuration $Configuration
    if ($LASTEXITCODE -ne 0) { throw "Solution test suite failed." }
}
finally {
    Remove-Item Env:NEXORSYS_TEST_POSTGRES -ErrorAction SilentlyContinue
    if ($started) { & $dockerPath stop $containerName | Out-Null }
}
