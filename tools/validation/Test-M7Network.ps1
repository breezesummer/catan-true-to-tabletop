param(
    [string]$ServerPath='.local/m7-v001/Builds/Windows/Server/Catan.Server.exe',
    [string]$OutputRoot='.local/m7-v001/network-run',
    [int]$Port=47781,
    [switch]$FullMatrix,
    [switch]$IndependentClients
)
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot '../development/Enter-DevEnvironment.ps1')
$m7Repo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$m7Server=[IO.Path]::GetFullPath((Join-Path $m7Repo $ServerPath))
if(!(Test-Path -LiteralPath $m7Server)){throw '缺少服务程序，请先运行 Build-M7.ps1。'}
$m7Root=[IO.Path]::GetFullPath((Join-Path $m7Repo $OutputRoot))
$m7Run=Join-Path $m7Root ([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$m7Data=Join-Path $m7Run 'private-server'
New-Item -ItemType Directory -Force -Path $m7Run | Out-Null
& $toolchain.dotnet build (Join-Path $m7Repo 'tests/Catan.Network.Probe/Catan.Network.Probe.csproj') -c Release --disable-build-servers -m:1
if($LASTEXITCODE -ne 0){throw '网络探针构建失败。'}
$m7Probe=Join-Path $m7Repo 'tests/Catan.Network.Probe/bin/Release/net10.0/Catan.Network.Probe.dll'
$m7Pin=Join-Path $m7Data 'server-fingerprint.txt'
$m7Process=$null
function Start-M7ProbeServer([string]$suffix){
    $m7Arguments=@('--data-dir',('"'+$m7Data+'"'),'--port',"$Port",'--disable-discovery','--request-limit-per-10s','100000','--ai-interval-ms','20')
    $script:m7Process=Start-Process -FilePath $m7Server -ArgumentList $m7Arguments -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $m7Run "server-$suffix.log") -RedirectStandardError (Join-Path $m7Run "server-$suffix-error.log")
    $m7Deadline=[DateTime]::UtcNow.AddSeconds(45)
    while([DateTime]::UtcNow -lt $m7Deadline){
        if($script:m7Process.HasExited){throw '测试服务未能启动。'}
        $m7Log=Join-Path $m7Run "server-$suffix.log"
        if((Test-Path -LiteralPath $m7Pin) -and (Test-Path -LiteralPath $m7Log) -and (Select-String -LiteralPath $m7Log -Pattern 'SHA-256' -Quiet)){return}
        Start-Sleep -Milliseconds 100
    }
    throw '测试服务启动超时。'
}
function Invoke-M7Probe([string]$mode,[string[]]$extra=@()){
    & $toolchain.dotnet $m7Probe --mode $mode --endpoint "https://127.0.0.1:$Port" --fingerprint $m7Pin --output $m7Run @extra
    if($LASTEXITCODE -ne 0){throw "网络探针失败：$mode"}
}
try{
    Start-M7ProbeServer 'first'
    Invoke-M7Probe 'prepare'
    # 模拟进程在已确认操作后退出；使用独立原目录恢复，测试材料从不写用户真实存档。
    Stop-Process -Id $m7Process.Id
    $m7Process.WaitForExit()
    Start-M7ProbeServer 'restarted'
    Invoke-M7Probe 'resume'
    Invoke-M7Probe 'matrix' $(if($FullMatrix){@('--all')}else{@()})
    if($IndependentClients){
        foreach($m7Rules in @('base','seafarers','cities-knights','combined')){
            $m7Scenario=if($m7Rules -eq 'combined'){'through-the-desert'}elseif($m7Rules -eq 'seafarers'){'heading-for-new-shores'}else{'base'}
            Invoke-M7Probe 'process-game' @('--rules',$m7Rules,'--scenario',$m7Scenario,'--seats','3')
        }
    }
    Write-Host "M7 网络证据已保存：$m7Run（private-* 文件仅本机保留）。"
}
finally{if($m7Process -and !$m7Process.HasExited){Stop-Process -Id $m7Process.Id};if($m7Process){$m7Process.Dispose()}}
