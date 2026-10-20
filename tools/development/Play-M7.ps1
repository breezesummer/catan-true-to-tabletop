param([string]$BuildRoot='game version/1.6/Catan1.6.14-Collector-Menu')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
