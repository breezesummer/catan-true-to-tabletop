param(
    [string]$ResultsRoot='.local/ai-strategy-v002/test-results',
    [string]$BuildRoot='.local/m6-ai-v002',
    [string]$OutputRoot='docs/verification/ai-strategy-v002'
)
$ErrorActionPreference='Stop'
$aiRepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$aiResults=Join-Path $aiRepoRoot $ResultsRoot
$aiBuild=Join-Path $aiRepoRoot $BuildRoot
$aiOutput=[IO.Path]::GetFullPath((Join-Path $aiRepoRoot $OutputRoot))
if (!$aiOutput.StartsWith($aiRepoRoot+'\',[StringComparison]::OrdinalIgnoreCase)) { throw '证据必须保存在仓库内。' }
[xml]$aiReport=Get-Content -LiteralPath (Join-Path $aiResults 'all-rules.trx') -Raw
$aiCounters=$aiReport.TestRun.ResultSummary.Counters
if ($aiReport.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$aiCounters.passed -ne [int]$aiCounters.total) { throw '完整回归尚未全部通过。' }
$aiLogs=@{'unity-build.log'='CATAN_M6_UNITY_BUILD_OK';'player-save.log'='CATAN_M6_PLAYER_SAVE_OK';'player-load.log'='CATAN_M6_PLAYER_LOAD_OK'}
foreach ($aiEntry in $aiLogs.GetEnumerator()) {
    if (!(Select-String -LiteralPath (Join-Path $aiBuild ('logs/'+$aiEntry.Key)) -Pattern $aiEntry.Value -Quiet)) { throw ('缺少成功标记：'+$aiEntry.Key) }
}
$aiMatches=@(foreach ($aiResult in $aiReport.TestRun.Results.UnitTestResult) {
    $aiText=[string]$aiResult.Output.StdOut
    if ($aiText -match 'rules=([^,]+), scenario=([^,]+), seats=(\d+), seed=(\d+), winner=([^,]+), turn=(\d+), commands=(\d+), seconds=([\d.]+)') {
        $aiMatch=[ordered]@{rules=$Matches[1];scenario=$Matches[2];seats=[int]$Matches[3];seed=[uint32]$Matches[4];winner=$Matches[5];turn=[int]$Matches[6];commands=[int]$Matches[7];seconds=[double]::Parse($Matches[8],[Globalization.CultureInfo]::InvariantCulture)}
        if ($aiText -match '(?m)^actions=([^\r\n]+)') { $aiMatch.actions=$Matches[1] | ConvertFrom-Json }
        [pscustomobject]$aiMatch
    }
})
if ($aiMatches.Count -ne 52) { throw '必须完成全部52个正常AI对局。' }
$aiIdentity=@('src/Catan.AI/bin/Release/netstandard2.1/Catan.AI.dll','tests/Catan.Rules.Tests/bin/Release/net10.0/Catan.AI.dll','Unity/Catan/Assets/Plugins/Catan.AI.dll',"$BuildRoot/Builds/Windows/CatanM6_Data/Managed/Catan.AI.dll")
$aiDllHashes=@($aiIdentity | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $aiRepoRoot $_) -Algorithm SHA256).Hash })
if (@($aiDllHashes | Select-Object -Unique).Count -ne 1) { throw '测试构建、Unity插件与Windows程序的AI程序集不一致。' }
New-Item -ItemType Directory -Path $aiOutput -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $aiResults 'all-rules.trx') -Destination $aiOutput -Force
foreach ($aiName in $aiLogs.Keys) { Copy-Item -LiteralPath (Join-Path $aiBuild ('logs/'+$aiName)) -Destination $aiOutput -Force }
$aiMatches | Sort-Object rules,scenario,seats,seed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $aiOutput 'matches.json') -Encoding UTF8
$aiSummary=[ordered]@{
    version='ai-strategy-v002';timezone='Asia/Shanghai';testStart=$aiReport.TestRun.Times.start;testFinish=$aiReport.TestRun.Times.finish
    total=[int]$aiCounters.total;passed=[int]$aiCounters.passed;failed=[int]$aiCounters.failed
    strategyCases=@($aiReport.TestRun.Results.UnitTestResult | Where-Object testName -Match 'M6(?:Setup|Seafaring|Cities|Combined)StrategyTests').Count
    completeGames=$aiMatches.Count;commands=($aiMatches | Measure-Object commands -Sum).Sum
    minimumTurn=($aiMatches | Measure-Object turn -Minimum).Minimum;maximumTurn=($aiMatches | Measure-Object turn -Maximum).Maximum
    unityVersion='6000.3.25f1';unityBuild=$true;separatePlayerSaveLoad=$true
    humanFullGame=$false;strengthBenchmark=$false;manualWindowInspection=$false
    note='仅登记本轮实际自动验证；不含权威存档、完整私人手牌或隐藏随机状态。'
}
$aiSummary | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $aiOutput 'summary.json') -Encoding UTF8
$aiIdentity+=@('Unity/Catan/Assets/Plugins/Catan.Core.dll',"$BuildRoot/Builds/Windows/CatanM6.exe",'tools/validation/Export-AiStrategyEvidence.ps1')
$aiIdentity+=@(Get-ChildItem -LiteralPath (Join-Path $aiRepoRoot 'src/Catan.AI') -Filter '*.cs' | ForEach-Object { $_.FullName.Substring($aiRepoRoot.Length+1) })
$aiIdentity+=@(Get-ChildItem -LiteralPath (Join-Path $aiRepoRoot 'tests/Catan.Rules.Tests') -Filter 'M6*StrategyTests.cs' | ForEach-Object { $_.FullName.Substring($aiRepoRoot.Length+1) })
$aiHashes=foreach ($aiRelative in $aiIdentity) { $aiFile=Join-Path $aiRepoRoot $aiRelative; [ordered]@{path=$aiRelative.Replace('\','/');sha256=(Get-FileHash -LiteralPath $aiFile -Algorithm SHA256).Hash.ToLowerInvariant()} }
$aiHashes | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $aiOutput 'sha256.json') -Encoding UTF8
Write-Output ('AI策略公开证据已导出：'+$aiOutput)
