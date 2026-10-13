# 黄色贸易进步卡照片转化 v001

类型：`progress-trade`。范围为贸易船队、商港、商人、大商家、垄断资源、垄断商品六种正面及黄色共同背面。原始文件虽然位于「发展卡/黄色发展卡」，本类型属于城市与骑士的贸易进步卡，不是基础版发展卡。

本类型已读 README、PROJECT_BRIEF、ROADMAP 当前阶段与 ART_PIPELINE。按照用户确认的忠实路线校正原照片；此项是美术转换流水线交付，不代表 M8 阶段完成或用户已确认最终效果。

## 来源与视觉角色对照

`recipe.json` 的 `references` 记录七张原件路径、SHA-256、像素尺寸与用途；原始 JPG 保留不改写。下表只是资产 ID 与当前规则枚举的视觉角色对照，照片印刷文字不是规则来源，也不会覆盖官方 2025 规则基线。

| 资产 ID | 原图印刷题名 | 当前规则枚举 | 原件 px | 朝向 |
| --- | --- | --- | --- | --- |
| `progress-trade-merchant-fleet` | 貿易船隊 | `MerchantFleet` | 3072 × 4096 | 正向 |
| `progress-trade-commercial-harbor` | 商港 | `CommercialHarbor` | 3072 × 4096 | 正向 |
| `progress-trade-merchant` | 商人 | `Merchant` | 3072 × 4096 | 正向 |
| `progress-trade-guild-dues` | 大商家 | `GuildDues`，题名差异待确认 | 3072 × 4096 | 正向 |
| `progress-trade-resource-monopoly` | 壟斷資源 | `ResourceMonopoly` | 1279 × 1706 | 正向 |
| `progress-trade-trade-monopoly` | 壟斷商品 | `TradeMonopoly` | 1279 × 1706 | 正向 |
| 共同背面 | 黄色城堡图案 | 贸易牌堆共同背面 | 3072 × 4096 | 城堡尖顶在上 |

「大商家」保持照片中的原题名与插画。当前代码使用 `GuildDues`；这里没有声称中文实体版本已确认，亦没有依据照片更改规则效果或牌数。六个模型表示六种正面模板，不是六张实体总量。实际牌堆数量继续由规则代码确定。

## 像素处理与限制

七张原图、七张校正 PNG 均已逐张查看。六正面题名在上，商人、商家人物与船帆方向正确；共同背面城堡及麦田路径正向。前五张高分辨率原图的 EXIF orientation 字段值为 0，两张垄断照片无该字段，均未作额外旋转。

`textureJobs.quad` 是输出 TL/TR/BR/BL 对应的原图像素坐标，人工读取卡片直边延长线交点。输出 1024 × 1536 px，使用四点透视变换和双三次插值。未使用 AI 重绘、OCR、文字重排、补字、去反光、锐化或颜色替换；处理记录及输出 SHA-256 见 `texture-correction.json`。

原照片的纸面印刷纹理、曝光、保护套反光、背面偏青色、纸面噪点和模糊被保留，两张垄断照片的原始细节明显低于其他照片。提高输出像素数不会创造细节。四角外少量背景保留，实际网格是封闭矩形薄卡，圆角尚待细化。单张照片无法确定真实毫米尺寸、纸面粗糙度、边缘微观形状或正背面配准。

## 参数与复现

工作尺寸为估算 54 × 80 × 0.3 mm，尚非实测或用户确认。米制 Blender 源与显式 FBX 使用同一配方；底面中心枢轴。每张卡有真实薄盒网格、独立正面、背面及纸边材质，PNG 贴图不是 3D 网格。

在仓库根目录执行照片处理：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/progress-trade/recipe.json
```

冻结后的正常重跑逐张比较 PNG 字节。几何、贴图或参数的后续改动应保存到新版本。

正式生产、四视图渲染及独立 FBX 回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-trade
```

清洁输出目录复现：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-trade -OutputRoot .local/art-pipeline/rebuild/progress-trade -SkipPreviews -CompareRoot art
```

生产工具 Blender 4.5.14 LTS，显式 FBX 参数 `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化输出，无动画。正式产物为六个资产 `.blend`、一个 `preview-stage.blend`、六个 FBX、七项 PNG 依赖、`manifest.json`、`verification.json` 及四张 1600 × 1000 预览。

## 验证记录

2026-10-09 的实际执行结果：

- 原件七项 SHA-256 已核对，全部原图与校正 PNG 已逐张人工查看；方向、边框、插画和印刷内容保留。
- 正式生产成功：六个资产源文件、六个 FBX、一个预览场景、七项纹理依赖和四张预览齐全。
- 六个 FBX 独立回读全部通过：各一个封闭 mesh、12 个三角面、UV、底面中心枢轴及三个实际使用的材质槽；尺寸误差低于 0.01 mm。
- 源 blend 内贴图已打包，FBX 旁另存七项 PNG 依赖。
- 清洁输出目录重建通过，`sameGeometryMaterialsTexturesReferences=true`；几何、UV、材质、纹理、参考来源、种子及导出参数一致。`.blend` 与 FBX 容器不以字节一致作判据。
- 正常照片处理重跑七项 PNG 字节一致，原件未变。
- `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四图已实际人工查看（1600 × 1000）。俯视和两张斜视显示全部六张正面；侧视薄卡边缘可见，但两排在该通用侧视中重叠，不能据此声称六张均可独立辨认。`crowded-vertex` 为通用斜视陈列，不是规则棋盘拥挤节点或拾取验收。
- 四张预览未翻牌。共同背面校正 PNG 已查看，背面材质及 UV 的实际面使用通过回读检查；Unity 翻牌视觉留待主代理统一验证。

正式日志：`.local/art-pipeline/logs/progress-trade-20261009-225305-102-{generate,verify}.log`。清洁重建日志：`progress-trade-20261009-225507-476-{generate,verify}.log`。正式验证文件：`art/exports/entity-conversion-v001/progress-trade/verification.json`。重建验证文件：`.local/art-pipeline/rebuild/progress-trade/exports/entity-conversion-v001/progress-trade/verification.json`。

每次正式生产和验证后，包装脚本均实际运行 `update_entity_checklist.py` 刷新清单。本类型未直接编辑共享清单或规则。

Unity 导入与运行时渲染由主代理统一集成验证，本子任务未启动 Unity。实物尺寸、实体版本差异、最终外观和正背面对齐仍待用户确认。
