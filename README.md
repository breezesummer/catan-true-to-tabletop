# 卡坦岛之三国争霸

一款以实体桌游体验为目标的 3D 卡坦岛游戏，包含资源生产、交易、道路与聚落建设、海上探索，以及城市发展与骑士防御。项目强调在同一张桌面上观察地图、拿取棋子、摆放建筑和操作卡牌，逐步呈现立体地形收藏版的质感。

项目由用户主导玩法与视觉选择、提供实体组件参考并试玩验收，AI 负责代码、美术生产与验证。当前项目版本标识为**内测版1.7.1**；各可玩构建的实际版本与验证范围见[开发日志](The%20Farmer%27s%20Log.md)。

## 玩法与内容

| 模式       | 内容范围                                                                                    |
| ---------- | ------------------------------------------------------------------------------------------- |
| 基础版     | 3–4 人，资源生产、建造、港口与玩家交易、发展卡、强盗、最长道路及最大骑士军队               |
| 航海家     | 船、海盗、金矿、探索与九种剧本                                                              |
| 城市与骑士 | 商品、城市设施、骑士、蛮族、发展卡、城墙及大都会                                            |
| 双扩展组合 | 已支持“驶向新海岸”“穿越沙漠”的 3–4 人配置，具体范围见[组合兼容表](docs/M5_SCENARIOS.md) |

单人模式可选择一个人类席位，其余由本地 AI 操作，支持暂停和保存恢复。好友联机包含局域网与在线大厅，提供权威房间、加入、同步、断线恢复及服务器保存；当前在线地址为 `175.178.212.142:47771`，新客户端首次进入在线游戏时自动连接。

## 平台与体验

- **Windows**：鼠标键盘操作，Unity 桌面客户端。
- **Android**：同一 Unity 工程的横屏触控版本，目标为 Android 8.0 及以上、64 位 ARM64 手机；与 PC 共用规则、当前 AI、资产和完整桌面 UI。
- **桌面表现**：实体照片来源的板块与卡面、三维玩家组件、手牌操作、交易界面、骰子统计、历史记录、菜单与音效。

基础版、两个扩展、已支持组合及本地 AI 已有工程验收记录。用户完整对局、最终美术、安卓真机、真实多设备及长期性能继续逐项验证，具体状态以[开发路线](docs/ROADMAP.md)及各验收文档为准。

## 开始试玩

在仓库根目录执行统一 Windows 启动脚本：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M6.ps1
```

脚本选择当前默认可玩构建。操作方式见[M6 试玩说明](docs/M6_PLAYTEST.md)，联机连接与房间使用见[M7 使用说明](docs/M7_IMPLEMENTATION.md)。PC 与 Android 下载入口及验证范围见[内测版1.6发布记录](docs/RELEASE_BETA_1_6.md)。

本地程序、APK 和分享包保存在 `game version`，按二级版本归档，不随源码仓库分发；历史构建路径及适用版本统一查阅[开发日志](The%20Farmer%27s%20Log.md)。本地缺少程序时，先按[开发环境](docs/ENVIRONMENT.md)配置固定工具链，再运行构建脚本：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Build-M7.ps1
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Build-Android.ps1
```

macOS 开发入口为 `tools/development/Start-Dev.command`，打开 Unity 使用 `Start-Unity.command`；Windows 与 macOS 的构建、签名配置和验证入口见[分系统构建说明](docs/CROSS_PLATFORM_BUILD.md)。

## 工程结构

| 目录                                                                       | 用途                                   |
| -------------------------------------------------------------------------- | -------------------------------------- |
| `Unity/Catan`                                                            | Unity 客户端、场景、UI 与运行资产      |
| `src/Catan.Core`                                                         | 独立于 Unity 的纯 C# 规则核心          |
| `src/Catan.AI`                                                           | 本地 AI 策略                           |
| `src/Catan.Network.Contracts`、`src/Catan.Rooms`、`src/Catan.Server` | 联机协议、权威房间与服务端             |
| `tests`                                                                  | 规则、AI、界面辅助逻辑、音效与联机验证 |
| `assets/references`                                                      | 原始实体参考、规则来源与索引           |
| `art`                                                                    | 版本化美术源文件、配方与导出结果       |
| `tools/development`                                                      | 环境、构建、启动和验证脚本             |
| `docs`                                                                   | 项目约定、设计说明、阶段验收与验证记录 |

客户端使用 Unity `6000.3.25f1` 与 URP `17.3.0`，规则核心以 `.NET Standard 2.1` 为兼容面，独立开发工具链固定 `.NET SDK 10.0.401`。规则采用稳定的 Tile／Vertex／Edge ID；本地玩家、AI 和远程玩家共用指令验证入口，AI 只读取自身有权查看的 `PlayerView`。

## 项目文档

- [项目约定](docs/PROJECT_BRIEF.md)：已确认目标、工作默认值与协作方式。
- [开发路线](docs/ROADMAP.md)：阶段目标、退出条件与当前状态。
- [开发日志](The%20Farmer%27s%20Log.md)：时间轴、每轮改动、发布入口与实际验证。
- [开发环境](docs/ENVIRONMENT.md)：工具链、环境配置与开发入口。
- [仓库存储与本地资料](docs/REPOSITORY_STORAGE.md)：代码与运行资源的入库范围、美术资料和验证原件的本地保存方式。
- [分系统构建](docs/CROSS_PLATFORM_BUILD.md)：Windows／macOS 启动、双端打包和 Android 签名迁移。
- [游戏百科](docs/GAME_ENCYCLOPEDIA.md)：发展卡实体名称、牌面描述与照片来源。
- [三国争霸游戏百科](docs/三国争霸游戏百科.md)：三国扩展玩法、54张武将原文、确认记录与逐将疑问。
- [三国争霸扩展实现方案](docs/三国争霸扩展实现方案.md)：新增机制、现有工程接入、分批实现与验收计划。
- [B5规则合同](docs/three-kingdoms/B5规则定稿.md)、[实现说明](docs/three-kingdoms/B5实现说明.md)与[验收记录](docs/verification/three-kingdoms-b5/验收记录.md)：49将棋盘状态包的规则、实现和证据入口。
- [本地规则索引](assets/references/rules/README.md)：规则原件与查询入口。
- [美术生产规范](docs/ART_PIPELINE.md)：参考采集、资产制作、导入和复现。
- [三国风格棋子制作流程](docs/三国风格棋子制作流程.md)：造型、组件与状态、参考多视图、建模材质、预算及游戏内验收。
- [豪华典藏版ultra主题实施方案](docs/ULTRA_THEME_IMPLEMENTATION_PLAN.md)：真实桌游与 ultra 双主题、三维资产、环境动态及分步验收。
- [AI 2.0 补丁](docs/AI_PATCH_2_0.md)：当前 AI 策略、交易限制和专用信息权限。
- [本地 AI 验收](docs/M6_ACCEPTANCE.md)：AI、规则与独立完整对局证据。
- [联机验收](docs/M7_ACCEPTANCE.md)：本机与公网已验证范围、后续待验事项。
- [云端连接与部署](docs/M7_SERVER.md)：在线地址、服务管理与公网验收入口。
