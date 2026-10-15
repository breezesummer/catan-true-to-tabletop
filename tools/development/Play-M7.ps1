param([string]$BuildRoot='.local/ui-feedback-v012-final')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
