# 地图板块照片转化 v001

类型：`tiles`。范围为草地、海洋、金矿、沙漠、小麦、砖块六张原照的印刷板块；森林保留此前实验样板。本批按用户确认的忠实路线校正原照片，没有 AI 重绘、文字重建或颜色替换。

已读 README、PROJECT_BRIEF、ROADMAP 当前阶段与 ART_PIPELINE。本项是资产流水线交付，不宣称 M8 成品验收完成，也不替用户确认外观或实物尺寸。

## 来源与像素处理

六张原件均为 1706 × 1279 px，逐张人工查看后读取印刷六角顶点；SHA-256、用途和点位保存在 `recipe.json`。原始 JPG 只读。处理结果为版本目录中的六张 1024 × 1024 PNG；工具与操作校验值见 `texture-correction.json`。

| 资产 ID | 原照片 | 人工检查的特征 |
| --- | --- | --- |
| tile-pasture | 草地.jpg | 羊群、池塘、右下岩石、黄边和印刷小标识保留 |
| tile-sea | 海洋.jpg | 原蓝色海面纹理、左上印刷小标识与光照保留 |
| tile-gold | 金矿.jpg | 河流、水面、岩石、黄边和右侧小标识保留 |
| tile-desert | 沙漠.jpg | 沙丘、表面纹理、原噪点与右下小标识保留 |
| tile-fields | 小麦.jpg | 成片麦田、捆束、田间道路、黄边和右上小标识保留 |
| tile-hills | 砖块.jpg | 矿坑、矿工、车架、黄边和左上小标识保留 |

原图为平顶六角。`hex` 六点顺序为原照 R/UR/UL/L/LL/LR，映射到输出右上/顶/左上/左下/底/右下；这是约逆时针 30° 的六角相位转换，`rotation=0` 表示映射后不再旋转 PNG。每张以顶点均值为中心，分成六三角，逐片原像素 affine 映射及双三次插值。插画没有镜像。照片失焦、曝光、照明变化与纸面噪点均保留，放大输出不能补足真实细节。

该方法并非精确镜头标定或全图透视模型；人工顶点误差、圆角裁切、极少边界背景与径向分片插值变化仍可能可见。六角外的中性填色仅用于 PNG 矩形空白区，不是原版组件内容。

## 网格、方向与尺度

输出为有厚度的印刷板块实际封闭网格，并非立体草地、山体、沙丘或海面景观。估算半径 39.6 mm，外框宽 68.58921198 mm、长 79.2 mm、厚 8 mm，尚未实测或获用户确认；厚度沿用工作样板以保证同类可比较。背面没有照片，使用中性边材质，明确登记未知。

Blender 使用米制，底面 `z=0`，枢轴位于底面中心。六角起点为 30°，顶点依次为 (+X,+Y)、顶、(-X,+Y)、(-X,-Y)、底、(+X,-Y)。显式 `frontUv` 为 `(0.9330127,0.75) / (0.5,1) / (0.0669873,0.75) / (0.0669873,0.25) / (0.5,0) / (0.9330127,0.25)`，与校正输出一一对应，避免按默认 80 mm 纹理尺度裁掉图边。

每种板块为 1 个封闭 mesh、20 个三角面，两个实际使用的材质槽：原照片顶面和中性侧边/底面。源 blend 内已打包贴图；显式 FBX 的六张 PNG 依赖另存在导出 `textures` 目录。

## 复现与实际验证

在仓库根目录首次正式生产：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type tiles
```

正常重跑照片校正会比较冻结 PNG 字节，差异需新的版本路径。正式输出目录一旦含资产或清单，生产脚本拒绝覆盖。在独立空目录重建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type tiles -OutputRoot .local/art-pipeline/rebuild/tiles -SkipPreviews -CompareRoot art
```

2026-10-09 实际运行两次 wrapper。工具为 Blender 4.5.14 LTS，FBX `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化且无动画。六张原照与六张校正 PNG 已逐张查看；正式生产及独立 FBX 回读均通过，原件校验值不变，网格封闭、UV 有效、全部材质槽使用、尺寸误差低于 0.01 mm、底面中心枢轴正确。

已实际查看四张 1600 × 1000 预览：`top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png`。俯视和两张斜视均显示六种完整插画和边框，贴图朝向与校正 PNG 一致；侧视显示真实厚度。侧视为保证每项可见的分层陈列；`crowded-vertex` 为通用斜视陈列，不能作为规则棋盘的拥挤节点拾取验收。未展示未知背面，也未在本类型子任务启动 Unity。

正式产物位于 `art/source/entity-conversion-v001/tiles`（六源模型、贴图与预览场景）、`art/exports/entity-conversion-v001/tiles`（六 FBX、manifest、verification 与依赖）以及 `art/previews/entity-conversion-v001/tiles`（四视图）。生产日志为 `.local/art-pipeline/logs/tiles-20261009-225042-058-{generate,verify}.log`。

清洁重建结果 `.local/art-pipeline/rebuild/tiles/exports/entity-conversion-v001/tiles/verification.json` 为 `status=passed`、`sameGeometryMaterialsTexturesReferences=true`；六资产几何/UV、材质、纹理、来源、种子、尺度与导出参数完全一致。重建日志为 `tiles-20261009-225133-418-{generate,verify}.log`。容器的 FBX/blend 字节可能带有工具信息，复现判据为实际内容签名。

wrapper 已刷新实体转换清单。本子任务未改变规则和路线图；Unity 导入、渲染与交互由主代理统一集成验证。实测尺寸、板块背面、原版外观与最终用户验收仍待确认。
