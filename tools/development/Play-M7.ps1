param([string]$BuildRoot='.local/Catan1.6.3-AudioVisual')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
