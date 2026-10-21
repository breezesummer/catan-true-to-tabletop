param([string]$OutputRoot='art',[switch]$Install)
$ErrorActionPreference='Stop'
$pastureRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pastureTools=Get-Content -LiteralPath (Join-Path $pastureRepo 'tools/development/toolchain.json') -Raw | ConvertFrom-Json
$pastureOutput=[IO.Path]::GetFullPath((Join-Path $pastureRepo $OutputRoot))
$pastureLog=Join-Path $pastureRepo '.local/ultra/u2-model.log'
New-Item -ItemType Directory -Force -Path (Split-Path $pastureLog) | Out-Null
& $pastureTools.blenderExecutable --background --python-exit-code 1 --python (Join-Path $pastureRepo 'art/recipes/ultra/v003/pasture/build.py') -- --output $pastureOutput *> $pastureLog
if($LASTEXITCODE -ne 0){throw "U2 草原生成失败：$pastureLog"}
& $pastureTools.blenderExecutable --background --python-exit-code 1 --python (Join-Path $pastureRepo 'art/recipes/ultra/v003/pasture/verify_blender.py') -- --root $pastureOutput *>> $pastureLog
if($LASTEXITCODE -ne 0){throw "U2 源文件／FBX回读失败：$pastureLog"}
if($Install){
    $pastureAssets=Join-Path $pastureRepo 'Unity/Catan/Assets/Resources/Ultra/v003'
    New-Item -ItemType Directory -Force -Path $pastureAssets | Out-Null
    Copy-Item -LiteralPath (Join-Path $pastureOutput 'exports/ultra/v003/pasture/pasture-mesh.bytes') -Destination $pastureAssets -Force
}
Get-Content -LiteralPath (Join-Path $pastureOutput 'exports/ultra/v003/pasture/manifest.json')
