param([ValidateSet('Shell', 'Code', 'Hub', 'Blender', 'Unity', 'UnitySmoke')][string]$Tool = 'Shell')
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $projectRoot
switch ($Tool) {
    'Shell' { & powershell.exe -NoExit -NoProfile }
    'Code' {
        & $toolchain.code --user-data-dir (Join-Path $projectLocalRoot 'vscode\user-data') `
            --extensions-dir $toolchain.codeExtensions $projectRoot
    }
    'Hub' {
        Start-Process -FilePath $toolchain.hub -ArgumentList @(
            ('--user-data-dir="' + (Join-Path $projectLocalRoot 'unity-hub') + '"')
        ) -WindowStyle Hidden
    }
    'Blender' { Start-Process -FilePath $toolchain.blenderExecutable -WindowStyle Hidden }
    { $_ -in 'Unity','UnitySmoke' } {
        $unityProject = if ($Tool -eq 'Unity') { Join-Path $projectRoot 'Unity\Catan' } else { Join-Path $projectRoot '.local\environment\UnitySmoke' }
        if (!(Test-Path (Join-Path $unityProject 'ProjectSettings\ProjectVersion.txt'))) {
            throw 'Unity project is missing; prepare the selected project first.'
        }
        Start-Process -FilePath $toolchain.unity -ArgumentList @(
            '-projectPath', ('"' + $unityProject + '"'),
            '-logFile', ('"' + (Join-Path $projectRoot '.local\environment\logs\unity-interactive.log') + '"')
        ) -WindowStyle Hidden
    }
}
