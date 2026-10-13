$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$integrationRepo=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$integrationProject=Join-Path $integrationRepo 'Unity/Catan'
$integrationLogs=Join-Path $integrationRepo '.local/art-integration-v003/logs'
New-Item -ItemType Directory -Force $integrationLogs | Out-Null
& $toolchain.dotnet build (Join-Path $integrationRepo 'src/Catan.AI/Catan.AI.csproj') -c Release --no-restore --disable-build-servers -m:1 -p:NuGetAudit=false
if($LASTEXITCODE -ne 0){throw 'Rules/AI build failed'}
foreach($integrationLibrary in @('Core','AI')){
    Copy-Item -LiteralPath (Join-Path $integrationRepo "src/Catan.$integrationLibrary/bin/Release/netstandard2.1/Catan.$integrationLibrary.dll") -Destination (Join-Path $integrationProject "Assets/Plugins/Catan.$integrationLibrary.dll") -Force
}
$integrationSettings=@{}
foreach($integrationRelative in @('Assets/M1Pipeline.asset','Assets/UniversalRenderPipelineGlobalSettings.asset','ProjectSettings/GraphicsSettings.asset','ProjectSettings/ProjectSettings.asset')){
    $integrationPath=Join-Path $integrationProject $integrationRelative
    $integrationSettings[$integrationPath]=[IO.File]::ReadAllBytes($integrationPath)
}
$integrationLog=Join-Path $integrationLogs 'unity-build.log'
try {
    $integrationProcess=Start-Process -FilePath $toolchain.unity -ArgumentList @('-batchmode','-nographics','-quit','-projectPath',('"'+$integrationProject+'"'),'-executeMethod','EntityIntegrationBuild.Build','-logFile',('"'+$integrationLog+'"')) -WindowStyle Hidden -PassThru
    $integrationProcess.WaitForExit()
}
finally{foreach($integrationSetting in $integrationSettings.GetEnumerator()){[IO.File]::WriteAllBytes($integrationSetting.Key,$integrationSetting.Value)}}
if($integrationProcess.ExitCode -ne 0 -or !(Select-String -LiteralPath $integrationLog -Pattern 'CATAN_ENTITY_INTEGRATION_BUILD_OK' -Quiet)){throw "Unity integration build failed: $integrationLog"}
Write-Host 'CATAN_ENTITY_INTEGRATION_WINDOWS_READY'
