param([string]$BuildRoot='game version/1.7/Catan1.7.1-U0-Profiling')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
