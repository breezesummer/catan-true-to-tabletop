# 蓝色政治进步卡照片转化 v001

类型为 `progress-politics`，范围为九种原照片正面。原照片只读保留，采用用户确认的原像素忠实校正路线，保留印刷插画与文字。本类型已阅读项目 README、PROJECT_BRIEF、ROADMAP 当前阶段及 ART_PIPELINE；该交付属于资产生产，不宣称 M8 或实物外观验收完成。

蓝色背面没有提供，九项 `backTexture` 均为 `null`。网格背面使用显式命名的 `_UnseenBackNeutral` 中性材质，状态为 `unseen; neutral material only`。没有借用黄色或绿色背面，也没有改色仿造蓝色背面。真实背面、尺寸、纸面材质与实物版本仍未知。

## 来源、视觉角色与保留内容

`recipe.json` 保存九张原件路径、SHA-256、像素尺寸、用途、手工读取的外卡四角和稳定英文资产 ID。四角顺序为输出 TL/TR/BR/BL 对应的原件点位，取卡片直边延长线交点，排除保护套外缘。九张均为正向，不需旋转，`rotation=0`。

| 原照片印刷题名 | 原件文件名 | 像素尺寸 | 视觉角色 / 资产 ID 后缀 | 逐张查看要点 |
| --- | --- | --- | --- | --- |
| 外交官 | 外交官.jpg | 1279 × 1706 | Diplomacy / diplomacy | 人物头在上、酒杯在左；完整长说明保留 |
| 間諜 | 间谍.jpg | 1279 × 1706 | Espionage / espionage | 酒馆屋梁在上、两个人物在下；暗部与模糊保留 |
| 指揮官 | 指挥官.jpg | 1279 × 1706 | Encouragement / encouragement | 骑马人物在上、持矛士兵在左下；两行原说明保留 |
| 權謀 | 权谋.jpg | 3072 × 4096 | Intrigue / intrigue | 持盾骑士在右、隐蔽人物在左；原船只字样保留 |
| 主教 | 主教.jpg | 1279 × 1706 | Taxation / taxation | 主教帽在上、乡间路在左；曝光与原说明保留 |
| 石碑 | 石碑.jpg | 1279 × 1706 | Constitution / constitution | 立碑在上、1 分在下；立即打出的原括号说明保留 |
| 叛徒 | 叛徒.jpg | 1279 × 1706 | Treason / treason | 红旗在上、蓝衣在地面；完整五行说明保留 |
| 婚禮 | 婚礼.jpg | 1279 × 1706 | Wedding / wedding | 教堂在右、婚礼人物在左下；原资源/商品说明保留 |
| 破壞者 | 破坏者.jpg | 3072 × 4096 | Sabotage / sabotage | 房顶在上、火焰居中；原弃牌与向下取整说明保留 |

上述视觉映射来自本地 `src/Catan.Core/M4ProgressModels.cs` 的政治卡枚举，仅用于资产识别和后续展示映射。照片保留旧中文名称和原有印刷效果；主教、指揮官、石碑等与官方 2025 名称不同，照片文本不是当前规则来源，不据此修改规则。实物版本及规则差异待确认。

仅使用四点透视变换和双三次插值，输出九张 1024 × 1536 PNG。没有 OCR、补字、重排文字、AI 重绘、锐化、去反光或颜色替换。原照片失焦、印刷网点、色偏和保护套反光保留，输出尺寸增加不会创造新细节。卡片圆角外保留极少原背景；本批实际网格为矩形薄盒体，圆角轮廓仍待细化。操作、输入/输出 SHA-256 在 `texture-correction.json`。

## 尺度、网格和复现

工作尺寸为估算 54 × 80 × 0.3 mm，未经实测或用户确认。源文件使用米制，卡底面中心为枢轴。每卡为 1 个封闭 mesh、12 个三角面，分别使用正面照片、未知中性背面和纸边三个材质槽，实际资产包含几何与 UV，不只是位图。

照片处理（在仓库根目录执行）：

```powershell
& 'C:/Users/BreezeSummer/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe' tools/art/rectify_entity_photos.py --recipe art/recipes/entity-conversion-v001/progress-politics/recipe.json
```

正式生产与独立 FBX 回读：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-politics
```

独立新目录清洁重建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type progress-politics -OutputRoot .local/art-pipeline/rebuild/progress-politics -SkipPreviews -CompareRoot art
```

2026-10-09 实际运行上述三个命令；流水线再次校正时九张冻结 PNG 字节均一致。Blender 为 4.5.14 LTS，FBX 导出为 `axisForward=-Z`、`axisUp=Y`、`globalScale=1`、`applyUnitScale=true`、`FBX_SCALE_UNITS`，三角化且无动画。回读确认尺寸误差低于 0.01 mm、封闭几何、UV、底面中心枢轴和三个材质槽均有面使用；九张依赖 PNG 校验通过。

正式产物为 `art/source/entity-conversion-v001/progress-politics` 中九个资产 `.blend`、一个 `preview-stage.blend` 和九张源纹理；`art/exports/entity-conversion-v001/progress-politics` 中九个 `.fbx`、九张依赖纹理、`manifest.json`、`verification.json` 和 `clean-rebuild-verification.json`；`art/previews/entity-conversion-v001/progress-politics` 中四张固定视图。

正式日志为 `.local/art-pipeline/logs/progress-politics-20261009-230722-555-{generate,verify}.log`，清洁重建日志为 `progress-politics-20261009-230809-493-{generate,verify}.log`。正式回读与清洁重建均为 `status=passed`，重建比较为 `sameGeometryMaterialsTexturesReferences=true`；实际几何、UV、材质、纹理、参考、种子与导出设置一致。容器文件字节包含工具元数据，重建采用实际内容签名比较。

## 实际查看与待验证范围

九张原照片与九张校正 PNG 已逐张人工查看，原插画方向、边框、标题和全部原说明保留。没有对原件做任何写入。

`top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四张 1600 × 1000 正式预览已逐张查看。俯视、默认斜视和斜向陈列均包含九张正面，无遮挡、没有旋转倒置；主教/指揮官/石碑等题名及大部分印刷布局可辨，细文字受整组缩放、原件清晰度和渲染光照限制。侧视呈现九张真实薄卡边缘，采用分层陈列让同列卡边可见，不能视为规则桌面摆放验收。`crowded-vertex` 是通用斜向资产陈列，并非规则棋盘拥挤节点拾取验收。四图均未翻面，缺失背面只验证中性材质存在并有面使用。

- 原件 SHA-256、九张校正 PNG 与稳定 ID：已核对。
- 九个封闭网格、源 blend、依赖纹理、显式 FBX 和四视图：已生成，四图已实际查看。
- 独立 FBX 回读和清洁输出目录复现：九张均通过。
- Unity 导入、渲染、翻牌与拾取：由主代理统一集成验证，本类型子任务未启动 Unity。
- 蓝色真实背面、实物尺寸、版本差异、纸面材质、圆角细化和用户最终外观确认：待补充或验收。

该类型没有改动游戏规则，不增加规则测试或更新路线图阶段状态。
