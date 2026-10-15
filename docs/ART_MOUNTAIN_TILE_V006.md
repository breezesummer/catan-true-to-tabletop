# 高山板块替换矿石资源卡派生地形 v006

2026-10-15，按用户反馈修正棋盘矿石地形的美术来源：原来 v004 使用矿石资源牌插画裁切，现在使用本次提供的实体高山六边形地图板块照片。四种规则模式共用同一地形映射。发行版本沿用当前内测版1.3，本次未请求微信发布压缩包。

## 资产与来源

- 原件：[高山照片](../assets/references/archives/mountain-tile-v006/高山.jpg)，与收件目录及本次附件逐字节一致，SHA-256 为 `3fb55da76875624c901e791892a44e122bde8ee35b343e73f9ac455bf27b084d`。
- 处理：[配方与复现](../art/recipes/entity-conversion-v006/ore-tile/README.md)。只对原像素做六边形几何校正及重采样；保留山峰插画、黄色边框、失焦和光照。
- 网格：闭合六边形厚板，1 个 Mesh、20 个三角面，底面中心轴心；68.58921198×79.2×8 mm 为沿用的估算尺寸。源 `.blend`、FBX、PNG、清单和四视图齐全。运行时延续既有倒角显示。
- 当前资源：`EntityAssets/v006/ore-tile/tile-ore`；矿石手牌仍为 `EntityAssets/v001/resource-cards/resource-ore-front`。旧版照片、卡面和 v004 产物保留追溯。

## 验证

结果以 [验证摘要](verification/mountain-tile-v006/summary.json) 为准。检查范围包括原件及贴图校验、Blender 回读与干净重建、Unity 导入、Windows 构建、四种玩法俯视及游戏相机渲染、矿石手牌独立来源、隐藏地形显示、碰撞与预览色，以及独立进程保存恢复。

相关 .NET 复验使用既有 `TabletopPresentationTests`、`TabletopCameraTests` 和 `OperationDockTests`，112 项通过；没有把旧版全量规则结果计为本轮重跑。

未验证：用户最终外观确认、实物尺寸、未知背面、用户本人完整对局和长期性能。照片中的山峰属于印刷插画，未声称重建为三维山峰。M7／M8 阶段状态保留。

## 当前可玩程序

[CatanM6.exe](../.local/m6-mountain-v006/Builds/Windows/CatanM6.exe)。`Play-M6.ps1`、`Play-AiStrategy.ps1` 和默认构建目录统一指向此版本；原有已发送压缩包保留为历史分发记录。

复跑：

```powershell
./tools/art/Build-MountainTile.ps1
./tools/art/Build-MountainTile.ps1 -OutputRoot .local/art-mountain-v006/rebuild -CompareRoot art -SkipPreviews
./tools/development/Build-M6.ps1 -ImportMountainTile -VerifyPlayer
./tools/validation/Test-EntityIntegration.ps1 -ArtOnly
```

资产输出目录冻结后不可覆盖；重复重建应使用新的 `-OutputRoot`。
