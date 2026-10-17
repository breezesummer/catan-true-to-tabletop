param([string]$BuildRoot='.local/texture-recovery-v001')
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'Play-M6.ps1') -BuildRoot $BuildRoot
