# 商人与双轮货车照片转化 v001

2026-10-09 已实际打开并检查 `assets/references/inbox/单位/商人.jpg`。照片为单件米灰中性色人物连货车的斜前方视角，导出一个实体棋子 `merchant-neutral-cart`。它与贸易进步卡 `progress-trade-merchant` 分属不同类型，未将卡面图案替代实体模型。原始 JPEG 未修改，路径与 SHA-256 固定在 `recipe.json` 和导出 manifest 中。

保留的可见轮廓包括圆软帽/布头巾及下缘横向褶带、长脸与鼻、宽松长外衣、胸前斜向带与小袋状浮雕、垂下的双臂、握持货车长把手的双手和分开的靴子。人物后方有双轮货车，保留底板、两侧栏板、车轮、桶形货物、鼓起的袋形货物与顶部平行卷状货物。照片照明较暗，具体帽种、货物种类、小脸部表情和衣服接缝未被确认；模型仅采用可见形状作工作近似。

没有比例尺，包围盒 `[14,19,25]` mm 为估算：较长的 Y 轴用于容纳人物后的货车，不能作为实测或用户确认值。源 Blender 使用米制，全部网格轴心为 `(0,0,0)`，X/Y 包围盒中心为零，靴底与车轮底同在 Z=0 支承平面。负 Y 为人物正面。FBX 显式导出 `-Z` 前向、`Y` 向上、1 米单位；Unity 中对应宽 14、高 25、深 19 mm。

材质为单一米灰塑料，线性颜色 `[0.40,0.38,0.28]`、粗糙度 `0.37`；原照曝光与白平衡不确定，颜色仅为近似。没有人为追加玩家色、金属涂装或未知文字。该资产是实际三维网格，没有使用图像生成，也不将 JPEG 当作已完成的模型。无平面校正任务及纹理依赖，`texture-correction.json` 的操作列表为空。

正式交付包含：

- `recipe.json`、自包含参数脚本 `builder.py`、`texture-correction.json` 与本说明。
- `art/source/entity-conversion-v001/merchant/merchant-neutral-cart.blend` 与 `preview-stage.blend`。
- `art/exports/entity-conversion-v001/merchant/merchant-neutral-cart.fbx`、`manifest.json`、`verification.json`、`clean-rebuild-verification.json`。
- `art/previews/entity-conversion-v001/merchant/` 四张 1600×1000 正式预览。

一个合并 mesh、一个材质、12,748 个三角面。人物、衣褶、栏板、轮圈、轮辐与货物分别以闭合壳生成后合并，部件间存在重叠，没有执行布尔融合，不作为打印用实体。UV 使用确定性逐面投影图集，不依赖 Smart UV 的岛排列。脚本无需外部模块或旧版占位模型。

实际运行命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type merchant
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type merchant -OutputRoot .local/art-pipeline/rebuild/merchant -SkipPreviews -CompareRoot art
```

Blender `4.5.14 LTS` 正式生成与独立 FBX 回读通过：实际三角网格、每条边恰有两相邻面、非退化三角面、有效 UV、材质槽使用、底心轴心、源与 FBX 包围盒一致；原照 SHA-256 未变。首次侧视曾发现靴底比轮底高约 0.26 mm，已调整支承并重新生成全部正式产物；首次候选保留在 `.local/art-pipeline/revisions/merchant-first-pass/`。最后从干净目录重建通过，资产 ID、几何、UV、材质、尺寸、导出设置和参考校验值一致。正式目录保存了比较报告；不要求 BLEND/FBX 二进制逐字节相同。

人工已实际打开并检查最终四张预览：

- `top.png`：人物在前、货车在后，长把手、车板、桶顶和三根卷状货物可分辨；帽顶遮挡脸部属正俯视局限。
- `side.png`：此共享视角为正前低视角，可检查完整人物高度、长衣、垂臂、靴子以及轮底与靴底对齐；货车在人物后被遮挡，不能单凭此图核实背侧。
- `game-camera.png`：帽带、鼻、胸前带、小袋、双手、双靴和后方堆货可见；米灰单色有明暗层次，衣服与脸部仍是概括雕塑。
- `crowded-vertex.png`：斜角可见轮圈/轮辐、栏板、长把手、货物与人物之间的连接，无部件悬离；这只是类型预览，没有相邻地块或道路，不作为实际拥挤节点拾取验收。

单照没有提供背面、底面、另一侧货物或真实尺寸。车轴、轮辐、后背以及货物隐藏面按闭合结构补齐，不能声称扫描、摄影测量或精确复原。Unity 正式相机、灯光、碰撞、拾取、相邻组件遮挡、目标硬件性能与 LOD、实测比例和用户最终视觉确认尚待主任务集成验收；该类型交付不意味着 M8 已完成。
