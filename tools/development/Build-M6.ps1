param([switch]$VerifyPlayer)
$m6BuildClock = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m6RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m6Project = Join-Path $m6RepoRoot 'Unity\Catan'
$m6Logs = Join-Path $m6RepoRoot '.local\m6\logs'
New-Item -ItemType Directory -Force -Path $m6Logs,(Join-Path $m6Project 'Assets\Plugins'),(Join-Path $m6Project 'Assets\Resources') | Out-Null
& $toolchain.dotnet build (Join-Path $m6RepoRoot 'src\Catan.AI\Catan.AI.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
Copy-Item -LiteralPath (Join-Path $m6RepoRoot 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $m6Project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $m6RepoRoot 'src\Catan.AI\bin\Release\netstandard2.1\Catan.AI.dll') -Destination (Join-Path $m6Project 'Assets\Plugins\Catan.AI.dll') -Force
Copy-Item -LiteralPath (Join-Path $m6RepoRoot 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $m6Project 'Assets\Resources\m1-scenario.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $m6Project -executeMethod M6Build.Build -logFile (Join-Path $m6Logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m6/logs/unity-build.log.' }
if (!(Select-String -LiteralPath (Join-Path $m6Logs 'unity-build.log') -Pattern 'CATAN_M6_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker.' }
Write-Host ("M6 .NET / Unity build completed in {0:N1} seconds." -f $m6BuildClock.Elapsed.TotalSeconds)
if ($VerifyPlayer) {
    $m6Player = Join-Path $m6RepoRoot '.local\m6\Builds\Windows\CatanM6.exe'
    $m6Evidence = Join-Path $m6RepoRoot '.local\m6\verification'
    New-Item -ItemType Directory -Force $m6Evidence | Out-Null
    foreach ($mode in @('save','load')) {
        $m6PlayerClock = [Diagnostics.Stopwatch]::StartNew()
        $m6Log = Join-Path $m6Logs "player-$mode.log"
        $m6Args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m6-verify-$mode",('"'+$m6Evidence+'"'),'-logFile',('"'+$m6Log+'"'))
        $m6WindowStyle = 'Hidden'
        $m6Process = Start-Process -FilePath $m6Player -ArgumentList $m6Args -WindowStyle $m6WindowStyle -PassThru
        $m6Exited = $false
        foreach ($m6Wait in 1..5) { if ($m6Process.WaitForExit(60000)) { $m6Exited = $true; break } }
        if (!$m6Exited) { Stop-Process -Id $m6Process.Id; throw "M6 player $mode timed out after five minutes." }
        if ($m6Process.ExitCode -ne 0) { throw "M6 player $mode failed; inspect $m6Log." }
        $m6Marker = if ($mode -eq 'save') {'CATAN_M6_PLAYER_SAVE_OK'} else {'CATAN_M6_PLAYER_LOAD_OK'}
        if (!(Select-String -LiteralPath $m6Log -Pattern $m6Marker -Quiet)) { throw "Missing player marker: $m6Marker" }
        if (Select-String -LiteralPath $m6Log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "M6 player runtime error: $m6Log" }
        Write-Host ("M6 hidden player {0} verified in {1:N1} seconds." -f $mode,$m6PlayerClock.Elapsed.TotalSeconds)

    }
}
Write-Host 'M6 Windows build ready: .local/m6/Builds/Windows/CatanM6.exe'

