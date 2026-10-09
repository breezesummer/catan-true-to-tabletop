param([string]$ResultsDirectory = '')
$ErrorActionPreference = 'Stop'
$m4RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $m4RepoRoot 'tools\development\Enter-DevEnvironment.ps1')
if (!$ResultsDirectory) { $ResultsDirectory = Join-Path $m4RepoRoot '.local\m4\test-results' }
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$env:CATAN_DOTNET = $toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION = 'Release'
& $toolchain.dotnet test (Join-Path $m4RepoRoot 'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj') -c Release --disable-build-servers -m:1 `
    --results-directory $ResultsDirectory --logger 'trx;LogFileName=m4-rules.trx' --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw "M4 rules and earlier-stage regression tests failed ($LASTEXITCODE)." }
Write-Output "Rules report: $(Join-Path $ResultsDirectory 'm4-rules.trx')"
