# 现有实体照片的游戏接入 v003

2026-10-10，补齐已转换但仍只在图库中的实体资源显示，并将此前目录遗漏的蓝色进步卡共同背面转入新版本。当前 M6 的基础版、航海家、城市与骑士及已支持双扩展使用对应照片资产。**正式显示入口与 M6 独立进程记录均已通过；最终用户外观、真实窗口操作和长期性能仍待确认，M8 状态不变。**

正式运行状态只按 [显示入口记录](verification/entity-integration-v003/runtime-integration.json)和 [M6 独立进程记录](verification/entity-integration-v003/m6-smoke.json)两者 `status=passed` 判断；照片覆盖、Unity 导入、图库检查和用户最终确认分别记录。

**2026-10-11 追加说明：**铁矿现已由 M1 立体山地样板改为 v004 矿石资源卡插画派生的平坦厚板；四类正式显示入口与人工目检通过。当前 115 个唯一资产 ID、124 个保留版本 FBX，原照仍为 135 张。来源是真实资源卡照片，真实六边形山地照片仍缺，完整手牌卡面保留。[v004 来源与验证](ART_ORE_TILE_V004.md)。下方 2026-10-10 数量与山地样板描述保留为 v003 历史记录。

## 全量清点与补转

[原照与资产审计](../art/recipes/entity-conversion-v003/progress-politics/photo-coverage-audit.json)确认 inbox 实际有 **135 张不重复原照**：历史 v001 清点 98 张、三色组件追加 36 张、遗漏蓝背 1 张。36 张原文件名备份不重复计数。全部原照已关联导出来源，原件和备份哈希一致，源模型、FBX 和贴图依赖无缺失或哈希变化。

现有 **114 个唯一资产 ID、123 个保留版本 FBX**。九件新版政治卡沿用已有 ID，仅替代旧版九件卡的未知中性背面，不能计为九种新组件。卡牌正背面和组合照片可共同参考同一个模型，因此照片数量不等于模型数量。

| 资产版本 | 资产数 | 当前处理 |
| --- | --- | --- |
| 首批照片样板 | 4 | 森林、木头卡、村庄、道路；森林和木头卡仍为旧版 AI 清理候选 |
| entity-conversion-v001 | 74 | 其余地图、卡牌、设施和雕塑；旧版产物保留 |
| entity-conversion-v002 | 36 | 蓝色、橙色、米色建筑与骑士；红色沿用已有来源 |
| entity-conversion-v003 | 9 个替代版本 | 九政治卡绑定新增原照背面，正面按 v001 原字节复制 |

新增蓝背原件为 `assets/references/inbox/发展卡/蓝色发展卡/卡背.jpg`，来源登记见 [blue-politics-back-v001](../assets/references/catalogs/blue-politics-back-v001.json)。仅使用记录角点的原像素透视校正，保留原照色偏、保护套反光和模糊；不做 AI 重绘、不从印刷文字改变规则。[生成与复现说明](../art/recipes/entity-conversion-v003/progress-politics/README.md)。

## 四类正式显示范围

| 游戏模式 | 棋盘与棋子 | 卡面入口 |
| --- | --- | --- |
| 基础版 | 森林、草地、小麦、丘陵、沙漠等适用照片板块；道路、村庄、城市、强盗 | 本人资源手牌缩略图及放大查看；基础发展卡尚无照片，继续使用现有文字操作 |
| 航海家 | 加入海洋、金矿、四玩家色船与海盗；未揭示迷雾保持公开占位 | 本人资源卡面；照片只随当前玩家有权读取的数据展示 |
| 城市与骑士 | 适用照片板块、建筑、骑士、城墙、大都会、商人；蛮族轨道和入侵船按公开位置显示 | 资源、商品、本人进步卡、城市设施正反面、升级/满级提示板、本人及公开保卫者奖励和公开胜利点进步卡 |
| 已支持双扩展 | 航海家船/海盗与城市骑士组件共同显示 | 与城市骑士相同的卡面入口，适用组合规则及当前 PlayerView 权限 |

七种有照片板块已用于对应规则地图。山地未收到照片，继续使用 M1 山地样板；照片板块是**有厚度的闭合印刷板块网格与顶面纹理**，不是立体树林、麦田或海面。数字和规则标记仍由规则展示数据提供。

P1 红色沿用首批/v001，P2 蓝色、P3 橙色、P4 米色使用 v002 建筑和骑士；船使用已有四色 v001。红色带墙城市和大都会由独立建筑与城墙装在一个中立根对象下，其他三色使用 v002 组合资产。等级、激活及三轨道身份标记独立保留，头盔与激活的外观对应仍是暂定约定。底心轴心、米制缩放及原材质保留，美术不增加交互碰撞体。

卡面使用本人手牌的缩略图与放大预览。悬停放大；点击固定后，已有背面可用“查看背面 / 查看正面”翻转，“关闭”或 Esc 取消。城市骑士及组合右侧“查看手牌与设施实体卡面”打开设施与进步卡区域。无商品/保卫者背面照片的卡片继续明确标注背面缺参考，不借用其他类别背面。

私有卡面只登记当前 PlayerView 的本人手牌和待决明确允许查看的信息，公开奖励使用公开投影。换座、隐私幕、菜单切换或卡片离开可见手牌时清空已固定预览。完整他人手牌、牌库顺序、隐藏地图和随机状态不进入美术适配器或公开验证记录。

## 验证记录与待验边界

| 验证层次 | 实际结果 | 证据与限制 |
| --- | --- | --- |
| 全量来源和产物清点 | 135/135 来源覆盖；36 备份哈希一致；114 唯一 ID、123 保留 FBX | 本次逐文件核对源/导出/贴图；旧批次 Blender/Unity 记录读取复核，未重新生成全部旧资产 |
| v003 Blender 生成与回读 | 9/9 通过；九张冻结正面不变，九卡均绑定原照背面 | [回读](../art/exports/entity-conversion-v003/progress-politics/verification.json)，真实闭合网格、UV、底心和估算尺寸 |
| v003 干净目录重建 | 9/9 几何、UV、材质、贴图和来源一致 | [重建](../art/exports/entity-conversion-v003/progress-politics/clean-rebuild-verification.json)，不要求 FBX/BLEND 二进制逐字节相同 |
| Unity v003 导入 | 固定 Unity 6000.3.25f1；九政治卡真实边界、三角面和 URP 材质绑定通过 | [导入记录](verification/entity-integration-v003/unity-import-v003.json)；仅导入不能代表正式游戏已显示 |
| 四类正式显示入口 | 60 个来源实例、9 件政治卡和 8 张视图记录通过 | [显示入口记录](verification/entity-integration-v003/runtime-integration.json)；实际渲染入口用公开夹具检查来源 Mesh/Material、米制、无碰撞、合法/非法预览色、迷雾无照片贴图，另检查卡面加载及授权清空方法 |
| 玩家组件本轮复验 | 104 个公开投影实例通过 | [玩家组件记录](verification/entity-integration-v003/player-components-verification.json)；检查单一组合根、独立等级/激活/轨道标记和来源 |
| 当前 M6 独立对局进程 | 四规则包，2 个进程保存恢复与继续 AI 行动通过 | [进程记录](verification/entity-integration-v003/m6-smoke.json)；权威测试存档保留在忽略的 `.local`，公开文件只留成功元数据 |
| 规则回归 | 496/496 通过，0 失败、0 跳过；约 11 分 11 秒 | [本轮 TRX](verification/entity-integration-v003/rules-regression.trx)；首次沙箱内测试宿主连接超时，获准在沙箱外重跑后通过，未修改规则或增加外观镜像测试 |
| 人工图像检查 | 已看原照、角点、校正图和全部五张新增 Blender 陈列视图；主任务另查看四类显示入口的全部 8 张 Unity 棋盘图 | 六类新增照片地块、四色船、公共雕塑、轨道及修正后的入侵船路径可见；发现并修正蓝背资源名及轨道 X 镜像。真实窗口鼠标操作仍待验，不能把夹具视为完整正常局试玩 |
| 最终用户确认与 M8 | 待验 | 实测尺寸、未知背底面、色彩与形体、节点操作、整局视觉体验、LOD、长期性能、音效、教程和清洁安装发布条件未全部验收 |

历史 v001/v002 图库与导入记录保留在各自版本目录。此次正式接入以当前 M6 构建为入口，未将旧 M2/M3/M4/M5 独立可执行文件的历史记录改写为本轮交付。

## 查看与复现

在项目根目录运行：

```powershell
# 当前正式入口：选择基础版、航海家、城市骑士或已支持组合。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Play-M6.ps1

# 导入新增 v003 政治卡并构建当前 M6。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-EntityIntegration.ps1

# 正式显示入口、玩家组件及两个独立 M6 进程的保存恢复检查。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-EntityIntegration.ps1

# 按当前证据更新清单；只有显示入口和 M6 进程记录均 passed 才标正式运行通过。
python tools/art/update_entity_checklist.py
```

新增网格位于 `Unity/Catan/Assets/Art/EntityAssets/v003/progress-politics`，预制体和照片贴图位于 `Assets/Resources/EntityAssets/v003/progress-politics`；其余照片继续引用已验证的首批/v001/v002 产物。统一显示适配为 `EntityBoardAssets`、`EntityCardAssets` 与 `PlayerComponentAssets`，规则入口、玩家权限和权威保存格式保持独立。

缺参考项目仍按 [美术转化清单](ART_CONVERSION_CHECKLIST.md)保留：实体版本和实测尺寸、山地、数字盘、海框、港口、骰子、基础发展卡、商品/保卫者背面、扩展特殊筹码与奇迹等。本次落实“现有照片转为游戏资产”的范围，未满足路线图 M8 全部退出条件。

本轮微信窗口访问请求返回 `Computer Use app approval timed out`，未能打开目标群或发送汇报，未改用其他通道。本批次没有满足 M8 退出条件，未更新 ROADMAP 阶段状态；工作区包含此前尚未提交的大批实体资源与修改，本次保留这些内容，没有混合提交推送。
