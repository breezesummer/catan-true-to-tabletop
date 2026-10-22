param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$FixtureDirectory='.local/ultra/u0-fixtures',
    [ValidateRange(3,600)][int]$SampleSeconds=30,
    [ValidateRange(2,120)][int]$WarmupSeconds=8,
    [ValidateRange(0,120)][int]$DrainFrames=16,
    [switch]$VisibleWindow,
    [ValidateRange(640,7680)][int]$Width=1920,
    [ValidateRange(480,4320)][int]$Height=1080,
    [switch]$RegenerateFixtures
)
$ErrorActionPreference='Stop'
$baselineRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function Resolve-BaselinePath([string]$Value) {
    if ([IO.Path]::IsPathRooted($Value)) { return [IO.Path]::GetFullPath($Value) }
    return [IO.Path]::GetFullPath((Join-Path $baselineRepo $Value))
}
$baselinePlayer=Resolve-BaselinePath $Player
$baselineOutput=Resolve-BaselinePath $OutputDirectory
$baselineFixtures=Resolve-BaselinePath $FixtureDirectory
$baselinePrivateRoot=(Join-Path $baselineRepo '.local')+[IO.Path]::DirectorySeparatorChar
if (!$baselineFixtures.StartsWith($baselinePrivateRoot,[StringComparison]::OrdinalIgnoreCase)) { throw '权威夹具必须保存在仓库 .local 内。' }
if (!(Test-Path -LiteralPath $baselinePlayer -PathType Leaf)) { throw "游戏程序不存在：$baselinePlayer" }
if (Test-Path -LiteralPath (Join-Path $baselineOutput 'result.json')) { throw '输出目录已有结果；请为每轮采样选择独立目录，防止旧结果混入。' }
. (Join-Path $baselineRepo 'tools/development/Enter-DevEnvironment.ps1')
if ($RegenerateFixtures -or !(Test-Path -LiteralPath (Join-Path $baselineFixtures 'public-manifest.json'))) {
    & $toolchain.dotnet run --project (Join-Path $PSScriptRoot 'UltraBaselineFixtures/UltraBaselineFixtures.csproj') -c Release -- $baselineRepo $baselineFixtures
    if ($LASTEXITCODE -ne 0) { throw '合法夹具生成或独立恢复检查失败。' }
}
foreach ($baselineCase in @('ordinary','maximum','crowded','fog')) {
    if (!(Test-Path -LiteralPath (Join-Path $baselineFixtures "$baselineCase.json"))) { throw "缺少私有夹具：$baselineCase" }
}
New-Item -ItemType Directory -Path $baselineOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $baselineFixtures 'public-manifest.json') -Destination (Join-Path $baselineOutput 'fixtures.json')
$baselineLog=Join-Path $baselineOutput 'player.log'
$baselineArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,
    '--ultra-baseline-verify',('"'+$baselineOutput+'"'),'--ultra-baseline-fixtures',('"'+$baselineFixtures+'"'),
    '--ultra-baseline-seconds',$SampleSeconds,'--ultra-baseline-warmup',$WarmupSeconds,'--ultra-baseline-drain',$DrainFrames,'-logFile',('"'+$baselineLog+'"'))
$baselineStarted=[DateTimeOffset]::UtcNow
$baselineWindowStyle=if($VisibleWindow){'Normal'}else{'Hidden'}
$baselineProcess=Start-Process -FilePath $baselinePlayer -ArgumentList $baselineArgs -WindowStyle $baselineWindowStyle -PassThru
$baselineDeadline=[DateTime]::UtcNow.AddSeconds(4*($SampleSeconds+$WarmupSeconds)+300)
while (!$baselineProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $baselineDeadline) { Stop-Process -Id $baselineProcess.Id; throw 'U0 基线采样超时。' }
}
$baselineResultPath=Join-Path $baselineOutput 'result.json'
if ($baselineProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $baselineResultPath) -or
    !(Select-String -LiteralPath $baselineLog -Pattern 'CATAN_ULTRA_BASELINE_OK:' -Quiet)) {
    throw "U0 基线采样失败，请查看 $baselineLog"
}
$baselineResult=Get-Content -LiteralPath $baselineResultPath -Raw | ConvertFrom-Json
if ($baselineResult.status -ne 'passed') { throw '采样未通过完整性检查。' }
$baselineRun=[ordered]@{
    schema='ultra-baseline-launch-v1'
    startedUtc=$baselineStarted.ToString('o')
    finishedUtc=[DateTimeOffset]::UtcNow.ToString('o')
    playerSha256=(Get-FileHash -LiteralPath $baselinePlayer -Algorithm SHA256).Hash.ToLowerInvariant()
    width=$Width;height=$Height;sampleSeconds=$SampleSeconds;warmupSeconds=$WarmupSeconds
    drainFrames=$DrainFrames
    hiddenWindow=(!$VisibleWindow);batchMode=$false;graphics='Direct3D11'
    note='真实渲染的独立构建自动采样；窗口方式见hiddenWindow，焦点见summary；不视为人工交互、Android 真机或热稳定验收。权威夹具只在 .local。'
}
$baselineRun | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $baselineOutput 'launch.json') -Encoding UTF8
foreach ($baselineCase in @('ordinary','maximum','crowded','fog')) {
    $baselineSummary=Get-Content -LiteralPath (Join-Path $baselineOutput "$baselineCase/summary.json") -Raw | ConvertFrom-Json
    [pscustomobject]@{局面=$baselineCase;帧数=$baselineSummary.sampleCount;P50毫秒=$baselineSummary.frameMs.p50;P95毫秒=$baselineSummary.frameMs.p95;P99毫秒=$baselineSummary.frameMs.p99;大于50毫秒=$baselineSummary.spikesOver50Ms}
}
