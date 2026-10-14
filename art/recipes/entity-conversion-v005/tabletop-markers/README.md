# 实体港口与圆片标记转化 v005

2026-10-14 转化本批 8 张照片：3:1 港口、木头/砖/羊毛/小麦/矿石五种 2:1 港口、点数牌共同背面、卡坦分标记。原件保留于 `assets/references/inbox/未分类`；分类归档逐字节复制至 `assets/references/archives/tabletop-markers-v005/{ports,tokens}`。8 张唯一 SHA-256、来源原路径、归档路径、用途和未知面登记于 `assets/references/catalogs/tabletop-markers-v005.json`，不重复计数分类副本。

本批沿用用户确认的原照片几何校正方式。没有 AI 重绘、去反光、锐化、颜色替换、OCR 或印刷文字再生成；照片中的字样不作为规则依据。实施前已读项目说明、当前路线、美术流水线和转化清单。

## 原像素处理与轮廓

六港口是带中央柄、两侧圆耳与沙岸平边的异形纸板，不能以圆片或矩形替代。照片先缩为 600×450 工作图，仅用 RGB 色差阈值提取轮廓（色差 >24、R<210，9 px 闭运算），沿像素格外边界取最大闭合环，再以 0.85 px 容差简化。色差处理只参与边界读数，不改动输出 RGB。沙岸中间 60% 的底边拟合直线，校平后记录外接四角，再对原 4096×3072 照片进行四点重投影与双三次采样，输出 1024×792 PNG。实际网格沿该轮廓裁切，因此凹入肩部不显示照片背景。

两圆片逐张目检外接椭圆矩形，重投影成 1024×1024。数字背面船图案另顺时针转 26°，使印刷船竖直；该矩形旋转产生的外角黑区位于圆形网格之外。卡坦分标记保留原照片的黄红天空与田野插画。坐标、图像尺寸、旋转、边界及工具校验值完整记录于 `recipe.json` 和 `texture-correction.json`；八张 `*-source-inspection.png` 保存原件边界读数。

这是估算尺度的工作校正，未经镜头/平面标定。小段纸边磨损、少量边界背景、原照色偏、模糊和照明继续保留；放大贴图不能补足照片细节。

## 网格与坐标

| 资产 ID | 内容 | 估算外框毫米 |
| --- | --- | --- |
| `port-generic` | 3:1 通用港口 | 44 × 34 × 1.5 |
| `port-wood` | 2:1 木头港口 | 44 × 34 × 1.5 |
| `port-brick` | 2:1 砖港口 | 44 × 34 × 1.5 |
| `port-wool` | 2:1 羊毛港口 | 44 × 34 × 1.5 |
| `port-wheat` | 2:1 小麦港口 | 44 × 34 × 1.5 |
| `port-ore` | 2:1 矿石港口 | 44 × 34 × 1.5 |
| `number-token-back` | 点数牌共同背面样板圆片 | 21 × 21 × 0.45 |
| `catan-victory-marker` | 卡坦分标记圆片 | 18 × 18 × 1.5 |

**全部尺寸为估算，没有尺量或用户尺度验收。** 点数背面厚度/直径匹配当前游戏数字圆片的工作尺寸，不代表实体实测。另一面照片与纸芯细节缺失，使用明确标为未知面的中性材质。

每件资产是实际闭合厚纸板 mesh，1 个网格对象、原照片面/估算纸芯/未知中性面三个实际使用的材质槽。港口按非凸轮廓拉伸，圆片为 64 段圆柱；不是把位图当成立体网格交付，也没有制作港口三维景观。

Blender 米制底面 z=0，枢轴为底面中心，印刷面朝 +Z。港口中央柄沿 +Y、沙岸沿 -Y；导入 Unity 后印刷面朝 +Y、柄沿 -Z、沙岸沿 +Z。沿岸摆放应让沙岸朝陆地、柄朝外海。所有 UV 使用归一化坐标，与照片校正一一对应。

源模型位于 `art/source/entity-conversion-v005/tabletop-markers`，FBX/依赖位于 `art/exports/entity-conversion-v005/tabletop-markers`，四视图位于 `art/previews/entity-conversion-v005/tabletop-markers`。数字背面贴图固定命名 `number-token-back.png`，供当前规则决定的数字正面共用；它不产生新数字或规则状态。

## 复现与验证

首次生成（正式目录必须为空；既有版本拒绝覆盖）：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-TabletopMarkers.ps1
```

独立空目录重建与内容对比：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-TabletopMarkers.ps1 -OutputRoot .local/art-tabletop-markers/clean-rebuild -SkipPreviews -CompareRoot art
```

`prepare.py` 可从分类归档原件生成；干净克隆中不存在未分类原位副本时仍可复现。已有源贴图逐字节校验，不覆盖不同内容。大量可复现 FBX、blend、导出依赖与预览按项目现有忽略策略本地保存；分类原件、源 PNG、配方和验证记录纳入版本保存。

2026-10-14 实际使用 Blender 4.5.14 LTS，显式 FBX 参数沿用固定流水线：`axis_forward=-Z`、`axis_up=Y`、单位缩放 1、`FBX_SCALE_UNITS`、三角化、无动画。正式生成和独立重建均完成 .blend/.fbx 回读；8/8 网格闭合、无退化三角形、UV 完整、全部材质槽有面、底心轴心正确；原照与归档 SHA-256 保持一致。`verification.json` 和 `clean-rebuild-verification.json` 均为 `status=passed`，干净重建几何、UV、材质、依赖纹理、来源及导出参数一致。

已人工查看原照联系表、六港口轮廓标注、八张校正贴图和四张 1600×1000 Blender 预览。俯视港口字样与图标完整，两圆片无镜像；两斜视见实际厚边；侧视将后排分层陈列以避免互遮。`crowded-vertex` 是类型陈列，并非实际棋盘节点拾取证据。目检范围见 `art/exports/entity-conversion-v005/tabletop-markers/visual-review.json`；尺寸与来源审计见同目录 `source-provenance-audit.json`。

本子任务不运行 Unity、不改变规则、不提交代码。Unity 实际导入、正式游戏接入与交互由主任务另行验证；最终用户外观、实测尺寸、未见面及长期性能仍待验，不能据本批转化宣称 M8 完成。
