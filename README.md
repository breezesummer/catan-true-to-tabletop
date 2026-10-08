# 卡坦岛 PC 项目

单人主导、AI 执行代码与美术生产的 3D 收藏版桌游项目。目标包含基础版、航海家、城市与骑士及双扩展组合，尽可能还原实体桌游的观察、拿取、摆放和交易体验。

用户提供实体组件参考并进行玩法与视觉验收。先逐步完成本地规则，随后交付单人 AI 对战和好友联机。每个规则阶段都应能独立本地试玩。

M0 项目启动阶段已于 2026 年 10 月 8 日完成。M1 本地交互切片已实现，正式 Unity 工程位于 `Unity/Catan`，Windows 试玩程序位于 `.local/m1/Builds/Windows/CatanM1.exe`。纯规则测试、Unity 兼容编译、真实 Windows 进程中的保存恢复和山地网格重建已有证据；阶段正式退出仍需交互与用户试玩验收，详见 [M1 验收记录](docs/M1_ACCEPTANCE.md)。M0 环境验证工程保留在 `.local/environment`。

启动 M1：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M1.ps1
```

操作和固定流程见 [M1 试玩说明](docs/M1_PLAYTEST.md)。这不是完整基础版；强盗、发展卡、港口、城市、胜利判定、AI 和联机尚未实现。

入口文档：

- [项目约定](docs/PROJECT_BRIEF.md)：已确认目标、工作默认值、首个版本的验收标准。
- [开发路线](docs/ROADMAP.md)：分阶段交付、退出条件和当前任务。
- [美术生产规范](docs/ART_PIPELINE.md)：实物采集、AI 生产、导入和复现要求。
- [开发环境](docs/ENVIRONMENT.md)：本机检查结果与工具链配置顺序。
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

用户已确认先用官方 2025 规则和明确标注的估算尺寸完成 M0，实体参考后补；实体版本、照片和实测尺寸仍未知。M1 已实现纯规则库、稳定格边点数据、本地交互与权威存档，并制作首件山地样板。用户尚未确认试玩和美术；按项目约定，本次不提前修改 `docs/ROADMAP.md` 的阶段进度，实际实施状态以 M1 验收记录为准。

阶段顺序：M0 启动 → M1 本地交互切片 → M2 基础版完整 → M3 航海家 → M4 城市与骑士 → M5 双扩展组合 → M6 AI 对手 → M7 好友联机 → M8 完整美术与发布验证。
