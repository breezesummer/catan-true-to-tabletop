param(
    [ValidateSet('All','Windows','Android','Server')][string]$Target='All',
    [string]$OutputRoot='game version/1.6/Catan1.6.12-Cloud-Online',
    [switch]$VerifyPlayer,
    [switch]$Arm64Only
)
$ErrorActionPreference='Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw '此入口用于 Windows；macOS 请使用 tools/development/Build-Game.command。'
}
switch ($Target) {
    'Windows' { & (Join-Path $PSScriptRoot 'Build-M7.ps1') -OutputRoot $OutputRoot -VerifyPlayer:$VerifyPlayer }
    'Android' { & (Join-Path $PSScriptRoot 'Build-Android.ps1') -OutputRoot $OutputRoot -Arm64Only:$Arm64Only }
    'Server' { & (Join-Path $PSScriptRoot 'Build-M7Server.ps1') -OutputRoot $OutputRoot }
    'All' {
        & (Join-Path $PSScriptRoot 'Build-M7.ps1') -OutputRoot $OutputRoot -VerifyPlayer:$VerifyPlayer
        & (Join-Path $PSScriptRoot 'Build-Android.ps1') -OutputRoot $OutputRoot -Arm64Only:$Arm64Only
    }
}
& (Join-Path $PSScriptRoot 'Keep-RecentGameBuilds.ps1')
