param([string]$BuildRoot='.local/release-1.6',[string]$ReleaseName='卡坦岛之三国争霸内测版1.6-PC')
$ErrorActionPreference='Stop'
$packageRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$packageBuild=Join-Path $packageRepo "$BuildRoot/Builds/Windows"
$packageRoot=Join-Path $packageRepo "$BuildRoot/release"
$packageFolder=Join-Path $packageRoot $ReleaseName
$packageZip=Join-Path $packageRoot "$ReleaseName.zip"
if(!(Test-Path -LiteralPath (Join-Path $packageBuild 'CatanM7.exe'))){throw '缺少已构建的可玩程序。'}
if((Test-Path -LiteralPath $packageFolder) -or (Test-Path -LiteralPath $packageZip)){throw '发行路径已存在；请使用新的发行目录，避免覆盖已发出的版本。'}
New-Item -ItemType Directory -Path $packageFolder -Force | Out-Null
foreach($packageItem in Get-ChildItem -LiteralPath $packageBuild -Recurse -File){
    $packageRelative=$packageItem.FullName.Substring($packageBuild.Length+1)
    if($packageRelative -match '(?i)DoNotShip|\.pdb$'){continue}
    $packageTarget=Join-Path $packageFolder $packageRelative
    New-Item -ItemType Directory -Path (Split-Path $packageTarget) -Force | Out-Null
    Copy-Item -LiteralPath $packageItem.FullName -Destination $packageTarget
}
Copy-Item -LiteralPath (Join-Path $packageRepo 'Unity/Catan/Assets/Resources/Fonts/OFL.txt') -Destination (Join-Path $packageFolder '中文字体许可-OFL.txt')
$packageReadme=@'
卡坦岛之三国争霸内测版1.6-PC

使用方法：先完整解压此文件夹，再双击 CatanM7.exe。请保留同目录的所有数据文件和运行库。
运行平台：Windows 64 位；本地单人及 AI 对局，另带局域网房间服务。包含基础版、航海家、城市与骑士和已支持的双扩展组合。

本版汇总：修复海港主页、数字牌、港口和棋子图标缺失；资源牌统一左键增加、右键减少；改进金矿选择、玩家交易、出牌与建造动画；蛮族结算期间保持本次攻守力量。PC 与 Android 共用规则、当前 AI、实体资产和完整桌面界面。
操作：拖拽地图/滚轮缩放，建筑拖到合法位置；右击骑士操作；点船选中再点空海岸改道；所属港口兑换（专属2:1/通用3:1）。左上银行、玩家交易、三设施入口；底部资源/发展页签、整理手牌（新牌追加末尾，读档恢复分类基线）。右下掷骰/等待弃牌/结束回合。Esc优先取消，再开菜单；主题切换、当前扩展与航海特殊行动从菜单查看。

本版沿用原优化 AI 和存档路径。请先保存并退出旧窗口，再启动新版。内测反馈请附画面、规则模式和复现步骤。
本包不含开发验收局、用户存档、私人日志或调试符号；局域网与在线房间已实现本机验证范围，使用方法见 Server/服务启动说明.md；当前没有公共在线服务器。真实多机、公网、清洁环境与长期性能待验。
'@
$packageReadme=$packageReadme.Replace('卡坦岛之三国争霸内测版1.6-PC',$ReleaseName)
[IO.File]::WriteAllText((Join-Path $packageFolder '试玩说明.txt'),$packageReadme,[Text.UTF8Encoding]::new($true))
$packageRows=foreach($packageFile in Get-ChildItem -LiteralPath $packageFolder -Recurse -File){
    [ordered]@{file=$packageFile.FullName.Substring($packageFolder.Length+1);bytes=$packageFile.Length;sha256=(Get-FileHash -LiteralPath $packageFile.FullName -Algorithm SHA256).Hash}
}
$packageRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $packageRoot '发行文件校验.json') -Encoding UTF8
Compress-Archive -LiteralPath $packageFolder -DestinationPath $packageZip -CompressionLevel Optimal
Get-Item -LiteralPath $packageZip | Select-Object FullName,Length
Get-FileHash -LiteralPath $packageZip -Algorithm SHA256
