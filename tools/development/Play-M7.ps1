param([string]$BuildRoot=(& (Join-Path $PSScriptRoot 'Get-GameBuildRoot.ps1')))
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
