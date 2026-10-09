param([string]$ResultsDirectory = '')
$ErrorActionPreference = 'Stop'
$m3RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $m3RepoRoot 'tools\development\Enter-DevEnvironment.ps1')
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $m3RepoRoot '.local\m3\test-results' }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$env:CATAN_DOTNET = $toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION = 'Release'
& $toolchain.dotnet test (Join-Path $m3RepoRoot 'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj') -c Release --disable-build-servers -m:1 `
    --results-directory $ResultsDirectory --logger 'trx;LogFileName=m3-rules.trx' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw "M3 rules and earlier-stage regression tests failed ($LASTEXITCODE)." }
Write-Output "Rules report: $(Join-Path $ResultsDirectory 'm3-rules.trx')"
