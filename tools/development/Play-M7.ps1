param([string]$BuildRoot='game version/1.6/Catan1.6.9-Online-Ip-Lobby')
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
