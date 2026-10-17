param(
    [string]$BuildRoot='.local/Catan1.6.3-AudioVisual',
    [int]$TimeoutSeconds=240,
    [switch]$SkipCapture,
    [switch]$Visible
)
$ErrorActionPreference='Stop'
$avRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$avPlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
$avOutput=Join-Path $avRepo "$BuildRoot/audio-visual-verification"
New-Item -ItemType Directory -Force -Path $avOutput | Out-Null
$avLog=Join-Path $avOutput 'player.log'
$avArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--audio-visual-verify',('"'+$avOutput+'"'),'-logFile',('"'+$avLog+'"'))
if($SkipCapture){$avArgs+='--audio-visual-no-capture'}
$avWindowStyle=if($Visible){'Normal'}else{'Hidden'}
$avProcess=Start-Process -FilePath $avPlayer -ArgumentList $avArgs -WindowStyle $avWindowStyle -PassThru
$avDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$avProcess.WaitForExit(1000))
{
    if([DateTime]::UtcNow -gt $avDeadline){Stop-Process -Id $avProcess.Id;throw '音画专项验证超时；未取得的截图不计为通过。'}
}
if($avProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $avLog -Pattern 'CATAN_AUDIO_VISUAL_OK:' -Quiet))
{Get-Content -LiteralPath $avLog -Tail 35;throw "音画专项验证失败：$avLog"}
if(Select-String -LiteralPath $avLog -Pattern 'Exception:|CATAN_AUDIO_VISUAL_FAILED' -Quiet){throw '音画专项存在运行异常。'}
Get-Content -LiteralPath (Join-Path $avOutput 'result.json') -Raw
