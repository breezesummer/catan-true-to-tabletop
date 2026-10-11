param([switch]$SkipBuild)
$ErrorActionPreference = 'Stop'
$uiRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
if (!$SkipBuild) { & (Join-Path $uiRepo 'tools/development/Build-M6.ps1') -VerifyPlayer }
$uiOutput = Join-Path $uiRepo '.local/operation-ui-v001'
New-Item -ItemType Directory -Force -Path $uiOutput | Out-Null
$uiPlayer = Join-Path $uiRepo '.local/m6/Builds/Windows/CatanM6.exe'
$uiLog = Join-Path $uiOutput 'player.log'
$uiArgs = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',
    '--operation-ui-verify',('"'+$uiOutput+'"'),'-logFile',('"'+$uiLog+'"'))
$uiProcess = Start-Process -FilePath $uiPlayer -ArgumentList $uiArgs -WindowStyle Hidden -PassThru
if (!$uiProcess.WaitForExit(240000)) { Stop-Process -Id $uiProcess.Id; throw 'Operation UI player verification timed out.' }
if ($uiProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $uiLog -Pattern 'CATAN_OPERATION_UI_OK' -Quiet)) {
    throw "Operation UI verification failed; inspect $uiLog"
}
if (Select-String -LiteralPath $uiLog -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw 'Operation UI runtime error.' }
Get-Content (Join-Path $uiOutput 'result.json')
Write-Host 'Private review saves remain in .local. Copy only aggregate results and synthetic review screenshots to public evidence.'
