param([string]$OutputDirectory = 'docs\verification\m5-2026-10-09')
$ErrorActionPreference = 'Stop'
$m5RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m5Output = [IO.Path]::GetFullPath((Join-Path $m5RepoRoot $OutputDirectory))
if (!$m5Output.StartsWith($m5RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be inside the repository.' }
$m5Rules = Join-Path $m5RepoRoot '.local\m5\test-results\m5-rules.trx'
[xml]$m5Report = Get-Content -LiteralPath $m5Rules -Raw
if ($m5Report.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$m5Report.TestRun.ResultSummary.Counters.failed -gt 0) { throw 'Rules tests have not passed.' }
$m5RequiredLogs = @{
    'unity-build.log' = 'CATAN_M5_UNITY_BUILD_OK'
    'player-save.log' = 'CATAN_M5_PLAYER_SAVE_OK'
    'player-load.log' = 'CATAN_M5_PLAYER_LOAD_OK'
}
foreach ($m5Entry in $m5RequiredLogs.GetEnumerator()) {
    $m5Path = Join-Path $m5RepoRoot ('.local\m5\logs\' + $m5Entry.Key)
    if (!(Select-String -LiteralPath $m5Path -Pattern $m5Entry.Value -Quiet)) { throw "Missing success marker in $m5Path" }
}
New-Item -ItemType Directory -Force $m5Output | Out-Null
Copy-Item -LiteralPath $m5Rules -Destination $m5Output -Force
foreach ($m5Name in $m5RequiredLogs.Keys) { Copy-Item -LiteralPath (Join-Path $m5RepoRoot ('.local\m5\logs\' + $m5Name)) -Destination $m5Output -Force }
foreach ($m5Name in @('m5-default.png','m5-top.png','m5-components-default.png','m5-components-top.png','m5-components-crowded.png','m5-components-side.png')) {
    $m5Path = Join-Path $m5RepoRoot ('.local\m5\verification\' + $m5Name)
    if (!(Test-Path -LiteralPath $m5Path)) { throw "Missing render evidence: $m5Name" }
    Copy-Item -LiteralPath $m5Path -Destination $m5Output -Force
}
# Authority saves and their private command histories remain in .local, never in public evidence.
$m5IdentityPaths = @(
    'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll',
    'Unity\Catan\Assets\Plugins\Catan.Core.dll',
    '.local\m5\Builds\Windows\CatanM5.exe',
    '.local\m5\Builds\Windows\CatanM5_Data\Managed\Assembly-CSharp.dll',
    '.local\m5\Builds\Windows\CatanM5_Data\Managed\Catan.Core.dll',
    'Unity\Catan\Packages\packages-lock.json',
    'Unity\Catan\Assets\Editor\M5Build.cs',
    'Unity\Catan\Assets\M5.unity',
    'art\recipes\m5-v001\manifest.json',
    'tools\development\Build-M5.ps1',
    'tools\validation\Test-M5Rules.ps1'
)
$m5IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m5RepoRoot 'src\Catan.Core') -Filter 'M5*.cs' | ForEach-Object { $_.FullName.Substring($m5RepoRoot.Length+1) }
$m5IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m5RepoRoot 'Unity\Catan\Assets\Scripts') -Filter 'M5*.cs' | ForEach-Object { $_.FullName.Substring($m5RepoRoot.Length+1) }
$m5IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m5RepoRoot 'tests\Catan.Rules.Tests') -Filter 'M5*.cs' | ForEach-Object { $_.FullName.Substring($m5RepoRoot.Length+1) }

$m5IdentityPaths += Get-ChildItem -LiteralPath $m5Output -File | Where-Object Name -ne 'sha256.json' | ForEach-Object { $_.FullName.Substring($m5RepoRoot.Length+1) }
$m5Hashes = foreach ($m5Relative in $m5IdentityPaths) {
    $m5File = Join-Path $m5RepoRoot $m5Relative
    [ordered]@{ path=$m5Relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $m5File -Algorithm SHA256).Hash.ToLowerInvariant(); bytes=(Get-Item -LiteralPath $m5File).Length }
}
$m5Hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $m5Output 'sha256.json') -Encoding UTF8
Write-Output "M5 public evidence exported: $m5Output"
