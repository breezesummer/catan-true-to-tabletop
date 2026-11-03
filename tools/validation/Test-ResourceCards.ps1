param([string]$BuildRoot='.local/resource-redraw-217',[string]$EvidenceRoot='docs/verification/resource-redraw-20261103',[switch]$Visible)
$ErrorActionPreference='Stop'
$cardRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cardOutput=[IO.Path]::GetFullPath((Join-Path $cardRepo $EvidenceRoot))
$cardPlayer=& (Join-Path $PSScriptRoot '../development/Find-GamePlayer.ps1') -BuildRoot $BuildRoot
New-Item -ItemType Directory -Force $cardOutput | Out-Null
$cardLog=Join-Path $cardOutput 'player.log'
$cardArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--resource-card-verify',('"'+$cardOutput+'"'),'-logFile',('"'+$cardLog+'"'))
$cardStyle=if($Visible){'Normal'}else{'Hidden'}
$cardProcess=Start-Process -FilePath $cardPlayer -ArgumentList $cardArgs -WindowStyle $cardStyle -PassThru
$cardDeadline=[DateTime]::UtcNow.AddSeconds(90)
while(!$cardProcess.WaitForExit(1000)){
    if([DateTime]::UtcNow -gt $cardDeadline){Stop-Process -Id $cardProcess.Id;throw '资源牌视觉检查超时。'}
}
if($cardProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $cardLog -Pattern 'CATAN_RESOURCE_CARDS_OK:' -Quiet)){
    throw "资源牌视觉检查失败：$cardLog"
}
if(Select-String -LiteralPath $cardLog -Pattern 'Exception:|CATAN_RESOURCE_CARDS_FAILED' -Quiet){throw '资源牌检查出现异常。'}
$cardHashes=@(0..3 | ForEach-Object {(Get-FileHash -LiteralPath (Join-Path $cardOutput "theme-$_.png")).Hash} | Sort-Object -Unique)
if($cardHashes.Count -ne 4){throw '四个主题截图未呈现不同结果。'}
Get-Content -LiteralPath (Join-Path $cardOutput 'result.json')
