param([string]$OutputDirectory = 'docs\verification\m4-2026-10-09')
$ErrorActionPreference = 'Stop'
$m4RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m4Output = [IO.Path]::GetFullPath((Join-Path $m4RepoRoot $OutputDirectory))
if (!$m4Output.StartsWith($m4RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be inside the repository.' }
$m4Rules = Join-Path $m4RepoRoot '.local\m4\test-results\m4-rules.trx'
[xml]$m4Report = Get-Content -LiteralPath $m4Rules -Raw
if ($m4Report.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$m4Report.TestRun.ResultSummary.Counters.failed -gt 0) { throw 'Rules tests have not passed.' }
$m4RequiredLogs = @{
    'unity-build.log' = 'CATAN_M4_UNITY_BUILD_OK'
    'player-save.log' = 'CATAN_M4_PLAYER_SAVE_OK'
    'player-load.log' = 'CATAN_M4_PLAYER_LOAD_OK'
}
foreach ($m4Entry in $m4RequiredLogs.GetEnumerator()) {
    $m4Path = Join-Path $m4RepoRoot ('.local\m4\logs\' + $m4Entry.Key)
    if (!(Select-String -LiteralPath $m4Path -Pattern $m4Entry.Value -Quiet)) { throw "Missing success marker in $m4Path" }
}
New-Item -ItemType Directory -Force $m4Output | Out-Null
Copy-Item -LiteralPath $m4Rules -Destination $m4Output -Force
foreach ($m4Name in $m4RequiredLogs.Keys) { Copy-Item -LiteralPath (Join-Path $m4RepoRoot ('.local\m4\logs\' + $m4Name)) -Destination $m4Output -Force }
foreach ($m4Name in @('m4-default.png','m4-top.png','m4-components-default.png','m4-components-top.png','m4-components-crowded.png','m4-components-side.png')) {
    $m4Path = Join-Path $m4RepoRoot ('.local\m4\verification\' + $m4Name)
    if (Test-Path -LiteralPath $m4Path) { Copy-Item -LiteralPath $m4Path -Destination $m4Output -Force }
}
# Authority saves and their private command histories remain in .local, never in public evidence.
$m4IdentityPaths = @(
    'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll',
    'Unity\Catan\Assets\Plugins\Catan.Core.dll',
    '.local\m4\Builds\Windows\CatanM4.exe',
    '.local\m4\Builds\Windows\CatanM4_Data\Managed\Assembly-CSharp.dll',
    '.local\m4\Builds\Windows\CatanM4_Data\Managed\Catan.Core.dll',
    'Unity\Catan\Packages\packages-lock.json',
    'Unity\Catan\Assets\Editor\M4Build.cs',
    'Unity\Catan\Assets\M4.unity',
    'art\recipes\m4-v001\manifest.json',
    'tools\development\Build-M4.ps1',
    'tools\validation\Test-M4Rules.ps1'
)
$m4IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m4RepoRoot 'src\Catan.Core') -Filter 'M4*.cs' | ForEach-Object { $_.FullName.Substring($m4RepoRoot.Length+1) }
$m4IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m4RepoRoot 'Unity\Catan\Assets\Scripts') -Filter 'M4*.cs' | ForEach-Object { $_.FullName.Substring($m4RepoRoot.Length+1) }
$m4IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m4RepoRoot 'tests\Catan.Rules.Tests') -Filter 'M4*.cs' | ForEach-Object { $_.FullName.Substring($m4RepoRoot.Length+1) }

$m4IdentityPaths += Get-ChildItem -LiteralPath $m4Output -File | Where-Object Name -ne 'sha256.json' | ForEach-Object { $_.FullName.Substring($m4RepoRoot.Length+1) }
$m4Hashes = foreach ($m4Relative in $m4IdentityPaths) {
    $m4File = Join-Path $m4RepoRoot $m4Relative
    [ordered]@{ path=$m4Relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $m4File -Algorithm SHA256).Hash.ToLowerInvariant(); bytes=(Get-Item -LiteralPath $m4File).Length }
}
$m4Hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $m4Output 'sha256.json') -Encoding UTF8
Write-Output "M4 public evidence exported: $m4Output"
