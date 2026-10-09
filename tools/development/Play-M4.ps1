. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m4RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m4Player = Join-Path $m4RepoRoot '.local\m4\Builds\Windows\CatanM4.exe'
if (!(Test-Path -LiteralPath $m4Player)) { throw 'Run tools/development/Build-M4.ps1 first.' }
New-Item -ItemType Directory -Force (Join-Path $m4RepoRoot '.local\m4\logs') | Out-Null
Start-Process -FilePath $m4Player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $m4RepoRoot '.local\m4\logs\play.log')+'"')) -WindowStyle Normal

