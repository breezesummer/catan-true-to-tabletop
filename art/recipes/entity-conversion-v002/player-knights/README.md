# 三色玩家骑士照片转化 v002

2026-10-10 逐张打开检查新增橙色、米色、蓝色的 18 张骑士原照，制作三色 × 三级旗形 × 戴头盔/无盔共 18 个实际网格。原始 JPEG 保留不改，18 个路径、SHA-256 和对应资产 ID 固定在 `recipe.json`、正式导出 `manifest.json` 中。v001 红色资产和生成脚本保留。

三色照片显示同一可见模具：圆形双阶底座、双腿与靴子、腿甲、躯干、持旗手、长旗杆、盾牌，以及裸头或灰绿可拆头盔。等级可见轮廓为一级三角旗、二级单燕尾旗、三级双燕尾旗。本版复制 v001 的参数化雕塑到自己的 `builder.py` 并复用该轮廓，各色单独导出材质和网格。照片用于识别可见轮廓与颜色；此路线是照片引导的参数化近似，没有从单张照片恢复精确曲面，不能宣称扫描、摄影测量或精确复原。

源 Blender 使用米制，估算包围盒为 X 宽 12、Y 深 12、Z 高 24 mm。18 个源模型和独立 FBX 回读的三轴尺寸均实际核对为 `[0.012, 0.012, 0.024]` m，误差小于 1 微米。底面位于 Z=0，圆底与 X/Y 包围盒中心位于根原点。FBX 显式使用 `-Z` 前向、`Y` 向上、1 米单位。每件合并为一个网格，但其组成是相互重叠的封闭壳，尚未布尔融合或制作打印实体。

三色程序塑料材质采用线性 RGB：橙色 `[0.95,0.23,0.018]`，米色 `[0.82,0.70,0.43]`，蓝色 `[0.008,0.024,0.12]`，粗糙度 0.34。戴盔模型另有灰绿头盔 `[0.23,0.26,0.19]`、粗糙度 0.46。材质名含各资产 ID 与 `Orange/Beige/Blue_Moulded_Plastic` 或 `Grey_Green_Helmet`，便于保留独立头盔材质。无需外部纹理依赖。照片的曝光/白平衡未经物理校准，颜色仍需用户视觉确认。

| 资产 ID 模式（color 为 orange / beige / blue） | 每色件数 | 每件三角面 | 每件材质槽 |
| --- | --- | --- | --- |
| knight-{color}-level-1-bare | 1 | 11,160 | 1 |
| knight-{color}-level-2-bare | 1 | 11,168 | 1 |
| knight-{color}-level-3-bare | 1 | 11,176 | 1 |
| knight-{color}-level-1-helmet | 1 | 11,120 | 2 |
| knight-{color}-level-2-helmet | 1 | 11,128 | 2 |
| knight-{color}-level-3-helmet | 1 | 11,136 | 2 |

18 件合计 200,664 三角面，主游戏无需同时生成全部变体。`bare/helmet` 只描述原照外观，本配方不把戴盔/无盔与规则激活状态对应视为用户已确认，也不改游戏规则。

实际运行 Blender `4.5.14 LTS`：

```powershell
$blender = 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe'
& $blender --background --python-exit-code 1 --python tools/art/entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-knights/recipe.json --output-root art
& $blender --background --python-exit-code 1 --python tools/art/verify_entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-knights/recipe.json --output-root art
& $blender --background --python-exit-code 1 --python tools/art/entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-knights/recipe.json --output-root .local/art-pipeline/rebuild-v002/player-knights-20261010-01 --skip-previews
& $blender --background --python-exit-code 1 --python tools/art/verify_entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-knights/recipe.json --output-root .local/art-pipeline/rebuild-v002/player-knights-20261010-01 --compare-root art
```

四条命令均成功退出。首次受限环境 Blender 未启动成功，随后获允许使用同一固定 Blender 和同一项目内输出路径完成生产。正式生成目录禁止覆盖已有版本清单，复跑生成时使用新的空输出根目录。

正式独立回读和干净目录重建对照均通过：18 件都有实际 FBX 网格、完整 UV、闭合非退化三角面、底心枢轴，全部材质槽被使用；源几何、UV、材质和清单相符，18 张原照 SHA-256 不变。重建的几何/UV 签名、材质、参考、种子、估算尺寸、包围盒和显式导出参数与正式输出相同；FBX/Blend 容器哈希可能受写入元数据影响，因此不以容器哈希当作重建几何的相等标准。报告分别为 `art/exports/entity-conversion-v002/player-knights/verification.json` 与 `clean-rebuild-verification.json`。

人工实际打开检查 `art/previews/entity-conversion-v002/player-knights/` 四张 1600×1000 PNG：

- `top.png`：18 个圆底完整可见，三色与戴盔状态可分；旗面直立，严格俯视不能充分读出等级。
- `side.png`：18 件在上下架总览中完整可见，三级旗形、双腿、持旗、盾和头盔可分；该架式布局只用于模型检查。
- `game-camera.png`：默认斜角总览三色区分清楚，灰绿头盔独立；深蓝明度较低，细节需要适当缩放和正式灯光核对。
- `crowded-vertex.png`：斜角类型总览没有部件脱离底座或被截断，旗与盔状态可见；没有真实地块、道路、建筑，不能当作正式拥挤交点/拾取检查。

尚未由本子任务验证 Unity 正式相机、灯光、碰撞、节点拾取、相邻资产遮挡和运行时绑定，由主任务统一集成并记录。缺少实测尺寸、多角度背面/底部、目标硬件性能、LOD 和用户外观确认。盾纹、面部、衣褶、后侧和底座石纹继续为简化近似；旗跨度比照片可见跨度收敛，以维持估算圆底占地内摆放，不代表实体的精确比例。此交付不标记 M8 成品美术或发布验收完成。
