# U0 逐棋子入口覆盖表

日期：2026-10-21。范围为真实桌游现有代码与资源文件；「存在」只表示资源路径已核对，不表示 ultra 已制作或逐项视觉验收。机器可读逐色逐状态清单见 [U0-piece-coverage.csv](U0-piece-coverage.csv)。

## 玩家颜色与逐件资产

P1 红、P2 蓝、P3 橙、P4 米白由 `PlayerComponentAssets.ColourKey` 解析；不是当前行动顺序。船的米白文件使用 ivory，建筑与骑士使用 beige。CSV 每行单列玩家、等级、状态、资源与入口，不用一条「全色支持」替代实际清单。

| 类别 | 逐色覆盖 | 棋盘入口 | 预览／供应区 | 动画入口及差异 |
| --- | --- | --- | --- | --- |
| 村庄 | 四色 | 四类 Renderer → Building(village) | PointerPreview(settlement)；WorldObjects.Piece(settlement)；缩略图 settlement | M6BuildPresentation.PublicPieces → 缓存正式模型副本 |
| 城市 | 四色 | 四类 Renderer → Building(city) | PointerPreview(city)；供应 city；缩略图 city | 村升城替换演出，保留旧模型副本 |
| 独立城墙 | 四色 | 作为组合建筑的构件，非独立城市显示 | 供应 wall 使用 city-wall；缩略图 wall | 正式动画使用带墙城市整体，而不是只飞城墙 |
| 带墙城市 | 四色 | M4/M5 → Building(city-with-wall) | PointerPreview(wall)；无独立供应组合 | ExpandedPieces；红色为 city+city-wall，另三色为整体 prefab |
| 大都会 | 四色 × 贸易／政治／科学 | M4/M5 → Building(metropolis) | PointerPreview(metropolis)；Texture 没有独立大都会分支 | 三条改良轨道复用同一颜色模型，Track 进入缓存外观签名；不能当作三套独立模型 |
| 带墙大都会 | 四色 × 三条轨道 | M4/M5 → Building(metropolis-with-wall) | 鼠标 metropolis 只用不带墙模型；wall 预览固定为带墙城市，存在组合状态差异 | ExpandedPieces；红色为 metropolis+city-wall，另三色整体 prefab |
| 道路 | 四色共用一个网格 | 四类 Renderer → PhotoSampleAssets.Piece | PointerPreview(road) 合法／非法着色；供应区硬编码同一资源；缩略图 road 四色 | PublicPieces；道路材质不能仅凭资源名推断颜色 |
| 船 | 四色 | M3/M5 → EntityBoardAssets.Ship | PointerPreview(ship)；供应 ship；缩略图 ship | M6BuildPresentation 航线移动；TabletopShipSelection 选中状态 |
| 战舰 | 四色，复用普通船 | M3 在船旁增加程序球形旗 | 普通船预览／供应／缩略图，无专用战舰图 | 动画公开棋子投影只写 Kind=ship，不能声称球形旗有独立移动演出 |
| 骑士 | 四色 × 1/2/3 级 × 活跃/休眠，共24态 | M4/M5 → Knight(seat,level,active) | PointerPreview 保留等级与活跃；供应固定休眠；缩略图只有等级，无活跃参数 | M6BuildPresentation 投影 Level/Active，升级、激活与移动需分别验收 |

红色建筑分别来自 PhotoSamples/v001 和 EntityAssets/v001；另三色来自 EntityAssets/v002/player-buildings。红色骑士为 level-N-active/resting，另三色为 colour-level-N-helmet/bare；头盔表示活跃只是当前美术约定。

## 公共棋子与剧本组件

| 棋子／状态 | 资源或现状 | 正式入口 | 预览／动画覆盖与缺口 |
| --- | --- | --- | --- |
| 强盗 | EntityAssets/v001/robber/robber-black-group | 四类 Renderer | PointerPreview(robber)；不在 M6 PublicPieces 建造飞行集合 |
| 海盗：未入场、边框、海格 | EntityAssets/v001/pirate/pirate-black | M3/M5；未入场应不创建，边框位置另算 | PointerPreview(pirate)；不得把未入场记作丢资产；不在普通建造飞行集合 |
| 商人：无位置／已放置 | EntityAssets/v001/merchant/merchant-neutral-cart | M4/M5 | PointerPreview(merchant)；无独立供应缩略图；不在普通建造飞行集合 |
| 蛮族轨道 | EntityAssets/v001/barbarian-track/barbarian-track | M4/M5 公共标记根 | 无玩家鼠标摆放入口 |
| 蛮族船：0–7各位置 | EntityAssets/v001/barbarian-ship/barbarian-ship | BarbarianShip → BarbarianLocalPosition，8个锚点 | 位置刷新复用同一模型；没有 U4 连续浮动动画 |
| 港口：通用／木／砖／羊／麦／矿 | EntityAssets/v005/tabletop-markers/port-* | TabletopReferenceMarkers | PointerPreview(port) 固定 generic，并非各资源港口分别预览 |
| 卡坦分标记 | EntityAssets/v005/tabletop-markers/catan-victory-marker | TabletopReferenceMarkers | 无独立拿起／建造飞行入口 |
| 数字牌 | TabletopUI/v008/NumberTokens；v005背面 | TabletopNumberTokens | 不属于玩家棋子；公开号码及背面分别迁移 |
| 部落奖励／赠卡边／赠港 | 公开文字和标记 | M3App 特殊标记绘制 | 无逐件成品3D，不套用普通玩家棋子预览 |
| 布匹村庄／布匹数量 | 公开文字 | M3App | 无独立成品模型或搬运动画 |
| 海盗堡垒／强度／滩头／西行起点 | 公开文字 | M3App | 堡垒状态不等于已有分级模型 |
| 五种奇观／建造等级 | 公开文字 | M3App | 无五类×等级的成品3D模型；U7需补 |
| 城市设施改良板／等级 | HUD 卡面与程序组件 | TabletopWorldObjects | 不应混算为城市或大都会网格，牌面另见 card-manifest |

## 迁移与验收列

CSV 的资源存在列为本轮实际文件检查。棋盘、鼠标预览、供应区、缩略图、动画分别列出，`无独立入口` 与 `复用` 都不能当作成品覆盖。全部 ultra 状态均为待实施。

U1 对每行检查主题解析、取消预览与动画回收；U2/U7按四视图检查比例、枢轴、颜色、等级和组合穿插。重点补测带墙大都会预览差异、战舰标记随移动、骑士供应固定休眠、三轨大都会外观识别，以及红色复合模型回收。没有运行视觉证据的行仍标记待验。

本表是实现审计，不新增规则。规则来源沿用 [资产迁移审计](U0-asset-migration.md) 的逐剧本PDF文件与页码，以及 [本地索引](../../../assets/references/rules/README.md)。
