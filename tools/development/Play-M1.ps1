. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$player = Join-Path $root '.local\m1\Builds\Windows\CatanM1.exe'
if (!(Test-Path -LiteralPath $player)) { throw 'Run tools/development/Build-M1.ps1 first.' }
Start-Process -FilePath $player -ArgumentList @('-screen-fullscreen','0','-screen-width','1440','-screen-height','900','-logFile',('"'+(Join-Path $root '.local\m1\logs\play.log')+'"')) -WindowStyle Normal
