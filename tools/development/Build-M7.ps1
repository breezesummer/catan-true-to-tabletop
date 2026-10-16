param([switch]$VerifyPlayer,[string]$OutputRoot='.local/menu-medieval-v002')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m7RepoRoot=(Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$m7Output=[IO.Path]::GetFullPath((Join-Path $m7RepoRoot $OutputRoot))
$m7Plugins=Join-Path $m7RepoRoot 'Unity/Catan/Assets/Plugins'
New-Item -ItemType Directory -Force -Path $m7Plugins | Out-Null
& $toolchain.dotnet build (Join-Path $m7RepoRoot 'src/Catan.Network.Contracts/Catan.Network.Contracts.csproj') -c Release --disable-build-servers -m:1
if($LASTEXITCODE -ne 0){throw 'M7 共享网络协议构建失败。'}
$m7Contract=Join-Path $m7RepoRoot 'src/Catan.Network.Contracts/bin/Release/netstandard2.1/Catan.Network.Contracts.dll'
Copy-Item -LiteralPath $m7Contract -Destination (Join-Path $m7Plugins 'Catan.Network.Contracts.dll') -Force
& (Join-Path $PSScriptRoot 'Build-M6.ps1') -OutputRoot $OutputRoot -VerifyPlayer:$VerifyPlayer
if($LASTEXITCODE -ne 0){throw 'M7 Unity 客户端构建失败。'}
& (Join-Path $PSScriptRoot 'Build-M7Server.ps1') -OutputRoot $OutputRoot
if($LASTEXITCODE -ne 0){throw 'M7 自包含 Windows 房间服务发布失败。'}
Write-Host "M7 Windows 客户端与随包服务：$m7Output/Builds/Windows/CatanM6.exe"
