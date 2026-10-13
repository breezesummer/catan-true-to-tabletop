# 铁矿资源卡插画派生板块 v004

2026-10-11，将棋盘铁矿改为平坦的实体卡片风格。来源是 `assets/references/inbox/资源牌/铁矿石.jpg`，已有原照片校正卡面 `art/source/entity-conversion-v001/resource-cards/textures/resource-ore-front.png` 裁切 `[112,392,912,1192]`，再用 bicubic 缩放为 1024×1024，映射到六边形顶面。来源、继承卡面与派生图 SHA-256 见 `recipe.json` 和 `texture-derivation.json`。

没有收到真实六边形山地板块照片，本资产明确是资源卡插画派生；原照、完整资源手牌和旧模型保留。只裁切与重采样原像素，不做 AI 重绘或修改规则。实际网格为闭合厚板，1 个 Mesh、20 个三角面、底心轴心，估算尺寸 68.5892×79.2×8 mm，背面为未知中性材质。

本批 `.blend`、显式 `.fbx`、PNG、清单和四视图在 `art/{source,exports,previews}/entity-conversion-v004/ore-tile`。Blender 4.5.14 LTS 回读及干净重建 1/1 通过；Unity 6000.3.25f1 导入和四类正式显示验证通过，`oreUsesFlatCardArt=true`。验证与人工目检记录见 [交付说明](../../../../docs/ART_ORE_TILE_V004.md)。尺寸、色彩、插画裁切与最终外观仍待用户确认。

在项目根目录用固定 Python 运行 `prepare.py`；固定 Blender 运行 `build.py` / `verify.py`，使用本目录的 `recipe.json`。例如：

```powershell
$orePython = 'C:\Users\BreezeSummer\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
$oreBlender = (Get-Content tools/development/toolchain.json -Raw | ConvertFrom-Json).blenderExecutable
& $orePython art/recipes/entity-conversion-v004/ore-tile/prepare.py
& $oreBlender --background --python-exit-code 1 --python art/recipes/entity-conversion-v004/ore-tile/build.py -- --recipe art/recipes/entity-conversion-v004/ore-tile/recipe.json --output-root .local/art-pipeline/ore-v004-new
& $oreBlender --background --python-exit-code 1 --python art/recipes/entity-conversion-v004/ore-tile/verify.py -- --recipe art/recipes/entity-conversion-v004/ore-tile/recipe.json --output-root .local/art-pipeline/ore-v004-new --compare-root art
```

输出目录须尚无资产；版本冻结文件不覆盖。干净重建比较几何、UV、材质、纹理和来源，不要求 FBX/BLEND 二进制逐字节相同。
