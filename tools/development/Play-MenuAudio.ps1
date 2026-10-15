param([string]$BuildRoot='.local/menu-audio-v001')
$ErrorActionPreference='Stop'
$menuRepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$menuPlayer=Join-Path $menuRepoRoot "$BuildRoot/Builds/Windows/CatanM6.exe"
if(!(Test-Path -LiteralPath $menuPlayer)){throw '请先使用 Build-M7.ps1 -OutputRoot .local/menu-audio-v001 构建田园菜单与音效版。'}
$menuLogDirectory=Join-Path $menuRepoRoot "$BuildRoot/logs"
New-Item -ItemType Directory -Force -Path $menuLogDirectory | Out-Null
Start-Process -FilePath $menuPlayer -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $menuLogDirectory 'play.log')+'"')) -WindowStyle Hidden
