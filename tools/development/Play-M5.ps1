. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m5RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m5Player = Join-Path $m5RepoRoot '.local\m5\Builds\Windows\CatanM5.exe'
if (!(Test-Path -LiteralPath $m5Player)) { throw 'Run tools/development/Build-M5.ps1 first.' }
New-Item -ItemType Directory -Force (Join-Path $m5RepoRoot '.local\m5\logs') | Out-Null
Start-Process -FilePath $m5Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m5RepoRoot '.local\m5\logs\play.log')+'"')) -WindowStyle Normal

