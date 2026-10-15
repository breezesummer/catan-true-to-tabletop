param([string]$BuildRoot='.local/m6-ui-v011',[string]$ReleaseName='卡坦岛之三国争霸内测版1.2')
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
卡坦岛之三国争霸内测版1.2

使用方法：先完整解压此文件夹，再双击 CatanM6.exe。请保留同目录的所有数据文件和运行库。
运行平台：Windows 64 位；本地单人及 AI 对局。包含基础版、航海家、城市与骑士和已支持的双扩展组合。

本次调整：放大时相机逐渐降低俯视角；紧凑建筑栏、正面建筑与俯视城墙、独立数量和镶边；金属质感手牌控件；设施独立展示；交易付出筛选已有资源；蛮族航程增加最长道路持有者与长度；顶部中央回合徽章。
操作：拖拽地图/滚轮缩放，建筑拖到合法位置；右击骑士操作；点船选中再点空海岸改道；所属港口兑换（专属2:1/通用3:1）。左上银行、玩家交易、三设施入口；底部资源/发展页签、整理手牌（新牌追加末尾，读档恢复分类基线）。右下掷骰/等待弃牌/结束回合。Esc优先取消，再开菜单；主题切换、当前扩展与航海特殊行动从菜单查看。

本版沿用原优化 AI 和存档路径。请先保存并退出旧窗口，再启动新版。内测反馈请附画面、规则模式和复现步骤。
本包不含开发验收局、用户存档、私人日志或调试符号；好友联机和成品发行验收仍未完成。
'@
$packageReadme=$packageReadme.Replace('卡坦岛之三国争霸内测版1.2',$ReleaseName)
[IO.File]::WriteAllText((Join-Path $packageFolder '试玩说明.txt'),$packageReadme,[Text.UTF8Encoding]::new($true))
$packageRows=foreach($packageFile in Get-ChildItem -LiteralPath $packageFolder -Recurse -File){
    [ordered]@{file=$packageFile.FullName.Substring($packageFolder.Length+1);bytes=$packageFile.Length;sha256=(Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash}
}
$packageRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageRoot '发行文件校验.json') -Encoding UTF8
Compress-Archive -LiteralPath $packageFolder -DestinationPath $packageZip -CompressionLevel Optimal
Get-Item -LiteralPath $packageZip | Select-Object FullName,Length
Get-FileHash -LiteralPath $packageZip -Algorithm SHA256
