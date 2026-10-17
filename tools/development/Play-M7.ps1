param([string]$BuildRoot='.local/lan-discovery-20261018')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
