$ErrorActionPreference = 'Stop'
$sampleRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$sampleTools = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$sampleLog = Join-Path $sampleRepo '.local/art-photo-samples/logs/unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $sampleLog) | Out-Null
$sampleArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+(Join-Path $sampleRepo 'Unity/Catan')+'"'),'-executeMethod','PhotoSampleBuild.Build','-logFile',('"'+$sampleLog+'"'))
$sampleOriginalSettings = @{}
foreach ($sampleRelative in @('Unity/Catan/Assets/M1Pipeline.asset','Unity/Catan/Assets/UniversalRenderPipelineGlobalSettings.asset','Unity/Catan/ProjectSettings/GraphicsSettings.asset','Unity/Catan/ProjectSettings/ProjectSettings.asset')) {
    $samplePath = Join-Path $sampleRepo $sampleRelative
    $sampleOriginalSettings[$samplePath] = [IO.File]::ReadAllBytes($samplePath)
}
try { $sampleProcess = Start-Process -FilePath $sampleTools.unity -ArgumentList $sampleArguments -WindowStyle Hidden -PassThru -Wait }
finally { foreach ($sampleSetting in $sampleOriginalSettings.GetEnumerator()) { [IO.File]::WriteAllBytes($sampleSetting.Key,$sampleSetting.Value) } }
$sampleExit = $sampleProcess.ExitCode
if ($sampleExit -ne 0 -or !(Select-String -LiteralPath $sampleLog -Pattern 'CATAN_PHOTO_UNITY_BUILD_OK' -Quiet)) { throw "Unity sample build failed: $sampleLog" }
Write-Host 'CATAN_PHOTO_SAMPLES_WINDOWS_READY'
