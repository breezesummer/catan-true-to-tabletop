param([string]$BuildRoot='game version/Catan1.6.4-Trade-Animation-History')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
