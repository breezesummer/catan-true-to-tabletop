param([string]$BuildRoot='.local/m6-map-refresh-v001', [string]$LegacyDirectory='', [switch]$Graphics)
$ErrorActionPreference='Stop'
$mapRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$mapStem="Catan"+(& (Join-Path $PSScriptRoot "../development/Get-GameVersion.ps1"))
$mapPlayer=Join-Path $mapRepo "$BuildRoot/Builds/Windows/$mapStem.exe"
$mapMode=if($Graphics){'graphics'}else{'headless'}
$mapOutput=Join-Path $mapRepo "$BuildRoot/map-verification/$mapMode"
New-Item -ItemType Directory -Force -Path $mapOutput | Out-Null
$mapLog=Join-Path $mapOutput 'player.log'
$mapArgs=@('-screen-fullscreen','0','--combined-map-verify',('"'+$mapOutput+'"'),'-logFile',('"'+$mapLog+'"'))
if($Graphics){$mapArgs+='-force-d3d11'}else{$mapArgs+='-nographics'}
if($LegacyDirectory){$mapArgs+=@('--combined-map-legacy',('"'+[IO.Path]::GetFullPath((Join-Path $mapRepo $LegacyDirectory))+'"'))}
$mapProcess=Start-Process -FilePath $mapPlayer -ArgumentList $mapArgs -WindowStyle Hidden -PassThru
$mapDeadline=[DateTime]::UtcNow.AddMinutes(3)
while(!$mapProcess.WaitForExit(1000)){
    if([DateTime]::UtcNow -gt $mapDeadline){Stop-Process -Id $mapProcess.Id;throw '组合地图验收超时。'}
}
if($mapProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $mapLog -Pattern 'CATAN_COMBINED_MAP_OK:' -Quiet)){throw "组合地图验收失败，请检查 $mapLog"}
Get-Content -LiteralPath (Join-Path $mapOutput 'result.json') -Encoding UTF8
