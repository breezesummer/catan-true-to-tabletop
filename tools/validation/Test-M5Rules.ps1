param([string]$ResultsDirectory = '')
$ErrorActionPreference = 'Stop'
$m5RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $m5RepoRoot 'tools\development\Enter-DevEnvironment.ps1')
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $m5RepoRoot '.local\m5\test-results' }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$env:CATAN_DOTNET = $toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION = 'Release'
& $toolchain.dotnet test (Join-Path $m5RepoRoot 'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj') -c Release --disable-build-servers -m:1 `
    --results-directory $ResultsDirectory --logger 'trx;LogFileName=m5-rules.trx' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw "M5 rules and earlier-stage regression tests failed ($LASTEXITCODE)." }
Write-Output "Rules report: $(Join-Path $ResultsDirectory 'm5-rules.trx')"
