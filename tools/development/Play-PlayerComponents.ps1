$ErrorActionPreference = 'Stop'
$componentRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$componentPlayer = Join-Path $componentRepo '.local/art-pipeline/Builds/Windows/CatanPlayerComponents-v002.exe'
if (!(Test-Path -LiteralPath $componentPlayer)) { throw 'Run tools/development/Build-PlayerComponents.ps1 first.' }
Start-Process -FilePath $componentPlayer -ArgumentList '-force-d3d11 -screen-fullscreen 0 -screen-width 1440 -screen-height 900' -WindowStyle Hidden
