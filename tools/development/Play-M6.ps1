param([string]$BuildRoot='.local/m6-ui-v005')
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m6RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m6Player = Join-Path $m6RepoRoot "$BuildRoot/Builds/Windows/CatanM6.exe"
if (!(Test-Path -LiteralPath $m6Player)) { throw '请先运行 tools/development/Build-M6.ps1 构建新版。' }
New-Item -ItemType Directory -Force (Join-Path $m6RepoRoot "$BuildRoot/logs") | Out-Null
Start-Process -FilePath $m6Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m6RepoRoot "$BuildRoot/logs/play.log")+'"')) -WindowStyle Normal

