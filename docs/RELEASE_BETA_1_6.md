# 卡坦岛之三国争霸内测版1.6

2026-10-17，按用户发布要求升级为内测版1.6，同步提供 Windows 完整包和 Android APK，构建产物使用当前联机开发阶段的 `CatanM7` 名称。两端共用规则、AI、实体资产和完整桌面界面。

## 下载与运行

- [Windows 完整包](https://github.com/breezesummer/catan-true-to-tabletop/releases/download/v1.6.0-beta/CatanM7-Beta-1.6-Windows-x64.zip)：完整解压后双击 `CatanM7.exe`，保留全部同目录文件；包含自带运行时的 `Server` 房间服务。
- [Android APK](https://github.com/breezesummer/catan-true-to-tabletop/releases/download/v1.6.0-beta/CatanM7-Beta-1.6-Android.apk)：保存到手机后使用系统文件管理器安装。支持 Android 8.0+、ARM64、OpenGL ES 3，另含模拟器用 x86_64。
- [GitHub Release](https://github.com/breezesummer/catan-true-to-tabletop/releases/tag/v1.6.0-beta)：中文安装说明、字体许可和 SHA-256 校验文件随包提供。
- [本机 Windows 程序](../.local/release-1.6/Builds/Windows/CatanM7.exe)：统一试玩、AI 和联机启动脚本均指向本版。

Android 包名保持 `com.catandevelopment.threekingdoms`，版本名 `1.6`，构建号 `10600`，沿用内测签名以支持覆盖安装。Windows 保留既有产品标识和存档路径；可执行文件改名不迁移用户存档。

## 本版内容

- 恢复主页海港原画、数字牌、港口图案和左侧棋子图标；构建前拒绝损坏的 Unity 配置，并校验关键贴图及材质绑定。
- 资源牌统一左键增加、右键减少；Android 使用点击增加、长按松开减少。银行、港口、玩家交易和资源选择共用该输入方式。
- 改进金矿选择、玩家交易、出牌展示、资源收取及沿路移动／升级动画；保存成功后返回主页。
- 蛮族攻击结算期间保持本次攻守力量快照，中途保存恢复显示一致；玩家间继续允许合法的多资源混合报价。
- 基础版、航海家、城市与骑士和已支持的双扩展组合保持可玩；Windows 提供房间服务，Android 可加入。当前没有公共在线服务器。

## 验证与边界

本次构建、独立回归、原生运行、覆盖安装、触控、发行包和发布结果见[发布验证摘要](verification/release-beta-1.6/summary.json)。原始日志、截图及私有测试局保存在 `.local/release-1.6/`，不进入发行附件。

已知边界：旧港口几何专项曾因港口与建筑占地重合失败，此次不将该专项计为通过。Android 真机、用户完整对局、真实多机／公网、清洁环境及长期性能继续待验；本次内测发布不表示完整 M7／M8 退出，路线图保持原状态。

已于 2026-10-17 20:49:06 +08:00 公开为 GitHub 预发布版本，标签对应源码提交 `adec765b0fc3e7b4f53418dd514ddd9878e7fa1e`。五个附件均核对 GitHub 返回的长度和 SHA-256，与本地最终产物一致。详见[公开发布回执](verification/release-beta-1.6/github-release.json)。
