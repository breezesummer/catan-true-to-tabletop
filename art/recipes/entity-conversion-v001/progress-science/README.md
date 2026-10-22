# 绿色科学进步卡照片转化 v001

类型：`progress-science`。范围为十种科学进步卡正面及绿色共同背面。原件虽然位于「发展卡/绿色发展卡」，本类型属于城市与骑士的科学进步卡，不是基础版发展卡。

已读项目 README、PROJECT_BRIEF、ROADMAP 当前阶段与 ART_PIPELINE，采用用户确认的原照片忠实校正路线。本类型交付不代表 M8 阶段完成或用户已确认最终外观。

## 来源与视觉角色对照

`recipe.json` 逐项登记十一张原件路径、SHA-256、像素尺寸和用途；原始 JPG 保留不改写。全部原件均为 1279 × 1706 px，无 EXIF orientation 字段。下表仅用于资产与当前 `Catan.Core.M4.ProgressCardKind` / `Catan.Core.M5.ProgressCardKind` 的视觉角色对照；照片中的印刷说明不作为当前官方 2025 规则来源。

| 资产 ID | 原照印刷题名 | 原件文件名 | 当前规则枚举 |
| --- | --- | --- | --- |
| `progress-science-alchemy` | 煉金術士 | 炼金术士.jpg | `Alchemy` |
| `progress-science-crane` | 起重機 | 起重机.jpg | `Crane` |
| `progress-science-engineering` | 工程師 | 工程师.jpg | `Engineering` |
| `progress-science-invention` | 發明家 | 发明家.jpg | `Invention` |
| `progress-science-irrigation` | 灌溉 | 灌溉.jpg | `Irrigation` |
| `progress-science-medicine` | 藥物 | 药物.jpg | `Medicine` |
| `progress-science-mining` | 採礦 | 采矿.jpg | `Mining` |
| `progress-science-printing` | 印刷廠 | 印刷厂.jpg | `Printing` |
| `progress-science-road-building` | 興建道路 | 新建道路.jpg | `RoadBuilding` |
| `progress-science-smithing` | 鍛冶匠 | 锻铁匠.jpg | `Smithing` |
| 共同背面 | 绿色城堡图案 | 发展卡背面.jpg | 科学牌堆共同背面 |

「鍛冶匠」与「興建道路」保留照片中的实际题名，不将文件名改写成卡面文字；「藥物」「採礦」等对应的英文枚举也不替换原图印刷。实体版本与现行英文规则的差异尚待确认。本次没有改变规则效果、牌数、名称展示逻辑或任何规则代码。十个网格代表十种正面模板，不代表牌堆仅有十张实体卡。

## 像素处理与限制

十一张原图、十一张校正 PNG 均已逐张实际查看。十正面题名在上，人物、建筑及地景方向正确；共同背面城堡尖顶在上。没有额外旋转。

`textureJobs.quad` 为输出 TL/TR/BR/BL 对应的原图像素坐标，人工读取卡片直边延长线交点。输出 1024 × 1536 px，以四点透视变换和双三次插值校正原照片像素。没有 AI 重绘、OCR、重新排版、补字、去反光、锐化或颜色替换。处理记录与输出 SHA-256 见 `texture-correction.json`。

照片的模糊、纸面印刷纹理、曝光、色偏、保护套反光和纸面噪点被保留；藥物、工程師、印刷廠的浅色与泛光仍可见，背面仍保留原照低对比度。输出像素数增加不会创造原照片缺失的细节。圆角外少量照片背景被保留，实际网格是封闭矩形薄卡，圆角尚待细化。

本批正面与共同背面的物理尺寸均未实测。单张背面照片不能确定背面真实毫米尺寸、纸面粗糙度或正背面印刷配准；共用背面依当前照片用途分配，不声称已验证全部实体卡背面相同。未知项留待实测与用户确认。

## 参数与复现

工作尺寸为估算 54 × 80 × 0.3 mm，尚非实测或用户确认。源 `.blend` 使用米制，卡片枢轴在底面中心；每张卡有实际封闭薄盒网格、正面、背面与纸边三个材质槽，照片 PNG 本身不是 3D 网格。

在仓库根目录处理照片：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/progress-science/recipe.json
```

冻结后的正常重跑逐张比较 PNG 字节。后续照片几何、贴图与参数修改应保存在新版本。

正式生成、四视图渲染与独立 FBX 回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-science
```

从干净输出目录重建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-science -OutputRoot .local/art-pipeline/rebuild/progress-science -SkipPreviews -CompareRoot art
```

Blender 4.5.14 LTS，显式 FBX 参数为 `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化导出，无动画。正式产物为十个资产 `.blend`、一个 `preview-stage.blend`、十个 FBX、十一项 PNG 依赖、`manifest.json`、`verification.json`、`clean-rebuild-verification.json` 与四张 1600 × 1000 预览。

## 实际验证记录

2026-10-09 实际执行：

- 十一项原件 SHA-256 核对通过；全部原图与校正 PNG 逐张人工查看，方向、边框、插画、题名和印刷说明保留。
- 正式 wrapper 成功，十个资产源文件、十个 FBX、一个预览场景、十一项纹理依赖与四张预览齐全。
- 十个 FBX 独立回读全部通过：各一个封闭 mesh、12 个非退化三角形、有 UV、底面中心枢轴、三个实际使用的材质槽，尺寸误差小于 0.01 mm。
- 源 `.blend` 内依赖贴图已打包；FBX 旁另存十一项 PNG 依赖。
- 清洁输出目录重建通过，`sameGeometryMaterialsTexturesReferences=true`；资产 ID、几何、UV、材质、纹理、来源、种子、估算状态与导出参数一致。`.blend` 和 FBX 容器不以字节一致作判据。
- 清洁重建 wrapper 的照片处理正常重跑通过，十一项 PNG 字节一致，原件未变。
- `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四张正式预览已实际人工查看。俯视与两张斜视展示十种正面，无裁切或遮挡；画面照明比原 PNG 更暗，细字在总览距离不能逐字辨读。
- 侧视采用三层陈列，十张卡片边缘均可见，底层两张接近桌面边界；0.3 mm 厚度在总览中约为细线，精确厚度以回读数据为据，不声称侧视已确认实物厚度。
- `crowded-vertex` 是通用斜视陈列，不是实际规则棋盘的拥挤节点、遮挡或拾取验收。
- 四张 Blender 预览没有翻牌。共同背面校正 PNG 已实际查看，背面材质及 UV 的实际面使用通过回读检查；Unity 翻牌视觉由主代理统一验证。

正式日志：`.local/art-pipeline/logs/progress-science-20261009-230638-723-{generate,verify}.log`。清洁重建日志：`.local/art-pipeline/logs/progress-science-20261009-230815-438-{generate,verify}.log`。正式回读与重建证据位于 `art/exports/entity-conversion-v001/progress-science/`。

正式生产及清洁重建 wrapper 均实际执行清单生成器；README 保存后再次刷新本类型清单。未手工修改共享脚本、规则、Unity 工程或 ROADMAP。Unity 导入与运行时渲染由主代理统一集成，本子任务未运行 Unity。实物尺寸、版本差异、最终外观与正背面对齐仍待用户确认。
