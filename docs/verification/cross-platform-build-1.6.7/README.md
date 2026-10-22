# 2026-10-19 分系统构建验证（1.6.7）

本轮仅修改启动／构建脚本和游戏版本标识，规则、AI、玩法及素材没有修改。工具使用本机 `开发栈/Development` 中的 Unity 6000.3.25f1、.NET SDK 10.0.401 及 Unity Android 工具链。

| 项目 | 实际结果 |
| --- | --- |
| 新入口与旧本机入口 | 从仓库根目录和 `/tmp` 调用检查均通过，解析到 `Development` |
| zsh 语法、Python 编译 | 通过 |
| 开发终端的 .NET 编译 | 通过，零警告、零错误 |
| Unity 源文件完整性 | 最终 1847 个文件通过 |
| 构建保护测试 | 9/9 通过；覆盖清理边界、符号链接、源文件损坏、缺失原签名、导入失败保留原件、临时密钥清理、构建副本移除过期源文件／产物 |
| Windows 客户端 | 在 Mac 上构建成功，PE32+ x86-64 |
| 随包房间服务 | 自包含 win-x64 发布成功，PE32+ x86-64 |
| Android APK | IL2CPP、ARM64 与 x86_64；1.6.7／10607，API26／36，sensorLandscape，非调试包 |
| Android 签名 | apksigner 验证 v2 通过；用户授权的独立临时测试签名，APK 内未发现 keystore 或 DPAPI 密码文件 |
| 版本保留 | 本机只有本轮一个内测目录，无需删除旧版；正式发布目录未变动 |

Android 首次尝试因原工程中文路径被 Unity 拒绝；工程短链接同样会被 Unity 解析回中文路径。最终采用真实英文临时工作区同步源文件，复用缓存，成功打包后回收产物。失败尝试不计入通过结果。首次产物检查误用了 `aapt badging` 不提供的方向字段，已改用包内 Manifest 的 `screenOrientation=6` 核实横屏设置。

产物位于 `game version/1.6/Catan1.6.7-CrossPlatformBuild-SigningTest`。Windows 程序和 Android APK 均名为 `Catan1.6.7`；构建日志在其 `logs` 下。文件大小和 SHA-256 见[机器摘要](summary.json)。Unity 在原工程生成的场景／资源序列化差异和重复编译 DLL 已还原，仅保留有意修改的版本字段。

没有原签名备份，未验证原签名导入与钥匙串交互；测试 APK 不能覆盖安装此前内测版。Windows 原生 PowerShell 入口、Windows 游戏运行、Android 真机安装／触控／实际联机仍待对应设备验证。本轮没有满足新的阶段退出条件，不更新 ROADMAP，不创建正式发布版。

复现入口见[分系统构建说明](../../CROSS_PLATFORM_BUILD.md)。脚本保护测试：

```zsh
python3 -m unittest discover -s tools/validation -p test_macos_build.py -v
```
