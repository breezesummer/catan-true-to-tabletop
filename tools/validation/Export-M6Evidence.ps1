param([string]$OutputDirectory = 'docs\verification\m6-2026-10-09')
$ErrorActionPreference = 'Stop'
$m6RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m6Output = [IO.Path]::GetFullPath((Join-Path $m6RepoRoot $OutputDirectory))
if (!$m6Output.StartsWith($m6RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be inside the repository.' }
$m6Rules = Join-Path $m6RepoRoot '.local\m6\test-results\m6-rules.trx'
[xml]$m6Report = Get-Content -LiteralPath $m6Rules -Raw
$m6Counters = $m6Report.TestRun.ResultSummary.Counters
if ($m6Report.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$m6Counters.failed -gt 0 -or [int]$m6Counters.passed -ne [int]$m6Counters.total) { throw 'All rules tests must pass before exporting acceptance evidence.' }
$m6RequiredLogs = @{
    'unity-build.log' = 'CATAN_M6_UNITY_BUILD_OK'
    'player-save.log' = 'CATAN_M6_PLAYER_SAVE_OK'
    'player-load.log' = 'CATAN_M6_PLAYER_LOAD_OK'
}
foreach ($m6Entry in $m6RequiredLogs.GetEnumerator()) {
    $m6Path = Join-Path $m6RepoRoot ('.local\m6\logs\' + $m6Entry.Key)
    if (!(Select-String -LiteralPath $m6Path -Pattern $m6Entry.Value -Quiet)) { throw "Missing success marker in $m6Path" }
}
New-Item -ItemType Directory -Force $m6Output | Out-Null
Copy-Item -LiteralPath $m6Rules -Destination $m6Output -Force
foreach ($m6Name in $m6RequiredLogs.Keys) { Copy-Item -LiteralPath (Join-Path $m6RepoRoot ('.local\m6\logs\' + $m6Name)) -Destination $m6Output -Force }
$m6Matches = foreach ($m6Result in $m6Report.TestRun.Results.UnitTestResult) {
    $m6Text = [string]$m6Result.Output.StdOut
    if ($m6Text -match 'rules=([^,]+), scenario=([^,]+), seats=(\d+), seed=(\d+), winner=([^,]+), turn=(\d+), commands=(\d+), seconds=([\d.]+)') {
        $m6Match = [ordered]@{ rules=$Matches[1]; scenario=$Matches[2]; seats=[int]$Matches[3]; seed=[uint32]$Matches[4]; winner=$Matches[5]; turn=[int]$Matches[6]; commands=[int]$Matches[7]; seconds=[double]::Parse($Matches[8],[Globalization.CultureInfo]::InvariantCulture) }
        if ($m6Text -match '(?m)^saved-decisions=([^\r\n]+)') { $m6Match.savedDecisions = @($Matches[1].Split(',')) }
        if ($m6Text -match '(?m)^actions=([^\r\n]+)') { $m6Match.actions = $Matches[1] | ConvertFrom-Json }
        $m6Match
    }
}
if (@($m6Matches).Count -ne 52) { throw 'Expected 52 M6 complete-game results.' }
$m6Matches | Sort-Object rules,scenario,seats,seed | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $m6Output 'matches.json') -Encoding UTF8
# Only selected public screenshots and reports may be exported. Authority saves stay in .local.
$m6IdentityPaths = @(
    'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll',
    'src\Catan.AI\bin\Release\netstandard2.1\Catan.AI.dll',
    'Unity\Catan\Assets\Plugins\Catan.Core.dll',
    'Unity\Catan\Assets\Plugins\Catan.AI.dll',
    '.local\m6\Builds\Windows\CatanM6.exe',
    '.local\m6\Builds\Windows\CatanM6_Data\Managed\Assembly-CSharp.dll',
    '.local\m6\Builds\Windows\CatanM6_Data\Managed\Catan.Core.dll',
    '.local\m6\Builds\Windows\CatanM6_Data\Managed\Catan.AI.dll',
    'Unity\Catan\Packages\packages-lock.json',
    'Unity\Catan\ProjectSettings\ProjectSettings.asset',
    'Unity\Catan\Assets\Editor\M6Build.cs',
    'Unity\Catan\Assets\M6.unity',
    'tools\development\Build-M6.ps1',
    'tools\development\Play-M6.ps1',
    'tests\Catan.Rules.Tests\Catan.Rules.Tests.csproj',
    'tools\validation\Test-M6Rules.ps1',
    'tools\validation\Export-M6Evidence.ps1'
)
$m6IdentityPaths += @('README.md','docs\ROADMAP.md','docs\M6_ACCEPTANCE.md','docs\M6_ARCHITECTURE.md','docs\M6_PLAYTEST.md','docs\M6_STRATEGY.md')
$m6IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m6RepoRoot 'src\Catan.AI') -File | ForEach-Object { $_.FullName.Substring($m6RepoRoot.Length+1) }
$m6IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m6RepoRoot 'Unity\Catan\Assets\Scripts') -Filter '*.cs' | Where-Object { $_.Name -match '^M([2-5](App|LocalGameHost)|6)' } | ForEach-Object { $_.FullName.Substring($m6RepoRoot.Length+1) }
$m6IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m6RepoRoot 'tools\development') -Filter 'Build-M*.ps1' | ForEach-Object { $_.FullName.Substring($m6RepoRoot.Length+1) }
$m6IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m6RepoRoot 'tests\Catan.Rules.Tests') -Filter 'M6*.cs' | ForEach-Object { $_.FullName.Substring($m6RepoRoot.Length+1) }
$m6IdentityPaths += Get-ChildItem -LiteralPath $m6Output -File | Where-Object Name -ne 'sha256.json' | ForEach-Object { $_.FullName.Substring($m6RepoRoot.Length+1) }
$m6Hashes = foreach ($m6Relative in ($m6IdentityPaths | Sort-Object -Unique)) {
    $m6File = Join-Path $m6RepoRoot $m6Relative
    [ordered]@{ path=$m6Relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $m6File -Algorithm SHA256).Hash.ToLowerInvariant(); bytes=(Get-Item -LiteralPath $m6File).Length }
}
$m6Hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $m6Output 'sha256.json') -Encoding UTF8
Write-Output "M6 public evidence exported: $m6Output"
