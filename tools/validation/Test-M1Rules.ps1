param(
    [string]$ResultsDirectory = '',
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release'
)
$ErrorActionPreference = 'Stop'
$m1RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
. (Join-Path $m1RepoRoot 'tools\development\Enter-DevEnvironment.ps1')
if (!$ResultsDirectory) {
    $ResultsDirectory = Join-Path $m1RepoRoot '.local\m1\test-results'
}
$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
New-Item -ItemType Directory -Path $ResultsDirectory -Force | Out-Null
$env:CATAN_DOTNET = $toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION = $Configuration
$m1TestProject = Join-Path $m1RepoRoot 'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj'
& $toolchain.dotnet test $m1TestProject --configuration $Configuration `
    --results-directory $ResultsDirectory --logger 'trx;LogFileName=m1-rules.trx' `
    --logger 'console;verbosity=normal'
if ($LASTEXITCODE -ne 0) { throw "M1 rules tests failed with exit code $LASTEXITCODE" }
Write-Output "M1 rules test report: $(Join-Path $ResultsDirectory 'm1-rules.trx')"
