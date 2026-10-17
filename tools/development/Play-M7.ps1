param([string]$BuildRoot='.local/barbarian-trade-fix-v001')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
