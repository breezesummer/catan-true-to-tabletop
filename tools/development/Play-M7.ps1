param([string]$BuildRoot='game version/1.6/Catan1.6.13-Card-Action-Flow')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
