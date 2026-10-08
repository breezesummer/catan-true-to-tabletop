# M1 实施与验收记录

记录日期：2026-10-08（Asia/Shanghai）。**M1 功能已实现，工程自动验证通过；阶段尚未正式完成。** 真实鼠标窗口验收被 Windows 安全中心网络权限弹窗阻挡，已请用户手动取消；用户亲自试玩和山地样板确认尚未取得。按项目约定，本次不提前更新 `ROADMAP.md` 的阶段进度。

## 已交付

| 任务 | 结果与代码入口 |
| --- | --- |
| M1-01 规则与拓扑 | `src/Catan.Core`，纯 C# 9 / .NET Standard 2.1；读取锁定场景的 19/54/72 稳定 ID，不从 Unity 对象推导 |
| M1-02 命令与循环 | 开局蛇形顺序及道路待决、第二村资源、受控产出、银行 4:1、付费道路、回合、有限供给、拒绝原子性与成功命令去重 |
| M1-03 棋盘与吸附 | `Unity/Catan` 正式 URP 工程；拿起、绿色合法目标、吸附预览、取消、落下、拒绝退回、斜视/俯视/缩放及地形显隐 |
| M1-04 山地样板 | 原创版本化 `.blend`/FBX、24 闭合网格、1,192 三角形、UV、11 种 PBR 材料、四视图、独立 FBX 回读与干净目录重建 |
| M1-05 存档恢复 | 本地权威存档、三个保存时机、重试回执、独立 Windows 进程恢复与随机继续；界面含保存/载入及换座遮罩 |

试玩入口：[操作说明](M1_PLAYTEST.md)。程序为 `.local/m1/Builds/Windows/CatanM1.exe`，配套目录必须完整保留。源代码和生成资产均在仓库，缓存和构建产物放在 `.local`。M0 工程保留未覆盖。规则基线、场景 JSON、原参考以及 M0 估算参数均未改动。

## 实际运行的验证

| 验证 | 实际结果 | 证据 |
| --- | --- | --- |
| 纯规则库编译 | `netstandard2.1`、C# 9，0 告警/错误 | `Build-M1.ps1` 输出、核心 csproj |
| 独立 .NET 规则测试 | **55/55 通过**；C01–C21 独立预期、N01–N10、重试/冲突、守恒、三个存档点、未知/篡改版本拒绝、权限与引用隔离 | [TRX](verification/m1-2026-10-08/m1-rules.trx) |
| .NET 跨进程恢复 | `Catan.SaveProbe` 两个独立 OS 进程，保存退出后继续 C21，对比完整权威状态 | 同一 TRX 的进程测试 |
| Unity 兼容 | Unity `6000.3.25f1` 实际加载规则 DLL 并调用 `Create`/玩家投影；脚本编译及 Windows x64 Mono 普通构建通过 | `.local/m1/logs/unity-build.log` 中 `CATAN_M1_UNITY_IMPORT_OK` / `CATAN_M1_UNITY_BUILD_OK` |
| Unity 模型导入 | 最低 Y=0、高度≤36mm、最大水平尺寸80mm；从清单重建 URP/Lit 颜色/金属度/粗糙度，使用线性色彩空间 | 同一构建日志；导入 FBX 与来源 SHA256 相同 |
| Windows 规则与 UI 入口 | `app.Pickup`/`app.Drop` 提交合法村路；非法 V46/E01 拒绝前后存档相同；换座立即清私有视图；取消不改变状态 | [player-save.log](verification/m1-2026-10-08/player-save.log) |
| Windows 保存及退出恢复 | 待决/首次产出存档规范内容相同，C17 重试不产出；C20 后退出进程，再启动加载，C21 正确继续且完整状态相同 | [player-load.log](verification/m1-2026-10-08/player-load.log) |
| 屏幕目标解析 | 对 54 点+72 边的中心坐标，在斜视/俯视和缩放上下限调用同一吸附解析器，4×126 个目标全部匹配稳定 ID | `M1PlayerVerification.cs`；同一 player-save 成功标记 |
| URP 实际 GPU 渲染 | AMD Radeon RX 9070 XT、D3D11；默认和俯视相机分别生成1440×1080图；非空彩色棋盘像素检查通过 | 下方两张图、player-load 的 `CATAN_M1_RENDER_OK` |
| 山地重建 | 两个干净输出目录重建、独立 FBX 回读；几何/UV/材料/参数/导出/预览配置一致 | [美术验收与四视图](M1_ART.md)、[重建 JSON](../art/exports/mountain-tile-v001/rebuild-verification.json) |

根目录复跑命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validation\Test-M1Rules.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Build-M1.ps1 -VerifyPlayer
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\art\Build-Mountain.ps1 -OutputRoot .local\art-rebuild-next -CompareRoot art
```

美术重建目标必须是新目录。最后一轮程序日志无未处理异常，Unity Editor 退出仍记录 M0 已见的 Mono 调试线程清理诊断，随后明确以退出码0完成；不能把这些诊断描述为不存在。普通 Windows 构建的保存/恢复进程均正常退出。构建曾使用 Development 选项，发现其触发网络权限提示后改为 `BuildOptions.None`；没有修改系统防火墙或其他权限设置。

## 已查看的画面与证据限制

代理逐张检查了 Blender 顶/侧/默认方向/拥挤节点四图，也查看了下列两张真实 Unity GPU 相机输出：19 地块、8 定居点、9 道路可见，山地网格位于三处矿石地块，棋子与地块相接，俯视下棋盘完整。

![M1 默认相机](verification/m1-2026-10-08/m1-default.png)

![M1 俯视相机](verification/m1-2026-10-08/m1-top.png)

这些输出使用 URP `SubmitRenderRequest`，只包含相机画面，不含 IMGUI 面板、数字标记或鼠标。不能将其称为完整界面截图。曾尝试从隐藏窗口截全屏，得到黑图；该图已排除出验收证据，保留在 `.local/m1/previews/m1-ui-rejected-hidden-window.png` 作为诊断。随后验证脚本只检查真正渲染成功的相机输出。

实际启动窗口已通过 computer-use 查看，观察到游戏界面和换座遮罩，但前方有系统网络权限弹窗。代理没有操作安全权限提示，因此尚未通过真实鼠标完成放子/取消/换座/保存/加载，也尚未确认实际窗口中的字体大小、相邻目标可读性和山体附近点选体验。自动入口测试及中心坐标解析检查不能填补这些待验项。

## 尚未满足的退出条件

- 用户按 [固定试玩流程](M1_PLAYTEST.md) 完成开局、交易、建造、结束、三种存档及重启恢复。
- 实际鼠标完成拿起、吸附、取消、合法落下与非法落点退回；斜视/俯视、缩放、相邻点、山体遮挡均需窗口检查。
- 用户确认首件样板的尺寸工作值、枢轴、材质与操作空间适合继续生产。实物照片和实测尺寸仍为后补项，不能声称实物复刻已通过。
- 本次仅设置60 FPS运行目标，没有进行1080p稳定60 FPS性能采样或长期运行验收。

没有实现强盗/7、资源短缺分配、城市/港口、发展卡、玩家交易、奖牌、胜利、扩展、AI或联机；界面明确显示这些切片限制。网络提示并不表示项目已经有联机功能。

## 证据身份

[SHA256 清单](verification/m1-2026-10-08/sha256.json) 标识当前规则 DLL、Unity 脚本、包锁、FBX、Windows 程序与程序集、测试报告及渲染图。权威测试存档留在 `.local/m1/verification`，未复制进公开验收日志；完整手牌、受控随机队列、去重记录只在本地权威保存与测试中使用。
