param([string]$BuildRoot='game version/1.7/Catan1.7.3-Ultra-Pasture',[string]$Output='.local/ultra/u2-validation')
$ErrorActionPreference='Stop'
$pastureRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$pastureOutput=[IO.Path]::GetFullPath((Join-Path $pastureRepo $Output))
if(Test-Path -LiteralPath $pastureOutput){throw '请使用新的验收目录，避免混入旧报告。'}
New-Item -ItemType Directory -Path $pastureOutput | Out-Null
$pastureVersion=& (Join-Path $PSScriptRoot '../development/Get-GameVersion.ps1')
$pastureExe=Join-Path $pastureRepo "$BuildRoot/Builds/Windows/Catan$pastureVersion.exe"
$pastureArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--ultra-pasture-verify',('"'+$pastureOutput+'"'),
    '--ultra-theme-fixtures',('"'+(Join-Path $pastureRepo '.local/ultra/u0-fixtures')+'"'),'-logFile',('"'+(Join-Path $pastureOutput 'player.log')+'"'))
$pastureProcess=Start-Process -FilePath $pastureExe -ArgumentList $pastureArgs -WindowStyle Hidden -PassThru
try {
    $pastureClock=[Diagnostics.Stopwatch]::StartNew()
    while(!$pastureProcess.WaitForExit(1000)){if($pastureClock.Elapsed.TotalSeconds -gt 240){throw 'U2 验收超时。'}}
    $pastureReport=Get-Content -LiteralPath (Join-Path $pastureOutput 'report.json') -Raw | ConvertFrom-Json
    if($pastureProcess.ExitCode -ne 0 -or $pastureReport.result -ne '通过'){throw "U2 验收失败：$($pastureReport.error)"}
    Write-Host "U2 草原样板验证通过：$pastureOutput"
} finally {if(!$pastureProcess.HasExited){Stop-Process -Id $pastureProcess.Id}}
