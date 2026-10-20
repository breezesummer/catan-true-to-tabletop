param([string]$BuildRoot='game version/1.7/Catan1.7.0-Ultra-Baseline')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
