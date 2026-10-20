# U0 地图与资产迁移审计

日期：2026-10-21（Asia/Shanghai）。审计对象：启动 U0 时的真实桌游实现。本文登记现状与 U1 待办，不表示双主题、ultra 美术或性能已经验收。

已阅读 `PROJECT_BRIEF.md`、`ROADMAP.md`、`ULTRA_THEME_IMPLEMENTATION_PLAN.md`、`ART_PIPELINE.md` 及本地规则索引。M8 成品体验仍待开始；此次资产审计不改变项目阶段状态。

## 地图支持范围

2026-10-21 已编译并调用当前纯规则工厂，导出 [26 个规则包／人数配置](U0-map-inventory.json)。航海家使用 `SeafarersScenarios.CreateSeeded`，组合使用 `CombinedScenarios.CreateSeeded`；基础版、城市与骑士实际建立会话后只读取 P1 的公开 `Board`。种子统一为 2025，仅用于复现本次枚举，不是性能夹具的种子。导出不含权威随机状态、牌库顺序或未揭示迷雾内容。

下表数量依次为 **Tile／Vertex／Edge**；海面边框、港口附加装饰海格、蛮族轨道、HUD 卡牌等不计入规则格数。实际渲染负载应在运行采样中另外计数，49 格不等于 49 个 Renderer。

| 规则包 | 地图 ID／名称 | 3 人规模 | 4 人规模 | 来源与覆盖要点 |
| --- | --- | --- | --- | --- |
| 基础版 | `standard` | 19／54／72 | 19／54／72 | `catan-base-2025-en.pdf` 第 3、11–12 页；正常新局随机地形、点数与海框港口，稳定拓扑来自 `m1-scenario.json` |
| 城市与骑士 | `standard` | 19／54／72 | 19／54／72 | `catan-cities-knights-2025-en.pdf` 第 4–5 页；基础轮廓另加城墙、骑士、大都会、商人及蛮族轨道 |
| 航海家 | `heading-for-new-shores`／驶向新海岸 | 35／92／126 | 42／108／149 | `catan-seafarers-2025-en.pdf` 第 4–5 页；金矿、主岛与小岛 |
| 航海家 | `four-islands`／四座岛屿 | 35／92／126 | 35／92／126 | 同上第 6–7 页；多个起始岛、公开海域 |
| 航海家 | `fog-islands`／迷雾岛屿 | 42／108／149 | 42／108／149 | 同上第 8–9 页；开局均有 12 个 `fog` 占位，不生成其真实地形 |
| 航海家 | `through-the-desert`／穿越沙漠 | 35／92／126 | 42／108／149 | 同上第 10–11 页；沙漠分区与金矿 |
| 航海家 | `forgotten-tribe`／被遗忘的部落 | 49／124／172 | 49／124／172 | 同上第 12–13 页；8 个奖励边、4 个赠卡边、6 个赠送港口 |
| 航海家 | `cloth-for-catan`／卡坦的布匹 | 42／108／149 | 42／108／149 | 同上第 14–15 页；8 座村庄、布匹供应与海盗 |
| 航海家 | `pirate-islands`／海盗群岛 | 49／124／172 | 49／124／172 | 同上第 16–17 页；3／4 座堡垒与预放船、14 格海盗航路、战舰旗 |
| 航海家 | `wonders-of-catan`／卡坦奇观 | 49／124／172 | 49／124／172 | 同上第 18–19 页；5 种奇观及禁放位置；无海盗 |
| 航海家 | `new-world`／新世界 | 42／108／149 | 42／108／149 | 同上第 20 页；自由地图种子、轮流安置港口 |
| 双扩展组合 | `heading-for-new-shores`／驶向新海岸 | 35／92／126 | 42／108／149 | 航海家第 4–5 页、城市与骑士第 12 页；当前新局随机地形、点数与港口 |
| 双扩展组合 | `through-the-desert`／穿越沙漠 | 35／92／126 | 42／108／149 | 航海家第 10–11 页、城市与骑士第 12 页；保留海陆轮廓与特殊区域 |

三个航海家剧本并列最大，建议性能最大地图选四人 `wonders-of-catan`，另用拥挤的四人组合地图覆盖骑士、城墙、大都会和蛮族船。最大组合地图为 42 格。性能局面具体状态与镜头路线以 [U0 基线](U0-baseline.md) 为准。

`CombinedScenarios.Ids` 仅包含表中两个剧本。其他七个航海家剧本、5–6 人扩充和收藏版特殊规则不因主题开发而成为组合模式支持项。航海家额外随机地形变体仍按 [M3 范围](../../M3_SCENARIOS.md) 登记；组合的新局随机化及旧固定存档兼容按 [M5 范围](../../M5_SCENARIOS.md) 登记。

规则 PDF 的实际本地目录为 `.local/reference-cache/official-2025/v001/`；本文引用页码均从 PDF 第 1 页起计。参考入口见 [本地规则索引](../../../assets/references/rules/README.md)。

## 三维资产与材质入口

下列路径均相对 `Unity/Catan/Assets/`。迁移状态全部为「已定位，尚未接入主题」，U0 不替换运行资产。

| 编号 | 入口 | 已核对现状 | U1 实施动作与可观察验证 |
| --- | --- | --- | --- |
| A01 | `Scripts/EntityBoardAssets.cs` | 地形混用 `PhotoSamples/v001/forest`、`EntityAssets/v001/tiles/*`、`EntityAssets/v006/ore-tile/tile-ore`；船、强盗、海盗、商人、蛮族轨道与船直接使用 v001；港口与分数标记使用 v005 | 在统一主题定义中列出全部公开资产 ID；正式棋子、外圈海格、预览均由同一解析入口选择。缺失 ultra 资产须有开发占位与诊断，不静默混入旧主题 |
| A02 | `Scripts/BoardRenderer.cs` | 基础版地形、村／城／路、强盗、数字、港口；资产缺失时生成六边平板或几何棋子 | 保持 `Tile/Vertex/Edge` 坐标、根名称及拾取位置；将占位与颜色策略归入主题，验证基本建造与取消 |
| A03 | `Scripts/M3BoardRenderer.cs`、`Scripts/M3App.cs` | 航海家另有船、海盗、战舰球形旗；迷雾为通用灰色六边形；公开地形／数字变化时 `M3App.RefreshBoard` 重建整个棋盘 | 检查迷雾揭示、港口移动和重建时主题仍一致；只从最新人类投影创建对象；重建与切换均保存相机位置、缩放、俯视状态 |
| A04 | `Scripts/M4BoardRenderer.cs` | 城墙、骑士等级与活跃状态、大都会、商人、公开标记根和蛮族轨道；部分组合建筑回退为程序几何 | 每种颜色及等级／状态纳入同一主题；保持休眠／活跃、城市带墙／不带墙与大都会识别，不借主题改变状态 |
| A05 | `Scripts/M5BoardRenderer.cs` | 组合同时有船、海盗和全部城市骑士组件；蛮族船以公开位置更新 | 四种组合配置逐一切换；首次蛮族攻击前海盗未入场、进入边框、移至海格等状态分别检查 |
| A06 | `Scripts/PlayerComponentAssets.cs` | 红色村／城／墙／大都会来自不同旧目录；其他颜色来自 v002；骑士为 4 色 × 3 级 × 2 状态。`Models`、`Plastics` 为静态缓存，当前没有主题释放入口 | 模型缓存键包含主题与资产版本，定义释放时机；红色复合建筑与其他颜色整体模型均覆盖；20 次切换后核对对象、材质和缓存占用 |
| A07 | `Scripts/PhotoSampleAssets.cs` | 道路仍从 `PhotoSamples/v001/road` 单独加载、按席位改色；旧村庄和美术画廊亦可直接调用；静态 `Models` 缓存 | 将对局道路与旧入口转接统一主题解析；历史画廊明确固定真实桌游，不影响原件复核 |
| A08 | `Scripts/TabletopPhysicalTileV005.cs` | `EntityBoardAssets.Terrain` 当前无条件调用厚板重建；依赖 8 mm 印刷顶面、六角 UV 与边材质，生成新网格和三个实例材质 | 仅真实桌游运行该后处理；ultra 真实网格必须绕过。比较原主题前后截图，核对旧网格和实例材质能销毁 |
| A09 | `PlayerComponentAssets.MatchBuildingPlastic`、`PhotoSampleAssets.Piece`、`EntityBoardAssets.Ship` | 按材质名 `Plastic` 替换村庄塑料；先清除 PropertyBlock。该假设适用于现有实体模型 | 将塑料修补限定于真实桌游；ultra 自己声明玩家色材质或渲染属性，避免船帆、金属、木构被替换 |
| A10 | `Scripts/TabletopPieceCache.cs` | 以公开位置及外观字符串增量缓存；没有全量清理 API；当前外观键不含主题 | 切换时显式清理缓存或销毁其所属棋盘并重建；不能只改全局主题后期待 unchanged 分支生成新模型 |
| A11 | `Scripts/TabletopScene.cs` | 烟熏橡木贴图、程序海面边框、三色环境光、方向光、独立 HUD 相机；全局修改 `RenderSettings` 和首个方向光 | 主题拥有桌面／海面／光照配置，保存与恢复相机、曝光相关状态；回退后不残留 ultra 光照、相机或音源 |
| A12 | `Scripts/TabletopNumberTokens.cs`、`Scripts/TabletopReferenceMarkers.cs` | 数字正面来自 `TabletopUI/v008/NumberTokens`，背面来自 v005；港口、卡坦分及公开港口旁装饰海格有独立创建路径 | 保留号码／贸易比例与实际公共分数；数字高度、禁布区作为统一锚点数据，装饰海格也按主题创建；不得覆盖 `fog` 格 |
| A13 | `Scripts/TabletopWorldObjects.cs` | HUD 棋子库直接解析船、骑士、建筑，硬编码道路路径；还生成骰子、卡纸、城市设施和纸面接触阴影 | 对局棋盘、玩家供应区与拖拽物保持同一主题；销毁 HUD 对象／生成网格／纹理／材质后可重建，不能只重建棋盘 |
| A14 | `Scripts/OperationPieceAssets.cs` | 2D 操作缩略图直接加载 `OperationUI/v002/pieces/*`，3D 鼠标预览调用多种资产入口；合法／非法 tint 用 PropertyBlock | 缩略图与 3D 预览一并主题化；切换取消未提交拿起态，恢复输入后仍能区分合法／非法，不提交新指令 |
| A15 | `Scripts/M6BuildPresentation.cs`、`TabletopShipSelection.cs` | 建造／升级／移动演出从已提交棋子复制模型并缓存，临时隐藏正式 Renderer；高亮使用 `TabletopUI/v012/BuildHighlight` | 切换前收尾飞行、高亮、旧模型副本与船选中态；恢复正式 Renderer，再对最新公开状态生成新主题，不能重复提交建造 |
| A16 | `Scripts/M3App.cs` 的特殊标记绘制 | 村庄／布匹、堡垒／滩头／西行起点、赠卡边、奇观目前主要为文字标记；战舰标记在 M3 Renderer 内生成 | U1 保留功能文字；U7 新三维特殊组件逐项映射，不把这些文字或球形旗误记为已完成的成品模型 |
| A17 | `Scripts/MenuMapPreviews.cs`、`PastoralMenuScreen.cs`、`M6PauseMenu.cs` | 地图示意图固定加载 `MenuPreviews/v001`；目前菜单背景选择是另一个概念，没有棋盘双主题偏好 | 新增准确主题名称、独立本地偏好与主题预览；首次保留真实桌游。地图预览仍只显示公开示意，旧存档不增加权威主题字段 |

坐标基线为 `x = X × √3 × 0.02 m`、`z = -Y × 0.02 m`，棋子基准面 `y = 0.008 m`。这些沿用现有估算尺度，不是实测实体尺寸。ultra 提高视觉高度时，保持规则锚点及交互碰撞不变，摇曳只作用于视觉子节点。

## 当前渲染配置

`Editor/M6Build.cs` 选择平台管线；资产存在不等于效果已启用，以下核对的是构建配置与序列化资源，不是 GPU 实测结论。

| 配置 | Windows 真实桌游 | Android 真实桌游 |
| --- | --- | --- |
| 管线 | `TabletopPipeline-v005.asset` | `TabletopPipeline-Android-v001.asset` |
| MSAA／渲染比例 | 4 倍／1.0 | 2 倍／1.0 |
| HDR | 开 | 关 |
| 主光阴影图／距离 | 4096／4 m | 1024／4 m |
| 软阴影 | 支持 | 关闭 |
| SSAO | `TabletopRenderer-v005.asset` 中启用；强度 1.1、半径 0.007 m、全分辨率 | `AndroidBuildConfiguration` 关闭 Renderer Features |
| SRP Batcher／Dynamic Batching | 开／关 | 开／关 |
| 深度／不透明纹理管线开关 | 均关；SSAO 所需的渲染通道另由其 Renderer Feature 处理 | 均关 |
| 后处理 | `M6.unity` 及 M1–M5 场景相机 `m_RenderPostProcessing: 0` | 同一游戏场景构建，未发现运行代码启用该开关 |

`DefaultVolumeProfile.asset` 存有 Bloom、ColorAdjustments、DepthOfField、Tonemapping 配置，但不能据此声称画面正在执行这些效果。桌面 `ConfigureLighting` 使用强度 1.22 的暖方向光及三色环境光；纸牌接触阴影和牌面薄光层是自定义生成效果，需与屏幕后处理分开统计。

可验证的成本关注点是：每地块厚板网格／材质实例、港口附加海格、HUD 卡纸网格、20 次切换后的静态纹理与模型引用、全分辨率 SSAO、4K 主光阴影，以及迷雾揭示导致的棋盘重建。它们仅是待采样的候选成本来源，未经过 Profiler／Frame Debugger 前不标为主要瓶颈。

## 牌面直接入口

逐牌数量、规则文字与来源以 `art/recipes/ultra/v001/cards/card-manifest.json` 为准。这里列出接入时必须覆盖的路径，避免只替换手牌：

| 路径 | 实际展示内容与迁移动作 |
| --- | --- |
| `EntityCardAssets.cs` | 资源／商品、25 种进步牌、城市改良正反面、两类提示与保卫者；集中改为稳定牌种 ID → 主题正面／合法牌背。`Textures` 为静态缓存，主题切换需定义释放策略 |
| `M2App.cs`、`M3App.cs`、`M4App.cs`、`M5App.cs` | 旧规则包界面的资源、进步牌、改良与保卫者；`EntityCardAssets.Display` 每帧重新确认可见牌并提供放大／翻面，不可绕过授权登记 |
| `OperationDock.cs` | 手牌、选牌详情、拖动资源、交易、城市改良；基础发展牌在部分入口仍是文本／程序纸面，不能统计为已有完整照片牌面 |
| `TabletopBankUI.cs` | 银行牌堆、进步牌背、产出／抽牌飞行；`DevelopmentBack()` 是程序生成占位，原文明确没有发展牌背原照；他人飞牌必须保留正确类别牌背 |
| `M6CardPresentation.cs` | 出牌／即时分值展示、网络事件等待与排队；进步牌／保卫者使用真实纹理，其他发展牌走文本卡片。切换时保持已提交事件顺序，不重复动作 |
| `TabletopHistoryUI.cs` | 已获准公开的历史牌面；保持历史投影的可见性判断 |
| `TabletopResourceChoiceUI.cs`、`ModernOperationUI.cs`、`OperationIconAssets.cs` | 资源／商品选择与费用缩略图；随主题解析，不重复维护独立牌种 |
| `TabletopUI.cs`、`StrategyHudArt.cs` | 对手手牌扇形／类别牌背；商品小图还直接加载 `TabletopUI/v010/{cloth,coin,paper}-cutout`，需要明确主题映射 |
| `TabletopWorldObjects.cs` | 预热资源牌与保卫者、三维牌堆和城市改良板；其卡纸材质、正反面与详情纹理也需回收 |
| `PhotoSampleAssets.cs`、`PhotoSampleGallery.cs`、`EntityAssetGallery.cs` | 旧照片样板和历史资产画廊直读；作为真实桌游来源复核工具保留，不能用画廊截图替代运行界面覆盖 |

隐藏卡映射只能接受当前人类获准看到的类别或已公开牌种，不从 AI 专用 `PlayerView` 获取额外信息，也不为选美术读取对手卡 ID。纹理缺失和加载占位不能回退到隐藏正面；隐私幕、换席、回菜单与主题切换均应重新验证详情授权。

## 原始参考登记

[原件校验清单](U0-reference-hashes.json) 实际逐文件计算了 192 个已存在图片的字节数与 SHA-256：`assets/references/inbox`、`originals`、`archives` 中的既有实体参考，以及 `art/source/menu-collector-v001` 中三个主菜单原件。重复存档按原路径分别登记，没有搬运、裁切、覆盖或删除任何原件。

本次检查 `assets/references`、`art/source`、`art/recipes`、`art/previews` 的目录、清单及与 ultra 相关的登记，**未发现已入库并注明用途的 ultra 专用原始参考图**。这是仓库可追溯原件缺口，不推断其他聊天或未登记目录不存在相关图片。

`art/source/menu-collector-v001/concept-original.png` 确实存在，SHA-256 为 `176fbe9055002cfb14d791e9fa78f51d4c3f90585e35dd3ebf5903648af864fa`；其说明明确是已确认的二维主菜单方向，不是 ultra 三维地形样板。本轮未将它复制或重新命名成 ultra 参考。

后续收到 ultra 原图时，登记到 `assets/references/ultra/v001`：原始文件路径／来源、原始字节校验、用途、已确认特征、实测尺寸或「估算」、背面及未知部分。全牌面缺少的原照和实现差异在逐牌清单中继续独立保留；生成插画不能替代规则文字来源或三维网格。

## U1 可执行顺序

1. 固定 `tabletop`／`ultra` 本地偏好及主题定义，先让全部公开模型、材质和贴图有唯一解析入口；默认真实桌游，ultra 使用明确开发占位。
2. 依 A01、A06–A09、A12–A14 接入资产；将旧厚板／塑料补丁限制在真实桌游，保持稳定坐标和现有输入验证入口。
3. 依 A02–A05 接入四类 Renderer；依 A10、A11、A15 实现统一销毁／重建／回退流程，取消本地预览并完成演出收尾，恢复最新可见状态、相机与待决界面。
4. 覆盖公开海盗边框／未入场、蛮族轨道全部步数、战舰、4 色 × 3 级 × 2 状态的骑士、组合建筑及装饰海格；覆盖表中 26 个配置的启动与切换冒烟检查。
5. 接入主题名称、预览与持久化；旧存档继续采用客户端偏好。以两个客户端分别选择不同主题验证同局、重连与重新开始。
6. 运行 20 次交替切换与加载失败回退，比较对象／音源／材质／纹理缓存及内存稳定区间；在交易、弃牌、强盗选择、卡牌演出和网络状态更新中验证，不以静态菜单切换替代。
7. 重跑 U0 四局面和固定镜头路线；记录图像、帧时间与内存差异。Android 真机、实际多机、U2 美术方向仍按各自证据验收。

本文只完成现状盘点。进入 U1 前仍须以 U0 基线记录确认可重复测量条件、预算和主要瓶颈；缺少真机测量时不得写成 Android 性能通过。

## 夹具独立复核

2026-10-21，独立读取 `.local/ultra/u0-fixtures` 四份输出，以对应规则库恢复，检查 `AssertInvariants()`、再次保存与原权威字符串逐字节相同、P1 人类 `Board` 没有隐藏牌／牌库／随机状态字段。四项均通过；不在本文或清单中保存权威内容。此检查不替代真实游戏性能采样。

| 局面 | 格／点／边 | 主棋子数 | 其他公开组件 | 采样状态 |
| --- | --- | --- | --- | --- |
| 普通 | 19／54／72 | 16 | 无城墙／大都会 | `ProductionAwaitRoll`，无待决／交易 |
| 最大 | 49／124／172 | 16 | 卡坦奇观公开标记 | `ProductionAwaitRoll`，无待决／交易 |
| 拥挤 | 42／108／149 | 59 | 另有 3 城墙、1 大都会；主棋子包含 14 村、5 城、15 路、21 船、4 骑士 | `Action`，无待决／交易 |
| 迷雾 | 42／108／149 | 16 | 12 个未揭示格仍为通用 `fog` | `ProductionAwaitRoll`，无待决／交易 |

这里的「主棋子」只合计村、城、路、船和骑士；不代表完整 Renderer、材质或模型实例总数。普通局在 M6 兼容信封里的 `ScenarioId=heading-for-new-shores` 是旧配置占位，实际规则包为基础版、地图为 `standard`，未把航海家剧本混入普通地图。
