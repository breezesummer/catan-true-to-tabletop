param([Parameter(Mandatory=$true)][string]$BuildRoot)
$ErrorActionPreference='Stop'
$playerRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$playerDirectory=Join-Path ([IO.Path]::GetFullPath((Join-Path $playerRepo $BuildRoot))) 'Builds/Windows'
$playerCandidates=@(Get-ChildItem -LiteralPath $playerDirectory -File -Filter 'Catan*.exe')
if($playerCandidates.Count -ne 1){throw '构建目录应包含且只包含一个 Catan 游戏程序。'}
$playerCandidates[0].FullName
