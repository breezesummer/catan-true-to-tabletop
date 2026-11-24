# 卡坦岛之三国争霸

内测版 **2.1.18**。玩法包括基础版、航海家、城市与骑士、九图双扩展，以及三国争霸、海港大师与捕鱼。

## 开始试玩

在仓库根目录运行 Windows 启动脚本，它会查找当前版本的可玩构建。

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M6.ps1
```

首次构建先配置[开发环境](docs/ENVIRONMENT.md)。Windows 运行[构建脚本](tools/development/Build-Game.ps1)，默认生成 EXE；macOS 运行[开发入口](tools/development/Start-Dev.command)。本地可玩版本保存在 `game version/`。Windows 闪退后可双击游戏旁的 `查看崩溃报告.exe`。

单人开局选择规则、地图与 3 或 4 人，其余席位由本地 AI 控制。好友同网可搜索局域网房间，异地可从在线大厅创建或加入房间。[房间使用与重连](docs/ONLINE_STATE_SYNC.md)

## 按任务查阅

开发前先读[项目约定](AGENTS.md)，再按本次修改选择一行。

| 要做的事                     | 从这里开始                                                                                                                                                                                                             |
| ---------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 改基础规则、航海或城市与骑士 | [规则来源](assets/references/rules/README.md) · [基础版](docs/M2_RULES_CHECKLIST.md) · [航海家](docs/M3_RULES_CHECKLIST.md) · [城市与骑士](docs/M4_RULES_CHECKLIST.md) · [九图组合裁定](docs/COMBINED_ALL_SCENARIOS.md) |
| 改三国武将、选牌或技能结算   | [扩展玩法与选择流程](docs/扩展玩法.md) · [选择合同](docs/three-kingdoms/B4.5选择合同.md) · [武将原始牌文](docs/三国争霸游戏百科.md#三武将与技能)                                                                        |
| 改海港、捕鱼或地图兼容       | [开局配置与地图裁定](docs/扩展玩法.md#海港大师与捕鱼)                                                                                                                                                                   |
| 改百科内容或表现             | [发展卡牌文](docs/GAME_ENCYCLOPEDIA.md) · [武将与发展牌右栏](docs/百科武将与发展牌右栏规范.md) · [素材清单](docs/游戏百科全局素材清单.md)                                                                               |
| 改菜单                       | [菜单打样流程](docs/菜单设计与实现模板.md) ·                                                                                                                                                                           |
| 改本地 AI 或托管决策         | [策略与可见信息](docs/AI_PATCH_2_0.md)                                                                                                                                                                                  |
| 加入房间、改同步或部署       | [房间使用与重连](docs/ONLINE_STATE_SYNC.md) · [联机诊断](docs/ONLINE_DIAGNOSTICS.md) · [服务端部署](docs/M7_SERVER.md)                                                                                                  |
| 构建、发布或整理仓库         | [跨平台构建](docs/CROSS_PLATFORM_BUILD.md) · [版本与发布约定](AGENTS.md) · [资料存储](docs/REPOSITORY_STORAGE.md)                                                                                                       |
| 理解工程边界、核对待验范围   | [工程约定](docs/PROJECT_BRIEF.md) · [待验范围](docs/ROADMAP.md)                                                                                                                                                         |

## 历史查询

[历史文档与验收索引](docs/archive/README.md)按主题查找旧方案、发布记录和实测证据。按日期追溯改动见[农场主手记](<The%20Farmer%27s%20Log.md>)。
