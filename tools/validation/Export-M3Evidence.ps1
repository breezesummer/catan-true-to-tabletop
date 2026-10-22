param([string]$OutputDirectory = 'docs\verification\m3-2026-10-09')
$ErrorActionPreference = 'Stop'
$m3RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m3Output = [IO.Path]::GetFullPath((Join-Path $m3RepoRoot $OutputDirectory))
if (!$m3Output.StartsWith($m3RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be inside the repository.' }
$m3Rules = Join-Path $m3RepoRoot '.local\m3\test-results\m3-rules.trx'
[xml]$m3Report = Get-Content -LiteralPath $m3Rules -Raw
if ($m3Report.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$m3Report.TestRun.ResultSummary.Counters.failed -gt 0) { throw 'Rules tests have not passed.' }
$m3RequiredLogs = @{
    'unity-build.log' = 'CATAN_M3_UNITY_BUILD_OK'
    'player-save.log' = 'CATAN_M3_PLAYER_SAVE_OK'
    'player-load.log' = 'CATAN_M3_PLAYER_LOAD_OK'
}
foreach ($m3Entry in $m3RequiredLogs.GetEnumerator()) {
    $m3Path = Join-Path $m3RepoRoot ('.local\m3\logs\' + $m3Entry.Key)
    if (!(Select-String -LiteralPath $m3Path -Pattern $m3Entry.Value -Quiet)) { throw "Missing success marker in $m3Path" }
}
New-Item -ItemType Directory -Force $m3Output | Out-Null
Copy-Item -LiteralPath $m3Rules -Destination $m3Output -Force
foreach ($m3Name in $m3RequiredLogs.Keys) { Copy-Item -LiteralPath (Join-Path $m3RepoRoot ('.local\m3\logs\' + $m3Name)) -Destination $m3Output -Force }
foreach ($m3Name in @('m3-default.png','m3-top.png','m3-ui-verified.png','m3-curtain-verified.png')) {
    $m3Path = Join-Path $m3RepoRoot ('.local\m3\verification\' + $m3Name)
    if (Test-Path -LiteralPath $m3Path) { Copy-Item -LiteralPath $m3Path -Destination $m3Output -Force }
}
# Authority saves and their private command histories remain in .local, never in public evidence.
$m3IdentityPaths = @(
    'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll',
    'Unity\Catan\Assets\Plugins\Catan.Core.dll',
    '.local\m3\Builds\Windows\CatanM3.exe',
    '.local\m3\Builds\Windows\CatanM3_Data\Managed\Assembly-CSharp.dll',
    '.local\m3\Builds\Windows\CatanM3_Data\Managed\Catan.Core.dll',
    'Unity\Catan\Packages\packages-lock.json'
)
$m3IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m3RepoRoot 'src\Catan.Core') -Filter 'M3*.cs' | ForEach-Object { $_.FullName.Substring($m3RepoRoot.Length+1) }
$m3IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m3RepoRoot 'Unity\Catan\Assets\Scripts') -Filter 'M3*.cs' | ForEach-Object { $_.FullName.Substring($m3RepoRoot.Length+1) }
$m3IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m3RepoRoot 'tests\Catan.Rules.Tests') -Filter 'M3*.cs' | ForEach-Object { $_.FullName.Substring($m3RepoRoot.Length+1) }
$m3IdentityPaths += 'tests\Catan.Rules.Tests\SeafarersPortPersistenceTests.cs'
$m3IdentityPaths += Get-ChildItem -LiteralPath $m3Output -File | Where-Object Name -ne 'sha256.json' | ForEach-Object { $_.FullName.Substring($m3RepoRoot.Length+1) }
$m3Hashes = foreach ($m3Relative in $m3IdentityPaths) {
    $m3File = Join-Path $m3RepoRoot $m3Relative
    [ordered]@{ path=$m3Relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $m3File -Algorithm SHA256).Hash.ToLowerInvariant(); bytes=(Get-Item -LiteralPath $m3File).Length }
}
$m3Hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $m3Output 'sha256.json') -Encoding UTF8
Write-Output "M3 public evidence exported: $m3Output"
