param([string]$BuildRoot='.local/Catan1.6.2-地图修路与界面优化',[int]$TimeoutSeconds=180,[switch]$Visible)
$ErrorActionPreference='Stop'
$polishRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$polishPlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
$polishOutput=Join-Path $polishRepo "$BuildRoot/table-polish-verification"
New-Item -ItemType Directory -Force -Path $polishOutput | Out-Null
$polishLog=Join-Path $polishOutput 'player.log'
$polishArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--table-polish-verify',('"'+$polishOutput+'"'),'-logFile',('"'+$polishLog+'"'))
$polishStyle=if($Visible){'Normal'}else{'Hidden'}
$polishProcess=Start-Process -FilePath $polishPlayer -ArgumentList $polishArgs -WindowStyle $polishStyle -PassThru
$polishDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$polishProcess.WaitForExit(1000))
{
    if([DateTime]::UtcNow -gt $polishDeadline){Stop-Process -Id $polishProcess.Id;throw '地图交互与界面专项验证超时。'}
}
if($polishProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $polishLog -Pattern 'CATAN_INTERACTION_VERIFY_OK:' -Quiet)){Get-Content -LiteralPath $polishLog -Tail 30;throw "专项验证失败：$polishLog"}
if(Select-String -LiteralPath $polishLog -Pattern 'Exception:|CATAN_INTERACTION_VERIFY_FAILED|CATAN_INTERACTION_GUI_UNAVAILABLE' -Quiet){throw '专项验证存在运行异常或缺少完整界面图。'}
Get-Content -LiteralPath (Join-Path $polishOutput 'result.json') -Raw
