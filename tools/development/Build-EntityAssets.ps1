$ErrorActionPreference = 'Stop'
$entityRepo = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$entityTools = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'toolchain.json') -Raw | ConvertFrom-Json
$entityLog = Join-Path $entityRepo '.local/art-pipeline/logs/unity-build.log'
New-Item -ItemType Directory -Force -Path (Split-Path $entityLog) | Out-Null
$entityArguments = @('-batchmode','-nographics','-quit','-projectPath',('"'+(Join-Path $entityRepo 'Unity/Catan')+'"'),'-executeMethod','EntityAssetBuild.Build','-logFile',('"'+$entityLog+'"'))
$entityOriginalSettings = @{}
foreach ($entityRelative in @('Unity/Catan/Assets/M1Pipeline.asset','Unity/Catan/Assets/UniversalRenderPipelineGlobalSettings.asset','Unity/Catan/ProjectSettings/GraphicsSettings.asset','Unity/Catan/ProjectSettings/ProjectSettings.asset')) {
    $entityPath = Join-Path $entityRepo $entityRelative
    $entityOriginalSettings[$entityPath] = [IO.File]::ReadAllBytes($entityPath)
}
try {
    $entityProcess = Start-Process -FilePath $entityTools.unity -ArgumentList $entityArguments -WindowStyle Hidden -PassThru
    # Start-Process -Wait also waits for persistent licensing descendants.
    # Wait only for the Editor process so settings restoration always runs.
    $entityProcess.WaitForExit()
}
finally { foreach ($entitySetting in $entityOriginalSettings.GetEnumerator()) { [IO.File]::WriteAllBytes($entitySetting.Key,$entitySetting.Value) } }
if ($entityProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $entityLog -Pattern 'CATAN_ENTITY_UNITY_BUILD_OK' -Quiet)) { throw "Unity entity build failed: $entityLog" }
& 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe' (Join-Path $entityRepo 'tools/art/update_entity_checklist.py')
if ($LASTEXITCODE -ne 0) { throw 'Checklist update failed' }
Write-Host 'CATAN_ENTITY_WINDOWS_READY'
