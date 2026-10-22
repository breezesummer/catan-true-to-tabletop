param([string]$BuildRoot = '.local/m6-entity-v005', [int]$TimeoutSeconds = 300)
$ErrorActionPreference = 'Stop'
$markerRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$markerPlayer = Join-Path $markerRepo "$BuildRoot/Builds/Windows/CatanM6.exe"
if (!(Test-Path -LiteralPath $markerPlayer)) { throw '缺少 M6 程序，请先完成固定 Unity 构建。' }
$markerOutput = Join-Path $markerRepo "$BuildRoot/tabletop-markers-verification"
New-Item -ItemType Directory -Force -Path $markerOutput | Out-Null
$markerLog = Join-Path $markerOutput 'player.log'
$markerResult = Join-Path $markerOutput 'result.json'
if (Test-Path -LiteralPath $markerResult) { Remove-Item -LiteralPath $markerResult }
$markerArgs = @('-batchmode', '-force-d3d11', '-screen-fullscreen', '0', '-screen-width', '1440', '-screen-height', '900',
    '--tabletop-markers-verify', ('"' + $markerOutput + '"'), '-logFile', ('"' + $markerLog + '"'))
$markerProcess = Start-Process -FilePath $markerPlayer -ArgumentList $markerArgs -WindowStyle Hidden -PassThru
$markerDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while (!$markerProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $markerDeadline) {
        Stop-Process -Id $markerProcess.Id
        throw "实体港口后台图形验证超时，请检查 $markerLog"
    }
}
if ($markerProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $markerResult) -or
    !(Select-String -LiteralPath $markerLog -Pattern 'CATAN_TABLETOP_MARKERS_OK:' -Quiet)) {
    throw "实体港口后台图形验证失败，请检查 $markerLog"
}
if (Select-String -LiteralPath $markerLog -Pattern 'Exception:|CATAN_TABLETOP_MARKERS_FAILED:' -Quiet) {
    throw "实体港口后台图形验证出现运行错误，请检查 $markerLog"
}
$markerSummary = Get-Content -Raw -Encoding UTF8 -LiteralPath $markerResult | ConvertFrom-Json
if ($markerSummary.status -ne 'passed' -or $markerSummary.cameraImages -ne 8 -or $markerSummary.detailImages -ne 8 -or $markerSummary.modes.Count -ne 4) {
    throw '实体港口证据不完整：应含四模式的 8 张游戏相机图，以及 8 张资产四视图。'
}
foreach ($markerMode in @('base', 'seafarers', 'cities-knights', 'combined')) {
    foreach ($markerView in @('top', 'game-camera')) {
        if (!(Test-Path -LiteralPath (Join-Path $markerOutput "$markerMode-$markerView.png"))) { throw '缺少游戏相机证据。' }
    }
}
foreach ($markerAsset in $markerSummary.assets) {
    if (!(Test-Path -LiteralPath (Join-Path $markerOutput "details/$markerAsset-four-views.png"))) { throw '缺少实体资产四视图证据。' }
}
Get-Content -Encoding UTF8 -LiteralPath $markerResult
Write-Host '相机图不含 IMGUI；程序几何与来源检查不代替用户外观确认。'
