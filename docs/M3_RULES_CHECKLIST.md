# M3 航海家条款验收清单

规则来源固定为 `catan-seafarers-2025-en`（2025 英文第六版）；原件、哈希及下载地址见 [规则来源基线](RULES_BASELINE.md)。页码均为 PDF 从 1 起计，与印刷页码一致。测试预期先从规则原文整理，不从实现输出反推。

航海家覆盖基础版相应条款；各剧本的明确例外覆盖航海家通用规则。未明确覆盖的基础规则继续适用。中文名称为工作译名。自动化测试、Unity 验证、用户试玩分别登记，不相互替代。

| 条款 | 来源 | 独立预期与关键边界 | 状态 |
| --- | --- | --- | --- |
| M3-R01 船 | 第 1–2 页 | 每人 15 船；木材与羊毛各 1；空海边可放船；海岸同边不可同时放路船；仅连己船或己建筑，不能仅连道路，不能跨敌建筑连接 | 自动案例通过，见对应表 |
| M3-R02 开局 | 第 3 页 | 沿岸起始村可选相邻船代替路；保留基础顺逆序及最终起始村资源；剧本限制起始区域 | 自动案例通过，见对应表 |
| M3-R03 移船 | 第 2 页 | 行动阶段每回合最多 1 次；当回合建船不能移；必须有一端不连己船/己建筑；己建筑间闭合船线不能移，即使被敌建筑插入；移除后仍须满足新落点连通；海盗相邻禁止移出/移入 | 自动案例通过，见对应表 |
| M3-R04 海盗 | 第 2–3 页 | 7 或骑士可选强盗/海盗；海盗移至新海格或外框；受害人必须在该格有船；偷随机一张资源；海盗周边禁止新船 | 自动案例通过，见对应表 |
| M3-R05 金矿 | 第 2 页 | 产出时每村任选 1、每城任选 2，城市可混合；不新增第六种资源；不能透支银行；待决选择可恢复且非当事席位不得代选 | 自动案例通过，见对应表 |
| M3-R06 最长航线 | 第 2 页 | 路船合计至少 5 获 2 分；路与船只有经己建筑才可相连；分岔、环路、敌建筑断路及平局处理沿用基础规则 | 自动案例通过，见对应表 |
| M3-R07 修路卡 | 第 3 页 | 免费两路、两船或一路一船；逐枚验证连通、空边、供应、海盗；不能因路供应为零而忽略可用船 | 自动案例通过，见对应表 |
| M3-S01 新岸 | 第 4–5 页 | 起始仅主岛；每个玩家首次在每座小岛建村额外 2 分，无关他人先后；自己回合至少 14 分胜 | 自动案例通过，见对应表 |
| M3-S02 四岛 | 第 6–7 页 | 起始可一岛或两岛，逐玩家记录家园岛；首次其他岛额外 2 分；13 分胜 | 自动案例通过，见对应表 |
| M3-S03 迷雾 | 第 8–9 页 | 船/路触及空格交点时揭示；陆地配随机号码并获得该资源 1 张，海格无号码无奖励；未知地图与牌序不公开；12 分胜 | 自动案例通过，见对应表 |
| M3-S04 沙漠 | 第 10–11 页 | 起始仅主区域；隔沙漠狭地和小岛作为独立探索区域，首次额外 2 分；14 分胜 | 自动案例通过，见对应表 |
| M3-S05 遗忘部落 | 第 12–13 页 | 起始仅主岛；只可在有号码格建村/放强盗；船到奖励边分别获得 1 分、发展卡或可迁移港口；奖励只取一次；拿牌视同当回合购买；港口须有沿岸己村且与已有港口至少隔一边，不能放时持有；13 分胜 | 自动案例通过，见对应表 |
| M3-S06 布料 | 第 14–15 页 | 三轮起始村，顺/逆/顺序，只有第三村收资源；8 村各 5 布，总库 10；连接立即取 1，此后号码产布；短缺由总库补足，但村为零不产；两布 1 分；不设最长航线 | 自动案例通过，见对应表 |
| M3-S06b 布料边界 | 第 15 页 | 连己建筑与村的船线闭合；首次建贸易关系前不可移海盗；之后可偷资源或 1 布；14 分或回合结束有至少 5 村空时结束，后者以分数再布量破同分 | 自动案例通过，见对应表 |
| M3-S07 海盗岛 | 第 16–17 页 | 固定额外村船与三层堡垒；3 人移除胜利点卡、4 人视为骑士；无最长航线/最大军队/强盗；低骰先移海盗舰队和攻击，再产出或弃牌；输随机失 1 加己城市数资源、赢任选 1、平手不变 | 自动案例通过，见对应表 |
| M3-S07b 军舰与堡垒 | 第 16–17 页 | 主航线须最短、不分岔，先己桥头后己堡垒；骑士仅升级距起点最近的普通船；战力多于骰数移除 1 层，少于移除末端 2 船，等于移除末端 1 船，随后结束回合；夺回堡垒变普通村；须夺堡且 10 分胜 | 自动案例通过，见对应表 |
| M3-S08 奇迹 | 第 18–19 页 | 起始避开桥/墙标记及 X；无海盗；每小岛首次额外 1 分；按条件独占一种奇迹，每级支付牌面五资源，同回合可多级；4 级完成或 10 分且等级严格高于所有对手时胜 | 自动案例通过，见对应表 |
| M3-S09 新世界 | 第 20 页 | 随机地形、号码与轮流放港；红号不相邻、港口至少隔一边；逐玩家家园岛；首次非家园岛额外 1 分；12 分胜 | 自动案例通过，见对应表 |
| M3-E01 原子与重试 | 项目约定 | 所有非法/越权命令均无状态改变，预览无副作用；重复 ID 不重复收费、移动或揭示，同 ID 不同内容拒绝 | 自动案例通过，见对应表 |
| M3-E02 守恒 | 项目约定 | 每种资源 19，路 15/船 15/村 5/城 4，各特殊剧本须包含保留棋子和奖励牌；布料总计 50；每个落点唯一 | 自动案例通过，见对应表 |
| M3-E03 权限 | 项目约定 | PlayerView 仅自己的资源/牌；对手只公开数量；未知地形、随机状态、牌堆顺序与私有指令历史均不可见；返回对象不能修改权威状态 | 自动案例通过，见对应表 |
| M3-E04 存档 | 项目约定 | 规则/剧本/数据版本、随机数、全部待决、当回合建船及已移船标记保存；生产加载拒绝改写；保存后同命令确定性一致 | 自动案例通过，见对应表 |
| M3-E05 完整局 | 路线图退出条件 | 代表剧本先完整局验收，再每个声明支持的单扩展剧本；测试策略只读当前席位 PlayerView，经统一 Execute 执行 | 自动案例通过，见对应表 |
| M3-U01 客户端 | 项目约定 | 固定 Unity 编译及独立 Windows 运行、隐藏信息换座、全部新待决有操作入口、保存恢复 | 工程验收通过，见 M3_ACCEPTANCE.md；完整 UI 像素/人工试玩未验 |

## 执行记录

2026-10-09，实际运行 `tools/validation/Test-M3Rules.ps1`，Release 构建 0 警告、0 错误，**240/240 项通过、0 跳过**；其中 M1/M2 回归 138 项，M3 新增 102 项。正式结果为 `.local/m3/test-results/m3-rules.trx`，测试运行总时间 36.8 秒，测试运行时 .NET 10.0.12、规则库兼容面 .NET Standard 2.1。Unity 和独立 Windows 客户端属于另行验收，不能由本结果代替。

独立受控局面仅由测试代码反射注入私有权威状态，生产 `Load` 拒绝无合法历史的此类局面。所有受控成功/失败操作均同时检查资源、路/船/村/城、发展牌及布料守恒，失败时比较前后存档字节。地图来源、固定布局和初始组件图核验见 [剧本登记](M3_SCENARIOS.md)。

## 条款与已通过案例对应

方法位于 [通用航海规则](../tests/Catan.Rules.Tests/M3RulesTests.cs)、[剧本案例](../tests/Catan.Rules.Tests/M3ScenarioTests.cs)、[特殊规则案例](../tests/Catan.Rules.Tests/M3SpecialRulesTests.cs)、[保存与权限](../tests/Catan.Rules.Tests/M3PersistenceTests.cs) 和 [完整局](../tests/Catan.Rules.Tests/M3FullGameTests.cs)。下表为实际运行映射；原文条款还经过逐条阅读核对，未将完整对局当作每个边界分支的替代。

| 条款 | 已通过的独立案例 |
| --- | --- |
| R01 | `ShipCostsWoodAndWoolAndCoastalEdgeCannotHoldBothPieceTypes`、`ShipCannotConnectOnlyToRoadOrThroughEnemyBuilding`、`FifteenShipsExhaustSupplyButZeroRoadSupplyStillAllowsRoadBuildingShips` |
| R02 | `CoastalSetupAllowsShipAndPreservesFiniteSupplies`、`AllOfficialScenariosHaveExpectedGoalAndStartingPieceCount`（9 剧本，各含 3/4 人） |
| R03 | `OpenShipMovesOnceWithoutPaymentAndCannotMoveAgainOrBeNewlyBuilt`、`ShipMoveCannotUseRemovedSourceAsItsOwnConnection`、`ClosedShipRouteAndNonEndpointCannotMoveEvenWithEnemyInterruption` |
| R04 | `PirateBlocksBuildAndMovingAwayAndOntoItsSeaHex`、`KnightMovesPirateAndOnlyShipsOnDestinationAreEligibleForTheft` |
| R05 | `GoldVillageAndCityChooseOneAndTwoResourcesWithoutCreatingNewResourceType`、`GoldChoiceDuringSetupReturnsToSamePendingRoadAnchor`、`FogGoldChoiceRestoresRemainingRoadBuildingPlacement`；海盗完整局的合法金矿待决保存 |
| R06 | `MixedRouteJoinsOnlyAtOwnBuilding`；M2 最长路图与奖励回归继续通过 |
| R07 | `RoadBuildingCanPlaceShipAndRoadWithNoResourcePayment`、`FifteenShipsExhaustSupplyButZeroRoadSupplyStillAllowsRoadBuildingShips` |
| S01/S02/S04 | `FirstSettlementOnEachForeignRegionAwardsBonusOnlyOncePerPlayer`（三种 2 分剧本）；各剧本开局与完整局 |
| S03 | `FogRevealsNewLandAndGivesItsResourceWithoutLeakingRemainingDrawOrder`、`FogGoldChoiceRestoresRemainingRoadBuildingPlacement`、`PlayerViewNeverContainsHiddenMapDeckRandomOrOtherHands` |
| S05 | `ForgottenTribeGiftCardIsCollectedOnceAndCannotPlaySameTurn`、`CollectedPortMustBePlacedAtOwnSeparatedCoastAndWorksImmediately`、`GiftPortPlacementRestoresRemainingRoadBuildingPlacement`、正常取得港口的独立保存案例（见下） |
| S06/S06b | `ClothStartsWithEightFiveTokenVillagesAndTenInSupplyAndNoLongestRoute`、`ClothProductionUsesGeneralSupplyForShortageButEmptyVillageProducesNothing`、`ClothScoresPairsAndVillageExhaustionFinishesAtEndOfTurn`、`ClothConnectionAwardsOneClothClosesShipsAndUnlocksPirateTheft` |
| S07/S07b | `PirateIslandDevelopmentAndReservedPieceSuppliesMatchEdition`（3/4 人）、`FortressAttackWinTieLossUseOneDieAndAlwaysEndTurn`（输/平/胜）、`KnightConvertsNearestNormalInvasionShipAndNeverMovesPirate`、`PirateFleetLossBeforeSevenCanRemoveDiscardObligationAndSevenStealsAnyOpponent`、`WinningFleetGoldIsChosenBeforeSevenDiscardAmountIsCalculated`、`InvasionMayChooseAnotherOwnedMainlandCoastButCannotRerootAfterSailingWest`、`WesternInvasionCannotBranchOrAttackBeforeReachingOwnFortress` |
| S08 | `WonderPrintedCostsAreExactAndFourLevelsWinImmediately`（5 座奇迹，各 4 级）、`WonderRequiresPrintedQualificationAndIsExclusive`（5 条件）、`TenPointsRequireStrictlyHigherWonderLevel`（并列/领先）、`FirstSettlementOnEachForeignRegionAwardsBonusOnlyOncePerPlayer`（1 分剧本） |
| S09 | `NewWorldPortsAreChosenInTurnAndPendingPortSaveIsAuthoritative`、剧本组件/随机布局核验，以及 3/4 人完整局 |
| E01 | `MalformedOrUnauthorizedCommandsAreAtomic`（8 类）、`SaveRestoresPendingSetupAndDeduplicatesShipPlacementWithoutExtraCost`、`PreviewIsDetachedAndFixtureSaveCannotBeLoadedAsProduction`；所有拒绝案例比较完整保存不变 |
| E02 | `M3Harness.Invariants` 在每个独立合法/非法命令后独立核对库存；完整局每指令调用核心不变量，终局再次独立核对 |
| E03 | `PlayerViewNeverContainsHiddenMapDeckRandomOrOtherHands`（迷雾/部落）、`DetachedShipTileAndGoldViewsCannotAlterAuthority`；基础手牌、公开回执与隐私字段的 M2 回归 |
| E04 | `AlteredAuthorityOrHiddenMapCannotLoad`（12 类）、`SaveRestoresPendingSetupAndDeduplicatesShipPlacementWithoutExtraCost`、完整局多待决恢复、`NewWorldPortsAreChosenInTurnAndPendingPortSaveIsAuthoritative`、正常取得港口保存案例 |
| E05 | `ViewOnlyLocalSeatsFinishNormalGameAndPendingSavesResumeDeterministically`（全部 9 剧本，各 3/4 人，共 18 局） |

奇迹五资源成本预期独立抄录自官方第 1 页组件图：长城木 1/砖 3/谷 1，大桥木 3/羊 1/谷 1，纪念碑谷 3/矿 2，剧院木 1/砖 1/羊 3，城堡砖 1/谷 1/矿 3。未依据运行中的 `WonderDefinition.Cost` 生成测试预期。

## 完整对局与待决恢复

`M3ViewOnlyDriver` 只接收当前操作席位的 `PlayerView`，通过统一 `Execute` 提交命令。策略没有会话、权威状态、保存、随机状态、未揭示地图、牌序或敌方手牌的访问入口。每局均来自正常 `Create` 和正常开局，未注入资源、棋子或骰点；测试外壳读取权威状态仅用于断言。测试席位不是 M6 正式 AI 功能，也不代表用户亲自试玩。

| 剧本 | 3 人：种子 / 终局回合 / 胜者 / 指令数 | 4 人：种子 / 终局回合 / 胜者 / 指令数 |
| --- | --- | --- |
| 新岸 | 1 / 71 / P1 / 263 | 2 / 117 / P2 / 491 |
| 四岛 | 11 / 52 / P2 / 187 | 3 / 84 / P4 / 325 |
| 迷雾 | 12 / 48 / P2 / 188 | 4 / 115 / P3 / 445 |
| 沙漠 | 13 / 92 / P2 / 361 | 5 / 80 / P3 / 300 |
| 遗忘部落 | 14 / 92 / P2 / 341 | 6 / 151 / P3 / 573 |
| 布料 | 15 / 75 / P3 / 292 | 7 / 87 / P4 / 367 |
| 海盗岛 | 16 / 96 / P1 / 376 | 8 / 101 / P3 / 410 |
| 奇迹 | 17 / 106 / P2 / 419 | 9 / 109 / P4 / 440 |
| 新世界 | 18 / 128 / P3 / 426 | 10 / 115 / P4 / 438 |

18 局共同覆盖开局待放路、弃牌、强盗/海盗受害者、免费路船；普通剧本覆盖移动强盗，海盗岛两局覆盖金矿奖励待决，新世界两局覆盖轮流放港。每类待决首次出现时保存、通过生产 `Load` 复原，然后向两个独立会话提交同一条命令，验证权威保存逐字一致；全部终局也经生产重放验证。

遗忘部落港口另由 [SeafarersPortPersistenceTests.cs](../tests/Catan.Rules.Tests/SeafarersPortPersistenceTests.cs) 的 `NormallyAcquiredGiftPortPendingRestoresAndContinuesInIndependentAuthority` 覆盖：正常 4 人种子 109 开局，策略仅读 `PlayerView`、积累及银行兑换资源、建船取得公开奖励港口，进入 `PortPlacement` 后保存、生产加载、相同放港命令继续，两个独立会话保存一致。这里没有把测试反射注入的待决当作生产保存通过。

人工已检查：官方通用条款与九个剧本规则文本、独立预期和奇迹图示成本、测试策略权限边界、实际 TRX 的 240 项结果。客户端的实际交互、Unity 兼容性、用户试玩和收藏版外观不在此份规则测试记录内。

