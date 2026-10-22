# 蛮族入侵船转化 v001

2026-10-09，已实际查看原始照片 `assets/references/inbox/单位/海盗（入侵卡上的单位）.jpg`。原照片及原文件名保持不变，SHA-256 为 `b0260a1c0dd5617747b74ca6642e59ce03d074264e78371780881651f9632efa`，亦记录在 `recipe.json`。

## 来源语义与制作内容

照片文件名使用“海盗”，但其所指是城市与骑士入侵轨道上的船标记。按照项目官方 2025 规则基线及既有工程语义，本类型显式映射为 `barbarian-ship`（蛮族入侵船）。航海家海洋板块上的海盗船为独立 `pirate` 类型；原文件不会为这项映射而改名。

本件是实际参数化三维网格。自包含 `builder.py` 不加载玩家船预制体、不导入其他类型的建模脚本或网格。保留原照可见的黑绿色塑料质感、带波纹和卷浪的宽底座、长窄凹甲板船体、三道凸起舷板与舷沿、侧舷浅浮雕、上翘圆端船首、卷曲尾端、厚弧面帆、弧形帆边、倾斜桅杆和卷曲桅顶。原照细部较暗，侧舷浅浮雕按简化圆凸饰制作，不能作为精确雕刻复原。

44 个封闭几何部件按两套材质合为 2 个渲染网格，共 10,974 个三角面；没有依赖位图纹理。每个多边形使用确定性投影 UV 岛，避免随机打包造成重建差异。船底接触并少量进入波浪支承面，首尾上翘离开水面。

本地坐标为 X 宽、Y 长、Z 高，源文件米制；估算总包围盒 `12 × 28 × 26 mm`，宽度包含波浪底座。全体对象原点为底面中心 `(0,0,0)`，X/Y 包围盒中心归零。FBX 导出轴为 `-Z forward / Y up`，预期 Unity 中长轴对应 Z。水线仅为底面以上 `1.8 mm` 的估算基准，尚无实测。

## 复现

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type barbarian-ship
# 已有正式产物后使用新的空输出目录，保留已有版本。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-EntityAssets.ps1 -Type barbarian-ship -OutputRoot .local/art-pipeline/rebuild/barbarian-ship-next -SkipPreviews -CompareRoot art
```

正式 `.blend` 及预览舞台位于 `art/source/entity-conversion-v001/barbarian-ship`；`.fbx`、manifest 和验证记录位于 `art/exports/entity-conversion-v001/barbarian-ship`。四种正式预览位于 `art/previews/entity-conversion-v001/barbarian-ship`。

## 实际验证

Blender `4.5.14 LTS` 已通过正式 wrapper 生成、导出和独立 FBX 回读。实际检查结果：2 个网格、10,974 个非退化三角面、每条网格边使用两次、UV 完整、两种材质槽均实际使用、底面 Z=0、对象原点归零、X/Y 占地居中，回读米制尺寸约 `[0.012, 0.028, 0.026]`。原照片 hash 未变化。

已实际查看四张正式 `1600×1000` 预览：`top`、`side`、`game-camera`、`crowded-vertex`。这些视角可核对波浪底座、船体占地、船首上翘、帆厚度及支承关系；共用 `side` 相机沿船长方向观看，因此又补充渲染并实际查看 `.local/art-pipeline/barbarian-ship-broadside.png`，核对横侧帆面、三道舷板和首尾轮廓。补充图是本地检查证据，不属于正式 manifest。正式 `crowded-vertex` 为本类型总览，不能代替 Unity 相邻节点摆放或拾取验收。

已在全新 `.local/art-pipeline/rebuild/barbarian-ship` 跳过重复预览进行重建，与正式输出比较：资产 ID、几何、三角面、UV、材质、零纹理依赖、参考来源、估算尺寸、随机种子及导出参数完全一致。正式 `clean-rebuild-verification.json` 保存该比较结果。初次视觉检查发现的小支承空隙已修正，初次输出保留在 `.local/art-pipeline/barbarian-ship-draft-001`；正式输出为修正后结果。

## 待验证与限制

仅有一张低照度斜侧照片。背面、底面、帆后面、准确雕刻、实物颜色与实测长宽高未知，未见侧舷按对称近似，水纹按可见曲线简化。本件是照片引导的参数化网格，不是摄影测量、扫描或用户已确认的最终还原。

本子任务未启动 Unity，未修改规则或开发路线。Unity 导入后的尺度、轴向、实际灯光、相邻组件摆放、拾取和运行表现由主任务集成检查；用户外观确认仍未取得。本次 Blender 通过不代替 Unity 验证或 M8 阶段验收。
