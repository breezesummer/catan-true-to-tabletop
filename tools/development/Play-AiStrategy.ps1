param([string]$BuildRoot='.local/m6-ai-v002-card-ratio-v002')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
