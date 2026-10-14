param([string]$OutputRoot = '', [switch]$SkipPreviews, [string]$CompareRoot = '')
$ErrorActionPreference = 'Stop'
$markersRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$markersTools = Get-Content -LiteralPath (Join-Path $markersRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
$markersPython = 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$markersRecipeRoot = Join-Path $markersRepo 'art/recipes/entity-conversion-v005/tabletop-markers'
if (!$OutputRoot) { $OutputRoot = Join-Path $markersRepo 'art' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$markersLogRoot = Join-Path $markersRepo '.local/art-pipeline/logs'
New-Item -ItemType Directory -Force -Path $markersLogRoot | Out-Null
$markersRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
& $markersPython (Join-Path $markersRecipeRoot 'prepare.py')
if ($LASTEXITCODE -ne 0) { throw '港口与标记原照归档/几何校正失败' }
$markersArgs = @('--background','--python-exit-code','1','--python',(Join-Path $markersRecipeRoot 'build.py'),'--','--recipe',(Join-Path $markersRecipeRoot 'recipe.json'),'--output-root',$OutputRoot)
if ($SkipPreviews) { $markersArgs += '--skip-previews' }
$ErrorActionPreference = 'Continue'
& $markersTools.blenderExecutable @markersArgs *> (Join-Path $markersLogRoot "tabletop-markers-$markersRun-generate.log")
$markersExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($markersExit -ne 0) { throw "港口与标记生成失败，查看 $markersLogRoot" }
$markersArgs = @('--background','--python-exit-code','1','--python',(Join-Path $markersRecipeRoot 'verify.py'),'--','--recipe',(Join-Path $markersRecipeRoot 'recipe.json'),'--output-root',$OutputRoot)
if ($CompareRoot) { $markersArgs += @('--compare-root',[IO.Path]::GetFullPath($CompareRoot)) }
$ErrorActionPreference = 'Continue'
& $markersTools.blenderExecutable @markersArgs *> (Join-Path $markersLogRoot "tabletop-markers-$markersRun-verify.log")
$markersExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($markersExit -ne 0) { throw "港口与标记回读验证失败，查看 $markersLogRoot" }
if ($CompareRoot) {
    $markersVerification = Join-Path $OutputRoot 'exports/entity-conversion-v005/tabletop-markers/verification.json'
    $markersReport = Get-Content -LiteralPath $markersVerification -Raw | ConvertFrom-Json
    if ($markersReport.status -eq 'passed' -and $markersReport.cleanRebuild.sameGeometryMaterialsTexturesReferences) {
        Copy-Item -LiteralPath $markersVerification -Destination (Join-Path ([IO.Path]::GetFullPath($CompareRoot)) 'exports/entity-conversion-v005/tabletop-markers/clean-rebuild-verification.json')
    }
}
& $markersPython (Join-Path $markersRecipeRoot 'audit.py') --output-root $OutputRoot
if ($LASTEXITCODE -ne 0) { throw '港口与标记来源和尺寸复核失败' }
Write-Host 'CATAN_TABLETOP_MARKERS_VERIFIED photographs=8 assets=8'
