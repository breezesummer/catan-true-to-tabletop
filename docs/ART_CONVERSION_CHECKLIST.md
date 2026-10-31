# 游戏实体美术资源转化清单

更新日期：2026-10-31（北京时间）；新增商人扩展原照批次，当前矿石地形仍为高山原照 v006。

范围：基础版、航海家、城市与骑士及已支持的组合。v005 的 143 张不重复实体照片统计为历史基线；2026-10-15 另补高山原照，分类归档副本不重复计数。原件只读保留。每一类型派发独立子任务，源文件、配方、贴图、导出、预览和验证齐全后更新本清单。

2026-10-31新增11张商人扩展照片，按用户要求重命名但不改变内容。六V形渔港、湖泊、臭靴及一／二／三鱼筹码已完成原像素校正、Blender MCP建模、干净目录重建、FBX回读与Unity正式入口替换；四模式截图见[本轮验收](verification/fishing-photo-v002/验收记录.md)，来源映射、制作脚本与重建检查见[原照配方](../art/recipes/harbor-fishing-v002/README.md)。本轮未获得实测尺寸或用户外观确认，不扩大下方历史143张的完成统计。

用户于 2026-10-09 选择**原照片校正贴图、尽量保留原版插画与文字**。新增平面资产只做几何校正，保留原照片像素、照明和模糊；不以照片中的规则文字改变官方规则。既有森林、木头卡、村庄、道路样板按用户说明保留，前两项仍是旧版 AI 清理候选。

勾选表示照片转化交付通过 Blender 回读、干净目录重建及四视图输出；Unity 导入另列。正式游戏接入代码与运行验证单独记录在下方 v003，不能把图库导入视为整局验收。全部尺寸仍是估算；用户外观确认、实测比例、隐藏面补充与长期性能待验。本清单不表示 M8 已满足退出条件。

## 已完成的实验样板

| 进度 | 类型 | 已有产物 | 验证 / 待确认 |
| --- | --- | --- | --- |
| [x] | 森林板块 | 印刷顶面＋六边形网格 | 既有样板工程验证；AI 清理候选、用户外观待确认 |
| [x] | 木头资源卡 | 正反贴图＋双面卡网格 | 既有样板工程验证；AI 清理候选、用户外观待确认 |
| [x] | 村庄 | 参数化模型 | 既有样板工程验证；单照近似、未知隐藏面 |
| [x] | 道路 | 参数化模型 | 既有样板工程验证；单照近似、未知底面 |

产物与入口：[第一批样板说明](PHOTO_SAMPLES_V001.md)。

## 本轮分类型流水线

| 进度 | 类型 / 子任务 ID | 内容与变体 | 照片数 | 产物和证据 | Unity 导入 |
| --- | --- | --- | --- | --- | --- |
| [x] | 地图板块（其余六类） `tiles` | 草地、海洋、金矿、沙漠、小麦、丘陵；六边形纸板网格＋校正顶面 | 6 | [配方与说明](../art/recipes/entity-conversion-v001/tiles/README.md)；[回读](../art/exports/entity-conversion-v001/tiles/verification.json) | 已验证 |
| [x] | 资源卡（其余四类） `resource-cards` | 羊毛、砖头、小麦、矿石；四张双面卡＋原照片资源背面 | 4 | [配方与说明](../art/recipes/entity-conversion-v001/resource-cards/README.md)；[回读](../art/exports/entity-conversion-v001/resource-cards/verification.json) | 已验证 |
| [x] | 商品卡 `commodity-cards` | 布匹、纸张、银币；背面缺参考，保留中性底色 | 3 | [配方与说明](../art/recipes/entity-conversion-v001/commodity-cards/README.md)；[回读](../art/exports/entity-conversion-v001/commodity-cards/verification.json) | 已验证 |
| [x] | 贸易进步卡（黄色） `progress-trade` | 六种正面＋黄色共同背面 | 7 | [配方与说明](../art/recipes/entity-conversion-v001/progress-trade/README.md)；[回读](../art/exports/entity-conversion-v001/progress-trade/verification.json) | 已验证 |
| [x] | 科学进步卡（绿色） `progress-science` | 十种正面＋绿色共同背面 | 11 | [配方与说明](../art/recipes/entity-conversion-v001/progress-science/README.md)；[回读](../art/exports/entity-conversion-v001/progress-science/verification.json) | 已验证 |
| [x] | 政治进步卡（蓝色） `progress-politics` | 九种冻结正面＋原照片蓝色共同背面（v003） | 10 | [配方与说明](../art/recipes/entity-conversion-v003/progress-politics/README.md)；[回读](../art/exports/entity-conversion-v003/progress-politics/verification.json) | 已验证 |
| [x] | 城市设施与升级提示板 `city-improvements` | 三轨道×五等级×正反面，另两张提示板照片 | 32 | [配方与说明](../art/recipes/entity-conversion-v001/city-improvements/README.md)；[回读](../art/exports/entity-conversion-v001/city-improvements/verification.json) | 已验证 |
| [x] | 卡坦保卫者奖励卡 `defender-card` | 一张正面；背面缺参考 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/defender-card/README.md)；[回读](../art/exports/entity-conversion-v001/defender-card/verification.json) | 已验证 |
| [x] | 蛮族入侵轨道板 `barbarian-track` | 文件名“海盗入侵卡”；显式登记为蛮族轨道，背面未知 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/barbarian-track/README.md)；[回读](../art/exports/entity-conversion-v001/barbarian-track/verification.json) | 已验证 |
| [x] | 玩家船 `ships` | 红、蓝、橘、米色；船体、桅杆、弧帆、后舱 | 4 | [配方与说明](../art/recipes/entity-conversion-v001/ships/README.md)；[回读](../art/exports/entity-conversion-v001/ships/verification.json) | 已验证 |
| [x] | 城市 `city` | 红色城市，底座、屋群与屋顶 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/city/README.md)；[回读](../art/exports/entity-conversion-v001/city/verification.json) | 已验证 |
| [x] | 城墙 `city-wall` | 由城市＋城墙、大都会＋城墙组合照片估算独立城墙，隐藏面未知 | 2 | [配方与说明](../art/recipes/entity-conversion-v001/city-wall/README.md)；[回读](../art/exports/entity-conversion-v001/city-wall/verification.json) | 已验证 |
| [x] | 大都会 `metropolis` | 红色大都会；三轨道身份标记尚无照片 | 2 | [配方与说明](../art/recipes/entity-conversion-v001/metropolis/README.md)；[回读](../art/exports/entity-conversion-v001/metropolis/verification.json) | 已验证 |
| [x] | 骑士 `knights` | 三级×有无头盔，共六种外观；外观状态不代替规则状态 | 6 | [配方与说明](../art/recipes/entity-conversion-v001/knights/README.md)；[回读](../art/exports/entity-conversion-v001/knights/verification.json) | 已验证 |
| [x] | 强盗 `robber` | 两张照片是同一黑色雕塑的不同视角 | 2 | [配方与说明](../art/recipes/entity-conversion-v001/robber/README.md)；[回读](../art/exports/entity-conversion-v001/robber/verification.json) | 已验证 |
| [x] | 海盗 `pirate` | 海洋板块上的黑色海盗船 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/pirate/README.md)；[回读](../art/exports/entity-conversion-v001/pirate/verification.json) | 已验证 |
| [x] | 蛮族入侵船 `barbarian-ship` | 文件名“海盗（入侵卡上的单位）”；显式映射为蛮族船 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/barbarian-ship/README.md)；[回读](../art/exports/entity-conversion-v001/barbarian-ship/verification.json) | 已验证 |
| [x] | 商人 `merchant` | 中性色商人雕塑，单视角近似 | 1 | [配方与说明](../art/recipes/entity-conversion-v001/merchant/README.md)；[回读](../art/exports/entity-conversion-v001/merchant/verification.json) | 已验证 |

当前类型进度：**18/18 类已转化，74 件资产；18/18 类 Unity 导入验证通过**。政治卡使用 v003 替代版；已有四类实验样板与三色 v002 另列，数量按唯一资产 ID 去重。

## 每类转化的交付要求（复用模板）

- [ ] 登记所有来源、SHA-256、可见特征、未知表面与估算尺寸。
- [ ] 平面照片记录四角/六角和校正尺寸；棋子记录参数化脚本与固定种子。
- [ ] 保存 `.blend`、显式 `.fbx`、PNG 依赖、材质和导出清单。
- [ ] 回读检查实际网格、底心轴心、尺寸、UV、闭合性、非退化三角形与来源完整性。
- [ ] 输出俯视、侧视、游戏相机、拥挤节点四种预览；人工看图并记录限制。
- [ ] 从干净目录重建并比较几何、材质、贴图；Unity 校验尺寸、材质与预制体。
- [ ] 更新本清单对应类型，用户确认独立记录；不把位图当作立体网格。

## 缺少参考、后续需要补齐

| 范围 | 待补项目 | 当前处理 |
| --- | --- | --- |
| 共通 | 实体版本、全桌布局、带尺尺寸、棋子顶侧背底面 | 尺寸标估算；单照隐藏形状标近似 |
| 玩家变体与实例 | 道路完整配色、多视角与实测颜色；整局实例数量和摆放 | 三色村庄、城市、城墙、大都会与骑士追加批次见下方 v002；照片白平衡与最终外观仍待确认 |
| 基础版 | 数字盘正面/字母；海框；两枚产出骰；基础发展卡五类型及背面；最长道路/最大军队卡；建造费用参考卡 | 六类港口和船图案数字背面已补转 v005；高山实物照片已提供并替换矿石地形为 v006，其余缺项维持规则显示 |
| 航海家 | 布匹筹码；中立村庄；海盗要塞；战船标记；五张奇迹卡与等级标记；迷雾背面；卡坦标记未知另一面 | 卡坦分标记已补转 v005；其余缺项不伪造照片来源 |
| 城市与骑士 | 商品/保卫者卡背面；事件骰；三种大都会身份标记；独立城墙多视图 | 蓝色进步卡背面已提供并补转 v003；其余缺背面使用明确中性底色 |

## 路径与运行

- 来源索引：[目录清点与任务定义](../art/recipes/entity-conversion-v001/catalog.json)。
- 本轮结果：[交付与验证说明](ART_CONVERSION_V001.md)；[跨类型审计](verification/entity-conversion-v001/pipeline-audit.json)。
- 当前全量清点：[135 张原照来源与资产审计](../art/recipes/entity-conversion-v003/progress-politics/photo-coverage-audit.json)。
- 通用生成与验证：`tools/art/Build-EntityAssets.ps1 -Type <类型ID>`。
- 校正：`tools/art/rectify_entity_photos.py --recipe <recipe.json>`。
- 版本根目录：`art/{recipes,source,exports,previews}/entity-conversion-v001/<类型ID>/`。
- Unity 导入：`tools/development/Build-EntityAssets.ps1`；查看：`tools/development/Play-EntityAssets.ps1`。
- 独立 Windows 检查：`tools/validation/Test-EntityAssets.ps1`；逐资产加载、材质检查与俯视截图，每类代表件另输出四视图及背面。
- Blender 的 `crowded-vertex` 是类型总览候选；实际相邻板块/道路/建筑环境由 Unity 查看程序另行提供，不能把陈列图当作节点拾取验收。
- 每类完成后运行 `tools/art/update_entity_checklist.py`；进度由实际证据生成，可安全重复执行。

类型表的照片数按任务引用计数，同一组合照片可被城墙与大都会共同引用。v001 原件目录为历史 98 张，v002 追加 36 张，v003 补登记此前遗漏的蓝色共同背面 1 张；当前总数读取实际 inbox 与全量审计，不重复计备份。


## 三色玩家组件追加批次 v002

2026-10-10 分类的橙色、米色、蓝色照片共 36 张，原文件名备份与 SHA-256 见[归档记录](../assets/references/catalogs/player-components-v001.json)。每色 6 种骑士外观、6 种建筑及城墙组合。旧版 v001 产物保留；本批次另存 v002，照片只作三维建模参考。

| 进度 | 类型 | 资产数 | 证据 | Unity |
| --- | --- | --- | --- | --- |
| [x] | 三色骑士 `player-knights` | 18 | [配方](../art/recipes/entity-conversion-v002/player-knights/README.md)；[回读](../art/exports/entity-conversion-v002/player-knights/verification.json) | 已验证 |
| [x] | 三色建筑与城墙组合 `player-buildings` | 18 | [配方](../art/recipes/entity-conversion-v002/player-buildings/README.md)；[回读](../art/exports/entity-conversion-v002/player-buildings/verification.json) | 已验证 |

v002 公开视图渲染入口：已有验证通过记录。村庄、城市、城墙、大都会和骑士按玩家席位选择资产；规则与保存格式不变。本轮全资源正式运行状态见 v003；最终外观、实测尺寸及长局性能仍待验。[追加批次说明](ART_CONVERSION_V002.md)。


## 蓝色政治进步卡背面补转 v003

遗漏的 `assets/references/inbox/发展卡/蓝色发展卡/卡背.jpg` 已在[新增来源登记](../assets/references/catalogs/blue-politics-back-v001.json)记录，原照片字节保留。九件卡的正面沿用 v001 冻结校正图，背面新增原像素几何校正；旧版产物保留。

| 转化 | 件数 | 证据 | Unity 导入 |
| --- | --- | --- | --- |
| [x] | 9 | [配方](../art/recipes/entity-conversion-v003/progress-politics/README.md)；[回读](../art/exports/entity-conversion-v003/progress-politics/verification.json)；[干净重建](../art/exports/entity-conversion-v003/progress-politics/clean-rebuild-verification.json) | 已验证 |

商品卡和保卫者卡背面仍缺参考，使用中性底色；蓝色政治卡共同背面已经补齐，不再列为缺参考。


## 全部现有照片的正式游戏接入 v003

**正式运行验证通过**。正式运行状态仅在 [显示入口记录](verification/entity-integration-v003/runtime-integration.json)与[M6 独立进程记录](verification/entity-integration-v003/m6-smoke.json)两者 `status=passed` 时更新。图库导入、照片覆盖和最终用户外观确认分别记录。

| 验收层次 | 当前结果 |
| --- | --- |
| 原照转化来源 | 135/135 张不重复原照进入导出来源；当前原照哈希审计通过 |
| 资产数量 | 115 个唯一资产 ID，124 个保留版本 FBX；v003 九件政治卡替代旧版，不能重复计为新类型 |
| Unity 导入与图库 | v001、v002 既有记录分批保留；v003 新政治卡导入独立列证，不将导入或图库视为整局验收 |
| 正式游戏运行 | 正式运行验证通过；范围为当前 M6 中基础版、航海家、城市与骑士和已支持组合 |
| 最终用户外观 / M8 | 待确认；尺寸、隐藏面、色彩、节点操作、长期性能和发布条件仍未全部验收 |

棋盘已接入八种有地块照片的板块；2026-10-15 高山照片 v006 替换了原先从矿石资源卡裁切的 v004 顶图，见文末。资源卡显示于本人手牌面板；城市与骑士及组合增加商品、进步卡、城市设施和公开奖励卡面。悬停放大、点击固定查看、已有背面翻面；私有卡面限定当前 PlayerView，换座或隐私幕清空。船、强盗、海盗、商人和蛮族轨道/船使用实体参考模型。八种照片板块是有厚度的印刷板块网格，不代表立体景观。

查看与重建：`tools/development/Play-M6.ps1`；`tools/development/Build-EntityIntegration.ps1`；`tools/validation/Test-EntityIntegration.ps1`。当前 M6 是本轮正式入口，旧独立阶段程序和图库记录按原批次保留。[接入说明与验证范围](ART_INTEGRATION_V003.md)。


## 铁矿卡片插画派生板块 v004（2026-10-11，历史记录；已由 v006 替换）

铁矿棋盘显示改为矿石资源卡插画派生的平坦厚板。来源是现有 `资源牌/铁矿石.jpg`，经已校正的完整资源卡面裁切 `[112,392,912,1192]`，再缩放为 1024×1024 顶图，映射到 8 mm 六边形网格。没有收到真实山地六边形照片；原照与完整手牌卡面保持不变，不把派生插画声称为山地实物复刻。

| 转化与干净重建 | Unity 导入 | 四类正式显示入口 |
| --- | --- | --- |
| 已验证 | 已验证 | 已验证 |

[配方与复现](../art/recipes/entity-conversion-v004/ore-tile/README.md)；[回读](../art/exports/entity-conversion-v004/ore-tile/verification.json)；[干净重建](../art/exports/entity-conversion-v004/ore-tile/clean-rebuild-verification.json)；[Unity 导入](verification/ore-tile-v004/unity-import.json)；[正式显示](verification/ore-tile-v004/runtime-integration.json)；[人工目检](verification/ore-tile-v004/visual-review.json)。

运行通过要求 `oreUsesFlatCardArt=true`，且基础、航海家、城市骑士、组合四类各含俯视与游戏相机证据。本轮仅改外观，不重复宣称规则测试或完整 M6 保存恢复验收；最终外观与 M8 退出条件仍待确认。[本轮说明](ART_ORE_TILE_V004.md)。

## 实体港口、数字背面与卡坦分标记 v005（2026-10-14）

未分类目录的八张原照已分类复制归档，原位字节保留。八个来源哈希与旧批次互不重复；当前总数 **143 张照片、123 个唯一资产 ID、132 个保留版本 FBX**。上方 v003／v004 的数量为历史统计，不包含本批次。

| 类型 | 件数／照片数 | 转化、回读、干净重建 | Unity 与正式游戏 |
| --- | --- | --- | --- |
| 通用 3:1 与五资源 2:1 港口 | 6／6 | 已通过，异形闭合厚纸板 | 四模式已接入，沿岸朝向、建筑占地与稳定 ID 更新通过 |
| 点数牌船图案背面 | 1／1 | 已通过，圆片网格与原像素贴图 | 当前数字圆牌新增底面，正面仍由规则数字决定 |
| 卡坦分标记 | 1／1 | 已通过，圆片网格与原像素贴图 | 航海家礼港／奖励边及 M3／M5 公开奖励计数通过 |

[分类索引](../assets/references/catalogs/tabletop-markers-v005.json)；[分类原件](../assets/references/archives/tabletop-markers-v005/classification.json)；[配方](../art/recipes/entity-conversion-v005/tabletop-markers/README.md)；[回读](../art/exports/entity-conversion-v005/tabletop-markers/verification.json)；[干净重建](../art/exports/entity-conversion-v005/tabletop-markers/clean-rebuild-verification.json)；[Unity 导入](verification/tabletop-markers-v005/unity-import.json)；[运行摘要](verification/tabletop-markers-v005/summary.json)。

本轮 151 项相关 .NET 检查、八件 Blender／Unity 验证、独立四模式保存恢复、1,903 项专项图形检查和既有桌面 28 配置／20,060 项检查通过。相机图和源模型细节已查看，完整 IMGUI 自动像素验收仍为 false；港口真实鼠标点击、用户整局、最终外观、实测尺度与长期性能仍待验。M7／M8 状态保留。当前入口：[CatanM6.exe](../.local/m6-entity-v005/Builds/Windows/CatanM6.exe)。

## 高山地图板块 v006（2026-10-15，当前矿石地形）

用户已补充真实高山六边形板块照片，现替换 v004 矿石资源卡插画派生地形。原照只做几何校正，保留山峰插画和黄色边框；完整资源手牌继续使用资源牌照片。沿用估算厚板尺度、底心轴心、米制单位及运行时倒角。

Blender 回读与干净重建、Unity 导入、Windows 构建、四模式俯视／游戏相机八张渲染图、矿石手牌来源隔离、预览／碰撞与独立保存恢复均通过；相关 .NET 112 项通过。用户最终外观、实物测量、未知背面及长期性能待验，M7／M8 状态保留。

[原件归档](../assets/references/archives/mountain-tile-v006/README.md)；[配方与复现](../art/recipes/entity-conversion-v006/ore-tile/README.md)；[回读](../art/exports/entity-conversion-v006/ore-tile/verification.json)；[干净重建](../art/exports/entity-conversion-v006/ore-tile/clean-rebuild-verification.json)；[Unity 导入](verification/mountain-tile-v006/unity-import.json)；[正式显示](verification/mountain-tile-v006/runtime-integration.json)；[本轮说明](ART_MOUNTAIN_TILE_V006.md)；[验证摘要](verification/mountain-tile-v006/summary.json)。

当前入口：[CatanM6.exe](../.local/m6-mountain-v006/Builds/Windows/CatanM6.exe)。上方各旧批次的数量与程序链接属于历史记录。
