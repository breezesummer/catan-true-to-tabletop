# 三色玩家建筑实体照片转化 v002

2026-10-10 通过 `橙色-source-inspection.png`、`米色-source-inspection.png`、`蓝色-source-inspection.png` 三张六图表实际查看全部 18 张已分类建筑照片。原照完整保留在 `assets/references/inbox/建筑`，逐张路径及 SHA-256 登记于 `recipe.json`，生成及回读均核对原件未变。每色覆盖村庄、城市、大都会、独立城墙、城市+城墙及大都会+城墙六种外形。

本次输出 **18 件显式 FBX、18 件模型 BLEND、1 件预览场景 BLEND、四视图及生成/回读/重建/插装净空报告**。这些是有材质与 UV 的实际三维网格，不是生成位图。无需外部纹理；材质统一采用同批骑士使用的三色线性 RGB 候选：橙色 `[0.95,0.23,0.018]`、米色 `[0.82,0.70,0.43]`、深蓝色 `[0.008,0.024,0.12]`，粗糙度 `0.32`。颜色受原照曝光和白平衡影响，没有宣称经过实物测色。

## 模型与估算尺度

| ID 前缀（末尾为 orange / beige / blue） | X宽 × Y深 × Z高 mm | 网格数 | 三角面/件 |
| --- | --- | --- | --- |
| village | 12 × 12 × 16 | 1 | 6,996 |
| city | 18 × 18 × 20 | 1 | 15,448 |
| metropolis | 18 × 18 × 24 | 1 | 16,604 |
| city-wall | 24 × 24 × 5 | 1 | 14,624 |
| city-with-wall | 24 × 24 × 20 | 2 | 30,072 |
| metropolis-with-wall | 24 × 24 × 24 | 2 | 31,228 |

源文件以米为单位，X宽/Y深/Z高，底面中心及各网格原点为 `(0,0,0)`；FBX 显式导出 `-Z forward / Y up`、缩放1、单位转换及三角化。18 件总计 344,916 个三角面；数量包含库内三色和独立/组合重复资产，不能视为整局同时使用的预算。

照片没有尺子，以上尺度沿用已有开发估算并保持同类三色严格一致，**不是实测尺寸或用户确认的最终比例**。村庄与城市当前高度/宽度比仍受旧估算影响，比照片透视下的矮圆轮廓更修长，后续带尺多角度参考可修正。底面、背部、遮挡或暗部细节未知，按可见结构简化补齐。

城市、大都会和城墙三种几何从 v001 **复制到新版本**；旧配方及资产没有修改，源脚本逐项 SHA 和副本字节一致性登记在 `builder-source-provenance.json`。城市含圆双阶底座、三个成组坡屋顶大厅、门廊柱、台阶及墙面浮雕。大都会含双尖塔、双方形垛口塔及前排微型坡屋顶建筑。村庄根据已保留 `photo-samples-v001` 的成组屋顶/烟囱/台阶配方适配，使用新照片可见的相邻圆润底座近似。城墙包含实际中央通孔、八座圆塔、32 个主墙垛和上下石饰；塔/墙垛数量、内壁及后部仍是估算。

两种组合分别包含本体与独立环形城墙，均从零枢轴居中装配成实际双网格，而不是只把组合照片登记为参考。组件内部由相交的封闭壳构成，未执行实体布尔并集；本成果用于游戏渲染，不表示可直接实体打印，也不是摄影测量或扫描复原。

## 实际运行的检查

Blender `4.5.14 LTS` 已实际完成正式生成、18 件 FBX 独立回导、干净目录重建及其回导比较。`verification.json` 记录全部18件通过：所有导出三角面无退化、所有壳的边各使用两次、UV覆盖完整、所有材质槽实际使用、原点/底面归零、X/Y包围盒居中、尺寸与配方一致、参考校验值未改变。`clean-rebuild-verification.json` 记录正式与干净重建的几何/UV、材质、尺寸、来源、种子和导出参数严格一致。FBX/BLEND 二进制本身可能含写入时间，复现比较使用实际几何和参数。

独立 `audit_insert_clearance.py` 已检查三色共 **6 种组合**：每件组合中两个零原点网格的几何/拓扑/UV与独立本体及墙完全相同；本体和墙材质色值一致；城墙 99 个封闭壳，最窄孔径约 `18.594397 mm`，墙顶以下城市/大都会最大直径约 `18.000001 mm`，最小单侧径向余量均约 `0.297198 mm`。详情见 `insert-clearance.json`。这是居中估算源网格的保守包络净空检查，不代表实物插装公差或 Unity 动画变换验证。

已实际查看正式 `top.png`、`side.png`、`game-camera.png`、`crowded-vertex.png` 四张 `1600 × 1000` 预览：18件完整入镜，同类三色形体一致；村庄底座和烟囱、城市门廊、尖顶/垛口大都会、城墙开孔及两类嵌套结构均可辨认。深蓝色在当前较暗预览灯光下细部对比低，需要在正式 Unity 灯光下复查。`crowded-vertex` 是类型库斜视排布，不能作为真实游戏节点、相邻地块遮挡或拾取验收。Unity、运行时交互与用户外观确认由主任务顺序集成，本建筑子任务没有启动 Unity。

首次不同材质候选的成功生成保留在 `.local/art-pipeline/attempts/v002-building-initial`。正式产物使用统一最终色表，输出目录已有 manifest 时工具拒绝覆盖；重建时需新目录。干净复现产物保留在 `.local/art-pipeline/rebuild/v002-player-buildings`。

## 复现命令

以下命令需在仓库根目录运行，正式目录已有版本时应传新 `--output-root`：

```powershell
& 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe' --background --python-exit-code 1 --python tools/art/entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-buildings/recipe.json --output-root .local/art-pipeline/rebuild/v002-player-buildings-new --skip-previews
& 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe' --background --python-exit-code 1 --python tools/art/verify_entity_pipeline_v002.py -- --recipe art/recipes/entity-conversion-v002/player-buildings/recipe.json --output-root .local/art-pipeline/rebuild/v002-player-buildings-new --compare-root art
& 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe' --background --python-exit-code 1 --python art/recipes/entity-conversion-v002/player-buildings/audit_insert_clearance.py
```

生成和验证日志在 `.local/art-pipeline/logs/v002-buildings`；本子任务未更改规则、Unity文件或路线图阶段状态。
