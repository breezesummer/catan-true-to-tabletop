# M0 工具链复验记录

验证日期：2026 年 10 月 8 日，Asia/Shanghai（UTC+8）。最终完整运行约在 22:52 完成，命令退出码为 `0`。本记录证明 M0 的工具链退出条件；不代表 M1 游戏功能、实物还原或用户试玩已经通过。

## 实际运行

工作目录为 `D:\Project\the-settlers-of-catan`，执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Verify-Environment.ps1 -Unity
```

本轮执行了两次完整检查，没有使用 `-PrepareUnity` 跳过 .NET 或 Blender。22:51 的首次复验通过构建与渲染，但暴露重复运行时的 Renderer 资产替换问题。修复后在 22:52 再执行同一完整命令，以下结论和散列以第二次结果为准。

另外实际检查了 `dotnet --info`、Unity/Hub/VS Code 可执行文件版本、`git --version`、`git lfs version`，用 PowerShell Parser 检查 `tools/development/*.ps1`，并逐字节核对实际 Unity 包锁与仓库包锁一致。

## 固定版本与结果

| 项目 | 本次核对值 | 验证结果 |
| --- | --- | --- |
| 操作系统 | Windows `10.0.26200`，`win-x64` | 本机运行环境 |
| .NET SDK / Runtime | `10.0.401` / `10.0.12`，位于 `D:\Development\dotnet` | `global.json` 精确固定 SDK；两项 xUnit 测试通过，失败 0、跳过 0 |
| 探针库 | `.NET Standard 2.1`，C# `9.0` | 独立编译；在 .NET 10、Unity Editor 与 Windows Player 中实际执行中文字符串检查 |
| 测试依赖 | Microsoft.NET.Test.Sdk `17.14.1`、xUnit `2.9.3`、VS adapter `3.1.5` | 完整运行实际发现并执行 2 项测试 |
| Unity Editor | `6000.3.25f1`，revision `e1dba0a9aba4` | C# 编译、FBX 导入、Windows x64 Mono Development Build 通过 |
| Unity Hub | `3.22.2` | 可执行文件版本核对；本次构建直接调用 Editor |
| URP | `17.3.0` | 实际 Unity 包锁与 `tools/development/unity-packages-lock.json` 一致；渲染请求通过 |
| Blender | `4.5.14 LTS`，build hash `62c1db4208e8` | 后台 Python 生成 `.blend`、FBX 和 manifest 成功 |
| VS Code | `1.133.0` | 可执行文件版本核对；本次未操作编辑器 UI |
| Git / Git LFS | `2.49.0.windows.1` / `3.6.1` | 版本命令通过 |
| GPU / 驱动 | AMD Radeon RX 9070 XT，`32.0.22042.14002`，VRAM `16304 MB` | Direct3D 11.0，feature level 11.1；实际执行 URP 渲染 |

.NET 测试分别验证程序集目标框架和中文字符串往返；不是游戏规则测试。Unity 校验导入模型世界尺寸为 `0.08 × 0.01 × 0.08 m`，底面在 `Y=0`，误差限为 `0.0001 m`。此合成立方体的人为尺寸是 `80 × 80 × 10 mm`，其 manifest 明确标记 `physical_dimensions_confirmed: false`。

导出约定已实际执行：米制、底面中心枢轴、FBX `global_scale=1.0`、`axis_forward=-Z`、`axis_up=Y`、`apply_scale_options=FBX_SCALE_UNITS`。FBX 是 Blender 生成的实际网格，PNG 只是该网格在 Unity 中的渲染证据。

Windows 程序先无图形运行并正常退出，再由 `Start-Process -WindowStyle Hidden` 启动 Direct3D 11 渲染检查。实际输出 `800 × 600` PNG，检测到 `25,620` 个绿色模型像素（门槛为 100）。已打开最终 PNG 进行视觉检查：灰色背景上的绿色薄方块清楚可见，非黑图。

![M0 合成测试网格的 Unity URP 渲染](m0-urp-2026-10-08.png)

## 证据与复核

当前工程和可运行程序分别位于 `.local/environment/UnitySmoke` 与 `.local/environment/Builds/Windows`。最终日志、TRX、截图、输入脚本副本、逐文件构建散列表已归档到：

```text
.local/environment/evidence/m0-20261008-225238-final/
```

该归档由 Git 忽略，保留在本机。下表的文件名相对于此目录；原始日志同时保留在 `.local/environment/logs`，后续复跑可能覆盖该原始位置。PNG 文件名使用 UTC，`145255` 对应本地 `22:52:55`。

| 检查 | 归档证据 | 观察结果 |
| --- | --- | --- |
| .NET | `logs/dotnet-test.log`、`BreezeSummer_BREEZEPRO_2026-10-08_22_52_38_net10.0.trx` | 2/2 Passed |
| Blender | `logs/blender-export.log`、`manifest.json` | `CATAN_ENVIRONMENT_BLENDER_OK` |
| Unity 导入与 Windows 构建 | `logs/unity-build.log` | `CATAN_ENVIRONMENT_UNITY_IMPORT_OK`、`CATAN_ENVIRONMENT_UNITY_BUILD_OK`；Editor 退出码 0 |
| 无图形 Player 执行 | `logs/player.log` | `CATAN_ENVIRONMENT_PLAYER_OK` |
| URP GPU 渲染 | `logs/player-graphics.log`、`windows-20261008-145255-416.png` | `CATAN_ENVIRONMENT_RENDER_OK`；25,620 绿色像素 |
| 版本输入 | `inputs/` | 本次脚本、工具版本表、SDK 版本与 Unity 包锁副本 |
| 完整 Windows 程序 | `windows-build-sha256.csv` | 对构建目录的 268 个文件（157,904,732 bytes）记录相对路径、长度与 SHA-256；不能只复制 EXE 运行 |

完整关键文件散列随仓库保存为 [M0_TOOLCHAIN_2026-10-08.sha256.json](M0_TOOLCHAIN_2026-10-08.sha256.json)。以下 SHA-256 用于快速核对：

| 文件 | SHA-256 |
| --- | --- |
| 最终渲染 PNG（本目录副本与原图相同） | `EC9F741B5E04FE6E71E7820C06D71A0F75186700BD3B2C286A4958F6AB82623D` |
| 最终 TRX | `0E19A9116CE885008A1DBD21C099B854D4C2B89EEDC6DC59CD2683C9D4DC7460` |
| Unity 构建日志 | `53F126C971B418878D1DAB5711FA360E2122517ACCE5C7619F4C18356761D12C` |
| Blender FBX | `F004770BFAED52F0DBD1064C9C15A8E62AE4DEC261763965E324D68C660D7948` |
| Unity 包锁 | `13461C21DBF05EF21B1A0BAF4BA5079D1CCCA2A857176712BE4136FD09967FE1` |
| Windows 全目录散列表 | `558C2E1A70BCBC216D62079086BC2E9BBEE27331B59BB43117E264084444341B` |

散列标识本次产物，不承诺重新导出的 FBX、`.blend`、日志或 Windows 构建逐字节相同；导出时间及构建元数据可能变化。可复现要求是脚本能够重建并通过相同的几何、程序集执行和渲染检查。

## 发现、修复与未验证范围

首次复验发现 `UnitySmoke.cs` 在已有 URP 管线仍引用 Renderer 时覆盖该 Renderer 资产，临时产生 4 条 `Default Renderer is missing` 错误。现改为复用已有 Renderer，缺失时才创建；同一环境工程完整复跑后，该错误消失，构建与渲染均通过。原始日志保存在 `.local/environment/evidence/m0-20261008-225113-initial/logs`。

最终 Editor 日志仍有 Mono 退出清理时的 `abort_threads` 与 `debugger-agent: Unable to listen` 诊断，随后明确以 `return code 0` 正常退出。Player 退出时记录 Unity `MemoryLeaks` 遥测（图形运行的 Immediate 统计为 57,804 bytes）；本次未调查引擎退出内存统计，也未把它当作游戏长期运行或无泄漏证明。

许可启动日志先记录 Licensing Client 签名校验 `Code 10` / `failed validation; ignoring` 及 `Access token is unavailable; failed to update`，随后成功握手、解析权益并更新许可，实际构建成功。本次没有更改许可设置，也没有完成签名提示的根因调查；Hub 在线登录及令牌续期不属于本次验证。

未验证 IL2CPP、其他电脑的清洁安装、1080p/60 FPS、正式棋盘交互、完整游戏规则、隐藏信息安全、存档恢复或收藏版美术。未重装工具，未修改全局环境变量。M1 起新增规则代码仍须独立测试并实际通过 Unity 编译，不能继承本探针的通过结论。
