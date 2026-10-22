. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m2RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m2Player = Join-Path $m2RepoRoot '.local\m2\Builds\Windows\CatanM2.exe'
if (!(Test-Path -LiteralPath $m2Player)) { throw 'Run tools/development/Build-M2.ps1 first.' }
New-Item -ItemType Directory -Force (Join-Path $m2RepoRoot '.local\m2\logs') | Out-Null
Start-Process -FilePath $m2Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m2RepoRoot '.local\m2\logs\play.log')+'"')) -WindowStyle Normal
