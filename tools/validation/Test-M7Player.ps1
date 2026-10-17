param(
    [string]$BuildRoot='.local/m7-v001',
    [string]$Endpoint='https://127.0.0.1:47781',
    [Parameter(Mandatory=$true)][string]$FingerprintFile,
    [int]$TimeoutSeconds=240,
    [int]$DiscoveryPort=47772
)
$ErrorActionPreference='Stop'
$m7Repo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$m7Root=[IO.Path]::GetFullPath((Join-Path $m7Repo $BuildRoot))
$m7Player=Join-Path $m7Root 'Builds/Windows/CatanM7.exe'
$m7Fingerprint=(Get-Content -Raw -LiteralPath $FingerprintFile).Trim()
$m7Modes=@(
    @{Rule='base';Scenario='base';Players=3},
    @{Rule='seafarers';Scenario='new-world';Players=4},
    @{Rule='cities-knights';Scenario='base';Players=3},
    @{Rule='combined';Scenario='through-the-desert';Players=4}
)
foreach($m7Mode in $m7Modes){
    $m7Output=Join-Path $m7Root ('player-network/'+$m7Mode.Rule)
    New-Item -ItemType Directory -Force -Path $m7Output | Out-Null
    $m7Configuration=Join-Path $m7Output 'configuration.json'
    @{Endpoint=$Endpoint;CertificateFingerprint=$m7Fingerprint;RuleSet=$m7Mode.Rule;ScenarioId=$m7Mode.Scenario;OutputDirectory=$m7Output;PlayerCount=$m7Mode.Players;MinimumTurn=3;TimeoutSeconds=$TimeoutSeconds;DiscoveryPort=$DiscoveryPort} | ConvertTo-Json | Set-Content -LiteralPath $m7Configuration -Encoding UTF8
    $m7Log=Join-Path $m7Output 'player.log'
    $m7Args=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--m7-verify',('"'+$m7Configuration+'"'),'-logFile',('"'+$m7Log+'"'))
    $m7Process=Start-Process -FilePath $m7Player -ArgumentList $m7Args -WindowStyle Hidden -PassThru
    try{
        $m7Deadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds+30)
        while(!$m7Process.WaitForExit(1000)){if([DateTime]::UtcNow -gt $m7Deadline){Stop-Process -Id $m7Process.Id;throw ('M7 独立Windows验证超时：'+$m7Mode.Rule)}}
        if($m7Process.ExitCode -ne 0 -or !(Select-String -LiteralPath $m7Log -Pattern 'CATAN_M7_UNITY_PLAYER_OK' -Quiet)){throw "M7 独立Windows验证失败，请检查 $m7Log"}
        # WSS 在部分 Unity Mono 环境不可用时，客户端会记录诊断并使用严格校验证书的 HTTPS 同步。
        # 这类受控降级由 summary.Transport 明确记录；仍拒绝未捕获运行异常与截图失败。
        if(Select-String -LiteralPath $m7Log -Pattern '^(?!CATAN_M7_TRANSPORT_DIAGNOSTIC).*Exception:|Failed to capture screen shot' -Quiet){throw "M7 Windows运行异常，请检查 $m7Log"}
        Write-Host ('M7 独立Windows已通过：'+$m7Mode.Rule+' / '+$m7Mode.Players+'席，服务器AI、权限视图、重连与无本地权威。')
    }
    finally{$m7Process.Dispose()}
}
