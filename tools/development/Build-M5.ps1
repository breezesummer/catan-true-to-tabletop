param([switch]$VerifyPlayer)
$m5BuildClock = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m5RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m5Project = Join-Path $m5RepoRoot 'Unity\Catan'
$m5Logs = Join-Path $m5RepoRoot '.local\m5\logs'
New-Item -ItemType Directory -Force -Path $m5Logs,(Join-Path $m5Project 'Assets\Plugins'),(Join-Path $m5Project 'Assets\Resources') | Out-Null
& $toolchain.dotnet build (Join-Path $m5RepoRoot 'src\Catan.AI\Catan.AI.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'Core / AI build failed.' }
Copy-Item -LiteralPath (Join-Path $m5RepoRoot 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $m5Project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $m5RepoRoot 'src\Catan.AI\bin\Release\netstandard2.1\Catan.AI.dll') -Destination (Join-Path $m5Project 'Assets\Plugins\Catan.AI.dll') -Force
Copy-Item -LiteralPath (Join-Path $m5RepoRoot 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $m5Project 'Assets\Resources\m1-scenario.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $m5Project -executeMethod M5Build.Build -logFile (Join-Path $m5Logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m5/logs/unity-build.log.' }
if (!(Select-String -LiteralPath (Join-Path $m5Logs 'unity-build.log') -Pattern 'CATAN_M5_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker.' }
Write-Host ("M5 .NET / Unity build completed in {0:N1} seconds." -f $m5BuildClock.Elapsed.TotalSeconds)
if ($VerifyPlayer) {
    $m5Player = Join-Path $m5RepoRoot '.local\m5\Builds\Windows\CatanM5.exe'
    $m5Evidence = Join-Path $m5RepoRoot '.local\m5\verification'
    New-Item -ItemType Directory -Force $m5Evidence | Out-Null
    foreach ($mode in @('save','load')) {
        $m5PlayerClock = [Diagnostics.Stopwatch]::StartNew()
        $m5Log = Join-Path $m5Logs "player-$mode.log"
        $m5Args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m5-verify-$mode",('"'+$m5Evidence+'"'),'-logFile',('"'+$m5Log+'"'))
        $m5WindowStyle = 'Hidden'
        $m5Process = Start-Process -FilePath $m5Player -ArgumentList $m5Args -WindowStyle $m5WindowStyle -PassThru
        $m5Exited = $false
        foreach ($m5Wait in 1..5) { if ($m5Process.WaitForExit(60000)) { $m5Exited = $true; break } }
        if (!$m5Exited) { Stop-Process -Id $m5Process.Id; throw "M5 player $mode timed out after five minutes." }
        if ($m5Process.ExitCode -ne 0) { throw "M5 player $mode failed; inspect $m5Log." }
        $m5Marker = if ($mode -eq 'save') {'CATAN_M5_PLAYER_SAVE_OK'} else {'CATAN_M5_PLAYER_LOAD_OK'}
        if (!(Select-String -LiteralPath $m5Log -Pattern $m5Marker -Quiet)) { throw "Missing player marker: $m5Marker" }
        if (Select-String -LiteralPath $m5Log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "M5 player runtime error: $m5Log" }
        Write-Host ("M5 hidden player {0} verified in {1:N1} seconds." -f $mode,$m5PlayerClock.Elapsed.TotalSeconds)

    }
}
Write-Host 'M5 Windows build ready: .local/m5/Builds/Windows/CatanM5.exe'

