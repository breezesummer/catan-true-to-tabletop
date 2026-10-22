$ErrorActionPreference = 'Stop'
$entityRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$entityPlayer = Join-Path $entityRepo '.local/art-pipeline/Builds/Windows/CatanEntityAssets-v001.exe'
if (!(Test-Path -LiteralPath $entityPlayer)) { throw 'Build entity gallery first with Build-EntityAssets.ps1' }
Start-Process -FilePath $entityPlayer -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900')
