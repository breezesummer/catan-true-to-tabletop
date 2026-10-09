. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m3RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m3Player = Join-Path $m3RepoRoot '.local\m3\Builds\Windows\CatanM3.exe'
if (!(Test-Path -LiteralPath $m3Player)) { throw 'Run tools/development/Build-M3.ps1 first.' }
New-Item -ItemType Directory -Force (Join-Path $m3RepoRoot '.local\m3\logs') | Out-Null
Start-Process -FilePath $m3Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m3RepoRoot '.local\m3\logs\play.log')+'"')) -WindowStyle Normal

