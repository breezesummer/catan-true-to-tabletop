param([string]$BuildRoot='game version/1.6/Catan1.6.6-Operation-History-Polish',[int]$TimeoutSeconds=300,[switch]$Capture)
$ErrorActionPreference='Stop'
$feedbackRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$feedbackPlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
$feedbackOutput=Join-Path $feedbackRepo '.local/feedback-1.6.6/ui'
New-Item -ItemType Directory -Force $feedbackOutput | Out-Null
$feedbackLog=Join-Path $feedbackOutput 'player.log'
$feedbackArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--feedback-verify',('"'+$feedbackOutput+'"'),'-logFile',('"'+$feedbackLog+'"'))
if($Capture){$feedbackArgs+='--feedback-screenshots'}
$feedbackProcess=Start-Process -FilePath $feedbackPlayer -WorkingDirectory $feedbackRepo -ArgumentList $feedbackArgs -WindowStyle Hidden -PassThru
$feedbackDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while(!$feedbackProcess.WaitForExit(1000)) {
    if([DateTime]::UtcNow -gt $feedbackDeadline){Stop-Process -Id $feedbackProcess.Id;throw '卡牌地图交互验证超时。'}
}
if($feedbackProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $feedbackLog -Pattern 'CATAN_INTERACTION_VERIFY_OK:' -Quiet)){
    Get-Content -LiteralPath $feedbackLog -Tail 30
    throw '卡牌地图交互验证失败；后台检查不作为视觉验收。'
}
if(Select-String -LiteralPath $feedbackLog -Pattern 'Exception:|CATAN_INTERACTION_VERIFY_FAILED' -Quiet){throw '卡牌地图交互存在运行异常。'}
Get-Content -LiteralPath (Join-Path $feedbackOutput 'result.json') -Raw
