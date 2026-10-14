# 卡坦岛 PC 项目

2026-10-14 当前版本为 [策略游戏 HUD 升级 v010](docs/UI_STRATEGY_V010.md)：高角度地图、完整港口海格、地图独立缩放/拖拽、三套主题、五圆片与三面骰盘、四玩家统计、单建筑模型、手牌页签与整理、骑士右键和船直接改道、所属港口兑换、右侧可收起历史。沿用当前优化 AI，规则与权威格式保持。

当前入口为 [CatanM6.exe](.local/m6-ui-v010/Builds/Windows/CatanM6.exe)，两个启动脚本统一本版；分享用 [UI升级版完整压缩包](.local/m6-ui-v010/release/卡坦岛之三国争霸UI升级版v010.zip)。.NET去重覆盖 **792 项通过**（保存探针加载项目环境后单项复验），最终Unity构建、四模式独立保存恢复、无图形/图形各28配置通过。实际查看两种分辨率布局、设施弹窗、海洋主题和地图独立缩放；完整鼠标操作全链、用户整局、全分辨率与最终外观待验，M7/M8不变。见[验证摘要](docs/verification/ui-strategy-v010/summary.json)。以下入口与统计均为历史记录。


2026-10-14 当前版本为 [实体港口与标记接入 v005](docs/ART_TABLETOP_MARKERS_V005.md)：未分类目录的八张照片已分类归档、原像素校正并制成真实厚纸板资产。六类实体港口接入四种模式，航海家礼港／航路奖励与公开奖励筹码随局面更新；点数牌新增船图案照片底面，正面数字保持规则数据。当前入口为 [CatanM6.exe](.local/m6-entity-v005/Builds/Windows/CatanM6.exe)，两个启动脚本统一使用本版和当前优化 AI。

151 项相关 .NET 检查、八件 Blender 回读／干净重建与 Unity 导入、固定 Windows 构建、四模式独立保存恢复，以及 1,903 项新资产图形检查通过。照片总数为 143，唯一资产 ID 为 123，保留版本 FBX 为 132。尺寸仍是估算；完整鼠标点击、全分辨率、用户整局及最终外观待验，M7／M8 状态保留。详细范围见 [验证摘要](docs/verification/tabletop-markers-v005/summary.json)。以下入口与统计均为历史记录。

2026-10-14 历史版本为 [双扩展地图刷新修复 v001](docs/BUG_COMBINED_MAP_REFRESH_V001.md)：修复“双扩展组合”每次新局地图相同。“驶向新海岸”“穿越沙漠”各 3／4 人现在随机地形、点数与港口，保留岛屿轮廓、沙漠屏障和剧本区域；旧存档按原地图恢复。当前优化 AI 延续。

全量与收口复验去重覆盖 **781 项通过（含 52 个完整 AI 局）**；1,032 张地图种子扫描、固定 Unity 构建、四模式独立保存恢复、实际入口两组连续新局／旧档恢复及实际窗口检查通过。当前入口为 [CatanM6.exe](.local/m6-map-refresh-v001/Builds/Windows/CatanM6.exe)，或运行 `tools/development/Play-M6.ps1`。详细次数及未验边界见 [验证摘要](docs/verification/combined-map-refresh-v001/summary.json)。M7／M8 状态保留。以下为历史版本记录，旧程序／压缩包不含本次修复。


2026-10-14 当前版本为 [中央棋盘与整桌缩放 v009](docs/UI_TABLETOP_V009.md)，发行名称 **卡坦岛之三国争霸内测版1.1**。恢复同一相机下的中央大棋盘、备件和设施同步缩放；银行左上，蛮族轨道与航程右上，扩大历史左下；四方玩家与镜像排列，自己的船横放、城墙分开，设施缩小，特殊行动位于结束回合上方。删除桌边按钮，按 Esc 打开菜单。

本轮 **761/761 项测试通过（含 52 个完整 AI 局）**，固定 Unity 构建、四模式独立保存恢复、无图形/图形各 28 配置均通过，并完成实际窗口检查。当前入口为 [CatanM6.exe](.local/m6-ui-v009/Builds/Windows/CatanM6.exe)，或运行 `tools/development/Play-M6.ps1`。分享用 [内测版1.1压缩包](.local/m6-ui-v009/release/卡坦岛之三国争霸内测版1.1.zip)，请完整解压后运行；包内 166 文件已逐一校验。完整用户对局、所有分辨率和长期性能仍待验。M7/M8 不变，见 [验证摘要](docs/verification/tabletop-ui-v009/summary.json)。以下为历史记录，旧入口不代表当前版本。

2026-10-13 当前版本为 [固定分区、逐张手牌与胜利分数 v008](docs/UI_TABLETOP_V008.md)。棋盘、银行、备件与设施保留连续木桌及一致观察角度，地图缩放不侵入周边分区；分数详情留足中文行高；手牌仅轻抬、点击额外上推，同资源逐张选择并让邻牌散开；去除悬停导致的文字置灰；建造费用改为名称和资源图标；历史下方固定显示蛮族／防御状态；开局可设置胜利分数；骑士取消状态球，点数牌参与三维遮挡且沙漠不再加标牌。

已实际通过 **753/753 项全量测试、133/133 项收口专项测试，去重覆盖 764 项**；固定 Unity 构建、四模式独立保存恢复、无图形／图形各 28 配置检查通过。黄色背景与空白点数已修复，并实际检查斜视／俯视同桌布局、同类单张手牌、费用图标、分数文字和开局分数设置。全分辨率人工外观、完整用户对局及长期性能仍待验，详见[公开验证摘要](docs/verification/tabletop-ui-v008/summary.json)。全部当前入口统一使用优化 AI，不再维护原策略新版；默认启动目标为 [CatanM6.exe](.local/m6-ui-v008-final/Builds/Windows/CatanM6.exe)，也可双击根目录 `启动卡坦.cmd`。M7/M8 阶段不变。以下为历史记录，旧路径不代表当前入口。

2026-10-13 新增 [圆角卡纸、收牌插入与分数构成 v007](docs/UI_TABLETOP_V007.md)：卡牌恢复圆角、纸芯与柔影；手牌离底悬浮，悬停轻抬微摆；操作框去除拼接暗缝；银行飞牌到达后才插入手牌；分数悬停查看构成，对手仅显示明分；城市设施放大并使用真实厚板网格；地图 6／8 点改为亮红。**双击根目录 `启动卡坦.cmd` 启动优化 AI 新版**，程序位于 `.local/m6-ui-v007/Builds/Windows/CatanM6.exe`；`Play-M6.ps1` 保留原策略新版，位于 `.local/m6-ui-v007-original-ai/Builds/Windows/CatanM6.exe`。旧窗口需先保存退出再启动新版。

本轮 **721/721 项测试通过，含 52 个完整 AI 局**；两版 Unity 6000.3.25f1 构建、四类规则独立保存恢复及优化版无图形／图形各 28 配置检查通过。已在真实窗口查看圆角、悬停、本人／对手分数组成、操作框、设施和红点，并实际完成银行兑换，检查飞牌末段逐帧插入。连续帧记录早段存在采样间隔，不作为稳定帧率或整段动画性能验收；最终手感、全分辨率人工外观与长期性能仍待验。见 [公开验证摘要](docs/verification/tabletop-ui-v007/summary.json)。

2026-10-13 新增 [手牌屏幕比例修复 v002](docs/UI_CARD_ASPECT_FIX_V002.md)：修正宽屏窗口下的二次拉伸，资源／进步卡保持原图比例，发展卡文字卡面同步修正。87 项相关回归、两版 Unity 构建、四种窗口比例几何检查及两版独立保存恢复通过；实际查看 1920×1080 木头卡抬起和进步卡预览。**双击根目录 `启动卡坦.cmd` 启动优化 AI 修复版**；原策略仍使用 `Play-M6.ps1`。旧窗口请先保存退出后重开，直接打开旧目录 EXE 不会加载修复。本轮按用户要求不发送微信。

2026-10-13 已修复 [手牌比例回归](docs/UI_CARD_ASPECT_FIX_V001.md)：47 张卡牌贴图保留原图宽高比，解决 v006 手牌变细长及木头牌比例不同的问题。87 项界面相关回归、Unity 构建、28 配置集成及两份程序独立保存恢复通过；实际检查手牌、点击抬牌和进步卡预览。两个原启动脚本均已指向修复版，策略选择继续保留；运行中的旧窗口需重启生效。

2026-10-13 新增 [AI 策略优化 v002](docs/AI_STRATEGY_V002.md)：初设覆盖高频骰点并补齐资源，城市考虑木地产纸；登岛比较奖励、额外路船成本与剩余产出；保城采用最低有效兵力，排水渠、贸易和大都会按阶段判断，末盘优先直接获胜与低成本争奖。新 AI 程序已集成下述 v006 界面与广播交易，现含手牌比例修复，保存在 `.local/m6-ai-v002-card-ratio-v002`，使用独立入口启动；`Play-M6.ps1` 继续保留 v006 原策略版本。原规则与存档兼容，不把本轮策略实现视为用户已确认对战强度。范围和验证见策略说明。

集成版 **698/698 项测试通过**，包含 59 项策略案例和 52 个完整对局；Unity 构建、独立 Windows 保存恢复及 28 配置后台检查通过。已测程序的 166 个文件已逐一核对并复制到本项目目录；本轮未进行用户对战或胜率对比。证据见 [AI 集成验收摘要](docs/verification/ai-strategy-v002/summary.json)。

启动新 AI 与 v006 界面集成版：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-AiStrategy.ps1
```

单人主导、AI 执行代码与美术生产的 3D 收藏版桌游项目。目标包含基础版、航海家、城市与骑士及双扩展组合，尽可能还原实体桌游的观察、拿取、摆放和交易体验。

用户提供实体组件参考并进行玩法与视觉验收。先逐步完成本地规则，随后交付单人 AI 对战和好友联机。每个规则阶段都应能独立本地试玩。

2026-10-13 当前 M6 已加入 [清爽卡牌、银行与骰子统计 v006](docs/UI_TABLETOP_V006.md)：精简常驻文字，手牌仅在点击选中后抬起；移除额外卡套边框与装饰横线，各处卡面保持完整比例；银行改为资源／商品牌堆及朝上的发展／进步牌背，新增收牌动画；历史增加骰子投掷与 2–12 点次数、本局频率；玩家交易增加“向所有玩家报价”。最终 **639/639 项测试通过**（新增 19 项骰子统计、19 项广播交易和 8 项收牌动画测试，含 52 个完整 AI 局），固定 Unity 构建、无图形／图形各 28 配置及最终程序独立 Windows 保存恢复通过。已在 v006 实际窗口检查鼠标悬停不抬牌、点击抬牌、完整手牌／进步卡预览／交易卡面和银行牌堆、78 次骰子统计与玩家筛选、银行兑换、向所有玩家报价及撤回。完整飞牌轨迹、广播接受／拒绝人工链路仍待验；批处理完整 UI 像素标记仍为 `false`。实现提交 `0685421` 已推送并核对远端一致。启动脚本默认使用 v006，证据与限制见 [公开摘要](docs/verification/tabletop-ui-v006/summary.json)。本次验证使用隔离工作区和原 AI 策略基线，不包含另一聊天的策略优化。

2026-10-13 历史交付 [底部手牌、实体桌面与历史 v005](docs/UI_TABLETOP_V005.md)：放大的手牌固定悬浮在屏幕底部，超量自动重叠且不随棋盘缩放；交易、骑士与卡牌使用统一现代操作面板；左下滚动动态可展开历史并按玩家筛选。桌面采用按实物尺寸密度平铺的 2K 色彩／法线贴图，既有照片板块增加倒角厚边、光照与接触阴影；保留平坦印刷面，尚未制作立体地形雕塑。593/593 项测试（含 18 项历史专项及 52 个完整 AI 局）、固定 Unity 构建、无图形／图形各 28 配置和独立保存恢复通过；最终程序已实际检查手牌缩放、历史筛选、骑士入口、弹窗阻挡和卡牌重叠命中，本轮另完成银行兑换与进步卡操作。当时启动脚本默认使用 v005，完整证据见 [公开摘要](docs/verification/tabletop-ui-v005/summary.json)。用户本人整局试玩、最终外观、全部分辨率及长期性能仍待验。

历史交付：2026-10-12 的 [桌面与实体摆放 v004](docs/UI_TABLETOP_V004.md) 实现完整手牌整齐横排，事件／红／白骰按上／左／右三角摆放，右下角“结束回合”，三张完整建设卡内部点击升级，所有桌面组件随地图缩放，以及原生 8K 橡木贴图与原生窗口分辨率渲染。当时 575 项独立测试、固定 Unity 构建、28 种配置集成检查及独立保存恢复通过，并实际检查拿放、缩放、俯视和小翻板升级。当时默认使用 v004；本轮新需求已在 v005 调整本人手牌与桌面材质，原程序、资源和证据继续保留。

2026-10-12 历史交付 [整桌交互修复 v003](docs/UI_TABLETOP_V003.md)：手牌回到屏幕下方正确位置、高级橡木桌面、一行费用且缺料标红、单枚棋子选择、三骰点击转动，以及每条 1.5 秒的中央红字提示。当时 576 项独立测试、固定 Unity 构建、28 种配置集成检查和两个保存恢复进程通过，并实际用鼠标检查城市与骑士开局、手牌、费用和掷骰；完整用户试玩及最终外观仍单列待验。

2026-10-11 历史交付 [整桌交互 v002](docs/UI_TABLETOP_V002.md)：连续木桌与中央大地图、三侧对手牌背和备件、自己前方的散排棋子与扇形手牌。移除固定操作栏和手牌背景边界，支持棋子拖放、桌上骰子、银行与玩家交易，特殊选择按需展开。当时 557 项独立测试、固定 Unity 构建、28 种配置后台检查和独立保存恢复通过。该轮按用户要求全程后台运行，未操控电脑；完整 UI 像素和实际鼠标手感未在该轮验收。

2026-10-11 已完成 [操作 UI v001](docs/UI_OPERATIONS_V001.md)：底部手牌、组件与城市设施、费用提示和双向资源选择，以及银行/港口与玩家交易交换盘。四类 M6 模式已接入；本轮 557 项独立测试、固定 Unity 构建及四模式运行检查通过，完整鼠标操作与手感待用户试玩。根目录 [开发进度日志](DEVELOPMENT_LOG.md) 从本轮持续维护，并补录 M0–M6 与既有美术历史。

2026-10-09 已收到实体照片，首批木头资源卡正反面、森林板块、道路与村庄样板见 [样板说明](docs/PHOTO_SAMPLES_V001.md)。执行 `tools/development/Play-PhotoSamples.ps1` 查看四个视角、翻牌、旋转与换色，并从样板程序进入 M6 试玩新资产。尺寸仍为估算，图片为 AI 清理候选，最终还原效果待用户确认。

实体资源按 [美术转化清单](docs/ART_CONVERSION_CHECKLIST.md) 分类型生成与验证。本轮平面贴图采用用户选择的原照片校正方式；`tools/development/Play-EntityAssets.ps1` 提供独立图库，正式接入和用户外观确认分别验收。

2026-10-10 全量清点确认 **135/135 张现有实体照片已进入资产来源，114 个唯一资产 ID、123 个保留版本 FBX**。补转遗漏的蓝色进步卡背面后，当前 M6 四类规则显示接入全部有照片的板块、棋子与卡面；卡面支持悬停放大、点击固定和已有背面翻面。正式显示入口与两个独立进程的保存恢复记录均通过，见 [v003 接入说明](docs/ART_INTEGRATION_V003.md)。缺照片的港口、骰子等仍保留规则显示；最终外观、实测尺寸和 M8 发布验收尚未完成。

2026-10-11 铁矿改为**矿石资源卡插画派生的平坦六边形厚板**，已通过 Blender、Unity 和四类正式显示入口验证。真实山地板块照片仍缺，原照片和完整手牌卡面保留。当前共 **115 个唯一资产 ID、124 个保留版本 FBX**，原照仍为 135 张；见 [铁矿 v004 来源与证据](docs/ART_ORE_TILE_V004.md)。

2026-10-10 新增橙色、米色、蓝色玩家组件 **36 件三维资产**，包含三级骑士与头盔状态、村庄、城市、城墙、大都会及组合，已接入四类规则包的游戏显示并重建 M6。运行 `tools/development/Play-PlayerComponents.ps1` 查看本批次；实际验证和近似限制见 [v002 交付说明](docs/ART_CONVERSION_V002.md)。尺寸和最终还原效果仍待确认。

M6 本地 AI 对手已于 2026 年 10 月 9 日完成工程验收：可选择基础版、航海家九剧本、城市与骑士或已支持的双扩展组合，3–4 人中任选一个人类席位，其余席位自动行动。AI 仅读取自身 PlayerView，经共同规则入口提交；支持交易、多步待决、暂停及保存恢复。**496/496 项测试通过，52 个正常完整局全部结束**，固定 Unity 构建、两个独立 Windows 进程和真实窗口操作均已验证。详见 [M6 验收记录](docs/M6_ACCEPTANCE.md)与[策略质量说明](docs/M6_STRATEGY.md)。

启动当前 M6：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M6.ps1
```

当前场景为 `Unity/Catan/Assets/M6.unity`，所有当前启动入口统一使用优化 AI，目标为 `.local/m6-entity-v005/Builds/Windows/CatanM6.exe`。本轮新增实体港口与标记见 [接入说明](docs/ART_TABLETOP_MARKERS_V005.md)，并延续此前地图刷新修复和存档兼容。操作见 [M6 试玩说明](docs/M6_PLAYTEST.md)，当前仍需用户确认对战强度、最终 UI 与美术；下一阶段为 M7 好友联机。

M5 双扩展组合已于 2026 年 10 月 9 日完成工程验收：支持“驶向新海岸”“穿越沙漠”各 3–4 人固定地图，均为 16 分获胜。船、海盗、金矿与骑士、蛮族、商品和进步卡共同生效。**383/383 项规则与回归测试通过**，四个正常完整局、固定 Unity 构建、独立 Windows 保存/恢复及真实鼠标开局操作均已验证。详见 [M5 验收记录](docs/M5_ACCEPTANCE.md)与[组合兼容表](docs/M5_SCENARIOS.md)。

启动保留的 M5：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M5.ps1
```

保留场景为 `Unity/Catan/Assets/M5.unity`，Windows 程序为 `.local/m5/Builds/Windows/CatanM5.exe`，操作见 [M5 试玩说明](docs/M5_PLAYTEST.md)。其余七个航海剧本暂未开放组合；用户本人整局试玩、最终 UI 与收藏版美术尚未确认。AI 模式见当前 M6。

M4 城市与骑士已于 2026 年 10 月 9 日完成工程验收：支持本地 3–4 人，包含商品、城市改良、骑士、蛮族、城墙、大都会，以及官方 2025 版全部 **25 种、54 张进步卡**。**332/332 项规则与回归测试通过**，三人/四人正常完整局、待决保存恢复、固定 Unity 构建和两个独立 Windows 进程均已实际运行。详见 [M4 验收记录](docs/M4_ACCEPTANCE.md)。

启动保留的 M4：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M4.ps1
```

保留场景为 `Unity/Catan/Assets/M4.unity`，Windows 程序为 `.local/m4/Builds/Windows/CatanM4.exe`，操作见 [M4 试玩说明](docs/M4_PLAYTEST.md)。新增组件是可复现的实际网格占位。用户本人整局试玩、完整 UI 像素和收藏版成品美术尚未确认；双扩展组合见当前 M5。

M3 航海家已于 2026 年 10 月 9 日完成工程验收：八个官方固定剧本与「新世界」均支持本地 3–4 人，包含船与移动、海盗、金矿、最长航线、迷雾探索、部落奖励、布匹、海盗要塞及奇迹。**240/240 项规则与回归测试通过**，9 个剧本 × 3/4 人共 18 个完整对局、238 项地图来源检查、固定版本 Unity 构建与独立 Windows 进程恢复均已实际运行。详见 [M3 验收记录](docs/M3_ACCEPTANCE.md)。

启动保留的 M3：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M3.ps1
```

当前场景为 `Unity/Catan/Assets/M3.unity`，Windows 程序为 `.local/m3/Builds/Windows/CatanM3.exe`，操作见 [M3 试玩说明](docs/M3_PLAYTEST.md)。新增组件是可辨识实际网格占位；用户整局试玩、完整 UI 像素检查与成品美术确认尚未执行。M3 没有自动启用旧版规则、可选随机地形变体或双扩展组合。

M2 基础版已于 2026 年 10 月 9 日完成工程验收，支持本地 3–4 人从正常开局玩到胜利。包含强盗与弃牌、发展卡、港口和玩家交易、城市、有限供应、最长道路、最大军队、隐私换座与权威存档。**138/138 项规则测试通过**，3 人和 4 人完整对局、固定版本 Unity 构建及独立 Windows 进程保存恢复均已实测。详见 [M2 验收记录](docs/M2_ACCEPTANCE.md)。

启动 M2：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Play-M2.ps1
```

正式 Unity 工程位于 `Unity/Catan`，M2 场景 `Assets/M2.unity` 和 Windows 程序 `.local/m2/Builds/Windows/CatanM2.exe` 独立保留。操作见 [M2 试玩说明](docs/M2_PLAYTEST.md)。AI 已在当前 M6 交付，好友联机与最终美术属于后续阶段。用户亲自完成整局试玩、美术确认和长时间性能验收尚未执行。

M0 已于 2026-10-08 完成，环境验证工程保留在 `.local/environment`。M1 固定切片仍可通过 `tools/development/Play-M1.ps1` 启动，原程序与独立存档入口保留；M1 的用户试玩和山地样板确认仍按 [原验收记录](docs/M1_ACCEPTANCE.md) 保留待验，不将 M2 工程验收视为用户已确认。

入口文档：

- [项目约定](docs/PROJECT_BRIEF.md)：已确认目标、工作默认值、首个版本的验收标准。
- [开发路线](docs/ROADMAP.md)：分阶段交付、退出条件和当前任务。
- [美术生产规范](docs/ART_PIPELINE.md)：实物采集、AI 生产、导入和复现要求。
- [开发环境](docs/ENVIRONMENT.md)：本机检查结果与工具链配置顺序。
- [M6 试玩说明](docs/M6_PLAYTEST.md)：单人规则选择、人类席位、AI 暂停与保存恢复。
- [M6 验收记录](docs/M6_ACCEPTANCE.md)：496 项测试、52 个正常完整局、Unity/Windows 和实际窗口证据。
- [M6 策略说明](docs/M6_STRATEGY.md)：启发式行为、交易取舍及已知质量缺口。
- [M6 架构](docs/M6_ARCHITECTURE.md)：独立 PlayerView 策略、共用执行入口、待决调度和固定人类视图。
- [M5 试玩说明](docs/M5_PLAYTEST.md)：组合剧本、跨海骑士、船、金矿与多席位待决。
- [M5 验收记录](docs/M5_ACCEPTANCE.md)：383 项测试、四个完整局、Unity/Windows 和真实窗口证据。
- [M5 组合规则](docs/M5_COMBINATION_RULES.md)：官方 2025 组合来源、快照及跨扩展覆盖顺序。
- [M5 剧本兼容表](docs/M5_SCENARIOS.md)：支持的四种配置及七个暂未开放的组合剧本。
- [M5 规则清单](docs/M5_RULES_CHECKLIST.md)：独立组合案例、守恒、非法命令和事务隔离。
- [M5 架构](docs/M5_ARCHITECTURE.md)：路线判断、权限、待决与版本化权威保存。
- [M4 试玩说明](docs/M4_PLAYTEST.md)：城市与骑士开局、商品/骑士/进步卡、多席位待决与恢复。
- [M4 验收记录](docs/M4_ACCEPTANCE.md)：332 项测试、3/4 人完整局、Unity/Windows 与四视图证据。
- [M4 规则清单](docs/M4_RULES_CHECKLIST.md)：官方条款、25 种卡牌与逐项独立案例。
- [M4 架构](docs/M4_ARCHITECTURE.md)：事件顺序、商品/卡牌权限、队列和版本化保存。
- [M3 试玩说明](docs/M3_PLAYTEST.md)：剧本选择、船、海盗、金矿及特殊操作。
- [M3 验收记录](docs/M3_ACCEPTANCE.md)：240 项测试、18 个完整局、Unity / Windows 证据与限制。
- [M3 剧本与地图](docs/M3_SCENARIOS.md)：官方页码、固定图与新世界、238 项独立检查。
- [M3 规则清单](docs/M3_RULES_CHECKLIST.md)：条款与独立测试方法映射。
- [M3 架构](docs/M3_ARCHITECTURE.md)：隐藏地图、特殊待决、版本化权威存档。
- [M2 试玩说明](docs/M2_PLAYTEST.md)：启动、完整回合、待决操作与保存恢复。
- [M2 验收记录](docs/M2_ACCEPTANCE.md)：完整对局、138 项测试、Unity / Windows / 实际窗口证据与限制。
- [M2 规则清单](docs/M2_RULES_CHECKLIST.md)：官方页码、独立预期与逐条测试映射。
- [M2 架构](docs/M2_ARCHITECTURE.md)：完整规则接口、权限与版本化存档。
- [M0 验收记录](docs/M0_ACCEPTANCE.md)：退出条件、实际验证与 M1 移交。
- [M1 验收记录](docs/M1_ACCEPTANCE.md)：实际测试证据、交付内容和待验项。
- [M1 架构](docs/M1_ARCHITECTURE.md)：规则核心、权限边界和存档版本。
- [M1 山地样板](docs/M1_ART.md)：源网格、导出清单、四视图和干净目录重建。
- [规则来源基线](docs/RULES_BASELINE.md)：三份官方 2025 规则的版本、来源和文件校验值。
- [参考与尺度基线](docs/REFERENCE_BASELINE.md)：估算尺寸与实体参考后补清单。
- [v0.1 验收流程](docs/acceptance/V0_1_ACCEPTANCE.md)：固定开局、行动、状态账本与非法操作检查。
- [参考素材目录](assets/references/README.md)：素材存放与登记方法。

`D:\Development` 下每个开发工具各用一个目录，Unity Hub 和 Editor 同属 `Unity` 目录。项目缓存、日志、临时文件、编辑器配置及环境安装下载放在本仓库 `.local/development`；验证工程和构建产物放在 `.local/environment`。启动项目专用开发终端或 VS Code：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Shell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Start-Dev.ps1 -Tool Code
```

用户已确认先用官方 2025 规则和明确标注的估算尺寸完成 M0，实体参考后补；实体版本、照片和实测尺寸仍未知。M2 沿用山地样板和估算尺寸，城市与强盗使用可辨识的基础网格占位，未替用户确认收藏版成品外观。只有完成退出条件的阶段才更新路线图；M1 原有待确认事项单独保留。

阶段顺序：M0 启动 → M1 本地交互切片 → M2 基础版完整 → M3 航海家 → M4 城市与骑士 → M5 双扩展组合 → M6 AI 对手 → M7 好友联机 → M8 完整美术与发布验证。
