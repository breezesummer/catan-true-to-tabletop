# 船棋子转化 v001

2026-10-09，已实际查看红色、蓝色、橘色、米色船四张原始照片；原件保持不变，SHA-256 记录在 `recipe.json`。

## 制作内容

四个独立资产使用同一可复现几何、四套塑料材质。实际三维网格包括长窄凹甲板船体、上翘首尾、沿船体曲线的三道凸起船舷、上沿、后舱双窗与门框浮雕、桅杆、带封闭厚度的单弧面帆，以及桅顶圆帽。照片中可见的轮廓、单帆与后舱是本次保留的特征；没有沿用道路加桅杆的占位外形。每船由 44 个封闭几何部件构建，再按材质合为 2 个渲染网格，总计 6,188 个三角面，保留全部 UV 岛。

配方本地坐标为 X 宽、Y 长、Z 高；估算包围盒 `9 × 26 × 28 mm`，自定义 builder 将几何烘焙为米制并归零底心，流水线导出使长轴对应 Unity Z。底心枢轴为估算支承面；水线估算为底面以上 `1.6 mm`。

## 复现

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type ships
# 已有 v001 产物时，使用新的输出目录，不能覆盖原版本。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type ships -OutputRoot .local/art-pipeline/rebuild/ships-next -SkipPreviews -CompareRoot art
```

## 实际验证结果

Blender `4.5.14 LTS` 已生成四套 `.blend` / `.fbx`，位于 `art/source/entity-conversion-v001/ships` 和 `art/exports/entity-conversion-v001/ships`。独立从 FBX 回读四件均通过：2 个网格、6,188 三角面、所有边使用两次（封闭）、无退化面、UV 完整、两种材质槽全部实际使用、各对象原点归零、底面 Z=0、X/Y 包围盒居中、米制尺寸约 `[0.009, 0.026, 0.028]`。四张原件 hash 均未变化。

`art/previews/entity-conversion-v001/ships` 的顶视、船首侧视、游戏相机与节点相机四张 1600×1000 图均已实际查看；模型摆放、四色差别、帆厚度与后舱可辨认。临时横侧视也已检查，保存在 `.local/art-pipeline/ship-debug-001/previews/entity-conversion-v001/ships/broadside-check.png`，用于对照原照单帆、双窗后舱和首尾曲线；临时横侧视不属于正式输出 manifest。

另在全新目录 `.local/art-pipeline/rebuild/ships` 重建四件，跳过重复预览并与正式资产比较，几何、UV、材质、纹理依赖（本类为零）、参考来源、估算尺寸、随机种子及导出参数均一致。正式 `verification.json` 保存反向比较记录。正式产出与重建调用的 wrapper 均已自动更新资源清单。

本子任务没有启动 Unity，也没有改动规则或更新开发路线。Unity 导入后的轴向、实际渲染、拾取和运行表现仍待主任务集成验收；本次 Blender 验证不能代替 Unity 验证。

## 限制

照片均为单个斜侧视，背面、底面与实测长宽高未知。未见侧面按对称、舱门按简化浮雕近似，颜色仅为原照光照下的近似。这个结果是照片参考的参数化模型，不是扫描复原；正式 Unity 灯光、拾取、拥挤摆放以及用户外观验收由主任务集成验证。
