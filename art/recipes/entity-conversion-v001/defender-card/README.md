# 卡坦保卫者奖励卡转化 v001

类型与资产 ID 均为 `defender-card`。本类型只有一张正面照片，交付一张忠实校正的正面 PNG 与一件实际薄卡网格，不生成或借用其他卡种背面。本次已读项目 README、PROJECT_BRIEF、ROADMAP 当前阶段与 ART_PIPELINE；这项资产交付不表示 M8 完成或用户已经确认外观。

## 来源与照片处理

原件：`assets/references/inbox/奖励卡/卡坦保卫者.jpg`，1706 × 1279 px，无 EXIF orientation 字段。原 JPG 只读保留，SHA-256 为 `3c32e1d715f85010436610507e90a69038a1848770b8e386bbbbb4fe535424da`，已核对。

人工查看原照片后，以实物卡片的四条外直边延长线交点估读角点，排除保护套外框。输出 TL/TR/BR/BL 分别对应原照片 `[420,1032]`、`[425,407]`、`[1421,376]`、`[1435,1060]`；顺时针 90° 朝向修正包含在角点排列中，额外 `rotation=0`。输出为 1024 × 1536 px，标题在上、城镇与夕阳在中、原印刷“1分”在下。

只执行原像素四点透视校正与双三次采样，不采用 AI 重绘、OCR 补字、去反光、填补、锐化或颜色替换。人工已查看原件与校正 PNG：标题、计分文字、插画、边框保留；照片失焦、曝光与保护套的粉色反光也保留。放大输出没有创造额外细节。操作、工具版本、源/输出校验值与配方校验值见 `texture-correction.json`。

圆角外的少量白底保留，本批薄盒网格没有还原圆角；单张照片无法确定纸面粗糙度、微观纸边和准确比例。照片中文字只作视觉参考，不改变官方规则数据。

## 网格、背面与尺度

工作尺寸为估算 54 × 80 × 0.3 mm，非实测或用户确认。源 `.blend` 使用米制，枢轴为底面中心，实际封闭网格有 1 个 mesh、12 个三角面；正面、纸边和未知背面使用三个独立材质槽。`backTexture=null`，背面为 `defender-card_UnseenBackNeutral` 中性底色，没有声称还原实物背面。

正式产物：

- `art/source/entity-conversion-v001/defender-card/defender-card.blend`，正面贴图已打包；同目录的 `preview-stage.blend` 为陈列预览场景。
- `art/source/entity-conversion-v001/defender-card/textures/defender-card-front.png`，以及导出目录中的同名 PNG 依赖。
- `art/exports/entity-conversion-v001/defender-card/defender-card.fbx`、`manifest.json`、`verification.json` 与 `clean-rebuild-verification.json`。
- `art/previews/entity-conversion-v001/defender-card/{top,side,game-camera,crowded-vertex}.png`，四图均为 1600 × 1000 px。

生产工具为 Blender 4.5.14 LTS。显式 FBX 使用 `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，输出三角面且无动画。FBX 独立回读确认闭合、无退化三角形、UV 完整、底心枢轴与所有材质槽实际使用；尺寸与配方差值小于 0.01 mm。

## 复现与实际验证

在仓库根目录执行：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/defender-card/recipe.json
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type defender-card
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type defender-card -OutputRoot .local/art-pipeline/rebuild/defender-card -SkipPreviews -CompareRoot art
```

已有正式 manifest 的目录不会被覆盖；以上正式生成命令用于首次生产，后续复现应使用新的干净输出目录。贴图正常重跑逐字节比较。本次正式生成日志为 `.local/art-pipeline/logs/defender-card-20261009-232818-362-{generate,verify}.log`；干净目录日志为 `defender-card-20261009-232855-912-{generate,verify}.log`。首次沙箱内 Blender 未启动，未产生模型；随后获自动审核接受的执行完成生产和独立回读。

2026-10-09 实际逐张查看四张正式 PNG：俯视与两张斜视中卡片正面完整、标题朝向正确；侧视为真实 0.3 mm 薄卡边缘，在这一总览尺度下接近细线，不能检查正面印刷。通用陈列将这张卡排在画面左侧。`crowded-vertex` 只是类型斜视陈列，不是规则棋盘节点拾取验收；四图没有背面视角。

正式 FBX 回读 `status=passed`。干净目录重新生成网格和贴图依赖并回读通过，`clean-rebuild-verification.json` 的 `sameGeometryMaterialsTexturesReferences=true`：几何、UV、材质、纹理、来源、种子、尺度与导出参数一致。源 JPG 校验未变，照片校正正常重跑通过。复现比较实际内容签名，不要求带工具容器数据的 `.blend`/FBX 字节完全相同。

Unity 导入、运行时翻面及渲染由主代理统一集成验证，本类型子任务没有启动 Unity；实际背面、实体版本、带尺尺寸、纸面材质、用户最终外观与游戏内拾取仍待确认。完成本说明后运行 `tools/art/update_entity_checklist.py`，清单据实际证据自动刷新。
