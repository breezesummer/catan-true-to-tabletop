param(
    [string]$OutputRoot = '',
    [string]$CompareRoot = '',
    [switch]$SkipPreviews
)
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$toolchain = Get-Content -LiteralPath (Join-Path $projectRoot 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
if ([string]::IsNullOrWhiteSpace($OutputRoot)) { $OutputRoot = Join-Path $projectRoot 'art' }
$OutputRoot = [System.IO.Path]::GetFullPath($OutputRoot)
$logDirectory = Join-Path $projectRoot '.local/art-validation'
New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
$runId = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$generateArgs = @('--background', '--python-exit-code', '1', '--python', (Join-Path $PSScriptRoot 'generate_mountain.py'), '--', '--output-root', $OutputRoot)
if ($SkipPreviews) { $generateArgs += '--skip-previews' }
& $toolchain.blenderExecutable @generateArgs *> (Join-Path $logDirectory "$runId-generate.log")
if ($LASTEXITCODE -ne 0) { throw "Mountain generation failed. See $logDirectory/$runId-generate.log" }
$verifyArgs = @('--background', '--python-exit-code', '1', '--python', (Join-Path $PSScriptRoot 'verify_mountain.py'), '--', '--output-root', $OutputRoot)
if (-not [string]::IsNullOrWhiteSpace($CompareRoot)) { $verifyArgs += @('--compare-root', [System.IO.Path]::GetFullPath($CompareRoot)) }
& $toolchain.blenderExecutable @verifyArgs *> (Join-Path $logDirectory "$runId-verify.log")
if ($LASTEXITCODE -ne 0) { throw "Mountain export verification failed. See $logDirectory/$runId-verify.log" }
Write-Host "CATAN_M1_MOUNTAIN_BUILD_VERIFIED $OutputRoot"
Write-Host "Logs: $logDirectory/$runId-*.log"
