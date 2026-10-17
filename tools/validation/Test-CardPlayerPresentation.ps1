param([string]$BuildRoot='.local/Catan1.6.2-地图修路与界面优化',[int]$TimeoutSeconds=150,[switch]$Visible)
$ErrorActionPreference='Stop'
$reviewRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$reviewRoot=Join-Path $reviewRepo $BuildRoot
$reviewPlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
$reviewEvidence=Join-Path $reviewRoot 'card-player-verification'
New-Item -ItemType Directory -Force -Path $reviewEvidence | Out-Null
$reviewLog=Join-Path $reviewEvidence 'player.log'
$reviewArguments=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--card-player-verify',('"'+$reviewEvidence+'"'),'-logFile',('"'+$reviewLog+'"'))
if(!$Visible){$reviewArguments+='--card-player-headless'}
$reviewStyle=if($Visible){'Normal'}else{'Hidden'}
$reviewProcess=Start-Process -FilePath $reviewPlayer -ArgumentList $reviewArguments -WindowStyle $reviewStyle -PassThru
$reviewTitle=''
$reviewDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$reviewProcess.WaitForExit(1000))
{
    if([DateTime]::UtcNow -gt $reviewDeadline){Stop-Process -Id $reviewProcess.Id;throw '自动亮牌与姓名显示验证超时。'}
    $reviewProcess.Refresh();if($reviewProcess.MainWindowTitle){$reviewTitle=$reviewProcess.MainWindowTitle}
}
if($reviewProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $reviewLog -Pattern 'CATAN_INTERACTION_VERIFY_OK:' -Quiet)){throw "自动亮牌与姓名显示验证失败：$reviewLog"}
if(Select-String -LiteralPath $reviewLog -Pattern 'Exception:|CATAN_INTERACTION_VERIFY_FAILED|CATAN_INTERACTION_GUI_UNAVAILABLE' -Quiet){throw '显示验证出现异常或未取得完整界面截图。'}
if($Visible -and $reviewTitle -ne ('Catan'+(& (Join-Path $PSScriptRoot '../development/Get-GameVersion.ps1')))){throw "游戏窗口名称不符合版本：$reviewTitle"}
Get-Content -LiteralPath (Join-Path $reviewEvidence 'result.json') -Raw
