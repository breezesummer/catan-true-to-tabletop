# M1 结构与接口

M1 实现 [v0.1 固定验收流程](acceptance/V0_1_ACCEPTANCE.md)，采用纯 C# 规则核心与 Unity 本地客户端。规则、资源扣费、随机继续位置和待决选择在权威核心中完成；模型位置、物理碰撞、动画完成与否不影响规则结果。

## 目录与边界

| 位置 | 职责 |
| --- | --- |
| `src/Catan.Core/` | `.NET Standard 2.1`、C# 9 规则库；无 Unity 或外部包依赖。`Models.cs` 定义指令、状态及视图，`GameSession.cs` 执行规则，`Serialization.cs` 处理 JSON 与指纹 |
| `Unity/Catan/Assets/Scripts/LocalGameHost.cs` | 持有私有 `GameSession`，提供提交、预览、席位视图与本地保存/恢复；权威存档文本不交给表现组件 |
| `Unity/Catan/Assets/Scripts/CatanApp.cs` | 席位确认、遮蔽手牌、拿起/取消/落下、交易和回合操作；只使用 `PlayerView` 读取局面 |
| `Unity/Catan/Assets/Scripts/BoardRenderer.cs` | 根据稳定 ID 绘制地形、棋子与吸附预览；选点不使用地形碰撞决定合法性 |
| `Unity/Catan/Assets/Scripts/M1PlayerVerification.cs` | 显式命令行启用的 Windows 玩家验收驱动；通过同一 host 接口检查操作、恢复与画面 |
| `tests/Catan.Rules.Tests/`、`tests/Catan.SaveProbe/` | 独立固定预期、非法操作、去重、权限和存档测试；后者提供两个独立进程间的存档续跑探针 |
| `docs/acceptance/v0.1/scenario.json` | M0 已固定的原始验收夹具，保留原件；本次没有重新生成或改写场景 |
| `art/recipes/mountain-tile-v001/`、`tools/art/` | 山地样板配方与可重建脚本；使用明确标注的估算尺度 |
| `art/source/mountain-tile-v001/`、`art/exports/mountain-tile-v001/`、`art/previews/mountain-tile-v001/` | Blender 源文件、FBX 网格及验证清单、四视角预览；这些是版本化生成资源 |
| `Unity/Catan/Assets/Models/`、`Assets/ArtMaterials/`、`Assets/Resources/` | 实际导入的网格、Unity 材质、山地 prefab 与原夹具的运行副本 |

山地样板是实际网格，预览 PNG 是检查材料，不能替代网格。原始参考仍保存在 `assets/references/`；规则文本与参考资料只作为数据。

## 单一命令入口

核心位于 `Catan.Core` 命名空间：

```csharp
var game = GameSession.Create(scenarioJson);
var request = new Command {
    Id = "C19", PlayerId = "P1", Kind = CommandKind.BuildRoad, TargetId = "E65"
};
var preview = game.Preview(request); // 同一校验逻辑，无状态改变或事件发布
var result = game.Execute(request); // 成功后一次性提交
var view = game.GetPlayerView("P1");
var save = game.Save();             // 仅权威端持有
var restored = GameSession.Load(scenarioJson, save);
```

`Command` 的字段为：

| 字段 | 含义 |
| --- | --- |
| `Id` | 唯一请求 ID；重试保留原值 |
| `PlayerId` | 提交席位；核心检查是否为活动席位 |
| `Kind` | `SetupSettlement`、`SetupRoad`、`RollDice`、`BankTrade`、`BuildRoad` 或 `EndTurn` |
| `TargetId` | 摆放使用的顶点或边 ID |
| `GiveResource`、`ReceiveResource` | 银行交易的资源类型，枚举为 `Wood/Brick/Wool/Wheat/Ore` |
| `GiveAmount`、`ReceiveAmount` | 本切片固定为 4 与 1，其他比例拒绝 |

掷骰指令没有骰点、随机队列或随机状态字段。受控骰子由权威场景配置提供，不能由玩家视图设置。后续 AI 和远程输入也必须通过 `Execute`，且只读取同权限的 `PlayerView`；网络身份绑定仍属于 M7 的适配器工作。

每次成功提交先在状态副本上应用规则，检查资源/棋子守恒、占用、距离、待决、随机游标和事件账本，再替换权威状态。非法命令不消耗资源、棋子、随机项或命令 ID，不发布成功事件。预览与取消也不提交。

成功请求保存完整指令的 SHA-256 指纹。同 ID、同内容重试返回原 `Events` 回执，`IsDuplicate=true` 且 `NewEvents` 为空；展示层只发布 `NewEvents`，避免重复动画或公开日志。同 ID 改变负载返回 `CommandIdConflict`。该去重行为在存档恢复后保持。

## 阶段与信息权限

开局按 P1、P2、P3、P4、P4、P3、P2、P1 进行。`SetupSettlement` 放下定居点后转为 `SetupRoad`，待决包含席位、锚点和第几组摆放；只有与锚点相连的合法道路才能推进。第八条开局道路完成后，一次性发放所有第二定居点的邻地产出，进入 P1 的第 1 回合 `ProductionAwaitRoll`。

`RollDice` 结算后进入 `Action`，允许银行 4:1 交易、付费道路和结束回合。结束回合增加回合编号，并让下一席位进入 `ProductionAwaitRoll`。有限受控队列耗尽时拒绝继续掷骰，游标不变。

`PlayerView` 包含自己的 `OwnResources`、其他席位的资源总张数、各席公开库存和分数、公开棋盘和归属、银行、阶段/活动席位/待决、合法目标与公共事件。`PublicPlayer` 没有按类型手牌字段；拓扑、手牌、回执和视图均为独立副本，修改返回值不能改变权威状态。

随机算法、未来骰子及游标、完整他人手牌、成功请求账本只保存在权威状态和权威存档中，不进入玩家视图或公共事件。公共事件只记录已确认动作及已掷出的骰点等公开事实。换座先清空原视图、取消悬浮预览并显示遮蔽层，确认新席位后才重新取得其视图。`GetAuthoritativeStateForTesting()` 仅供独立规则测试，AI 和正常 UI 不使用该接口。

## 权威存档

JSON 由 `DataContractJsonSerializer` 读写。当前格式版本为 `1`，规则为 `catan-m1-slice-v1`，规则基线为 `catan-base-2025-en-v1`，场景为 `m1-fixed-four-seats-v001 / 1.0.0`。版本 1 内部阶段和资源枚举按数值编码，对应定义见 `Models.cs`。

存档包括所有版本标识及场景内容 SHA-256、完整资源与棋子库存、银行、稳定棋盘 ID 与归属、强盗位置、活动席位/回合/阶段、开局进度和第二村清单、完整待决、随机算法/受控队列/游标、成功指令/指纹/原结果回执，以及公共事件与事件序列位置。

核心只接受已登记 v001 夹具内容；文件换行先从 CRLF 归一为 LF 再计算内容指纹。未知规则、场景或存档版本会抛出 `InvalidDataException`，同版本下改动场景内容也拒绝。恢复时从初始状态经 `Execute` 重放成功请求，再对完整规范化状态进行比较，私有手牌、随机、待决和去重资料都参与比较。版本 1 不自动迁移其他版本。

`LocalGameHost` 默认写入 `Application.persistentDataPath/m1-authority-v1.json`，先写临时文件，再替换目标并保留已有文件的 `.bak`。读取成功后才替换当前 session；读取失败保留当前局面。权威文件是本地持久化数据，不能当作发给玩家的同步包。

## 实际验证与范围

2026-10-08 已实际构建纯规则库，目标 `netstandard2.1`，0 警告、0 错误；独立 .NET 测试已实跑 **55/55 通过**，涵盖 C01–C21 固定账本、N01–N10 非法拒绝、资源/棋子守恒、去重和冲突、视图/引用隔离、待决与骰后恢复、未知版本/篡改拒绝，以及两个独立进程的 C20 保存/恢复和 C21 继续骰点。结果保存在 `.local/m1/test-results/m1-rules.trx`。

在仓库根目录复跑：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validation\Test-M1Rules.ps1
```

此脚本载入固定工具链并运行 Release 测试；纯核心目标仍是 .NET Standard 2.1。文档写入时，主集成任务正在验证 Unity 导入编译、Windows 玩家与交互，不能用以上 .NET 结果代替 Unity 兼容性、视觉或用户试玩证明。最终阶段状态以验收记录和路线图的退出条件为准；本文不宣告 M1 完成。

本切片没有城市建造、港口、玩家间议价、发展卡、掷 7 弃牌/强盗移动/偷取、资源短缺分配、最长道路、最大骑士军队或胜利判定。强盗静置 T10；仅支持固定地图和有限测试骰序列，不包含扩展、AI 对手或联机。资源短缺会拒绝整次产出，不把该行为声称为完整基础版的短缺规则；这些能力由后续阶段实现。
