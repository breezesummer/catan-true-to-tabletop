param([string]$BuildRoot='.local/lan-discovery-20261018',[int]$TimeoutSeconds=150)
$ErrorActionPreference='Stop'
$lanRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$lanRoot=[IO.Path]::GetFullPath((Join-Path $lanRepo $BuildRoot))
$lanOutput=Join-Path $lanRoot ('menu-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Force $lanOutput | Out-Null
$lanLog=Join-Path $lanOutput 'player.log'
$lanArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--lan-menu-verify',('"'+$lanOutput+'"'),'-logFile',('"'+$lanLog+'"'))
$lanPlayer=Start-Process -FilePath (Join-Path $lanRoot 'Builds/Windows/CatanM7.exe') -ArgumentList $lanArgs -WindowStyle Hidden -PassThru
try {
    $lanDeadline=[DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while(!$lanPlayer.WaitForExit(1000)) {
        if([DateTime]::UtcNow -gt $lanDeadline){Stop-Process -Id $lanPlayer.Id;throw "局域网菜单验收超时，请查看 $lanLog"}
    }
    if($lanPlayer.ExitCode -ne 0 -or !(Select-String -LiteralPath $lanLog -Pattern '^CATAN_LAN_MENU_OK' -Quiet)){throw "局域网菜单验收失败，请查看 $lanLog"}
    if(Select-String -LiteralPath $lanLog -Pattern '^(?!CATAN_M7_TRANSPORT_DIAGNOSTIC).*Exception:|error CS|CATAN_LAN_MENU_FAILED' -Quiet){throw "客户端运行异常，请查看 $lanLog"}
    Get-Content -LiteralPath (Join-Path $lanOutput 'summary.json')
    Write-Host "局域网菜单功能验收通过：$lanOutput；renderableCaptures 为 0 时不计入视觉验收。"
}
finally {
    # 超时后只清理此验收客户端创建、使用本轮独立目录的服务。
    Get-CimInstance Win32_Process | Where-Object { $_.ParentProcessId -eq $lanPlayer.Id -and $_.Name -eq 'Catan.Server.exe' -and $_.CommandLine.Contains($lanOutput) } | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
    $lanPlayer.Dispose()
}
