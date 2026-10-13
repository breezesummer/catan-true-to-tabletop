param([string]$BuildRoot='.local/m6-ui-v009',[int]$TimeoutSeconds=300)
$ErrorActionPreference='Stop'
$persistRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$persistPlayer=Join-Path $persistRepo "$BuildRoot/Builds/Windows/CatanM6.exe"
$persistDirectory=Join-Path $persistRepo "$BuildRoot/persistence"
New-Item -ItemType Directory -Force -Path $persistDirectory | Out-Null
foreach($persistMode in @('save','load')){
    $persistLog=Join-Path $persistDirectory "player-$persistMode.log"
    $persistArgs=@('-batchmode','-nographics',"--m6-verify-$persistMode",('"'+$persistDirectory+'"'),'-logFile',('"'+$persistLog+'"'))
    $persistProcess=Start-Process -FilePath $persistPlayer -ArgumentList $persistArgs -WindowStyle Hidden -PassThru
    $persistDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(!$persistProcess.WaitForExit(1000)){
        if([DateTime]::UtcNow -gt $persistDeadline){Stop-Process -Id $persistProcess.Id;throw "独立保存／恢复进程超时：$persistMode"}
    }
    $persistMarker=if($persistMode -eq 'save'){'CATAN_M6_PLAYER_SAVE_OK'}else{'CATAN_M6_PLAYER_LOAD_OK'}
    if($persistProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $persistLog -Pattern $persistMarker -Quiet)){throw "独立保存／恢复验证失败，请检查 $persistLog"}
    Write-Host "独立 $persistMode 进程已通过：四类规则、混合席位、待决交易及保存恢复。"
}
