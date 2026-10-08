param([switch]$Unity, [switch]$PrepareUnity)
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$validationRoot = Join-Path $projectRoot '.local\environment'
$logs = Join-Path $validationRoot 'logs'
New-Item -ItemType Directory -Path $logs -Force | Out-Null
Set-Location $projectRoot
function Invoke-Checked([string]$Executable, [string[]]$Arguments, [string]$Log) {
    $previousErrorAction = $ErrorActionPreference
    try {
        # Windows PowerShell treats native stderr as errors, even on successful runs.
        $ErrorActionPreference = 'Continue'
        & $Executable @Arguments 2>&1 | Tee-Object -FilePath $Log
        $exitCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousErrorAction }
    if ($exitCode -ne 0) { throw "$Executable failed ($exitCode). See $Log" }
}
if ((& $toolchain.dotnet --version) -ne $toolchain.dotnetSdk) { throw 'SDK version does not match toolchain.json.' }
$core = Join-Path $validationRoot 'DotNetSmoke\Core'
$tests = Join-Path $validationRoot 'DotNetSmoke\Tests'
$art = Join-Path $validationRoot 'art\v001'
if (!$PrepareUnity) {
New-Item -ItemType Directory -Path $core, $tests -Force | Out-Null
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <Nullable>disable</Nullable>
    <AssemblyName>Catan.EnvironmentProbe</AssemblyName>
  </PropertyGroup>
</Project>
'@ | Set-Content (Join-Path $core 'Core.csproj') -Encoding UTF8
@'
namespace Catan.EnvironmentProbe
{
    public static class Probe
    {
        public static string RoundTrip(string value)
        {
            return System.Text.Encoding.UTF8.GetString(System.Text.Encoding.UTF8.GetBytes(value));
        }
    }
}
'@ | Set-Content (Join-Path $core 'Probe.cs') -Encoding UTF8
@'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsTestProject>true</IsTestProject>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" PrivateAssets="all" />
    <ProjectReference Include="../Core/Core.csproj" />
  </ItemGroup>
</Project>
'@ | Set-Content (Join-Path $tests 'Tests.csproj') -Encoding UTF8
@'
using System.Reflection;
using System.Runtime.Versioning;
using Catan.EnvironmentProbe;
using Xunit;
public class ToolchainChecks
{
    [Fact]
    public void LibraryTargetsUnityCompatibleFramework()
    {
        Assert.Equal(".NETStandard,Version=v2.1",
            typeof(Probe).Assembly.GetCustomAttribute<TargetFrameworkAttribute>().FrameworkName);
    }
    [Fact]
    public void LibraryExecutesWithChineseText()
    {
        const string label = "\u5361\u5766\u5c9b\u73af\u5883\u9a8c\u8bc1";
        Assert.Equal(label, Probe.RoundTrip(label));
    }
}
'@ | Set-Content (Join-Path $tests 'ToolchainChecks.cs') -Encoding UTF8
Invoke-Checked $toolchain.dotnet @('test', (Join-Path $tests 'Tests.csproj'), '-c', 'Release', '--logger', 'trx', '--results-directory', (Join-Path $validationRoot 'TestResults')) (Join-Path $logs 'dotnet-test.log')
New-Item -ItemType Directory -Path $art -Force | Out-Null
Invoke-Checked $toolchain.blenderExecutable @('--background', '--factory-startup', '--python-exit-code', '1', '--python', (Join-Path $PSScriptRoot 'blender_smoke.py'), '--', $art) (Join-Path $logs 'blender-export.log')
}
if (!$Unity -and !$PrepareUnity) { Write-Host 'PASS: .NET tests and Blender export. Unity was not requested.'; return }
if (!(Test-Path $toolchain.unity)) { throw 'Unity Editor is not installed at the pinned path.' }
$unityProject = Join-Path $validationRoot 'UnitySmoke'
$editor = Join-Path $unityProject 'Assets\Editor'
$plugins = Join-Path $unityProject 'Assets\Plugins'
$models = Join-Path $unityProject 'Assets\Models'
$scripts = Join-Path $unityProject 'Assets\Scripts'
$packages = Join-Path $unityProject 'Packages'
$settings = Join-Path $unityProject 'ProjectSettings'
New-Item -ItemType Directory -Path $editor,$plugins,$models,$scripts,$packages,$settings -Force | Out-Null
$manifest = @{ dependencies = @{
    'com.unity.render-pipelines.universal' = $toolchain.urp
    'com.unity.modules.physics' = '1.0.0'
    'com.unity.modules.screencapture' = '1.0.0'
} } | ConvertTo-Json -Depth 3
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $packages 'manifest.json'), $manifest, $utf8)
Copy-Item (Join-Path $PSScriptRoot 'unity-packages-lock.json') (Join-Path $packages 'packages-lock.json') -Force
$projectVersion = "m_EditorVersion: $($toolchain.unityEditor)`nm_EditorVersionWithRevision: $($toolchain.unityEditor) ($($toolchain.unityRevision))`n"
[IO.File]::WriteAllText((Join-Path $settings 'ProjectVersion.txt'), $projectVersion, $utf8)
Copy-Item (Join-Path $core 'bin\Release\netstandard2.1\Catan.EnvironmentProbe.dll') $plugins -Force
Copy-Item (Join-Path $art 'environment-cube-v001.fbx') $models -Force
Copy-Item (Join-Path $PSScriptRoot 'UnitySmoke.cs') $editor -Force
Copy-Item (Join-Path $PSScriptRoot 'SmokeRuntime.cs') $scripts -Force
if ($PrepareUnity -and !$Unity) {
    Write-Host "Prepared Unity validation project at $unityProject. Compilation and build have not run."
    return
}
Invoke-Checked $toolchain.unity @('-batchmode','-nographics','-quit','-projectPath',$unityProject,'-executeMethod','EnvironmentSmoke.Build','-logFile',(Join-Path $logs 'unity-build.log')) (Join-Path $logs 'unity-console.log')
$player = Join-Path $validationRoot 'Builds\Windows\EnvironmentSmoke.exe'
Invoke-Checked $player @('-batchmode','-nographics','--environment-smoke','-logFile',(Join-Path $logs 'player.log')) (Join-Path $logs 'player-console.log')
if (!(Select-String -Path (Join-Path $logs 'player.log') -Pattern 'CATAN_ENVIRONMENT_PLAYER_OK' -Quiet)) { throw 'Player completion marker missing.' }
$previews = Join-Path $validationRoot 'Previews'
New-Item -ItemType Directory -Path $previews -Force | Out-Null
$preview = Join-Path $previews ('windows-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.png')
$graphicsLog = Join-Path $logs 'player-graphics.log'
# Render the URP camera into a texture without relying on a visible window.
$graphicsPlayer = Start-Process -FilePath $player -ArgumentList @('-batchmode','-force-d3d11','--environment-render',('"' + $preview + '"'),'-logFile',('"' + $graphicsLog + '"')) -WindowStyle Hidden -PassThru
if (!$graphicsPlayer.WaitForExit(45000)) {
    Stop-Process -Id $graphicsPlayer.Id
    throw 'Graphics player timed out. See player-graphics.log.'
}
if ($graphicsPlayer.ExitCode -ne 0) { throw "Graphics player failed ($($graphicsPlayer.ExitCode)). See player-graphics.log." }
if (!(Test-Path $preview) -or !(Select-String -Path (Join-Path $logs 'player-graphics.log') -Pattern 'CATAN_ENVIRONMENT_RENDER_OK' -Quiet)) { throw 'Graphics validation did not complete.' }
@{ screenshot = $preview; graphicsLog = (Join-Path $logs 'player-graphics.log') } | ConvertTo-Json | Set-Content (Join-Path $logs 'graphics-verification.json') -Encoding UTF8
if ($PrepareUnity) { Write-Host 'PASS: Unity import/build, Windows player execution and graphics capture; reused existing .NET/Blender artifacts.' }
else { Write-Host 'PASS: .NET tests, Blender export, Unity import/build, Windows player execution and graphics capture.' }
