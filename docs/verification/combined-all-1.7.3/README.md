# 双扩展九图工程验收（1.7.3）

日期：2026-10-21。范围为九张海洋地图各三人、四人配置；组合变体见 [规则说明](../../COMBINED_ALL_SCENARIOS.md)。

## 实际结果

- 新增专项测试25项通过：18种开局、逐步保存恢复、非法席位拒绝、隐藏地图隔离、部落奖励、战舰升级、奇观终局、布匹终局、迷雾探索、海盗奖励先于七点弃牌、中立村庄不可被叛徒占用。另一个分数展示回归测试通过，合计26项。
- 最后将持久化测试、组合分数测试和新增专项一起重跑，105/105通过。
- 产品AI完整对局矩阵为九图×两人数×两种子（97、2027），36种配置均取得通过记录。初跑发现海盗奖励顺序和叛徒占用中立村庄问题，修复后重跑受影响地图；记录保留早期失败，不将不同运行伪装成一次全绿。
- 联机测试131/131通过，含全部地图的规则配置矩阵。
- 扩大规则回归1176项中，初跑1152通过、24失败。修复本次引入的登岛奖励标签差异，并配置子进程所需CATAN_DOTNET后，两项重跑通过。剩余22项牌名／展示断言失败均在修改前HEAD独立基线上复现；本轮未改动既有卡牌命名决策。不能宣称全套测试通过。
- Unity 6000.3.25f1 Windows构建成功，独立程序保存／恢复检查通过；随包Windows自包含房间服务构建成功。
- 最终实际程序遍历18配置，创建54次新局、18次存档往返完全一致，地图地形对象与公开状态一致。结果见 [程序结果](player-map-results.json)。
- 隐藏窗口截图为黑屏，不作为视觉通过证据。地图观感、专属面板鼠标交互、用户整局试玩、Android和公网部署尚未验收。本轮没有更新云服务器。

## 可复现入口

在仓库根目录执行，dotnet需指向本机SDK；涉及持久化子进程时同时设置CATAN_DOTNET。

- 专项：dotnet test tests/Catan.Rules.Tests --filter FullyQualifiedName~CombinedAllScenarioTests
- 完整局：dotnet test tests/Catan.Rules.Tests --filter "FullyQualifiedName~M6FullGameTests&DisplayName~combined"
- 联机：dotnet test tests/Catan.Network.Tests
- 构建：tools/development/Build-Game.ps1 -Target Windows -OutputRoot "game version/1.7/Catan1.7.3-Combined-Scenarios" -VerifyPlayer
- 实际程序：tools/validation/Test-CombinedMaps.ps1 -BuildRoot "game version/1.7/Catan1.7.3-Combined-Scenarios" -Graphics

[test-results.json](test-results.json)保存各次TRX计数、完整局与专项结果、回归失败名和旧基线复现结果。原始TRX仍保留在本地TestResults目录；测试名是自动化标识。
