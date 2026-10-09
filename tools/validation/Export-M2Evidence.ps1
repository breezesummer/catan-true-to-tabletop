param([string]$OutputDirectory = 'docs\verification\m2-2026-10-09')
$ErrorActionPreference = 'Stop'
$m2RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m2Output = [IO.Path]::GetFullPath((Join-Path $m2RepoRoot $OutputDirectory))
if (!$m2Output.StartsWith($m2RepoRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence output must be inside the repository.' }
$m2Rules = Join-Path $m2RepoRoot '.local\m2\test-results\m2-rules.trx'
[xml]$m2Report = Get-Content -LiteralPath $m2Rules -Raw
if ($m2Report.TestRun.ResultSummary.outcome -ne 'Completed' -or [int]$m2Report.TestRun.ResultSummary.Counters.failed -gt 0) { throw 'Rules tests have not passed.' }
$m2Sources = @(
    '.local\m2\test-results\m2-rules.trx',
    '.local\m2\logs\unity-build.log',
    '.local\m2\logs\player-save.log',
    '.local\m2\logs\player-load.log',
    '.local\m2\logs\player-ui.log'
)
foreach ($m2Source in $m2Sources) { if (!(Test-Path -LiteralPath (Join-Path $m2RepoRoot $m2Source))) { throw "Missing evidence: $m2Source" } }
New-Item -ItemType Directory -Force $m2Output | Out-Null
foreach ($m2Source in $m2Sources) { Copy-Item -LiteralPath (Join-Path $m2RepoRoot $m2Source) -Destination $m2Output -Force }
foreach ($m2Image in @('m2-default.png','m2-top.png','m2-ui-verified.png','m2-curtain-verified.png')) {
    $m2ImagePath = Join-Path $m2RepoRoot ('.local\m2\verification\' + $m2Image)
    if (Test-Path -LiteralPath $m2ImagePath) { Copy-Item -LiteralPath $m2ImagePath -Destination $m2Output -Force }
}
# Saves contain hidden information and deliberately stay in .local. Only public verification logs are exported.
$m2IdentityPaths = @(
    'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll',
    'Unity\Catan\Assets\Plugins\Catan.Core.dll',
    '.local\m2\Builds\Windows\CatanM2.exe',
    '.local\m2\Builds\Windows\CatanM2_Data\Managed\Assembly-CSharp.dll',
    '.local\m2\Builds\Windows\CatanM2_Data\Managed\Catan.Core.dll',
    'Unity\Catan\Packages\packages-lock.json'
)
$m2IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m2RepoRoot 'src\Catan.Core') -Filter 'M2*.cs' | ForEach-Object { $_.FullName.Substring($m2RepoRoot.Length+1) }
$m2IdentityPaths += Get-ChildItem -LiteralPath (Join-Path $m2RepoRoot 'Unity\Catan\Assets\Scripts') -Filter 'M2*.cs' | ForEach-Object { $_.FullName.Substring($m2RepoRoot.Length+1) }
$m2IdentityPaths += 'Unity\Catan\Assets\Scripts\BoardRenderer.cs'
$m2IdentityPaths += Get-ChildItem -LiteralPath $m2Output -File | Where-Object Name -ne 'sha256.json' | ForEach-Object { $_.FullName.Substring($m2RepoRoot.Length+1) }
$m2Hashes = foreach ($m2Relative in $m2IdentityPaths) {
    $m2File = Join-Path $m2RepoRoot $m2Relative
    [ordered]@{ path=$m2Relative.Replace('\','/'); sha256=(Get-FileHash -LiteralPath $m2File -Algorithm SHA256).Hash.ToLowerInvariant(); bytes=(Get-Item -LiteralPath $m2File).Length }
}
$m2Hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $m2Output 'sha256.json') -Encoding UTF8
Write-Output "M2 public evidence exported: $m2Output"
