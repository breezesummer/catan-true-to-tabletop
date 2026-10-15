param([string]$BuildRoot='.local/m6-release-1.4',[string]$ReleaseName='卡坦岛之三国争霸内测版1.4-安卓',[switch]$CreateZip)
$ErrorActionPreference='Stop'
$androidPackageRepo=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$androidPackageBuild=Join-Path $androidPackageRepo "$BuildRoot/Builds/Android/Catan.apk"
$androidPackageRoot=Join-Path $androidPackageRepo "$BuildRoot/release"
$androidPackageFolder=Join-Path $androidPackageRoot $ReleaseName
$androidPackageZip=Join-Path $androidPackageRoot "$ReleaseName.zip"
if(!(Test-Path -LiteralPath $androidPackageBuild)){throw '缺少已构建的安卓 APK。'}
if((Test-Path -LiteralPath $androidPackageFolder) -or (Test-Path -LiteralPath $androidPackageZip)){throw '发行路径已存在；请使用新的发行目录。'}
New-Item -ItemType Directory -Path $androidPackageFolder -Force | Out-Null
Copy-Item -LiteralPath $androidPackageBuild -Destination (Join-Path $androidPackageFolder "$ReleaseName.apk")
Copy-Item -LiteralPath (Join-Path $androidPackageRepo 'Unity/Catan/Assets/Resources/Fonts/OFL.txt') -Destination (Join-Path $androidPackageFolder '中文字体许可-OFL.txt')
$androidPackageReadme=@'
卡坦岛之三国争霸内测版1.4-安卓

将 APK 保存到手机，再用系统文件管理器打开安装；微信内不能直接运行。若收到的是可选 ZIP，请先完整解压。
本应用为横屏、本地单人及 AI 对局；无需账号。基础版、航海家、城市与骑士和已支持的双扩展组合与 PC 共用规则、当前 AI 和资产。

触控：单指拖动空白棋盘移动视角；双指缩放与平移。选择建造后点击合法目标，也可拖放棋子。点击骑士打开操作，点击自己的船再点新海岸改道。手机菜单、取消、视角、建造、交易、手牌和设施都有屏幕按钮。
返回键先取消操作，再打开菜单。切到后台会尝试保存当前局面，也可以从菜单主动保存、恢复、暂停 AI 和返回开局。两端存档留在各自设备，尚无联网或云同步。

兼容目标为 Android 8.0 及以上的 64 位手机（ARM64），需要 OpenGL ES 3；包内另含模拟器用 x86_64。已在 Android 15 模拟器验收，真机完整对局与长期性能待验。内测反馈请附设备型号、安卓版本、画面、规则模式和复现步骤。
中文字体为 Noto Sans CJK SC，SIL Open Font License 1.1 许可随包附上。工程未复制另一套规则或 AI。
'@
$androidPackageReadme=$androidPackageReadme.Replace('卡坦岛之三国争霸内测版1.4-安卓',$ReleaseName)
[IO.File]::WriteAllText((Join-Path $androidPackageFolder '安装与触控说明.txt'),$androidPackageReadme,[Text.UTF8Encoding]::new($true))
$androidBuildInfo=Join-Path $androidPackageRepo "$BuildRoot/Builds/Android/build-info.json"
if(Test-Path -LiteralPath $androidBuildInfo){Copy-Item -LiteralPath $androidBuildInfo -Destination (Join-Path $androidPackageFolder '安卓构建信息.json')}
$androidPackageRows=foreach($androidPackageFile in Get-ChildItem -LiteralPath $androidPackageFolder -Recurse -File){
    [ordered]@{file=$androidPackageFile.FullName.Substring($androidPackageFolder.Length+1);bytes=$androidPackageFile.Length;sha256=(Get-FileHash -LiteralPath $androidPackageFile.FullName -Algorithm SHA256).Hash}
}
$androidPackageRows | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $androidPackageRoot '安卓发行文件校验.json') -Encoding UTF8
if($CreateZip){Compress-Archive -LiteralPath $androidPackageFolder -DestinationPath $androidPackageZip -CompressionLevel Optimal}
$androidReleaseApk=Join-Path $androidPackageFolder "$ReleaseName.apk"
Get-Item -LiteralPath $androidReleaseApk | Select-Object FullName,Length
Get-FileHash -LiteralPath $androidReleaseApk -Algorithm SHA256
if($CreateZip){Get-Item -LiteralPath $androidPackageZip | Select-Object FullName,Length}
