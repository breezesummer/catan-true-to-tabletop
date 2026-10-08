# 开发环境

配置日期：2026 年 10 月 8 日。`D:\Development` 下每个工具各占一个目录，Unity Hub 与 Editor 合并在 `Unity` 目录内。项目缓存、日志、临时文件和项目专用编辑器配置都位于 `D:\Project\the-settlers-of-catan\.local`。现有 C 盘 .NET 6 运行时未移除，本项目通过专用启动脚本使用 D 盘 SDK。

| 工具 | 固定版本 / 路径 | 实际检查 |
| --- | --- | --- |
| .NET SDK | `10.0.401`，`D:\Development\dotnet` | `--info`、.NET Standard 2.1 编译、两项 xUnit 环境测试通过 |
| .NET 运行时 | `10.0.12`，随 SDK 放在 D 盘 | Core、ASP.NET Core、Windows Desktop 均在 SDK 的 `shared` 目录 |
| Unity Hub | `3.22.2`，`D:\Development\Unity\Hub` | 与 Editor 同属 Unity 工具目录；Editor 安装根目录为 `D:\Development\Unity` |
| Unity Editor | `6000.3.25f1`，revision `e1dba0a9aba4`，`D:\Development\Unity\6000.3.25f1` | 许可可用；实际导入、C# 编译、Windows x64 Mono 构建及程序运行通过 |
| URP | Editor 自带 `17.3.0` | 验证工程已导入并使用该版本构建；实际解析的包锁已保存 |
| Blender | `4.5.14 LTS`，`D:\Development\Blender\blender-4.5.14-windows-x64` | 后台 Python 脚本运行、保存 `.blend` 和导出 FBX 通过 |
| VS Code | 复用 `1.133.0`，`D:\Development\Microsoft VS Code` | 扩展在该工具目录的 `Extensions` 内；项目专用配置在项目 `.local/development/vscode` 内 |
| Git / Git LFS | 复用 `2.49.0.windows.1` / `3.6.1`，`D:\Tool\Git` | 版本命令通过；未迁移或重装已有 Git |

项目专用 VS Code 扩展已安装：C# `2.160.4`、C# Dev Kit `3.40.210`、Unity `1.3.2`、.NET Install Tool `3.2.0`。通过已有 SDK 路径配置，避免扩展另行下载默认位置的运行时。

路径职责：

| 内容 | 位置 |
| --- | --- |
| 开发工具 | `D:\Development\dotnet`、`Unity`（含 `Hub` 与 Editor 版本目录）、`Blender`、已有 VS Code |
| VS Code 扩展组件 | `D:\Development\Microsoft VS Code\Extensions` |
| 本项目环境安装下载、分段和安装元数据 | 项目内 `.local/development/downloads` |
| 本项目 .NET、NuGet、Unity 依赖缓存 | 项目内 `.local/development/caches` |
| 项目专用 Hub、VS Code、Blender 配置及其缓存/日志 | 项目内 `.local/development/unity-hub`、`vscode`、`blender` |
| 本项目开发会话临时文件 | 项目内 `.local/development/temp` |
| 环境配置迁移记录与原登记备份 | 项目内 `.local/development/migration` |
| 项目的配置与可复现脚本 | 仓库内 `global.json`、`.vscode`、`tools/development` |
| 环境验证工程、模型、测试报告、日志和 Windows 构建 | 仓库内 `.local/environment`，由 Git 忽略 |
| 后续正式工程、规则源码和美术资产 | 仓库内，按开发路线及 ART_PIPELINE.md 建立 |

本次未全局改写 PATH、TEMP，也未修改原有 VS Code 的个人设置。脚本仅为当前进程及其子进程设置环境；直接从普通终端输入 `dotnet` 仍可能选到原有 C 盘运行时。后续开发应使用下列入口。

从项目根目录启动开发终端、编辑器或 Unity Hub：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Shell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Code
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Hub
```

也可在已有 PowerShell 中加载项目环境：

```powershell
. .\tools\development\Enter-DevEnvironment.ps1
dotnet --info
```

`global.json` 固定 SDK 精确版本并禁用版本滚动；`toolchain.json` 固定本机工具路径。`tools/development/unity-packages-lock.json` 保存实际导入的 Unity 包依赖版本，验证脚本会将它复制为工程的 `Packages/packages-lock.json`。VS Code 的项目终端自动加载相同环境。Unity Hub 通过入口使用本项目的用户数据目录。Windows 公共 Unity 快捷方式通过同一启动脚本加载环境；迁移后的注册修复由 `tools/development/Repair-UnityRegistration.ps1` 执行，修改公共快捷方式和系统卸载登记需要管理员权限。

此前在 Development 顶层创建的 `Caches`、`Downloads`、`Logs`、`Temp`、`UnityHubData` 和 `VSCode-Catan` 已迁移出该目录；文件保留在上述项目目录中。其他原有开发工具目录不受这次整理影响。

复跑 .NET 与 Blender 检查：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Verify-Environment.ps1
```

已跑的检查：

- 在项目 `.local/environment/DotNetSmoke` 编译 `.NET Standard 2.1` 探针库，限制为 C# 9；在 `.NET 10` 中执行两项 xUnit 测试，均通过。测试检查目标框架和中文字符串跨程序集执行，仅证明工具链，不代表游戏规则已经实现。
- Blender 后台从脚本生成合成测试方块，使用米制、底面中心枢轴；保存 `.blend`、FBX 及来源/导出参数清单。尺寸为人为设置的 80 × 80 × 10 mm，不是卡坦实物的测量数据。Unity 实际导入后检查世界尺寸为 0.08 × 0.01 × 0.08 m、底面位于 Y=0，误差限 0.0001 m，均通过。
- Unity Editor 实际加载 `.NET Standard 2.1` 探针 DLL 并执行中文字符串检查，编译验证脚本，建立 URP 场景，导出 Windows x64 Mono 程序；程序无图形启动后再次执行 DLL 检查并正常退出。
- Windows 程序通过 Direct3D 11 在 AMD Radeon RX 9070 XT 上执行 URP 相机渲染请求，将结果读回为 800 × 600 PNG。自动检查检测到 25,620 个绿色模型像素；人工查看截图确认绿色测试方块可见。截图位于 `.local/environment/Previews/windows-20261008-144215-923.png`，本次通过记录在 `logs/graphics-verification.json`。这是工具链样板，不是正式收藏版美术验收。
- `.NET` 安装包通过官方 SHA-512 校验后解压；Blender ZIP 通过官方 SHA-256 校验后解压；Unity Editor 安装包通过官方 MD5 校验；Unity Hub 与 Editor 安装器的 Authenticode 签名有效。
- PowerShell 脚本语法检查通过，VS Code 扩展版本命令通过。未进行正式游戏交互或性能验收。

验证证据保存在项目 `.local/environment/logs`、`.local/environment/TestResults` 和 `.local/environment/art/v001`。这些是可重建的环境样板；正式美术资产另按美术规范验收。

日志完成标记为 `CATAN_ENVIRONMENT_UNITY_IMPORT_OK`、`CATAN_ENVIRONMENT_UNITY_BUILD_OK`、`CATAN_ENVIRONMENT_PLAYER_OK` 和 `CATAN_ENVIRONMENT_RENDER_OK`。初次使用窗口截图时得到黑图，现已改为直接渲染 URP 相机到纹理并检查模型像素；最终运行通过。Editor 退出时出现 Mono 调试线程清理警告，退出码为 0；最终 Windows 程序运行日志未报告异常或渲染错误。

批处理 Editor 启动时还记录了访问令牌不可用的更新提示，随后成功解析许可权益、更新许可并完成构建。本次已验证本机许可可用于编译构建；未验证 Hub 在线令牌续期。

2026 年 10 月 8 日用户完成登录后，Unity 成功读取许可。复跑完整环境检查：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Verify-Environment.ps1 -Unity
```

验证工程位于 `.local/environment/UnitySmoke`，程序及配套数据位于 `.local/environment/Builds/Windows`。`Start-Dev.ps1 -Tool Unity` 可打开该环境验证工程；它只包含合成方块与工具链检查，没有游戏规则或交互。已生成并核对 Unity 包锁。Mono 用于 M0 环境验证；后续需要 IL2CPP 时须另行配置 Visual Studio C++/Windows SDK。

本次先运行完整 `-Unity` 检查，随后针对截图脚本修复使用 `-PrepareUnity -Unity` 复跑 Unity 导入、构建和程序检查，复用已经通过的 .NET DLL 与 Blender FBX。`-PrepareUnity` 单独使用只准备工程，不执行 Unity；完整复验仍使用上述 `-Unity` 命令。

M0 收口时再次运行完整 `-Unity` 检查，最终于 2026-10-08 22:52（Asia/Shanghai）通过。复跑发现并修复了 `UnitySmoke.cs` 覆盖现有 Renderer 资产导致的临时错误；修复后独立 .NET 2/2、Blender、Unity 导入编译、Windows 构建运行与 URP 渲染全部通过。新截图已打开核对。完整命令、固定版本、证据归档、SHA-256 及引擎退出诊断见 [M0 工具链复验记录](verification/M0_TOOLCHAIN_2026-10-08.md)。上文较早的截图与验证经过作为历史记录保留，以该复验记录为收口证据。

M0 已完成，见 [阶段验收](M0_ACCEPTANCE.md)。按用户确认，开发采用官方 2025 规则和明确标注的估算尺寸，实体版本与实测资料后补；[v0.1 人工验收序列](acceptance/V0_1_ACCEPTANCE.md) 已固定并核对数据自洽性。尚未实施正式游戏功能，不能把环境通过结论用于代替 M1 的规则、Unity 交互和存档验收。

配置依据：[.NET Windows 手动安装](https://learn.microsoft.com/dotnet/core/install/windows#manual-install)、[Unity 下载存档](https://unity.com/releases/editor/archive)、[Unity 安装命令](https://docs.unity3d.com/6000.3/Documentation/Manual/InstallingUnity.html)、[Unity 包缓存配置](https://docs.unity3d.com/6000.3/Documentation/Manual/upm-config-cache.html)、[Blender 4.5 官方下载目录](https://download.blender.org/release/Blender4.5/)、[VS Code 命令行配置](https://code.visualstudio.com/docs/configure/command-line)。

渲染验证使用 [Unity RenderPipeline.SubmitRenderRequest](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Rendering.RenderPipeline.SubmitRenderRequest.html)，并核对了本机 URP 17.3.0 的渲染请求实现。
