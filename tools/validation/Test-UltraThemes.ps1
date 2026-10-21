param(
    [string]$BuildRoot='game version/1.7/Catan1.7.2-Ultra-Themes',
    [string]$Fixtures='.local/ultra/u0-fixtures',
    [string]$Pending='.local/ultra/u1-pending-v2',
    [string]$Output='.local/ultra/u1-validation',
    [switch]$Network,
    [int]$Port=47881
)
$ErrorActionPreference='Stop'
$themeRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$themeBuild=[IO.Path]::GetFullPath((Join-Path $themeRepo $BuildRoot))
$themeOutput=[IO.Path]::GetFullPath((Join-Path $themeRepo $Output))
if(Test-Path -LiteralPath $themeOutput){throw '验收输出目录必须是新的目录，避免把旧结果误作新结果。'}
New-Item -ItemType Directory -Path $themeOutput -Force | Out-Null
$themeVersion=& (Join-Path $PSScriptRoot '../development/Get-GameVersion.ps1')
$themeExe=Join-Path $themeBuild "Builds/Windows/Catan$themeVersion.exe"
$themeProcesses=@()
$themeServer=$null
function Start-ThemeProbe([string]$role) {
    $themeArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--ultra-theme-verify',('"'+$themeOutput+'"'))
    if($role -eq 'local') {
        $themeArgs+=@('--ultra-theme-fixtures',('"'+(Join-Path $themeRepo $Fixtures)+'"'),'--ultra-theme-pending',('"'+(Join-Path $themeRepo $Pending)+'"'))
    } else {$themeArgs+=@('--ultra-theme-role',$role,'--ultra-theme-endpoint',"http://127.0.0.1:$Port")}
    $themeArgs+=@('-logFile',('"'+(Join-Path $themeOutput ($role+'.log'))+'"'))
    Start-Process -FilePath $themeExe -ArgumentList $themeArgs -WindowStyle Hidden -PassThru
}
try {
    if($Network) {
        $themeServer=Start-Process -FilePath (Join-Path $themeBuild 'Builds/Windows/Server/Catan.Server.exe') -ArgumentList @('--mode','online','--port',"$Port",'--data-dir',('"'+(Join-Path $themeOutput 'private-server')+'"'),'--ai-interval-ms','100') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $themeOutput 'server.log') -RedirectStandardError (Join-Path $themeOutput 'server-error.log')
        $themeRoles=@('host','guest')
    } else {$themeRoles=@('local')}
    foreach($themeRole in $themeRoles){$themeProcesses+=Start-ThemeProbe $themeRole}
    $themeClock=[Diagnostics.Stopwatch]::StartNew()
    foreach($themeProcess in $themeProcesses){while(!$themeProcess.WaitForExit(1000)){if($themeClock.Elapsed.TotalSeconds -gt 390){throw 'U1 验收进程超时。'}}}
    foreach($themeRole in $themeRoles){
        $themeReport=Get-Content -LiteralPath (Join-Path $themeOutput ($themeRole+'-report.json')) -Raw | ConvertFrom-Json
        if($themeReport.result -ne '通过'){throw "U1 $themeRole 验证失败，请检查日志。"}
        Write-Host "U1 $themeRole 验证通过：$themeOutput"
    }
} finally {
    foreach($themeProcess in $themeProcesses){if(!$themeProcess.HasExited){Stop-Process -Id $themeProcess.Id}}
    if($themeServer -and !$themeServer.HasExited){Stop-Process -Id $themeServer.Id}
}
