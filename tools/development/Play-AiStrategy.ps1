param([string]$BuildRoot='.local/m6-android-pc-ui-v001')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
