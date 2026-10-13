param(
    [ValidateSet('player-knights','player-buildings')][string]$Type,
    [string]$OutputRoot = '',
    [switch]$SkipPreviews,
    [string]$CompareRoot = ''
)
$ErrorActionPreference = 'Stop'
$componentRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$componentTools = Get-Content -LiteralPath (Join-Path $componentRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
if (!$OutputRoot) { $OutputRoot = Join-Path $componentRepo 'art' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$componentTypes = if ($Type) { @($Type) } else { @('player-knights','player-buildings') }
$componentLogs = Join-Path $componentRepo '.local/art-pipeline/logs-v002'
New-Item -ItemType Directory -Force -Path $componentLogs | Out-Null
foreach ($componentType in $componentTypes) {
    $componentRecipe = Join-Path $componentRepo "art/recipes/entity-conversion-v002/$componentType/recipe.json"
    $componentRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
    $componentArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'entity_pipeline_v002.py'),'--','--recipe',$componentRecipe,'--output-root',$OutputRoot)
    if ($SkipPreviews) { $componentArgs += '--skip-previews' }
    & $componentTools.blenderExecutable @componentArgs *> (Join-Path $componentLogs "$componentType-$componentRun-generate.log")
    if ($LASTEXITCODE -ne 0) { throw "Generation failed: $componentType; inspect $componentLogs" }
    $componentArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'verify_entity_pipeline_v002.py'),'--','--recipe',$componentRecipe,'--output-root',$OutputRoot)
    if ($CompareRoot) { $componentArgs += @('--compare-root',[IO.Path]::GetFullPath($CompareRoot)) }
    & $componentTools.blenderExecutable @componentArgs *> (Join-Path $componentLogs "$componentType-$componentRun-verify.log")
    if ($LASTEXITCODE -ne 0) { throw "FBX verification failed: $componentType; inspect $componentLogs" }
    if ($CompareRoot) {
        $componentReport = Join-Path $OutputRoot "exports/entity-conversion-v002/$componentType/verification.json"
        Copy-Item -LiteralPath $componentReport -Destination (Join-Path ([IO.Path]::GetFullPath($CompareRoot)) "exports/entity-conversion-v002/$componentType/clean-rebuild-verification.json")
    }
    Write-Host "CATAN_PLAYER_COMPONENT_TYPE_VERIFIED $componentType"
}
