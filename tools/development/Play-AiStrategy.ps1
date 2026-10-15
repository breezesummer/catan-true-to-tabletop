param([string]$BuildRoot='.local/m7-v001')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
