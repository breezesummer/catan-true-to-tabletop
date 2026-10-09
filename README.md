# 卡坦岛 PC 项目

单人主导、AI 执行代码与美术生产的 3D 收藏版桌游项目。目标包含基础版、航海家、城市与骑士及双扩展组合，尽可能还原实体桌游的观察、拿取、摆放和交易体验。

用户提供实体组件参考并进行玩法与视觉验收。先逐步完成本地规则，随后交付单人 AI 对战和好友联机。每个规则阶段都应能独立本地试玩。

M6 本地 AI 对手已于 2026 年 10 月 9 日完成工程验收：可选择基础版、航海家九剧本、城市与骑士或已支持的双扩展组合，3–4 人中任选一个人类席位，其余席位自动行动。AI 仅读取自身 PlayerView，经共同规则入口提交；支持交易、多步待决、暂停及保存恢复。**496/496 项测试通过，52 个正常完整局全部结束**，固定 Unity 构建、两个独立 Windows 进程和真实窗口操作均已验证。详见 [M6 验收记录](docs/M6_ACCEPTANCE.md)与[策略质量说明](docs/M6_STRATEGY.md)。

启动当前 M6：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M6.ps1
```

当前场景为 `Unity/Catan/Assets/M6.unity`，Windows 程序为 `.local/m6/Builds/Windows/CatanM6.exe`，操作见 [M6 试玩说明](docs/M6_PLAYTEST.md)。策略是本地启发式基线；用户本人整局试玩、对战强度、最终 UI 与美术仍待确认。下一阶段为 M7 好友联机。

M5 双扩展组合已于 2026 年 10 月 9 日完成工程验收：支持“驶向新海岸”“穿越沙漠”各 3–4 人固定地图，均为 16 分获胜。船、海盗、金矿与骑士、蛮族、商品和进步卡共同生效。**383/383 项规则与回归测试通过**，四个正常完整局、固定 Unity 构建、独立 Windows 保存/恢复及真实鼠标开局操作均已验证。详见 [M5 验收记录](docs/M5_ACCEPTANCE.md)与[组合兼容表](docs/M5_SCENARIOS.md)。

启动保留的 M5：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M5.ps1
```

保留场景为 `Unity/Catan/Assets/M5.unity`，Windows 程序为 `.local/m5/Builds/Windows/CatanM5.exe`，操作见 [M5 试玩说明](docs/M5_PLAYTEST.md)。其余七个航海剧本暂未开放组合；用户本人整局试玩、最终 UI 与收藏版美术尚未确认。AI 模式见当前 M6。

M4 城市与骑士已于 2026 年 10 月 9 日完成工程验收：支持本地 3–4 人，包含商品、城市改良、骑士、蛮族、城墙、大都会，以及官方 2025 版全部 **25 种、54 张进步卡**。**332/332 项规则与回归测试通过**，三人/四人正常完整局、待决保存恢复、固定 Unity 构建和两个独立 Windows 进程均已实际运行。详见 [M4 验收记录](docs/M4_ACCEPTANCE.md)。

启动保留的 M4：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M4.ps1
```

保留场景为 `Unity/Catan/Assets/M4.unity`，Windows 程序为 `.local/m4/Builds/Windows/CatanM4.exe`，操作见 [M4 试玩说明](docs/M4_PLAYTEST.md)。新增组件是可复现的实际网格占位。用户本人整局试玩、完整 UI 像素和收藏版成品美术尚未确认；双扩展组合见当前 M5。

M3 航海家已于 2026 年 10 月 9 日完成工程验收：八个官方固定剧本与「新世界」均支持本地 3–4 人，包含船与移动、海盗、金矿、最长航线、迷雾探索、部落奖励、布匹、海盗要塞及奇迹。**240/240 项规则与回归测试通过**，9 个剧本 × 3/4 人共 18 个完整对局、238 项地图来源检查、固定版本 Unity 构建与独立 Windows 进程恢复均已实际运行。详见 [M3 验收记录](docs/M3_ACCEPTANCE.md)。

启动保留的 M3：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M3.ps1
```

当前场景为 `Unity/Catan/Assets/M3.unity`，Windows 程序为 `.local/m3/Builds/Windows/CatanM3.exe`，操作见 [M3 试玩说明](docs/M3_PLAYTEST.md)。新增组件是可辨识实际网格占位；用户整局试玩、完整 UI 像素检查与成品美术确认尚未执行。M3 没有自动启用旧版规则、可选随机地形变体或双扩展组合。

M2 基础版已于 2026 年 10 月 9 日完成工程验收，支持本地 3–4 人从正常开局玩到胜利。包含强盗与弃牌、发展卡、港口和玩家交易、城市、有限供应、最长道路、最大军队、隐私换座与权威存档。**138/138 项规则测试通过**，3 人和 4 人完整对局、固定版本 Unity 构建及独立 Windows 进程保存恢复均已实测。详见 [M2 验收记录](docs/M2_ACCEPTANCE.md)。

启动 M2：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M2.ps1
```

正式 Unity 工程位于 `Unity/Catan`，M2 场景 `Assets/M2.unity` 和 Windows 程序 `.local/m2/Builds/Windows/CatanM2.exe` 独立保留。操作见 [M2 试玩说明](docs/M2_PLAYTEST.md)。AI 已在当前 M6 交付，好友联机与最终美术属于后续阶段。用户亲自完成整局试玩、美术确认和长时间性能验收尚未执行。

M0 已于 2026-10-08 完成，环境验证工程保留在 `.local/environment`。M1 固定切片仍可通过 `tools/development/Play-M1.ps1` 启动，原程序与独立存档入口保留；M1 的用户试玩和山地样板确认仍按 [原验收记录](docs/M1_ACCEPTANCE.md) 保留待验，不将 M2 工程验收视为用户已确认。

入口文档：

- [项目约定](docs/PROJECT_BRIEF.md)：已确认目标、工作默认值、首个版本的验收标准。
- [开发路线](docs/ROADMAP.md)：分阶段交付、退出条件和当前任务。
- [美术生产规范](docs/ART_PIPELINE.md)：实物采集、AI 生产、导入和复现要求。
- [开发环境](docs/ENVIRONMENT.md)：本机检查结果与工具链配置顺序。
- [M6 试玩说明](docs/M6_PLAYTEST.md)：单人规则选择、人类席位、AI 暂停与保存恢复。
- [M6 验收记录](docs/M6_ACCEPTANCE.md)：496 项测试、52 个正常完整局、Unity/Windows 和实际窗口证据。
- [M6 策略说明](docs/M6_STRATEGY.md)：启发式行为、交易取舍及已知质量缺口。
- [M6 架构](docs/M6_ARCHITECTURE.md)：独立 PlayerView 策略、共用执行入口、待决调度和固定人类视图。
- [M5 试玩说明](docs/M5_PLAYTEST.md)：组合剧本、跨海骑士、船、金矿与多席位待决。
- [M5 验收记录](docs/M5_ACCEPTANCE.md)：383 项测试、四个完整局、Unity/Windows 和真实窗口证据。
- [M5 组合规则](docs/M5_COMBINATION_RULES.md)：官方 2025 组合来源、快照及跨扩展覆盖顺序。
- [M5 剧本兼容表](docs/M5_SCENARIOS.md)：支持的四种配置及七个暂未开放的组合剧本。
- [M5 规则清单](docs/M5_RULES_CHECKLIST.md)：独立组合案例、守恒、非法命令和事务隔离。
- [M5 架构](docs/M5_ARCHITECTURE.md)：路线判断、权限、待决与版本化权威保存。
- [M4 试玩说明](docs/M4_PLAYTEST.md)：城市与骑士开局、商品/骑士/进步卡、多席位待决与恢复。
- [M4 验收记录](docs/M4_ACCEPTANCE.md)：332 项测试、3/4 人完整局、Unity/Windows 与四视图证据。
- [M4 规则清单](docs/M4_RULES_CHECKLIST.md)：官方条款、25 种卡牌与逐项独立案例。
- [M4 架构](docs/M4_ARCHITECTURE.md)：事件顺序、商品/卡牌权限、队列和版本化保存。
- [M3 试玩说明](docs/M3_PLAYTEST.md)：剧本选择、船、海盗、金矿及特殊操作。
- [M3 验收记录](docs/M3_ACCEPTANCE.md)：240 项测试、18 个完整局、Unity / Windows 证据与限制。
- [M3 剧本与地图](docs/M3_SCENARIOS.md)：官方页码、固定图与新世界、238 项独立检查。
- [M3 规则清单](docs/M3_RULES_CHECKLIST.md)：条款与独立测试方法映射。
- [M3 架构](docs/M3_ARCHITECTURE.md)：隐藏地图、特殊待决、版本化权威存档。
- [M2 试玩说明](docs/M2_PLAYTEST.md)：启动、完整回合、待决操作与保存恢复。
- [M2 验收记录](docs/M2_ACCEPTANCE.md)：完整对局、138 项测试、Unity / Windows / 实际窗口证据与限制。
- [M2 规则清单](docs/M2_RULES_CHECKLIST.md)：官方页码、独立预期与逐条测试映射。
- [M2 架构](docs/M2_ARCHITECTURE.md)：完整规则接口、权限与版本化存档。
- [M0 验收记录](docs/M0_ACCEPTANCE.md)：退出条件、实际验证与 M1 移交。
- [M1 验收记录](docs/M1_ACCEPTANCE.md)：实际测试证据、交付内容和待验项。
- [M1 架构](docs/M1_ARCHITECTURE.md)：规则核心、权限边界和存档版本。
- [M1 山地样板](docs/M1_ART.md)：源网格、导出清单、四视图和干净目录重建。
- [规则来源基线](docs/RULES_BASELINE.md)：三份官方 2025 规则的版本、来源和文件校验值。
- [参考与尺度基线](docs/REFERENCE_BASELINE.md)：估算尺寸与实体参考后补清单。
- [v0.1 验收流程](docs/acceptance/V0_1_ACCEPTANCE.md)：固定开局、行动、状态账本与非法操作检查。
- [参考素材目录](assets/references/README.md)：素材存放与登记方法。

`D:\Development` 下每个开发工具各用一个目录，Unity Hub 和 Editor 同属 `Unity` 目录。项目缓存、日志、临时文件、编辑器配置及环境安装下载放在本仓库 `.local/development`；验证工程和构建产物放在 `.local/environment`。启动项目专用开发终端或 VS Code：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Shell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Code
```

用户已确认先用官方 2025 规则和明确标注的估算尺寸完成 M0，实体参考后补；实体版本、照片和实测尺寸仍未知。M2 沿用山地样板和估算尺寸，城市与强盗使用可辨识的基础网格占位，未替用户确认收藏版成品外观。只有完成退出条件的阶段才更新路线图；M1 原有待确认事项单独保留。

阶段顺序：M0 启动 → M1 本地交互切片 → M2 基础版完整 → M3 航海家 → M4 城市与骑士 → M5 双扩展组合 → M6 AI 对手 → M7 好友联机 → M8 完整美术与发布验证。
