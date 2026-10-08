# Repairs Windows registration after relocating Hub; run as administrator.
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$toolchain = Get-Content (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$hub = $toolchain.hub
if (!(Test-Path -LiteralPath $hub)) { throw 'The relocated Hub executable is missing.' }
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = New-Object Security.Principal.WindowsPrincipal($identity)
if (!$principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Administrator rights are required to repair the machine-wide installation registration.'
}
$migration = Join-Path $projectRoot '.local\development\migration'
New-Item -ItemType Directory -Path $migration -Force | Out-Null
$key = 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall\Unity Technologies - Hub'
$uninstaller = Join-Path (Split-Path $hub) 'Uninstall Unity Hub.exe'
Set-ItemProperty -LiteralPath $key -Name UninstallString -Value ('"' + $uninstaller + '" /allusers')
Set-ItemProperty -LiteralPath $key -Name QuietUninstallString -Value ('"' + $uninstaller + '" /allusers /S')
Set-ItemProperty -LiteralPath $key -Name DisplayIcon -Value ($hub + ',0')
Set-ItemProperty -LiteralPath $key -Name InstallLocation -Value (Split-Path $hub)
$hubData = Join-Path $projectRoot '.local\development\unity-hub'
foreach ($root in @('HKCU:\Software\Classes\unityhub', 'HKLM:\Software\Classes\unityhub')) {
    $command = Join-Path $root 'shell\open\command'
    if (Test-Path -LiteralPath $command) {
        Set-ItemProperty -LiteralPath $command -Name '(default)' -Value ('"' + $hub + '" --user-data-dir="' + $hubData + '" "%1"')
    }
    $icon = Join-Path $root 'DefaultIcon'
    if (Test-Path -LiteralPath $icon) { Set-ItemProperty -LiteralPath $icon -Name '(default)' -Value ($hub + ',0') }
}
$wsh = New-Object -ComObject WScript.Shell
$records = @()
$links = @(
    'C:\Users\Public\Desktop\Unity Hub.lnk',
    'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Unity Hub.lnk',
    'C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Unity 6000.3.25f1\Unity.lnk'
)
foreach ($path in $links) {
    if (!(Test-Path -LiteralPath $path)) { continue }
    $shortcut = $wsh.CreateShortcut($path)
    $records += @{ Path=$path; Target=$shortcut.TargetPath; Arguments=$shortcut.Arguments; WorkingDirectory=$shortcut.WorkingDirectory; IconLocation=$shortcut.IconLocation }
    $tool = if ($path -like '*Unity Hub.lnk') { 'Hub' } else { 'Unity' }
    $shortcut.TargetPath = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    $shortcut.Arguments = '-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "' + (Join-Path $PSScriptRoot 'Start-Dev.ps1') + '" -Tool ' + $tool
    $shortcut.WorkingDirectory = $projectRoot
    $shortcut.IconLocation = if ($tool -eq 'Hub') { $hub + ',0' } else { $toolchain.unity + ',0' }
    $shortcut.Save()
}
$backup = Join-Path $migration 'shortcuts-before.json'
if (!(Test-Path -LiteralPath $backup)) { $records | ConvertTo-Json | Set-Content $backup -Encoding UTF8 }
Set-Content (Join-Path $migration 'unity-registration-repaired.txt') 'Completed Windows registration and shortcut repair.' -Encoding UTF8
