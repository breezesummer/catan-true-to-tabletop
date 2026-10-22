# 蛮族入侵轨道照片转化 v001

类型与资产 ID 均为 `barbarian-track`，交付 **1 件异形薄板实际网格、1 张原照片校正正面贴图和四视图**。实施前已阅读 AGENTS、README、PROJECT_BRIEF、ROADMAP 当前阶段及 ART_PIPELINE；本次是分类型美术转化，不代表 M8 完成或用户外观验收。

## 来源、语义与尺寸

唯一原件为 `assets/references/inbox/单位/海盗入侵卡.jpg`，3072 × 4096 px。原文件名和原文件均保持不变，SHA-256 为 `c394f472541936a13a2e90c02702ace117903b783f3dba6c10130bbdf49da72a`。照片文件名沿用“海盗”，资产明确映射为城市与骑士规则中的**蛮族入侵轨道**，不据此改变官方 2025 规则、海盗角色或入侵流程。

已实际查看原图，并由独立只读审查复核：印刷方向为纵向，船桅杆向上、沙滩在下；可见 **7 个浅色轨道圈与左下 1 个红色登陆圈**。右上起点圈内为远景帆船，另 6 个浅色圈内为线描船。可见圈路径为右上、上中、上左、中部、中右下、右下、下中、左下红圈。这里只记录视觉位置，圈与规则状态的交互没有在本子任务实现。

外轮廓有右上和左下两个圆弧外突、右侧与左侧两处圆滑内凹，不能直接用矩形卡替代。照片整体包围盒宽高约 0.91，按主体直边透视展开后工作估计约 **0.94–0.95**。工作尺寸为 **190 × 200 × 1.5 mm**，全部是无尺照片下的估算；实物版本、真实比例、厚度、刀模和纸面材质均未确认。未套用资源卡的 54 × 80 mm。

本资产用独立 `customBuilder` 制作异形轮廓，`kind: custom`、`role: panel`，几何用途仍是平面轨道板。`backTexture: null`；没有背面照片，底面和纸边使用明确命名的中性材质，不复用其他卡牌背面声称还原。

## 原像素校正和轮廓

`prepare_recipe.py` 保存人工观察点并冻结 `recipe.json`。`photoGeometry.bodyQuad` 为主体四条直边延长交点（原图像素，TL/TR/BR/BL）：`(520,638), (2270,696), (2680,3570), (687,3602)`。它们是透视框，**不是异形纸板的四个实体角**。原图上实际纸边另外以 76 点 `sourceOutline` 完整记录；`source-outline-inspection.png` 的红线/黄点标示该纸边，绿线仅标示主体透视框。

根据同一投影框展开画布覆盖两端外突，`textureJobs.quad` 为展开外接画布 TL/TR/BR/BL：`(14.1102,611.6422), (2737.3053,702.3262), (3213.2938,3574.9418), (108.4840,3625.5211)`；输出 **1946 × 2048 px** PNG，旋转为 0。主体框宽高比 0.61 与成板约 0.95 的比例是结合近圆形印刷做的工作观察，不是实测数据。

共享 `rectify_entity_photos.py` 使用 PIL 四点投影、双三次插值重采样原像素。无 AI 重绘、OCR 替字、补画、去反光、锐化或颜色替换；原插画、印刷网点、反光、模糊、曝光和边缘磨损保留。`texture-correction.json` 登记源、输出、角点、尺寸、方法及 SHA-256；正面 PNG SHA-256 为 `a0ac9f03d95cc65bffa0b79f4a4cfd12e6e0e93b0367d3078b4502f15c1fbb8a`。

PNG 的矩形画布含外部桌面和右下少量源照片画幅以外的黑色默认区域；**这些区域位于实际异形网格之外**。网格顶面 UV 按同一投影轮廓匹配，仅采样真实轮廓内原照片像素。没有以背景分割或补画重建形状。手工轮廓是单照近似，外沿仍可能残留少量阴影/桌面像素，曲线由分段直线近似，未知侧边材质没有推测为已还原。

## 网格与复现

`build_track.py` 将 76 点凹多边形正确三角剖分，再沿厚度生成封闭壳。每件 1 个网格、**300 个非退化三角面**：顶面与底面各 74 面，侧边 152 面。底面中心为原点，底面 z=0；米制。正面、未知中性底面和未知中性纸边三个材质均有实际使用面。源 `.blend` 打包正面图片；FBX 同时保留独立 PNG 依赖。

正式生成与回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type barbarian-track
```

干净目录重建与比较：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type barbarian-track -OutputRoot .local/art-pipeline/rebuild/barbarian-track -SkipPreviews -CompareRoot art
```

生成器冻结交付后拒绝覆盖已有版本；后续复现选择新的空输出目录。校正重跑核对原件校验值和 PNG 字节相同；改角点、轮廓或视觉须另开新版本。显式 FBX 参数：`axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`、三角化、无动画；Blender **4.5.14 LTS**。

产物：

- `art/recipes/entity-conversion-v001/barbarian-track`：配方、元数据作者脚本、独立网格生成器、原图轮廓检查图与校正记录。
- `art/source/entity-conversion-v001/barbarian-track`：资产 `.blend`、`preview-stage.blend`、1 张校正 PNG。
- `art/exports/entity-conversion-v001/barbarian-track`：1 个 FBX、1 张 PNG 依赖、manifest、verification、clean-rebuild-verification。
- `art/previews/entity-conversion-v001/barbarian-track`：`top`、`side`、`game-camera`、`crowded-vertex` 四张 1600 × 1000 PNG。

## 已运行验证与待验

2026-10-09 正式 wrapper 和干净重建均完成。独立 FBX 回读通过：单个封闭网格、300 个非退化三角面、完整 UV、所有材质有使用面、底心枢轴、正面 PNG 依赖及原件校验值。回读实际尺寸约 **190.0000 × 200.0000 × 1.50003 mm**，与工作估算数值误差低于 0.01 mm。两份报告 `status: passed`；`clean-rebuild-verification.json` 的 `sameGeometryMaterialsTexturesReferences: true`，几何、UV、材质、贴图、来源、种子与导出设置一致。

已实际打开查看原图、轮廓检查图、校正 PNG 和四张正式 Blender 预览。俯视及斜视保留八圈、船与登陆插画及两个内凹，方向正确，外接矩形桌面/黑区未贴到板面；侧视可见薄板边缘。通用单资产布局仍预留第二列空位，因此画面留白较多；侧视为约数像素的薄边，不适合评价纸边精细材质。照片反光及浅色圈模糊照原样保留。

`crowded-vertex` 是通用斜视资产陈列，未声称为实际拥挤游戏节点或拾取验收；四预览仅显示正面/侧边，底面中性材质和 UV 由 FBX 回读验证。正式日志：`.local/art-pipeline/logs/barbarian-track-20261009-234021-122-{generate,verify}.log`；重建日志：`barbarian-track-20261009-234054-181-{generate,verify}.log`。

本子任务未运行 Unity，未修改规则、路线图或游戏交互。Unity 导入、翻面显示、实际轨道状态标记、游戏内替换、实测比例与用户最终外观确认由主任务统一集成和验收，仍待验证。清单在 README 和干净重建证据就绪后由共享更新工具刷新。
