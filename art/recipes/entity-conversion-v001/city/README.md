# 城市棋子转化 v001

2026-10-09 已实际查看 `assets/references/inbox/建筑/红色城市.jpg`。原件不变，SHA-256 在配方中记录。

## 制作内容

实际参数化三维网格包含圆形双阶底座、三个高度不同的坡屋顶大厅、屋脊与屋面凸纹、突出门廊、四根立柱、台阶、窗框与墙面浮雕。城市有独立成组屋顶与门廊布局，没有采用放大村庄的外形。所有同材质组件合并为一个网格，内部保留封闭几何壳与 UV，单红色塑料材质。最终 UV 使用确定性按面投影图集，每个面独立排列，避免 Blender 自动打包的浮点差异；本模型无需依赖贴图。

源坐标为 X 宽、Y 深、Z 高，米制。包围盒 `18 × 18 × 20 mm` 是估算，底面中心为枢轴。背面和底面细节、实测尺寸未知；未见结构按简化对称形式补齐。原照颜色受曝光、照明和白平衡影响，材质颜色只作近似。

## 复现与验证

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type city -OutputRoot .local/art-pipeline/rebuild/city -SkipPreviews -CompareRoot art
```

已使用 Blender `4.5.14 LTS` 执行正式生成与 FBX 回导，产物为一个网格、一个材质、15,448 个三角面。检查通过：全部几何壳封闭、UV 齐全、无退化三角面、材质使用正确、底心枢轴及估算包围盒一致，原参考哈希不变。独立干净目录重建的几何/UV、材质、来源和导出参数与正式产物严格一致，报告保存到导出目录 `clean-rebuild-verification.json`。

实际查看了最终俯视、侧视、游戏相机和 `crowded-vertex` 四张 `1600 × 1000` 预览；可辨认圆形双阶底座、门廊/柱、组屋顶及台阶。高度采用 20 mm 估算，形体较修长，真实比例仍需实测/用户确认。此单资产预览中的 `crowded-vertex` 仅显示一件城市，不构成相邻棋子拥挤摆放验收；完整占地/拾取由 Unity 集成检查。正式输出不允许覆盖；已有该版本时应使用新输出目录。

这是照片参考的参数化近似模型，非摄影测量或扫描复原。正式 Unity 灯光、拾取、拥挤摆放和用户外观确认尚未由本子任务验证，交由主任务集成验收。
