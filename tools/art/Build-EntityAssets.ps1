param([Parameter(Mandatory=$true)][string]$Type, [string]$OutputRoot = '', [switch]$SkipPreviews, [string]$CompareRoot = '')
$ErrorActionPreference = 'Stop'
$entityRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$entityTools = Get-Content -LiteralPath (Join-Path $entityRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
$entityPython = 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
if ($Type -notmatch '^[a-z][a-z0-9-]+$') { throw 'Invalid type identifier' }
$entityRecipe = Join-Path $entityRepo "art/recipes/entity-conversion-v001/$Type/recipe.json"
if (!(Test-Path -LiteralPath $entityRecipe)) { throw "Missing recipe: $entityRecipe" }
if (!$OutputRoot) { $OutputRoot = Join-Path $entityRepo 'art' }
$OutputRoot = [IO.Path]::GetFullPath($OutputRoot)
$entityLogRoot = Join-Path $entityRepo '.local/art-pipeline/logs'
New-Item -ItemType Directory -Force -Path $entityLogRoot | Out-Null
$entityRun = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
& $entityPython (Join-Path $PSScriptRoot 'rectify_entity_photos.py') --recipe $entityRecipe
if ($LASTEXITCODE -ne 0) { throw "Photo correction failed: $Type" }
$entityArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'entity_pipeline.py'),'--','--recipe',$entityRecipe,'--output-root',$OutputRoot)
if ($SkipPreviews) { $entityArgs += '--skip-previews' }
$ErrorActionPreference = 'Continue'
& $entityTools.blenderExecutable @entityArgs *> (Join-Path $entityLogRoot "$Type-$entityRun-generate.log")
$entityExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($entityExit -ne 0) { throw "Entity generation failed: $Type; inspect $entityLogRoot" }
$entityArgs = @('--background','--python-exit-code','1','--python',(Join-Path $PSScriptRoot 'verify_entity_pipeline.py'),'--','--recipe',$entityRecipe,'--output-root',$OutputRoot)
if ($CompareRoot) { $entityArgs += @('--compare-root',[IO.Path]::GetFullPath($CompareRoot)) }
$ErrorActionPreference = 'Continue'
& $entityTools.blenderExecutable @entityArgs *> (Join-Path $entityLogRoot "$Type-$entityRun-verify.log")
$entityExit = $LASTEXITCODE
$ErrorActionPreference = 'Stop'
if ($entityExit -ne 0) { throw "Entity FBX verification failed: $Type; inspect $entityLogRoot" }
if ($CompareRoot) {
    $entityComparison = Join-Path $OutputRoot "exports/entity-conversion-v001/$Type/verification.json"
    $entityComparedRoot = [IO.Path]::GetFullPath($CompareRoot)
    $entityReport = Get-Content -LiteralPath $entityComparison -Raw | ConvertFrom-Json
    if ($entityReport.status -eq 'passed' -and $entityReport.cleanRebuild.sameGeometryMaterialsTexturesReferences) {
        Copy-Item -LiteralPath $entityComparison -Destination (Join-Path $entityComparedRoot "exports/entity-conversion-v001/$Type/clean-rebuild-verification.json")
    }
}
& $entityPython (Join-Path $PSScriptRoot 'update_entity_checklist.py')
if ($LASTEXITCODE -ne 0) { throw 'Checklist update failed' }
Write-Host "CATAN_ENTITY_TYPE_VERIFIED $Type"
