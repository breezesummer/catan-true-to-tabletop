# M2 基础版条款验收清单

规则基线为 `catan-base-2025-en-v1`。主来源为已锁定的 [官方 2025 英文基础版规则](https://www.catan.com/sites/default/files/2025-03/CN3081%20CATAN%E2%80%93The%20Game%20Rulebook%20secure%20%281%29.pdf)，来源与原件 SHA-256 见 [RULES_BASELINE.md](RULES_BASELINE.md)。以下页码为 PDF 页码，与印刷页码一致。2025 版的 Invention 采用工作译名“发明”；其效果为从银行取两张资源。

清单区分独立局面测试、实际完整对局和客户端验收。状态仅在对应验证执行后填写；自动化对局不代表用户已经试玩或确认美术。

| 条款 ID | 来源 | 应有行为及独立预期 | 验证 |
| --- | --- | --- | --- |
| M2-R01 | 第 3 页 | 3–4 人；每资源 19 张；每人道路 15、村 5、城 4；发展卡骑士 14、胜利点 5、其余各 2 | 自动测试通过；案例见下表 |
| M2-R02 | 第 3、11–12 页 | 正常可变开局：洗混 6 片完整海框且保留每片港口位置、先手掷骰、资源与号码配置、强盗在沙漠、顺序与倒序各放村路一次、第二村领取相邻资源 | 自动测试通过；案例见下表 |
| M2-R03 | 第 6 页 | 两骰决定产出；村 1、城 2、多个建筑累计；强盗所在格不产出；其他资源正常结算 | 自动测试通过；案例见下表 |
| M2-R04 | 第 6 页 | 多人同时请求且银行不足则该资源均不发；只有一人请求则领取剩余数量 | 自动测试通过；案例见下表 |
| M2-R05 | 第 6 页 | 掷 7 无产出；资源大于 7 才弃一半向下取整；7 张不弃、9 张弃 4；发展卡不计入 | 自动测试通过；案例见下表 |
| M2-R06 | 第 6、9 页 | 弃牌完毕才可移强盗；必须换格；只可选新格有建筑的其他玩家；随机偷一张资源；骑士不触发弃牌 | 自动测试通过；案例见下表 |
| M2-R07 | 第 7 页 | 银行 4:1、通用港 3:1、对应资源港 2:1；须有港口建筑；不能交换相同资源、不能透支银行 | 自动测试通过；案例见下表 |
| M2-R08 | 第 7 页 | 玩家交易包含当前玩家；非当前玩家可向当前玩家报价；双方同意并原子交换；不能赠送、交换同类资源或旁席互换 | 自动测试通过；案例见下表 |
| M2-R09 | 第 8 页 | 道路花木砖各 1；须为空边且连接己方路或建筑；敌方建筑不能作为穿越连接点 | 自动测试通过；案例见下表 |
| M2-R10 | 第 8 页 | 村花木、砖、羊毛、谷物各 1；须连接己方道路、空点、与所有建筑至少隔两边；供应耗尽拒绝 | 自动测试通过；案例见下表 |
| M2-R11 | 第 9 页 | 城花谷物 2、矿石 3，只升级己村；归还村棋子、消耗城市；城市供应至多 4 | 自动测试通过；案例见下表 |
| M2-R12 | 第 9 页 | 购买发展卡花羊毛、谷物、矿石各 1；牌堆耗尽拒绝；抽牌保持隐藏，已用牌不回牌堆 | 自动测试通过；案例见下表 |
| M2-R13 | 第 6、9 页 | 发展卡可掷骰前或行动阶段使用；非胜利点卡每回合限 1、购入当回合禁用；不得在待决流程中插入 | 自动测试通过；案例见下表 |
| M2-R14 | 第 9 页 | 骑士移动强盗并盗取；首次已用 3 骑士获最大军队 2 分，平局保留、严格超过才转移 | 自动测试通过；案例见下表 |
| M2-R15 | 第 9 页 | 发明领取银行两张资源，可同种；供应不足的组合拒绝，不扣牌、不部分领取 | 自动测试通过；案例见下表 |
| M2-R16 | 第 9 页 | 垄断收取所有其他玩家指定资源，其他资源保持；零张仍可合法使用 | 自动测试通过；案例见下表 |
| M2-R17 | 第 9 页 | 修路免费放两条合法路，逐条仍须连通；剩一枚道路时仅放一条；剩零枚不可使用 | 自动测试通过；案例见下表 |
| M2-R18 | 第 8 页、官方 FAQ | 最长路至少 5：分岔不可把三臂相加、环计 6、每边仅计一次、敌村切断；当前持有人平局保留，无持有人并列不授奖 | 自动测试通过；案例见下表 |
| M2-R19 | 第 9–10 页 | 胜利点卡平时隐藏；购入当回合也计胜；只能在当前玩家自己的回合达到至少 10 分时结束，开回合即可胜利 | 自动测试通过；案例见下表 |
| M2-R20 | 第 6、10 页 | 建造、交易可交错重复；结束回合切至下一席；胜利后拒绝新操作 | 自动测试通过；案例见下表 |
| M2-E01 | 项目约定 | 非法指令、错误席位、错误阶段、负数/溢出、无效枚举均拒绝，存档字节不变；指令去重不重扣费、ID 冲突拒绝 | 自动测试通过；案例见下表 |
| M2-E02 | 项目约定 | 资源和棋子守恒；25 张发展卡守恒；所有落点有效且唯一；任何返回视图/回执不与权威状态共享可变引用 | 自动测试通过；案例见下表 |
| M2-E03 | 项目约定 | 其他玩家仅见资源与未用发展卡数量；不见具体手牌、牌堆顺序、随机状态；公开事件不能泄漏抽牌或被盗资源种类 | 自动测试通过；案例见下表 |
| M2-E04 | 项目约定 | 保存完整版本、随机状态、待决决策和去重回执；待决弃牌/强盗/盗取/修路/交易均可恢复；后续同命令产生同状态 | 自动测试通过；案例见下表 |
| M2-E05 | 项目约定 | 版本不兼容和篡改权威状态拒绝；生产加载不能接收测试夹具任意改写；M1 存档仍由 M1 规则解释 | 自动测试通过；案例见下表 |
| M2-E06 | 第 12 页 → 第 10 页 | 完整自动化对局从正常开局到胜利；驱动只读取席位 `PlayerView`，只经 `Execute` 提交行动，不读取牌序/随机/敌方手牌 | 自动测试通过；案例见下表 |
| M2-U01 | 项目约定 | Unity 兼容编译和 Windows 实际运行；隐私换座遮蔽手牌，当前待决席位可完成全部动作，提供规则提示 | 由 M2 验收记录登记 |

最长路与空手邻居等补充边界使用 [CATAN 官方基础版 FAQ](https://www.catan.com/faq/basegame)，限与上述 2025 规则一致的解释。固定受控测试局面属于工程测试，不声称为官方开局或正式游戏选项。

## 执行记录

2026-10-09，实际完成 Release 构建，0 警告、0 错误；海框港口修正后的完整测试运行 **138/138 通过、0 跳过**（M1 原 55 项 + M2 新 83 项），记录为 `.local/m2/test-results/m2-physical-frames-final.trx`。此记录包含最长路切断后的并列归还案例及 5 种海框种子的独立检查，替代较早港口实现下的对局结果。最终 Unity 与客户端集成运行以 [M2 验收记录](M2_ACCEPTANCE.md) 为准。

海框预期直接来自官方第 3 页六片组件图：从凸榫到凹槽编号每片五条岸边 `0..4`，单通用港、单木港、单矿港各位于边 2；羊毛/砖/谷物三片分别在边 1 有对应资源港、边 4 有通用港。测试用独立固定海岸边序核验整片组合，禁止将九个港口单独洗混。已实际检查种子 `0`、`1`、`42`、`1234`、`4294967295`。

普通沙箱内 VSTest 的测试宿主通信曾超时，未作为成功证据；上述通过结果来自获准的沙箱外 VSTest 运行。测试规则库目标为 `.NET Standard 2.1`，测试运行时为 `.NET 10.0.12`。Unity 兼容编译与真实 Windows 客户端运行由主验收记录单独登记，不能用这些 .NET 结果替代。

`M2ViewOnlyDriver` 只接收当前操作席位的 `PlayerView`，返回 `Command`，没有会话、权威状态、随机状态、牌堆或其他席位手牌的访问入口。测试外壳经 `Execute` 提交，状态不变量和存档只用于断言，不作为策略输入。以下两局均从 `Create` 正常随机地图、正常先手与摆放开始，没有测试注入或修改资源：

| 人数 / 种子 | 终局 | 指令数 | 实际恢复点 |
| --- | --- | --- | --- |
| 3 人 / `1` | 第 59 回合 P1 达到至少 10 分 | 220 | 开局待放路、弃牌、移强盗、选被盗席位、免费修路；每处恢复后下一指令结果逐字相同；终局完整重放相同 |
| 4 人 / `2` | 第 62 回合 P3 达到至少 10 分 | 240 | 同上 |

交易报价保存与恢复另用正常开局的 `PendingTradeSurvivesProductionSaveAndResolvesWithSameReceipt` 验证。独立受控局面仅由测试代码反射注入私有测试会话，同时保持资源、棋子、卡牌等不变量；生产 `Load` 会拒绝这种没有合法操作历史的任意局面，专门的负例已通过。

## 条款与案例对应

下表中的方法名均位于 [M2RulesTests.cs](../tests/Catan.Rules.Tests/M2RulesTests.cs)、[M2PersistenceTests.cs](../tests/Catan.Rules.Tests/M2PersistenceTests.cs) 或 [M2FullGameTests.cs](../tests/Catan.Rules.Tests/M2FullGameTests.cs)。带参数的测试方法包含表中各固定变体；预期数量、资源账本和固定路图由测试代码独立指定。

| 条款 | 已通过案例 |
| --- | --- |
| R01–R02 | `NormalSetupHasFiniteOfficialSupplySnakeOrderAndSecondVillageIncome`（3/4 人）、`UnsupportedSeatCountIsRejected`（2/5 人）、`VariableSetupPreservesTerrainAndNumberSupplyAndIsSeedDeterministic`、`VariablePortsShuffleSixPhysicalFramesPreservingTheirPrintedPortPositions`（5 个独立种子） |
| R03 | `ProductionPaysVillageOneAndCityTwoExceptRobber`（有/无强盗） |
| R04 | `ShortageWithMultipleRecipientsPaysNoneAndSingleRecipientGetsRemainder`（单人/多人） |
| R05 | `SevenDiscardsFloorHalfOnlyAboveSevenBeforeMovingAndNeverCountsDevelopmentCards` |
| R06 | `KnightBeforeRollMovesRobberWithoutDiscardAndStealsOnlyEligibleResource`、`EmptyHandAdjacentVictimIsSelectableAndNoVictimEndsRobberMove` |
| R07 | `BankAndOwnedPortsUseCorrectRatio`（4/3/2）、`PortsRequireOwnedBuildingAndCorrectResourceAndBankStock` |
| R08 | `OtherSeatMayOfferTradeToActiveSeatAndAcceptanceIsAtomic`、`InvalidPlayerTradesDoNotMutateAnything`（5 种非法报价）、`RejectedTradeAndCancelledTradeReturnToActionWithoutTransferringCards` |
| R09–R11 | `BuildingCostsDistanceConnectionCityReplacementAndSupplyAreExact`、`OpponentBuildingBlocksRoadContinuationAndCannotBeUpgraded`、`FinitePieceSupplyRejectsConstructionEvenWithEnoughResources`（道路/村/城） |
| R12 | `NewlyBoughtVictoryPointWinsImmediatelyAndDevelopmentDeckCannotOverdraw`、`ViewsAndPublicReceiptsExposeOnlyOwnCardsAndNoRandomDeckOrPrivateCommandHistory` |
| R13 | `NewDevelopmentCardsAndSecondCardInSameTurnAreRejected`（四种非胜利点卡）、掷骰前骑士与修路的独立案例 |
| R14 | `LargestArmyRequiresThreePreservesTieAndTransfersOnlyOnStrictLead`、`FifthRoadAndThirdKnightAwardExactlyTwoPointsThroughNormalCommands` |
| R15 | `InventionTakesTwoResourcesAndInsufficientSelectionIsAtomic`（同种/不同种） |
| R16 | `MonopolyCollectsOnlyNamedResourceIncludingZeroCase`（0/7 张） |
| R17 | `RoadBuildingPlacesTwoFreeConnectedRoadsAndBlocksOtherCommandsUntilFinished`、`RoadBuildingRespectsRemainingRoadPieceSupply`（剩 1/0 枚） |
| R18 | `LongestRoadCountsTrailWithoutRepeatingEdgesOrSummingBranches`（分岔 2、环 6、直链 5、带尾环 8）、`OpponentSettlementCutsLongestRoadAndTiedHolderRetainsAward`、`BreakingAwardHoldersRoadReturnsTileToSupplyWhenOtherLeadersTie` |
| R19 | `HiddenVictoryPointsCanWinImmediatelyButOnlyOnTheirOwnersTurn`、`NewlyBoughtVictoryPointWinsImmediatelyAndDevelopmentDeckCannotOverdraw` |
| R20 | 两个完整对局及 `HiddenVictoryPointsCanWinImmediatelyButOnlyOnTheirOwnersTurn` 中终局拒绝 |
| E01 | `MalformedOrUnauthorizedCommandsAreAtomic`（8 种）、`SaveRestoresSetupDecisionRandomStateAndCommandDeduplication`、`PreviewHasNoEffectsAndSharesExecuteValidation`；所有独立非法操作比较前后存档 |
| E02 | `DetachedViewsCommandsReceiptsAndAuthoritativeSnapshotsCannotMutateSession`；每个独立成功/失败案例运行独立资源/棋子/发展卡守恒；完整对局每指令运行规则不变量 |
| E03 | `ViewsAndPublicReceiptsExposeOnlyOwnCardsAndNoRandomDeckOrPrivateCommandHistory`、`DiscardAndTheftReceiptsDoNotPublishResourceTypesOrClientSuppliedSecrets` |
| E04 | `SaveRestoresSetupDecisionRandomStateAndCommandDeduplication`、`PendingTradeSurvivesProductionSaveAndResolvesWithSameReceipt`、两个完整对局中的五类待决存档恢复 |
| E05 | `AlteredAuthorityCannotLoadEvenWhenItLooksPlausible`（12 类篡改）、`TestFixtureCannotBeLoadedAsAProductionSaveAndM1SaveCannotBecomeM2` |
| E06 | `ViewOnlyLocalSeatsFinishNormalGameAndPendingSavesResumeDeterministically`（3/4 人） |

人工已检查内容：官方原文第 3、6–12 页的条款文本、上述固定局面预期、测试策略权限边界与实际 TRX 输出。这里没有将自动化操作视为用户试玩确认，也没有验收收藏版美术、正式 AI 或联机功能。
