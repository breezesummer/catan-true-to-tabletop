. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m6RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m6Player = Join-Path $m6RepoRoot '.local\m6\Builds\Windows\CatanM6.exe'
if (!(Test-Path -LiteralPath $m6Player)) { throw 'Run tools/development/Build-M6.ps1 first.' }
New-Item -ItemType Directory -Force (Join-Path $m6RepoRoot '.local\m6\logs') | Out-Null
Start-Process -FilePath $m6Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m6RepoRoot '.local\m6\logs\play.log')+'"')) -WindowStyle Normal

