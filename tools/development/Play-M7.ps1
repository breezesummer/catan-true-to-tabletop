param([string]$BuildRoot='game version/Catan1.6.5-Card-Interaction-Audio')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
