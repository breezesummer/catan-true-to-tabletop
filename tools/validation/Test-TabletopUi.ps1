param([switch]$Graphics,[string]$BuildRoot='.local/m6-ui-v009', [int]$TimeoutSeconds = 900)
$ErrorActionPreference = 'Stop'
$tabletopRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$tabletopPlayer = Join-Path $tabletopRepo "$BuildRoot/Builds/Windows/CatanM6.exe"
if (!(Test-Path -LiteralPath $tabletopPlayer)) { throw '缺少 M6 程序，请先完成固定 Unity 构建。' }
$tabletopMode = if ($Graphics) { 'graphics' } else { 'headless' }
$tabletopOutput = Join-Path $tabletopRepo "$BuildRoot/tabletop-verification/$tabletopMode"
New-Item -ItemType Directory -Force -Path $tabletopOutput | Out-Null
$tabletopLog = Join-Path $tabletopOutput 'player.log'
$tabletopArgs = @('-batchmode', '-screen-fullscreen', '0', '-screen-width', '1440', '-screen-height', '900',
    '--tabletop-ui-verify', ('"' + $tabletopOutput + '"'), '-logFile', ('"' + $tabletopLog + '"'))
if ($Graphics) { $tabletopArgs += @('-force-d3d11', '--tabletop-camera-render') }
else { $tabletopArgs += '-nographics' }
$tabletopProcess = Start-Process -FilePath $tabletopPlayer -ArgumentList $tabletopArgs -WindowStyle Hidden -PassThru
$tabletopDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while (!$tabletopProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $tabletopDeadline) {
        Stop-Process -Id $tabletopProcess.Id
        throw "桌面后台验证超时，请检查 $tabletopLog"
    }
}
if ($tabletopProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $tabletopLog -Pattern 'CATAN_TABLETOP_UI_OK:' -Quiet)) {
    throw "桌面后台验证失败，请检查 $tabletopLog"
}
if (Select-String -LiteralPath $tabletopLog -Pattern 'Exception:|CATAN_TABLETOP_UI_FAILED:' -Quiet) {
    throw "桌面后台验证出现运行错误，请检查 $tabletopLog"
}
Get-Content -Encoding UTF8 -LiteralPath (Join-Path $tabletopOutput 'result.json')
Write-Host '仅输出汇总证据；camera-only 不含 UI，完整界面需另行人工检查。'
