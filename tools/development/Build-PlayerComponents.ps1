$ErrorActionPreference = 'Stop'
$componentRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$componentTools = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$componentLog = Join-Path $componentRepo '.local/art-pipeline/logs-v002/unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $componentLog) | Out-Null
$componentArgs = @('-batchmode','-nographics','-quit','-projectPath',('"'+(Join-Path $componentRepo 'Unity/Catan')+'"'),'-executeMethod','PlayerComponentBuild.BuildAndGame','-logFile',('"'+$componentLog+'"'))
$componentSettings = @{}
foreach ($componentRelative in @('Unity/Catan/Assets/M1Pipeline.asset','Unity/Catan/Assets/UniversalRenderPipelineGlobalSettings.asset','Unity/Catan/ProjectSettings/GraphicsSettings.asset','Unity/Catan/ProjectSettings/ProjectSettings.asset','Unity/Catan/Assets/M6.unity')) {
    $componentPath = Join-Path $componentRepo $componentRelative
    $componentSettings[$componentPath] = [IO.File]::ReadAllBytes($componentPath)
}
try {
    $componentProcess = Start-Process -FilePath $componentTools.unity -ArgumentList $componentArgs -WindowStyle Hidden -PassThru
    $componentProcess.WaitForExit()
} finally {
    foreach ($componentSetting in $componentSettings.GetEnumerator()) { [IO.File]::WriteAllBytes($componentSetting.Key,$componentSetting.Value) }
}
if ($componentProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $componentLog -Pattern 'CATAN_PLAYER_COMPONENT_BUILD_OK' -Quiet) -or !(Select-String -LiteralPath $componentLog -Pattern 'CATAN_M6_UNITY_BUILD_OK' -Quiet)) { throw "Unity build failed: $componentLog" }
Write-Host 'CATAN_PLAYER_COMPONENT_WINDOWS_READY'
