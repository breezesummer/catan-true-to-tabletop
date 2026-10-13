# 城市设施照片转化 v001

类型 `city-improvements`，共 17 件实际网格：15 件双面等级翻板、2 件整板照片提示资产。读取项目 README、PROJECT_BRIEF、ROADMAP 当前阶段及 ART_PIPELINE 后制作；这是资产流水线交付，不代表 M8 退出条件或用户外观验收已完成。

## 变体、尺寸与来源

| 组别与 ID | 1 级 | 2 级 | 3 级 | 4 级 | 5 级 |
| --- | --- | --- | --- | --- | --- |
| 绿色科学 `improvement-science-1..5` | 修道院 | 圖書館 | 引水渠 | 劇場 | 大學 |
| 蓝色政治 `improvement-politics-1..5` | 市政廳 | 教堂 | 要塞 | 大教堂 | 卡坦議會 |
| 黄色贸易 `improvement-trade-1..5` | 市場 | 行會 | 同業公會 | 銀行 | 商業中心 |

照片文件名使用简体，资产 label 使用观察到的印刷题名；绿色四级照片实际印「劇場」，不是文件名「剧院」。正面为题名、等级与商品费用面，反面为建筑插画及红骰点面；按原照片文件名配对，不把未拍面自动复用。另有 `guidance-upgrade`（费用面整板）与 `guidance-max`（插画面整板），各自仅包含已见的一面，底面使用明确中性纸板材质。

等级翻板从照片观察近正方形，工作尺寸估算 **40 × 40 × 1 mm**，贴图为 **1024 × 1024 px**。整板三色组合外轮廓约 2.25:1，工作尺寸估算 **180 × 80 × 1 mm**，贴图为 **2304 × 1024 px**。全部 `kind: panel`；未套用资源卡的 54 × 80 mm 比例。实测尺度、实际厚度、卡槽、铰链、纸面材质和正反面配准尚未知，不能把这些估算视为用户确认。

原件来自 `assets/references/inbox/城市设施` 的全部 32 个 JPG（30 个等级单面 + 2 个组合板）；`recipe.json` 逐件登记路径、SHA-256 与用途。三张原件为 3072 × 4096 px，其余等级照为 1279 × 1706 px，整板照为 1706 × 1279 px。原件保存不变。

## 像素校正与限制

已人工查看全部原照联系表，并查看典型原照、裁切边界和两张整板原照。`textureJobs` 的 `quad` 为人工读取卡板四条外直边延长线交点，顺序为 TL/TR/BR/BL。`prepare_recipe.py` 保留这些读数；高分辨率原件的读数从检查副本映射回原始像素坐标，实际校正在原照片上执行。

共享 `rectify_entity_photos.py` 使用 PIL 四点投影与双三次插值，只采样原照片像素。没有 AI 重绘、OCR 替字、补画、去反光、锐化、颜色替换或规则文字重排。曝光、印刷网点、模糊、边缘磨损和反光照原样保留，输出采样放大不会增加真实细节。`texture-correction.json` 记录每件方法、四角、源与输出 SHA-256。

组合板裁切包含可见组件整体边界，避免把整幅照片外部桌面贴到面板上。原照片内部孔洞、卡槽间隙、单卡歪斜与分层仍是照片像素，本资产是静态照片拼板展示，**尚不是可操作城市建设控件**。15 张独立双面翻板可作为后续状态控件的视觉资源，但本子任务没有接入操作逻辑。照片上的旧版提示只作视觉来源，不修改官方 2025 规则。

圆角尚未在薄盒工作网格上建模，角外可能有极少桌面像素。两张组合板底面未见，真实拼装结构没有推测为已还原。

## 可复现导出

在仓库根目录首次生产：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city-improvements
```

清洁输出目录重建并比较：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city-improvements -OutputRoot .local/art-pipeline/rebuild/city-improvements -SkipPreviews -CompareRoot art
```

交付冻结后，PNG 重跑会核对字节相同；改四角或贴图需要新版本。共享生成器拒绝覆盖已有资产源和导出，复现应选择新的空输出目录。`prepare_recipe.py` 仅保留元数据作者流程；正常导出直接读取冻结的 `recipe.json`。

Blender **4.5.14 LTS** 生成米制实际网格，底面中心枢轴，显式 FBX `axisForward=-Z` / `axisUp=Y` / `globalScale=1` / `applyUnitScale=true` / `FBX_SCALE_UNITS`，三角化、无动画。每件 1 个封闭网格、12 个三角面，包含前面、反面/未知底面中性材质及纸边三槽。源 blend 打包贴图，导出目录保留独立 PNG 依赖。

产物位于：

- `art/source/entity-conversion-v001/city-improvements`：17 个资产 `.blend`、1 个预览场景、32 个校正 PNG。
- `art/exports/entity-conversion-v001/city-improvements`：17 个 FBX、32 个 PNG 依赖、manifest、verification、clean-rebuild-verification。
- `art/previews/entity-conversion-v001/city-improvements`：`top`、`side`、`game-camera`、`crowded-vertex` 四张 1600 × 1000 PNG。

## 实际验证与待验

2026-10-09 完成正式 wrapper 与新目录重建。17 件独立 FBX 回读均通过：封闭/非退化三角网格、UV、材质使用、底面中心、尺寸误差低于 0.01 mm、纹理与全部 32 份原件 SHA-256。两份报告均 `status: passed`；`clean-rebuild-verification.json` 中 `sameGeometryMaterialsTexturesReferences: true`，几何、UV、材质、贴图、来源、种子与导出参数一致。

已逐项查看全部 32 个校正 PNG（30 个等级面联系表及 2 块整板放大图），另放大检查科学四级题名、政治三级插画及高分辨率市场正面。印刷文字、骰点、商品和插画方向完整，边框没有大面积外桌背景。

四张 Blender 预览已实际打开查看：俯视及两个斜视包含全部 17 件，三色与等级对应正确；侧视显示薄纸板边缘并分层陈列。由于两块整板比翻板宽，通用布局留白较大，等级文字不适合在这四图中细读，详细文字检查依据校正 PNG。侧视陈列不是实际桌面状态；`crowded-vertex` 是通用斜视，并非拥挤规则节点拾取验收。四图只显示正面，反面 PNG 已人工查看，FBX 背面材质/UV/面使用已验证；Unity 翻板显示由主代理统一集成检查。

生产日志 `.local/art-pipeline/logs/city-improvements-20261009-230159-587-{generate,verify}.log`；重建日志与新目录报告保留在 `.local/art-pipeline`。wrapper 已归档干净重建证据并自动更新总清单。

本类型子任务未启动 Unity、未修改游戏规则或路线图。Unity 导入/运行、后续真实城市建设交互、用户尺寸与最终外观确认由统一集成/验收另行报告。
