param(
    [string]$OutputRoot = 'game version/1.6/Catan1.6.12-Cloud-Online',
    [ValidateSet('win-x64','linux-x64','linux-arm64')][string]$Runtime = 'win-x64'
)
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Enter-DevEnvironment.ps1')
$m7RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$m7OutputRoot = [IO.Path]::GetFullPath((Join-Path $m7RepoRoot $OutputRoot))
$m7ServerOutput = if ($Runtime -eq 'win-x64') { Join-Path $m7OutputRoot 'Builds\Windows\Server' } else { Join-Path $m7OutputRoot "Server\$Runtime" }
New-Item -ItemType Directory -Force -Path $m7ServerOutput | Out-Null
# 常规测试会刷新无 RID 的还原缓存；发布前显式还原目标运行时，避免复用缺少 win-x64 的缓存。
& $toolchain.dotnet restore (Join-Path $m7RepoRoot 'src\Catan.Server\Catan.Server.csproj') -r $Runtime --force --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'M7 权威房间服务运行时还原失败。' }
& $toolchain.dotnet publish (Join-Path $m7RepoRoot 'src\Catan.Server\Catan.Server.csproj') -c Release -r $Runtime --self-contained true -p:PublishSingleFile=false --no-restore -o $m7ServerOutput --disable-build-servers -m:1
if ($LASTEXITCODE -ne 0) { throw 'M7 权威房间服务发布失败。' }
Copy-Item -LiteralPath (Join-Path $m7RepoRoot 'docs\M7_SERVER.md') -Destination (Join-Path $m7ServerOutput '服务启动说明.md') -Force
Write-Host "M7 自包含房间服务已生成：$m7ServerOutput"
