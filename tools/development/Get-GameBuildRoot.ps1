$ErrorActionPreference = 'Stop'
$version = & (Join-Path $PSScriptRoot 'Get-GameVersion.ps1')
$settings = Get-Content -LiteralPath (Join-Path $PSScriptRoot '../../AGENTS.md') -Raw -Encoding utf8
$description = [regex]::Match($settings, '(?m)^构建短述：([A-Za-z0-9]+(?:-[A-Za-z0-9]+)*)\r?$')
if (!$description.Success) { throw 'AGENTS.md 缺少有效的构建短述。' }
$series = ([version]$version).ToString(2)
"game version/$series/Catan$version-$($description.Groups[1].Value)"
