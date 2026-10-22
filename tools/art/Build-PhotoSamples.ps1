param([string]$OutputRoot = '', [switch]$SkipPreviews, [string]$CompareRoot = '')
$ErrorActionPreference = 'Stop'
$sampleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sampleTools = Get-Content -LiteralPath (Join-Path $sampleRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
if (!$OutputRoot) { $OutputRoot = Join-Path $sampleRepo 'art' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$sampleLogs = Join-Path $sampleRepo '.local/art-photo-samples/logs'
New-Item -ItemType Directory -Force -Path $sampleLogs | Out-Null
$sampleRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$sampleArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'generate_photo_samples.py'),'--','--output-root',$OutputRoot)
if ($SkipPreviews) { $sampleArgs += '--skip-previews' }
$ErrorActionPreference = 'Continue'
& $sampleTools.blenderExecutable @sampleArgs *> (Join-Path $sampleLogs "$sampleRun-generate.log")
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0) { throw "Sample generation failed: $sampleLogs/$sampleRun-generate.log" }
$verifyArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'verify_photo_samples.py'),'--','--output-root',$OutputRoot)
if ($CompareRoot) { $verifyArgs += @('--compare-root',[IO.Path]::GetFullPath($CompareRoot)) }
$ErrorActionPreference = 'Continue'
& $sampleTools.blenderExecutable @verifyArgs *> (Join-Path $sampleLogs "$sampleRun-verify.log")
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0) { throw "FBX verification failed: $sampleLogs/$sampleRun-verify.log" }
Write-Host "CATAN_PHOTO_SAMPLES_BUILD_VERIFIED $OutputRoot"
