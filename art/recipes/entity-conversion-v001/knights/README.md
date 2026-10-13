# 骑士照片参考转化 v001

2026-10-09 已逐张检查 `assets/references/inbox/单位/红色*骑士.jpg` 六张原照，制作三级旗形 × 戴盔/无盔六个可导入的实际网格。原始 JPEG 未修改，路径与 SHA-256 固定在 recipe 和导出 manifest 中。

可见特征包括红色塑料圆形双阶底座、分开的双腿与靴子、腿甲、躯干、弯曲持旗手、长旗杆、风衣、另一侧盾牌、无盔面部，以及覆盖脸部的灰绿头盔/颈甲。等级用照片可见的三角旗、单燕尾旗、双燕尾旗区分。此交付是照片引导的参数化雕塑近似，没有将图片当作 3D 模型，也不是扫描或摄影测量。

尺寸 `[12,12,24]` mm 是包围盒估算，源 Blender 使用米制，底面与枢轴为原点，支承圆底座及 X/Y 包围盒中心对齐。正式 FBX 使用 `-Z` 前向、`Y` 向上和 1 米单位。每件合并为一个 mesh；无盔使用一个红塑料材质，戴盔使用红塑料和灰绿头盔两个材质。独立封闭壳相互重叠，尚未作布尔融合或打印用实体化。

| 导出 ID | 照片依据 | 三角面 |
| --- | --- | --- |
| knight-level-1-resting | 红色一级骑士、三角旗、无盔 | 11,160 |
| knight-level-2-resting | 红色二级骑士、单燕尾旗、无盔 | 11,168 |
| knight-level-3-resting | 红色三级骑士、双燕尾旗、无盔 | 11,176 |
| knight-level-1-active | 红色戴头盔一级骑士、三角旗 | 11,120 |
| knight-level-2-active | 红色戴头盔二级骑士、单燕尾旗 | 11,128 |
| knight-level-3-active | 红色戴头盔三级骑士、双燕尾旗 | 11,136 |

`active`/`resting` 是戴盔/无盔的暂定显示文件映射，仍待用户确认，不是已确认的实体规则差异，不改变规则逻辑。只有红色有照片依据，运行时其他玩家颜色应只替换红塑料材质，保留头盔材质。

实际运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type knights
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type knights -OutputRoot .local/art-pipeline/rebuild/knights -SkipPreviews -CompareRoot art
```

Blender `4.5.14 LTS` 正式生成及独立 FBX 重导入检查通过：六件均为闭合三角网格、UV 完整、底心枢轴、材质全部使用、尺寸与源包围盒一致，源几何/材质与清单一致，六张原照校验值未变化。独立干净目录重建通过，几何、确定性逐面 UV、材质、参数、导出设置和参考校验值完全一致，报告已保存为 `art/exports/entity-conversion-v001/knights/clean-rebuild-verification.json`。无纹理依赖，因为雕塑使用程序化颜色材质。

人工已打开并检查 `art/previews/entity-conversion-v001/knights/` 的四张 1600×1000 预览：

- `top.png`：底座占地与戴盔差异清楚；严格正俯视时旗面几乎侧立，等级旗形不能充分区分。
- `side.png`：六件完整可见，三种旗形、双腿、盾与头盔能区分；采用上下架分布避免前后行遮挡，仅用于模型检查。
- `game-camera.png`：雕塑轮廓、支承底座、持旗和盾可辨识，头盔保持独立灰绿颜色。
- `crowded-vertex.png`：斜角类型总览，六件没有自身部件脱落或明显穿出底座占地。该图没有地块、道路与相邻村庄，不是正式拥挤交点或拾取验收。

尚未验证：Unity 正式相机/灯光/碰撞/拾取与相邻组件遮挡（主任务统一集成）；目标硬件性能与 LOD；实测尺寸；背面与底部；用户视觉确认。盾纹、面部、衣褶、底座石纹与隐藏侧面为简化近似，真实比例、塑料色受单照透视/光照影响；旗面比实体照片可见跨度更收敛，以维持估算圆底占地内的摆放边界，待用户验收后可按多视角资料继续修订。不能据此宣称精确复原或成品美术验收。
