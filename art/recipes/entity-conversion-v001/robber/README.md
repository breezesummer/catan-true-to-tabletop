# 强盗三人群像照片转化 v001

2026-10-09 已实际打开并检查 `assets/references/inbox/单位/强盗.jpg` 与 `强盗 (2).jpg`。两张照片是同一件黑色三人抱团雕像的不同俯仰角度，合并为一个资产 `robber-black-group`，没有按文件数量制作两款。两张原始 JPEG 未修改，来源路径与 SHA-256 固定在 recipe 与导出 manifest 中。

保留的可见特征为：后方高个的长脸、软帽、长外衣和搭在同伴周围的手臂；左前人物的圆头、短衣、两枚钮扣、交叉抱臂与分开的靴子；右前人物的兜帽、较矮躯干、握持姿态和斜向粗短棍。照片没有清楚显示独立圆盘底座，因此只在脚部下方加入很薄的异形连接支承，未知底面连接方式按近似处理。脸部、衣褶、手指与隐藏背面为简化雕塑，没有宣称扫描、摄影测量或精确还原。

包围盒 `[18,12,25]` mm 是按群像宽高比例给出的估算，不是实测或用户确认值。源 Blender 使用米制，Z 底面为零，X/Y 包围盒中心与全部物体轴心归零。FBX 显式使用 `-Z` 前向、`Y` 向上、1 米单位。黑塑料材质采用近似炭黑和适度高光；照片局部亮面按反光处理，没有添加未知的金属或涂色区域。材质颜色仍受原照曝光与白平衡限制。

正式交付包含：

- 配方 `recipe.json`、自包含脚本 `builder.py` 和 `texture-correction.json`（无平面贴图加工任务）。
- `art/source/entity-conversion-v001/robber/robber-black-group.blend` 与预览场景 `preview-stage.blend`。
- `art/exports/entity-conversion-v001/robber/robber-black-group.fbx`、`manifest.json`、`verification.json` 和 `clean-rebuild-verification.json`。
- `art/previews/entity-conversion-v001/robber/` 四张 1600×1000 正式预览。

一个合并 mesh、一个黑塑料材质、15,408 个三角面，无纹理依赖。每个子部件为闭合壳，相互重叠后合并；未执行布尔融合，不作为打印用实体模型。UV 使用确定性逐面投影图集，避免 Smart UV 排列产生重建微小漂移。

实际运行命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type robber
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type robber -OutputRoot .local/art-pipeline/rebuild/robber -SkipPreviews -CompareRoot art
```

Blender `4.5.14 LTS` 正式生成和独立 FBX 回读通过：闭合三角网格、有效 UV、底心轴心、所有材质被使用、源与 FBX 包围盒一致。两张原照校验值未变。干净输出目录重建通过：资产 ID、几何、确定性 UV、材质、尺寸参数、导出设置和参考校验值完全一致，比较报告复制到正式导出目录；不要求 FBX / BLEND 二进制文件逐字节一致。

人工已实际打开最终四视图并检查：

- `top.png`：三个人物的头顶、前后布局和短棍占地可分辨；炭黑轮廓有高光，严格正俯视不显示全部脸部和钮扣。
- `side.png`：完整高度与脚部支承可见；后方高个、左前抱臂两钮扣、右前兜帽与斜棍均可辨识。
- `game-camera.png`：三人群像、抱臂、兜帽、斜棍和黑塑料高光清楚，没有单人物胶囊替代；细脸部表情仍较概括。
- `crowded-vertex.png`：斜角下群像轮廓、持棍和脚部可见，无部件脱离。这是类型预览，没有地块、道路或相邻村庄，不属于实际拥挤交点或拾取验收。

尚未验证 Unity 正式相机、灯光、碰撞、拾取及相邻组件遮挡，由主任务统一集成；目标硬件性能与 LOD、实测尺寸、后背、底面及用户最终视觉确认也待验收。该模型是照片参考下可复现、可导入的参数化雕塑近似，不能据此更新 M8 阶段完成状态。
