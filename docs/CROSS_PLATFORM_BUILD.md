# 分系统启动与构建

同一 Unity 工程构建 Windows 客户端与 Android APK。macOS 使用 `.command`／zsh 和系统 Python 3；Windows 继续使用 PowerShell，默认工具链保存在 `D:\Development`。不要求在 Mac 安装 PowerShell。

## macOS

默认工具目录从仓库位置推导：当仓库在 `开发栈/Project/catan-true-to-tabletop`，使用 `开发栈/Development`。迁移机器或使用其他目录时，在调用前设置 `CATAN_DEVELOPMENT_ROOT`；Unity 编辑器也可以通过 `UNITY_EDITOR` 覆盖。

在仓库根目录执行：

```zsh
# 核对路径、双端构建模块、Android 工具与源文件
./tools/development/Start-Dev.command check
# 打开 VS Code；也可双击此文件
./tools/development/Start-Dev.command
# 打开游戏工程
./tools/development/Start-Unity.command
# 进入开发终端；编译 .NET 使用 build
./tools/development/Start-Dev.command shell
./tools/development/Start-Dev.command build
```

构建入口如下，`--help` 显示所有参数：

```zsh
# Windows 客户端与随包的 win-x64 自包含房间服务
./tools/development/Build-Game.command --target windows
# Android 默认包含 ARM64 和 x86_64；可加 --arm64-only
./tools/development/Build-Game.command --target android
# 默认同时构建 Windows、房间服务和 Android
./tools/development/Build-Game.command
# 仅构建服务端，支持 win-x64、linux-x64、linux-arm64
./tools/development/Build-Game.command --target server --runtime linux-x64
```

Windows 客户端使用 Mono，Android 使用 IL2CPP。每次客户端构建都先重新编译规则、AI 和网络协议 DLL，通过源文件完整性检查后调用 Unity；Windows 客户端构建包括随包服务。构建锁阻止同一仓库的新入口并发打包；运行前应关闭占用该工程的 Unity 编辑器。这里的 macOS 是构建宿主，本轮没有新增可玩的 macOS 客户端。

Android 的 Unity 路径检查不接受中文工程目录，也会解析工程短链接，因此脚本在 `/tmp/catan-用户编号-仓库哈希-build` 创建带归属标记的真实英文工作区，仅同步 Assets、Packages、ProjectSettings。已有导入缓存以 APFS 写时复制复用，随后独立更新；工具与密钥通过英文短链接访问。完成后把 APK 收回 `game version`，原项目不迁移。临时工作区可在 Unity 退出后删除，下次构建会重建。

原 `.local/development/start-dev-2026-10-19.command`、`start-unity-2026-10-19.command` 继续转到受版本管理的新入口；这些本机旧入口不随仓库分发。

## Android 签名

覆盖安装旧版本需要同一签名密钥。Windows 的 DPAPI 密码文件不能在 macOS 解密；复制源码或 APK 也不能恢复原私钥。

如有原 `.keystore` 与密码，在交互终端导入一次：

```zsh
./tools/development/Build-Game.command --import-android-keystore /绝对路径/catan-internal.keystore
```

脚本先验证 `catan-internal` 别名与密码，再将密钥保存在本仓库 `.local/development/credentials/android`，密码保存在 macOS 登录钥匙串，不写入源码、日志或命令行参数。已有不同密钥时停止，避免替换。也可通过进程环境变量 `CATAN_ANDROID_KEYSTORE` 与 `CATAN_ANDROID_KEYSTORE_PASS` 提供原签名。

缺少原签名时，正常构建明确停止，不自动创建替代签名。只验证打包时执行：

```zsh
./tools/development/Build-Game.command --target android --test-signing
# 同时验证两个平台：
./tools/development/Build-Game.command --test-signing
```

测试模式生成临时独立密钥，退出后删除；APK 元数据标记 `build-verification-only`，输出目录含 `SigningTest`。该 APK 不能覆盖安装旧内测版，也不用于正式发布。本轮用户确认没有原密钥备份，采用此模式验证。

## Windows

原 `Start-Dev.ps1`、`Build-M7.ps1`、`Build-Android.ps1` 继续可用，新增统一入口：

```powershell
# 全部；单端可用 -Target Windows、Android 或 Server
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Build-Game.ps1
```

默认读取 `tools/development/toolchain.json` 的 Windows 配置。工具目录变更时可通过 `CATAN_DEVELOPMENT_ROOT` 覆盖，并沿用原来的子目录结构。Windows 保持原 DPAPI 签名保管流程；Mac 使用钥匙串，两者不混用。Windows 入口在非 Windows 宿主上提示对应的 Mac 入口。

## 产物与验证边界

当前默认目录为 `game version/1.6/Catan1.6.7-CrossPlatformBuild`，测试签名追加 `-SigningTest`；程序与 APK 均名为 `Catan1.6.7`。自定义 `--output-root` 必须在本仓库 `game version` 下，目录名须包含当前版本；测试签名必须保留 `SigningTest` 标识。

- Windows：`Builds/Windows/Catan1.6.7.exe` 与 `Builds/Windows/Server`。
- Android：`Builds/Android/Catan1.6.7.apk` 与含签名／文件摘要的 `build-info.json`。
- 构建过程：`logs`；Mac 成功构建另写 `构建说明.json`。
- 新统一入口成功后只保留最近五个内测目录；带 `release` 或 `正式发布.txt` 标记的正式发布目录保留，符号链接不参与清理。

构建成功说明工具链、编译及 APK 签名验证通过。Windows 游戏运行、完整界面、Android 真机安装与触控仍须在对应环境验收；不以 Mac 上生成文件替代目标平台运行验收。具体本轮结果见[开发日志](../The%20Farmer%27s%20Log.md)。
