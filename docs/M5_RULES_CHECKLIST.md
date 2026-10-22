# M5 组合规则验收清单

规则来源和逐条独立预期见 [组合规则基线](M5_COMBINATION_RULES.md)。以下方法位于 `tests/Catan.Rules.Tests/M5CombinationTests.cs`；完整对局位于 `M5FullGameTests.cs`。执行结果必须以 [验收记录](M5_ACCEPTANCE.md)与测试报告为准，不能仅由存在测试代码推断通过。

| 条款 | 独立案例 |
| --- | --- |
| C01 支持范围、开局、地图 | `SupportedMapsKeepOfficialTopologyAndSetCombinedRules`（四组合）；`UnsupportedScenariosAreExplicitlyRejected`；`UnsupportedPlayerCountsAreRejected` |
| C02 首攻前后海盗 | `PirateCannotMoveOrBeChasedBeforeFirstAttack`；`FirstAttackCountsRemoteCitiesAndSeaKnightsBeforeProduction`；正常首次劫掠保存恢复 |
| C03 全岛蛮族 | `FirstAttackCountsRemoteCitiesAndSeaKnightsBeforeProduction`；四个正常完整局 |
| C04 金矿 | `GoldCityPaysTwoResourcesAndNeverCommoditiesOrAqueduct`；`MerchantCannotOccupyGoldAndTaxationCannotMovePirate` |
| C05 跨海骑士 | `KnightTravelsAcrossRoadShipJunctionWithoutBuildingButLongestRouteDoesNotJoin`；`RecruitKnightAtSeaIntersectionCostsWoolOreAndCanNotMoveWhenInactive`；`ForeignKnightBlocksTravelAlongOwnShipChain` |
| C06 船线开闭 | `OwnKnightClosesShipRouteAndCannotBeAbandoned`；`EnemyKnightCutsLongestRouteButDoesNotMakeAdjacentShipsMovable`；`ShipPreviewAndDuplicateAreAtomicAndConflictingRetryRejected` |
| C07 最长航线 | 混合路线、敌骑士截断的上述两项独立预期 |
| C08 骑士驱逐海盗 | `PirateChaseDeactivatesKnightAndCannotSwitchToRobber` |
| C09 征税 | `MerchantCannotOccupyGoldAndTaxationCannotMovePirate` |
| C10 道路建设 | `RoadBuildingProgressPlacesOneRoadAndOneShipWithoutPayment` |
| C11 外交 | `DiplomacyShipBesidePirateMayRelocateButOnlyAsShip` |
| C12 其他进步卡 | `IntrigueUsesShipConnectionToDisplaceAnEnemyKnight`；`TreasonAllowsReplacementKnightAlongShipsAndRequiresVictimChoice`；`InventionCanSwapUnprotectedGoldNumberWithoutChangingResourceKind` |
| C13 16 分门槛 | `CombinedVictoryRequiresSixteenRatherThanSingleExpansionThreshold`；四个正常完整局 |
| 地形及非法操作 | `SeaOnlyVerticesCannotHoldSettlementsAndSeaOnlyEdgesCannotHoldRoads`；所有拒绝案例比较完整存档不变 |
| 权限、保存、重试 | `ViewAndEventsHaveNoPrivateDeckRandomOrOpponentHand`；`SetupSaveAndRandomContinuationAreExactAndTamperingIsRejected`；船预览/重复指令案例 |

`PirateVictimsComeFromShipsAndTheftMayTakeCommodityWithoutPublicDisclosure` 另核对海盗只针对船主、能偷商品而不公开种类。`M5ReviewRegressionTests.cs` 的六项复核案例覆盖官方 FAQ 的无锚环线、单锚环线、双锚封闭，以及金矿/水渠先后耗尽银行时不遗留无合法选项的待决。

`M5TransactionTests.cs` 五项案例验证事务副本与完整序列化的值相等、所有可变分支无共享引用、发明预览不换号码或耗卡、锻造第二目标失败时回滚第一次晋升、骑士撤退与金矿预览不推进待决。它们用于验证组合大地图下的预览性能改动没有破坏原子性。

完整局从生产 `Create` 开始，策略只接收有权席位的 `PlayerView`，不读取权威状态、存档、其他玩家手牌或随机数。每种首次遇到的待决使用生产 `Load` 建立第二会话，提交同一条后续命令，比较权威保存完全一致；终局再次重放加载。每条成功命令检查规则不变量，终局独立核对资源每种 19、商品每种 12、进步牌 54、道路和船各 15、村 5、城 4 的守恒。

这是组合专项清单；M1–M4 原有回归仍随全套测试运行。未改变的城市骑士卡牌来源沿用 [M4 清单](M4_RULES_CHECKLIST.md)，不能用旧版卡牌名称替代 2025 效果。新增复核发现的回归案例另列在验收记录中。
