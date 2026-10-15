param([string]$BuildRoot='.local/m6-ui-v011')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
