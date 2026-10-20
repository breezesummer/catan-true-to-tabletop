param(
    [Parameter(Mandatory=$true)][string]$Player,
    [Parameter(Mandatory=$true)][string]$OutputDirectory,
    [string]$FixtureDirectory='.local/ultra/u0-fixtures',
    [ValidateRange(3,600)][int]$SampleSeconds=30,
    [ValidateRange(2,120)][int]$WarmupSeconds=8,
    [ValidateRange(640,7680)][int]$Width=1920,
    [ValidateRange(480,4320)][int]$Height=1080,
    [switch]$RegenerateFixtures,
    [switch]$VisibleWindow,
    [switch]$CaptureOnly
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
$baselineCases=@('ordinary','maximum','crowded','fog')
$baselineManifest=Get-Content -LiteralPath (Join-Path $baselineFixtures 'public-manifest.json') -Raw | ConvertFrom-Json
if ($baselineManifest.schema -ne 'ultra-baseline-fixtures-v1' -or @($baselineManifest.scenarios).Count -ne 4) {
    throw '夹具清单格式不正确，请使用 -RegenerateFixtures 重新生成。'
}
foreach ($baselineCase in $baselineCases) {
    $baselineFixtureFile=Join-Path $baselineFixtures "$baselineCase.json"
    if (!(Test-Path -LiteralPath $baselineFixtureFile -PathType Leaf)) { throw "缺少私有夹具：$baselineCase" }
    $baselineEntries=@($baselineManifest.scenarios | Where-Object { $_.id -eq $baselineCase })
    if ($baselineEntries.Count -ne 1 -or [string]$baselineEntries[0].fileSha256 -notmatch '^[0-9a-fA-F]{64}$') {
        throw "夹具缺少唯一的文件校验值：$baselineCase；请使用 -RegenerateFixtures 重新生成。"
    }
    $baselineFixtureHash=(Get-FileHash -LiteralPath $baselineFixtureFile -Algorithm SHA256).Hash
    if (![string]::Equals($baselineFixtureHash,[string]$baselineEntries[0].fileSha256,[StringComparison]::OrdinalIgnoreCase)) {
        throw "夹具文件 SHA256 与清单不符，已拒绝启动采样：$baselineCase"
    }
}
$baselineManaged=Join-Path (Split-Path -Parent $baselinePlayer) ([IO.Path]::GetFileNameWithoutExtension($baselinePlayer)+'_Data/Managed')
$baselineAssemblies=@(foreach ($baselineAssembly in @('Assembly-CSharp.dll','Catan.Core.dll','Catan.AI.dll','Catan.Network.Contracts.dll')) {
    $baselineAssemblyPath=Join-Path $baselineManaged $baselineAssembly
    if (!(Test-Path -LiteralPath $baselineAssemblyPath -PathType Leaf)) { throw "缺少待测游戏程序集：$baselineAssembly" }
    [ordered]@{file=$baselineAssembly;sha256=(Get-FileHash -LiteralPath $baselineAssemblyPath -Algorithm SHA256).Hash.ToLowerInvariant()}
})
$baselinePlayerHash=(Get-FileHash -LiteralPath $baselinePlayer -Algorithm SHA256).Hash.ToLowerInvariant()
foreach ($baselineAssemblyEntry in $baselineAssemblies) {
    if ($baselineAssemblyEntry.sha256 -notmatch '^[0-9a-f]{64}$') { throw '待测程序集校验值无效。' }
}
New-Item -ItemType Directory -Path $baselineOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $baselineFixtures 'public-manifest.json') -Destination (Join-Path $baselineOutput 'fixtures.json')
$baselineLog=Join-Path $baselineOutput 'player.log'
$baselineArgs=@('-force-d3d11','-screen-fullscreen','0','-screen-width',$Width,'-screen-height',$Height,
    '--ultra-baseline-verify',('"'+$baselineOutput+'"'),'--ultra-baseline-fixtures',('"'+$baselineFixtures+'"'),
    '--ultra-baseline-seconds',$SampleSeconds,'--ultra-baseline-warmup',$WarmupSeconds,'-logFile',('"'+$baselineLog+'"'))
if ($VisibleWindow) { $baselineArgs+='--ultra-baseline-visible-window' }
if ($CaptureOnly) { $baselineArgs+='--ultra-baseline-capture-only' }
$baselineWindowStyle=if($VisibleWindow){'Normal'}else{'Hidden'}
$baselineStarted=[DateTimeOffset]::UtcNow
$baselineProcess=Start-Process -FilePath $baselinePlayer -ArgumentList $baselineArgs -WindowStyle $baselineWindowStyle -PassThru
$baselineDeadline=[DateTime]::UtcNow.AddSeconds(4*($SampleSeconds+$WarmupSeconds)+300)
while (!$baselineProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $baselineDeadline) { Stop-Process -Id $baselineProcess.Id; throw 'U0 基线采样超时。' }
}
$baselineResultPath=Join-Path $baselineOutput 'result.json'
$baselineSuccessMarker=if($CaptureOnly){'CATAN_ULTRA_BASELINE_CAPTURE_ONLY_OK:'}else{'CATAN_ULTRA_BASELINE_OK:'}
if ($baselineProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $baselineResultPath) -or
    !(Select-String -LiteralPath $baselineLog -Pattern $baselineSuccessMarker -Quiet)) {
    throw "U0 基线采样失败，请查看 $baselineLog"
}
$baselineResult=Get-Content -LiteralPath $baselineResultPath -Raw | ConvertFrom-Json
if ($CaptureOnly) {
    if ($baselineResult.status -ne 'captured-only' -or $baselineResult.performanceMeasured -ne $false) { throw '仅截图模式的结果标记不正确。' }
} elseif ($baselineResult.status -ne 'passed' -or $baselineResult.performanceMeasured -ne $true) { throw '完整性能采样未通过完整性检查。' }
$baselineRun=[ordered]@{
    schema='ultra-baseline-launch-v1'
    startedUtc=$baselineStarted.ToString('o')
    finishedUtc=[DateTimeOffset]::UtcNow.ToString('o')
    playerSha256=$baselinePlayerHash
    managedAssemblies=$baselineAssemblies
    fixtureFileHashesVerified=$true
    captureOnly=[bool]$CaptureOnly;performanceMeasured=(!$CaptureOnly)
    width=$Width;height=$Height;sampleSeconds=if($CaptureOnly){0}else{$SampleSeconds};warmupSeconds=$WarmupSeconds
    hiddenWindow=(!$VisibleWindow);windowStyle=$baselineWindowStyle;batchMode=$false;graphics='Direct3D11'
    note='窗口显示模式按实际启动参数记录，焦点和持续渲染由运行时实测；不视为人工交互、Android 真机或热稳定验收。权威夹具只在 .local。'
}
$baselineRun | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $baselineOutput 'launch.json') -Encoding UTF8
foreach ($baselineCase in $baselineCases) {
    $baselineSummary=Get-Content -LiteralPath (Join-Path $baselineOutput "$baselineCase/summary.json") -Raw | ConvertFrom-Json
    if ($CaptureOnly) {
        [pscustomobject]@{局面=$baselineCase;截图数=$baselineSummary.screenshots;已测性能=$false;Renderer数=$baselineSummary.sceneCensus.activeRenderers;Unity分配字节=$baselineSummary.memorySnapshot.allocatedBytes}
    } else {
        [pscustomobject]@{局面=$baselineCase;帧数=$baselineSummary.sampleCount;P50毫秒=$baselineSummary.frameMs.p50;P95毫秒=$baselineSummary.frameMs.p95;P99毫秒=$baselineSummary.frameMs.p99;大于50毫秒=$baselineSummary.spikesOver50Ms}
    }
}
