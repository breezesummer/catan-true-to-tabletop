param([string]$BuildRoot='game version/1.6/Catan1.6.9-Online-Ip-Lobby')
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m6RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m6Player = & (Join-Path $PSScriptRoot 'Find-GamePlayer.ps1') -BuildRoot $BuildRoot
if (!(Test-Path -LiteralPath $m6Player)) { throw "当前试玩程序不存在，请执行 tools/development/Build-M7.ps1 -OutputRoot $BuildRoot。" }
New-Item -ItemType Directory -Force (Join-Path $m6RepoRoot "$BuildRoot/logs") | Out-Null
Start-Process -FilePath $m6Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m6RepoRoot "$BuildRoot/logs/play.log")+'"')) -WindowStyle Normal

