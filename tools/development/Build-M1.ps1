param([switch]$VerifyPlayer)
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$project = Join-Path $root 'Unity\Catan'
$logs = Join-Path $root '.local\m1\logs'
New-Item -ItemType Directory -Force -Path $logs,(Join-Path $project 'Assets\Plugins'),(Join-Path $project 'Assets\Models'),(Join-Path $project 'Assets\Resources'),(Join-Path $project 'Assets\ArtMaterials') | Out-Null
& $toolchain.dotnet build (Join-Path $root 'src\Catan.AI\Catan.AI.csproj') -c Release
if ($LASTEXITCODE -ne 0) { throw 'Core / AI build failed' }
Copy-Item -LiteralPath (Join-Path $root 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'src\Catan.AI\bin\Release\netstandard2.1\Catan.AI.dll') -Destination (Join-Path $project 'Assets\Plugins\Catan.AI.dll') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $project 'Assets\Resources\m1-scenario.json') -Force
Copy-Item -LiteralPath (Join-Path $root 'art\exports\mountain-tile-v001\mountain-tile-v001.fbx') -Destination (Join-Path $project 'Assets\Models\mountain-tile-v001.fbx') -Force
Copy-Item -LiteralPath (Join-Path $root 'art\exports\mountain-tile-v001\manifest.json') -Destination (Join-Path $project 'Assets\Models\mountain-manifest.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $project -executeMethod M1Build.Build -logFile (Join-Path $logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m1/logs/unity-build.log' }
if (!(Select-String -Path (Join-Path $logs 'unity-build.log') -Pattern 'CATAN_M1_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker' }
if ($VerifyPlayer) {
    $player = Join-Path $root '.local\m1\Builds\Windows\CatanM1.exe'
    $savePath = Join-Path $root '.local\m1\verification\after-end.json'
    $preview = Join-Path $root '.local\m1\previews'
    foreach ($mode in @('save','load')) {
        $log = Join-Path $logs "player-$mode.log"
        $args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m1-verify-$mode",('"'+$savePath+'"'),'-logFile',('"'+$log+'"'))
        if ($mode -eq 'load') { $args += @('--m1-capture',('"'+$preview+'"')) }
        $proc = Start-Process -FilePath $player -ArgumentList $args -WindowStyle Hidden -PassThru
        if (!$proc.WaitForExit(60000)) { Stop-Process -Id $proc.Id; throw "Player $mode timed out" }
        if ($proc.ExitCode -ne 0) { throw "Player $mode failed; inspect $log" }
        $marker = if ($mode -eq 'save') {'CATAN_M1_PLAYER_SAVE_OK'} else {'CATAN_M1_PLAYER_RESTORE_OK'}
        if (!(Select-String -Path $log -Pattern $marker -Quiet)) { throw "Player $mode missing marker" }
        if (Select-String -Path $log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "Player $mode has a runtime error; inspect $log" }
    }
    foreach ($image in @('m1-default.png','m1-top.png')) { if (!(Test-Path (Join-Path $preview $image))) { throw "Camera capture missing: $image" } }
}
Write-Host 'M1 Windows build ready: .local/m1/Builds/Windows/CatanM1.exe'
