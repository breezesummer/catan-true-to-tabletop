param([string]$ResultsDirectory = '')
$ErrorActionPreference = 'Stop'
$m2RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $m2RepoRoot 'tools\development\Enter-DevEnvironment.ps1')
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $m2RepoRoot '.local\m2\test-results' }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$env:CATAN_DOTNET = $toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION = 'Release'
& $toolchain.dotnet test (Join-Path $m2RepoRoot 'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj') -c Release --disable-build-servers -m:1 `
    --results-directory $ResultsDirectory --logger 'trx;LogFileName=m2-rules.trx' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw "M2 rules and M1 regression tests failed ($LASTEXITCODE)." }
Write-Output "Rules report: $(Join-Path $ResultsDirectory 'm2-rules.trx')"
