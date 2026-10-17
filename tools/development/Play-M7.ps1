param([string]$BuildRoot='.local/Catan1.6.2-地图修路与界面优化')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
