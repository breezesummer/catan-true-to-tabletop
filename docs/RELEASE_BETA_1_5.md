# 卡坦岛之三国争霸内测版1.5

2026-10-16，按用户发布要求，将内测版1.4升级为内测版1.5，同步提供 Windows 完整压缩包与 Android APK。两端使用本仓库同一份规则、当前 AI、实体资产和完整桌面界面。

## 下载与运行

- [Windows 完整包](../.local/release-1.5/release/卡坦岛之三国争霸内测版1.5-PC.zip)：完整解压后双击 `CatanM6.exe`，保留同目录全部文件；包含自带 .NET 运行时的 `Server` 房间服务。
- [Android APK](../.local/release-1.5/release/卡坦岛之三国争霸内测版1.5-Android/卡坦岛之三国争霸内测版1.5-Android.apk)：保存到手机后用系统文件管理器安装。目标为 Android 8.0+、ARM64、OpenGL ES 3，包内另含模拟器使用的 x86_64。
- [本机 Windows 程序](../.local/release-1.5/Builds/Windows/CatanM6.exe)：统一启动脚本与默认构建、打包入口均指向本版。
- [发行文件目录](../.local/release-1.5/release/)：包含两端说明、字体许可与文件校验清单。

Android 包名保持 `com.catandevelopment.threekingdoms`，版本名为 `1.5`，构建号为 `10500`，沿用原有内测签名。Windows 保留原产品标识与本地存档位置，规则版本和权威存档格式沿用。

## 本版汇总

- 中世纪海港主页、五项主菜单，以及分层开局、加载、局域网／在线房间和声音／外观设置。
- 交互音效和环境声、手牌数与限额、弃牌点选确认、三档 AI 速度、收牌与建造动画。
- 现有实体高山、港口、标记和玩家组件，基础版、航海家、城市与骑士及已支持组合保持可玩。
- Windows 随包房间服务；Android 作为加入端。在线入口需要自行部署可信 HTTPS 服务，当前没有公共在线服务器。

## 验证与边界

本轮实际构建、测试、运行、签名与发行包校验结果见[发布验证摘要](verification/release-beta-1.5/summary.json)。原始日志、截图、私有测试局和模拟器存档备份保存在 `.local/release-1.5/`，不进入发行包。

用户本人完整对局、Android 真机、真实多机与公网联机、清洁环境及长期性能仍待验。本次内测发布不满足完整 M7／M8 阶段退出条件，路线图保持原状态。

GitHub 下载入口：[内测版1.5 Release](https://github.com/breezesummer/catan-true-to-tabletop/releases/tag/v1.5.0-beta)。本次向 GitHub 分发现有 PC 与 Android 产物，不重新构建或升级版本；附件上传和公开状态以 Release 页面及发布验证摘要为准。未向微信或应用商店上传。
