param([switch]$VerifyPlayer, [switch]$CaptureUI)
if ($CaptureUI -and !$VerifyPlayer) { throw 'CaptureUI requires VerifyPlayer.' }
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m2RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m2Project = Join-Path $m2RepoRoot 'Unity\Catan'
$m2Logs = Join-Path $m2RepoRoot '.local\m2\logs'
New-Item -ItemType Directory -Force -Path $m2Logs,(Join-Path $m2Project 'Assets\Plugins'),(Join-Path $m2Project 'Assets\Resources') | Out-Null
& $toolchain.dotnet build (Join-Path $m2RepoRoot 'src\Catan.AI\Catan.AI.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'Core / AI build failed.' }
Copy-Item -LiteralPath (Join-Path $m2RepoRoot 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $m2Project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $m2RepoRoot 'src\Catan.AI\bin\Release\netstandard2.1\Catan.AI.dll') -Destination (Join-Path $m2Project 'Assets\Plugins\Catan.AI.dll') -Force
Copy-Item -LiteralPath (Join-Path $m2RepoRoot 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $m2Project 'Assets\Resources\m1-scenario.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $m2Project -executeMethod M2Build.Build -logFile (Join-Path $m2Logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m2/logs/unity-build.log.' }
if (!(Select-String -LiteralPath (Join-Path $m2Logs 'unity-build.log') -Pattern 'CATAN_M2_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker.' }
if ($VerifyPlayer) {
    $m2Player = Join-Path $m2RepoRoot '.local\m2\Builds\Windows\CatanM2.exe'
    $m2Evidence = Join-Path $m2RepoRoot '.local\m2\verification'
    New-Item -ItemType Directory -Force $m2Evidence | Out-Null
    foreach ($mode in @('save','load')) {
        $m2Log = Join-Path $m2Logs "player-$mode.log"
        $m2Args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m2-verify-$mode",('"'+$m2Evidence+'"'),'-logFile',('"'+$m2Log+'"'))
        $m2WindowStyle = 'Hidden'
        if ($CaptureUI -and $mode -eq 'load') {
            $m2Args += @('--m2-capture-ui',('"'+$m2Evidence+'"'))
            $m2WindowStyle = 'Normal'
        }
        $m2Process = Start-Process -FilePath $m2Player -ArgumentList $m2Args -WindowStyle $m2WindowStyle -PassThru
        if (!$m2Process.WaitForExit(60000)) { Stop-Process -Id $m2Process.Id; throw "M2 player $mode timed out." }
        if ($m2Process.ExitCode -ne 0) { throw "M2 player $mode failed; inspect $m2Log." }
        $m2Marker = if ($mode -eq 'save') {'CATAN_M2_PLAYER_SAVE_OK'} else {'CATAN_M2_PLAYER_LOAD_OK'}
        if (!(Select-String -LiteralPath $m2Log -Pattern $m2Marker -Quiet)) { throw "Missing player marker: $m2Marker" }
        if (Select-String -LiteralPath $m2Log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "M2 player runtime error: $m2Log" }
        if ($CaptureUI -and $mode -eq 'load') {
            if (!(Select-String -LiteralPath $m2Log -Pattern 'CATAN_M2_UI_CAPTURE_OK' -Quiet)) { throw 'Full UI capture failed.' }
            Copy-Item -LiteralPath $m2Log -Destination (Join-Path $m2Logs 'player-ui.log') -Force
        }
    }
}
Write-Host 'M2 Windows build ready: .local/m2/Builds/Windows/CatanM2.exe'
