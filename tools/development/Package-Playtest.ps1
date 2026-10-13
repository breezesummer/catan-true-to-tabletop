param([string]$BuildRoot='.local/m6-ui-v009',[string]$ReleaseName='卡坦岛之三国争霸内测版1.1')
$ErrorActionPreference='Stop'
$packageRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageBuild=Join-Path $packageRepo "$BuildRoot/Builds/Windows"
$packageRoot=Join-Path $packageRepo "$BuildRoot/release"
$packageFolder=Join-Path $packageRoot $ReleaseName
$packageZip=Join-Path $packageRoot "$ReleaseName.zip"
if(!(Test-Path -LiteralPath (Join-Path $packageBuild 'CatanM6.exe'))){throw '缺少已构建的可玩程序。'}
if((Test-Path -LiteralPath $packageFolder) -or (Test-Path -LiteralPath $packageZip)){throw '发行路径已存在；请使用新的发行目录，避免覆盖已发出的版本。'}
New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null
foreach($packageItem in Get-ChildItem -LiteralPath $packageBuild){
    if($packageItem.Name -like '*DoNotShip*'){continue}
    Copy-Item -LiteralPath $packageItem.FullName -Destination $packageFolder -Recurse
}
$packageReadme=@'
卡坦岛之三国争霸内测版1.1

使用方法：先完整解压此文件夹，再双击 CatanM6.exe。请保留同目录的所有数据文件和运行库。
运行平台：Windows 64 位；本地单人及 AI 对局。包含基础版、航海家、城市与骑士和已支持的双扩展组合。

本次调整：棋盘居中并恢复全桌统一缩放；银行左上、蛮族轨道和航程右上、扩大历史左下；左右玩家镜像摆放；本人船横放、城墙分开；缩小城市设施。
操作：鼠标滚轮缩放；Esc 打开游戏菜单；已有浮窗或拿起棋子时，Esc 先关闭/取消，再按一次打开菜单。菜单内可保存、恢复、切换俯视和新开对局。骑士与特殊行动位于右下结束回合上方。

本版沿用原优化 AI 和存档路径。请先保存并退出旧窗口，再启动新版。内测反馈请附画面、规则模式和复现步骤。
本包不含开发验收局、用户存档、私人日志或调试符号；好友联机和成品发行验收仍未完成。
'@
[IO.File]::WriteAllText((Join-Path $packageFolder '试玩说明.txt'),$packageReadme,[Text.UTF8Encoding]::new($true))
$packageRows=foreach($packageFile in Get-ChildItem -LiteralPath $packageFolder -Recurse -File){
    [ordered]@{file=$packageFile.FullName.Substring($packageFolder.Length+1);bytes=$packageFile.Length;sha256=(Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash}
}
$packageRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageRoot '发行文件校验.json') -Encoding UTF8
Compress-Archive -LiteralPath $packageFolder -DestinationPath $packageZip -CompressionLevel Optimal
Get-Item -LiteralPath $packageZip | Select-Object FullName,Length
Get-FileHash -LiteralPath $packageZip -Algorithm SHA256
