# 高山地图板块 v006

来源为用户 2026-10-15 提供的高山地图板块照片；该地形产出矿石。归档原件位于 `assets/references/archives/mountain-tile-v006/高山.jpg`，与收件目录原照及本次附件 SHA-256 一致。矿石资源手牌继续使用完整资源牌照片。

`prepare.py` 记录六个顶面内沿角点，将原像素分为六个三角形做 affine 校正，以 bicubic 重采样为 1024×1024。保留黄色边框、山峰插画和照片原有模糊及光照；未做生成、重绘、锐化或颜色替换。`source-inspection.png` 标出角点，`texture-correction.json` 记录原件、脚本、工具版本和输出校验值。旧 v004 派生资产保留用于追溯。

厚板采用现有 Blender 显式网格导出器，1 个闭合 Mesh、20 个三角面、底心轴心；估算尺寸 68.58921198×79.2×8 mm。源 `.blend`、FBX、贴图、清单和顶视／侧视／游戏相机／拥挤节点四视图分别保存。照片中的高山插画不代表三维山峰网格，未知背面使用中性材质。

生成与回读：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-MountainTile.ps1`。

干净重建：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-MountainTile.ps1 -OutputRoot .local/art-mountain-v006/rebuild -CompareRoot art -SkipPreviews`。

Unity 导入与当前 Windows 构建：`powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-M6.ps1 -ImportMountainTile -VerifyPlayer`。

完整验证与可玩入口见 [本轮说明](../../../../docs/ART_MOUNTAIN_TILE_V006.md)。
