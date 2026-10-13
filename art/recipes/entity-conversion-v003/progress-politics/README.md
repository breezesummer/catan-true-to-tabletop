# 政治进步卡背面补转 v003

原件 `assets/references/inbox/发展卡/蓝色发展卡/卡背.jpg` 在冻结的 v001 目录清单中遗漏。本批将这一张照片做原像素透视校正，绑定到全部九种政治进步卡背面。九张 v001 正面按原字节复制，已有 v001 产物保留。

输出为九件闭合双面薄卡网格，各有 `.blend`、显式 `.fbx`、正背面 PNG 与底面中心轴心；不是立体雕塑。尺寸仍为估算 54 × 80 × 0.3 mm。原照色偏、照明、保护套反光和模糊保留，圆角外可能保留少量原背景；没有 AI 重绘或改变规则文字。

`prepare.py` 从保留的原照及 v001 冻结正面确定性重建新纹理、配方与来源登记。角点为卡片直边延长线交点，记录在 `recipe.json` 和 `source-outline-inspection.png`。`build.py` / `verify.py` 复用公共工具并设置新的版本目录，公共工具不作修改。生成清单另记录版本包装器 SHA-256。

在项目根目录使用固定 Python 和 Blender 工具链：

```powershell
$politicsPython = 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$politicsBlender = (Get-Content tools/development/toolchain.json -Raw | ConvertFrom-Json).blenderExecutable
& $politicsPython art/recipes/entity-conversion-v003/progress-politics/prepare.py
& $politicsBlender --background --python-exit-code 1 --python art/recipes/entity-conversion-v003/progress-politics/build.py -- --recipe art/recipes/entity-conversion-v003/progress-politics/recipe.json --output-root art
& $politicsBlender --background --python-exit-code 1 --python art/recipes/entity-conversion-v003/progress-politics/verify.py -- --recipe art/recipes/entity-conversion-v003/progress-politics/recipe.json --output-root art
```

生成器拒绝覆盖已有网格产物。干净重建选择尚不存在的 `--output-root`，验证时追加 `--compare-root art`；重新请求纹理会检查已有字节一致性。验证包括 FBX 回读、真实闭合网格、UV、三角面、轴心、估算尺寸、原件哈希、全部背面绑定、正面不变，以及干净重建几何/材质/纹理一致。四种常规视图加背面视图仅为类型陈列图；Unity 接入与用户最终外观确认需独立验收。

2026-10-10 实际运行 Blender 4.5.14 LTS 生成和回读：9/9 通过，全部背面引用新原照贴图，九张正面与 v001 哈希一致。干净目录重建 9/9 通过，结果在 `art/exports/entity-conversion-v003/progress-politics/{verification,clean-rebuild-verification}.json`。人工检查原照、校正图、角点图及全部五张 Blender 陈列视图，背面翻转方向已修正后重建。本子任务没有运行 Unity。

`audit_photo_coverage.py` 只读核对原件、备份及源/导出/贴图文件，并更新本目录的 `photo-coverage-audit.json`。当前 135 张不重复原照全部进入版本化导出来源，原件/36 张备份哈希无变化、资产依赖无缺失。保留各版本的 FBX 总计 123，唯一资产 ID 为 114；本批九件替代已有九种政治卡，不能重复算成九种新组件。其他批次的 Blender/Unity 验证只读取既有记录，没有在本次重复运行。
