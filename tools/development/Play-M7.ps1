param([string]$BuildRoot='.local/Catan1.6.1-自动亮牌与玩家显示')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
