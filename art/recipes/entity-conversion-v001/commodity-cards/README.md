# 商品牌原照片转化 v001

类型 `commodity-cards`，包含布匹、纸张、银币三张正面。已阅读项目 README、PROJECT_BRIEF、ROADMAP 当前阶段和 ART_PIPELINE。遵循用户 2026-10-09 确认的忠实路线；本次转化不是 M8 成品验收，也不是用户外观确认。

## 来源与忠实处理

三张原件位于 `assets/references/inbox/商品牌`，均为 1279 × 1706 px 竖向照片。逐张查看原照后，人工读取外卡直边延长线交点，以输出 TL/TR/BR/BL 顺序记录于 `recipe.json`。三张均保留原竖直方向，`rotation=0`，输出 1024 × 1536 px PNG。原件 SHA-256、用途、四角、输出 SHA-256 与处理方法可在 `recipe.json`、`texture-correction.json` 核对；JPG 原件未修改。

| 资产 ID | 来源 | 检查特征与局限 |
| --- | --- | --- |
| `commodity-cloth` | 布匹.jpg | 保留草地羊群、黄布折叠与卷轴边框；原照明显失焦，放大不增加真实细节 |
| `commodity-paper` | 纸张.jpg | 保留树林、纸张叠层与绿色边框；纸面印刷纹理、原曝光保留 |
| `commodity-coin` | 银币.jpg | 保留山地、两枚银币与紫色边框；纹理、原曝光保留；仅供已有 Coin 商品的视觉映射，不更改规则 |

仅执行原像素四点透视变换和双三次插值，不使用 AI、OCR、补字、重绘、锐化、去反光或颜色替换。保护套反光、照明差异和模糊仍存在。圆角之外的少量白色背景保留，当前网格为薄盒体，实体圆角轮廓未还原。

**商品牌背面尚无明确参考**：三项 `backTexture=null`，源与 FBX 的底面为 `_UnseenBackNeutral` 中性材质（线性 RGB 0.66 / 0.61 / 0.49），不是已还原背面；没有借用资源牌背面或修改其颜色。实物版本、尺寸、真实纸面粗糙度、完整正反面对齐仍未知。

## 参数与复现

工作尺寸为估算 **54 × 80 × 0.3 mm**，非实测、非用户确认。Blender 使用米制，枢轴位于底面中心。每张包含一件封闭实际 mesh、12 个三角形、UV、正面照片/未知底面/纸边三个材质槽；源 `.blend` 内贴图已打包，导出目录单独包含三张 PNG 依赖。位图与实际可用网格分别保存。

照片处理，在仓库根执行：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/commodity-cards/recipe.json
```

首次正式生产与 FBX 回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type commodity-cards
```

独立输出目录复现（现有正式版本不覆盖）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type commodity-cards -OutputRoot .local/art-pipeline/rebuild/commodity-cards -SkipPreviews -CompareRoot art
```

正式版本与已生成重建目录受保护；若再次重建应指定新的空输出目录。照片冻结后正常重跑会核对 PNG 字节，差异应使用新的版本路径。

实际工具为 Blender 4.5.14 LTS，显式 FBX `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化且无动画。源、导出、预览分别位于 `art/{source,exports,previews}/entity-conversion-v001/commodity-cards/`；导出目录包含 `manifest.json`、`verification.json` 和 `clean-rebuild-verification.json`。

## 实际验证与待验

2026-10-09 已逐张查看三张原图及三张校正 PNG：插画正向，卡面外框完整，未发现意外裁去插画。正常第二次照片处理三张 PNG 字节一致。

已实际执行正式生产及独立 FBX 回读：三项均 `status=passed`，尺寸误差低于 0.01 mm，闭合、非退化三角形、底心枢轴、UV、全部材质槽使用及纹理依赖通过。正式日志为 `.local/art-pipeline/logs/commodity-cards-20261009-225921-175-{generate,verify}.log`。

已实际查看 `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四张 1600 × 1000 预览：俯视与两个斜视可辨认三张正面、完整边框及方向；侧视只显示约 0.3 mm 卡边，无法从该图确认插画或纸面厚度的实物准确性。`crowded-vertex` 为通用斜视陈列，不是棋盘拥挤节点或拾取验收。四图未翻面；未知中性底面由源材质与 FBX 材质/面使用验证，实际引擎翻面检查由主代理统一执行。

已实际执行干净目录重建，`.local/art-pipeline/rebuild/commodity-cards/exports/entity-conversion-v001/commodity-cards/verification.json` 为 `status=passed`、`sameGeometryMaterialsTexturesReferences=true`。正式导出目录已保存同一比较证据。重建日志为 `.local/art-pipeline/logs/commodity-cards-20261009-230018-636-{generate,verify}.log`。比较使用几何、UV、材质、依赖纹理、来源与导出参数，未以 Blender / FBX 容器字节相等作为判据。

正式 wrapper 已在清洁重建通过后更新美术转化清单。子任务未启动 Unity、未执行规则测试、未改规则或 ROADMAP；Unity 导入和运行时渲染由主代理统一集成验证。商品背面参考、带尺实测、真实圆角及用户最终视觉验收待补。
