param([switch]$VerifyPlayer, [switch]$CaptureUI)
if ($CaptureUI -and !$VerifyPlayer) { throw 'CaptureUI requires VerifyPlayer.' }
$m3BuildClock = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m3RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m3Project = Join-Path $m3RepoRoot 'Unity\Catan'
$m3Logs = Join-Path $m3RepoRoot '.local\m3\logs'
New-Item -ItemType Directory -Force -Path $m3Logs,(Join-Path $m3Project 'Assets\Plugins'),(Join-Path $m3Project 'Assets\Resources') | Out-Null
& $toolchain.dotnet build (Join-Path $m3RepoRoot 'src\Catan.Core\Catan.Core.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
Copy-Item -LiteralPath (Join-Path $m3RepoRoot 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $m3Project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $m3RepoRoot 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $m3Project 'Assets\Resources\m1-scenario.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $m3Project -executeMethod M3Build.Build -logFile (Join-Path $m3Logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m3/logs/unity-build.log.' }
if (!(Select-String -LiteralPath (Join-Path $m3Logs 'unity-build.log') -Pattern 'CATAN_M3_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker.' }
Write-Host ("M3 .NET / Unity build completed in {0:N1} seconds." -f $m3BuildClock.Elapsed.TotalSeconds)
if ($VerifyPlayer) {
    $m3Player = Join-Path $m3RepoRoot '.local\m3\Builds\Windows\CatanM3.exe'
    $m3Evidence = Join-Path $m3RepoRoot '.local\m3\verification'
    New-Item -ItemType Directory -Force $m3Evidence | Out-Null
    foreach ($mode in @('save','load')) {
        $m3PlayerClock = [Diagnostics.Stopwatch]::StartNew()
        $m3Log = Join-Path $m3Logs "player-$mode.log"
        $m3Args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m3-verify-$mode",('"'+$m3Evidence+'"'),'-logFile',('"'+$m3Log+'"'))
        $m3WindowStyle = 'Hidden'
        if ($CaptureUI -and $mode -eq 'load') {
            $m3Args += @('--m3-capture-ui',('"'+$m3Evidence+'"'))
        }
        $m3Process = Start-Process -FilePath $m3Player -ArgumentList $m3Args -WindowStyle $m3WindowStyle -PassThru
        $m3Exited = $false
        foreach ($m3Wait in 1..5) { if ($m3Process.WaitForExit(60000)) { $m3Exited = $true; break } }
        if (!$m3Exited) { Stop-Process -Id $m3Process.Id; throw "M3 player $mode timed out after five minutes." }
        if ($m3Process.ExitCode -ne 0) { throw "M3 player $mode failed; inspect $m3Log." }
        $m3Marker = if ($mode -eq 'save') {'CATAN_M3_PLAYER_SAVE_OK'} else {'CATAN_M3_PLAYER_LOAD_OK'}
        if (!(Select-String -LiteralPath $m3Log -Pattern $m3Marker -Quiet)) { throw "Missing player marker: $m3Marker" }
        if (Select-String -LiteralPath $m3Log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "M3 player runtime error: $m3Log" }
        Write-Host ("M3 hidden player {0} verified in {1:N1} seconds." -f $mode,$m3PlayerClock.Elapsed.TotalSeconds)
        if ($CaptureUI -and $mode -eq 'load') {
            if (!(Select-String -LiteralPath $m3Log -Pattern 'CATAN_M3_UI_CAPTURE_OK' -Quiet)) { throw 'Full UI capture failed.' }
            Copy-Item -LiteralPath $m3Log -Destination (Join-Path $m3Logs 'player-ui.log') -Force
        }
    }
}
Write-Host 'M3 Windows build ready: .local/m3/Builds/Windows/CatanM3.exe'

