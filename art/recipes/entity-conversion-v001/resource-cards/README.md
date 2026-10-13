# 资源卡照片转化 v001

类型：`resource-cards`。范围为羊毛、砖头、小麦、铁矿石四种正面及一张共同资源牌背面；木头正面仍保留此前实验样板。本批按用户确认的忠实路线处理原照片，不采用旧版 AI 清理背面。

本类型已读 README、PROJECT_BRIEF、ROADMAP 当前阶段及 ART_PIPELINE。此项属于资产转化流水线，不宣称 M8 成品验收完成，也不替用户确认样板效果。

## 来源、像素处理与方向

`recipe.json` 的 `references` 保存原件路径、SHA-256 与用途；原始 JPG 只读。`textureJobs` 记录输出尺寸和四角坐标，四角顺序为输出 TL/TR/BR/BL 对应的原照片点位；因此旋转写在四角排列中，`rotation` 为 0。

| 资产 | 原件尺寸 px | 朝向修正 | 人工检查要点 |
| --- | --- | --- | --- |
| 羊毛 | 1706 × 1279 | 顺时针 90° | 羊头在左、四腿在下；草地与羊群未重画 |
| 砖头 | 1706 × 1279 | 逆时针 90° | 矿工头在上、矿车在下；原砖块排列保留 |
| 小麦 | 1279 × 1706 | 180° | 麦穗在绑束上方、切断茎在下；麦田与边框保留 |
| 铁矿石 | 1279 × 1706 | 0° | 山峰在上、河流向下；矿石纹理保留 |
| 共同背面 | 1706 × 1279 | 顺时针 90° | 月亮在村庄上方、人像在左下；灰度照片保留 |

五张原图无 EXIF orientation 字段。四角对齐卡片直边延长线交点，输出为 1024 × 1536 px。仅使用原像素四点透视变换及双三次插值；不作 OCR、填补、去反光、锐化、重绘或颜色替换。操作记录及输出 SHA-256 在 `texture-correction.json`。

因原件分辨率与失焦，输出尺寸增加不会创造细节。原照片曝光、保护套反光、纸面噪点与模糊保留。卡片圆角外的极少白色背景保留；本批工作网格为薄盒体，圆角轮廓还原仍待后续细化。单张照片无法确定准确尺寸、纸面粗糙度、边缘微观形状与真实背面配准。

## 参数与复现

工作尺寸为估算 54 × 80 × 0.3 mm，尚非实测或用户确认。米制 Blender 源与显式 FBX 使用同一配方；枢轴位于卡底面中心。每张卡应包含独立正面、背面与纸边材质，实际网格并非单张位图。

在仓库根目录执行照片处理：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/resource-cards/recipe.json
```

冻结后的正常重跑会比较 PNG 字节，配方差异需新版本路径。首次工作中发现砖头旋转方向错误，已在 v001 交付冻结前纠正；最终 `recipe.json` 和校正记录均为人工检查通过的逆时针版本。

正式生产及独立 FBX 回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type resource-cards
```

新目录复现（无需重渲染既有预览）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type resource-cards -OutputRoot .local/art-pipeline/rebuild/resource-cards -SkipPreviews -CompareRoot art
```

生产工具为 Blender 4.5.14 LTS，显式 FBX `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化输出且无动画。每张卡为 1 个封闭 mesh、12 个三角面；尺寸与配方误差低于 0.01 mm，UV、底面中心枢轴及三个材质槽均经 FBX 回读确认。源 blend 内贴图已打包，导出目录另存五项 PNG 依赖。

正式产物为 `art/source/entity-conversion-v001/resource-cards/*.blend`（4 个资产源与 1 个预览场景）、`art/exports/entity-conversion-v001/resource-cards/*.fbx`（4 个模型）、导出目录 `manifest.json` / `verification.json` / `textures` 和 `art/previews/entity-conversion-v001/resource-cards` 四张预览。

2026-10-09 实际检查了 `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四图（1600 × 1000）。俯视和两个斜视均能辨认四张正面、方向与边框，侧视显示真实薄卡边缘；侧视为保证每项可见的分层陈列，不能作为桌面摆放测试。`crowded-vertex` 是通用斜视陈列，并非规则棋盘拥挤节点拾取验收。四图未翻面；共同背面校正 PNG 已逐张查看，背面在 FBX 的材质/UV 与面使用经过检查，实际引擎翻牌视觉由主代理统一验证。

生产日志为 `.local/art-pipeline/logs/resource-cards-20261009-224601-611-{generate,verify}.log`。重建结果见 `.local/art-pipeline/rebuild/resource-cards/exports/entity-conversion-v001/resource-cards/verification.json`：`status=passed`、`sameGeometryMaterialsTexturesReferences=true`。几何、UV、材质、纹理、来源、种子及导出参数一致；`.blend` 和 FBX 容器字节会带工具数据，复现判据使用实际内容签名。

## 交付与验证状态

- 原件五项 SHA-256：已核对。
- 五张原图与五张校正结果：已逐张人工查看，确认插画朝向。
- 贴图配方、校正记录与冻结 PNG：已生成；正常重跑五张 PNG 字节一致。
- Blender 实际网格、FBX、源文件与四视图：已生成，四图已人工检查。
- 清洁输出目录复现、FBX 回读：四张均通过。
- Unity 导入与渲染：由主代理统一集成验证，本类型子任务未启动 Unity。
- 用户实物尺寸与最终外观验收：待用户确认。
