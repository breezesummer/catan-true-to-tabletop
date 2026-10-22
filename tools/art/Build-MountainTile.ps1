param([string]$OutputRoot = 'art', [switch]$SkipPreviews, [string]$CompareRoot = '')
$ErrorActionPreference = 'Stop'
$mountainRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$mountainTools = Get-Content -LiteralPath (Join-Path $mountainRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
$mountainPython = 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$mountainRecipe = Join-Path $mountainRepo 'art/recipes/entity-conversion-v006/ore-tile'
if (![IO.Path]::IsPathRooted($OutputRoot)) { $OutputRoot = Join-Path $mountainRepo $OutputRoot }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$mountainLogs = Join-Path $mountainRepo '.local/art-mountain-v006/logs'
New-Item -ItemType Directory -Force -Path $mountainLogs | Out-Null
$mountainRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
& $mountainPython (Join-Path $mountainRecipe 'prepare.py')
if ($LASTEXITCODE -ne 0) { throw '高山原照归档或几何校正失败' }
$mountainArgs = @('--background', '--python-exit-code', '1', '--python', (Join-Path $mountainRecipe 'build.py'), '--', '--recipe', (Join-Path $mountainRecipe 'recipe.json'), '--output-root', $OutputRoot)
if ($SkipPreviews) { $mountainArgs += '--skip-previews' }
& $mountainTools.blenderExecutable @mountainArgs *> (Join-Path $mountainLogs "$mountainRun-generate.log")
if ($LASTEXITCODE -ne 0) { throw "高山厚板生成失败：$mountainLogs" }
$mountainArgs = @('--background', '--python-exit-code', '1', '--python', (Join-Path $mountainRecipe 'verify.py'), '--', '--recipe', (Join-Path $mountainRecipe 'recipe.json'), '--output-root', $OutputRoot)
if ($CompareRoot) {
    if (![IO.Path]::IsPathRooted($CompareRoot)) { $CompareRoot = Join-Path $mountainRepo $CompareRoot }
    $CompareRoot = [IO.Path]::GetFullPath($CompareRoot)
    $mountainArgs += @('--compare-root', $CompareRoot)
}
& $mountainTools.blenderExecutable @mountainArgs *> (Join-Path $mountainLogs "$mountainRun-verify.log")
if ($LASTEXITCODE -ne 0) { throw "高山厚板回读失败：$mountainLogs" }
if ($CompareRoot) {
    $mountainVerification = Join-Path $OutputRoot 'exports/entity-conversion-v006/ore-tile/verification.json'
    Copy-Item -LiteralPath $mountainVerification -Destination (Join-Path $CompareRoot 'exports/entity-conversion-v006/ore-tile/clean-rebuild-verification.json')
}
Write-Host 'CATAN_MOUNTAIN_TILE_VERIFIED assets=1'
