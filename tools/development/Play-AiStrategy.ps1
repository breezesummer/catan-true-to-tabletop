param([string]$BuildRoot='.local/m6-map-refresh-v001')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
