param([switch]$VerifyPlayer)
$m4BuildClock = [Diagnostics.Stopwatch]::StartNew()
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m4RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m4Project = Join-Path $m4RepoRoot 'Unity\Catan'
$m4Logs = Join-Path $m4RepoRoot '.local\m4\logs'
New-Item -ItemType Directory -Force -Path $m4Logs,(Join-Path $m4Project 'Assets\Plugins'),(Join-Path $m4Project 'Assets\Resources') | Out-Null
& $toolchain.dotnet build (Join-Path $m4RepoRoot 'src\Catan.Core\Catan.Core.csproj') -c Release --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'Core build failed.' }
Copy-Item -LiteralPath (Join-Path $m4RepoRoot 'src\Catan.Core\bin\Release\netstandard2.1\Catan.Core.dll') -Destination (Join-Path $m4Project 'Assets\Plugins\Catan.Core.dll') -Force
Copy-Item -LiteralPath (Join-Path $m4RepoRoot 'docs\acceptance\v0.1\scenario.json') -Destination (Join-Path $m4Project 'Assets\Resources\m1-scenario.json') -Force
& $toolchain.unity -batchmode -nographics -quit -projectPath $m4Project -executeMethod M4Build.Build -logFile (Join-Path $m4Logs 'unity-build.log') | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Unity build failed; inspect .local/m4/logs/unity-build.log.' }
if (!(Select-String -LiteralPath (Join-Path $m4Logs 'unity-build.log') -Pattern 'CATAN_M4_UNITY_BUILD_OK' -Quiet)) { throw 'Missing build success marker.' }
Write-Host ("M4 .NET / Unity build completed in {0:N1} seconds." -f $m4BuildClock.Elapsed.TotalSeconds)
if ($VerifyPlayer) {
    $m4Player = Join-Path $m4RepoRoot '.local\m4\Builds\Windows\CatanM4.exe'
    $m4Evidence = Join-Path $m4RepoRoot '.local\m4\verification'
    New-Item -ItemType Directory -Force $m4Evidence | Out-Null
    foreach ($mode in @('save','load')) {
        $m4PlayerClock = [Diagnostics.Stopwatch]::StartNew()
        $m4Log = Join-Path $m4Logs "player-$mode.log"
        $m4Args = @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900',"--m4-verify-$mode",('"'+$m4Evidence+'"'),'-logFile',('"'+$m4Log+'"'))
        $m4WindowStyle = 'Hidden'
        $m4Process = Start-Process -FilePath $m4Player -ArgumentList $m4Args -WindowStyle $m4WindowStyle -PassThru
        $m4Exited = $false
        foreach ($m4Wait in 1..5) { if ($m4Process.WaitForExit(60000)) { $m4Exited = $true; break } }
        if (!$m4Exited) { Stop-Process -Id $m4Process.Id; throw "M4 player $mode timed out after five minutes." }
        if ($m4Process.ExitCode -ne 0) { throw "M4 player $mode failed; inspect $m4Log." }
        $m4Marker = if ($mode -eq 'save') {'CATAN_M4_PLAYER_SAVE_OK'} else {'CATAN_M4_PLAYER_LOAD_OK'}
        if (!(Select-String -LiteralPath $m4Log -Pattern $m4Marker -Quiet)) { throw "Missing player marker: $m4Marker" }
        if (Select-String -LiteralPath $m4Log -Pattern 'Exception:|Failed to capture screen shot' -Quiet) { throw "M4 player runtime error: $m4Log" }
        Write-Host ("M4 hidden player {0} verified in {1:N1} seconds." -f $mode,$m4PlayerClock.Elapsed.TotalSeconds)

    }
}
Write-Host 'M4 Windows build ready: .local/m4/Builds/Windows/CatanM4.exe'

