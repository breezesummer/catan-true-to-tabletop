# 实体港口、点数背面与卡坦筹码 v005

2026-10-14，处理 `assets/references/inbox/未分类` 的八张新照片：通用 3:1 港口、木头／砖／羊毛／小麦／矿石五种 2:1 港口、船图案点数牌背面及卡坦分标记。沿用用户已确认的原照片几何校正方式，保留原件字节；归档副本、配方、校正贴图与厚纸板网格另存 `entity-conversion-v005/tabletop-markers`。

港口以公开的两个沿岸顶点和相邻陆块计算面向海洋的位置，四种当前规则模式共用显示适配。实际网格、照片材质和稳定港口 ID 参与显示，点击港口仍进入银行／港口交易。航海家公开地图上的赠送港口与奖励边采用同一批资产，获得或安置后随公开视图移除、增加；未翻开的港口顺序不进入显示适配。

卡坦奖励筹码按玩家公开的 `BonusVictoryPoints` 显示，奖励边只读公开 `BonusEdgeIds`。规则依据为本地 `catan-seafarers-2025-en.pdf` **PDF 第 12 页**：航路奖励筹码取得后放在玩家前方、每枚 1 分；赠送港口的安置仍由原规则入口决定。照片不改变计分、合法落点或胜利条件。

点数牌保留原有结构化 2–12 点正面和红色 6／8，新增真正闭合底面并绑定船图案原照。背面不会作为迷雾数字或隐藏地图提示，正常摆放仍显示数字正面；专项细节图翻转纸牌检查背面。此次没有新增翻点数牌操作。

所有资产使用米制、底心枢轴和独立于互动碰撞的真实 Mesh。照片可见的纸板厚度与形状用于估算，实测尺寸、未知背底面、照片色彩及用户最终外观确认仍待补齐；本批次不是立体地形雕塑，也未满足 M8 全部退出条件。

复现与试玩：

```powershell
# 从分类原照重建到一个尚不存在的输出目录；已归档版本拒绝覆盖。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/art/Build-TabletopMarkers.ps1 -OutputRoot .local/art-tabletop-markers/rebuild-v001

# 导入新资产并导出当前优化 AI 的可玩版本。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-TabletopMarkers.ps1 -VerifyPlayer

# 四模式和新资产的专用图形验证。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopMarkers.ps1

# 当前正式入口。
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Play-M6.ps1
```

当前可玩程序为 [CatanM6.exe](../.local/m6-entity-v005/Builds/Windows/CatanM6.exe)。实际验证结果与未验边界见 [公开摘要](verification/tabletop-markers-v005/summary.json)，历史资产、原照和开工时已有工作区修改保留。只维护当前优化 AI，M7／M8 阶段状态不变。

干净 Git 克隆中已有冻结 `manifest.json`，默认生成目录因此会拒绝覆盖；请使用新的 `-OutputRoot`，生成器会把 `.blend`、FBX、依赖与回读报告输出到该目录。仅在原正式 `.blend`／FBX 仍存在时使用配方说明中的 `-CompareRoot art` 做完整几何比较；克隆后直接构建游戏使用已纳入 Git 的 Unity FBX／预制体，不需要先覆盖冻结导出目录。

本轮实际通过 **151／151 项相关 .NET 检查**；Blender 4.5.14 LTS 正式生成和空目录重建均通过八件回读，几何、UV、材质、贴图和来源一致；Unity 6000.3.25f1 导入 **8／8** 真实网格，共 4,204 个三角形；Windows 构建与两个独立图形进程的四模式保存恢复、继续 AI 行动通过。

新资产运行验证完成 **1,903 项检查、72 个来源实例**：四模式正常初设、六类港口、同 ID 迁移／换图／删除、公开筹码计数、无碰撞体、真实三角形占地与建筑间距、规则数字正面、照片底面和迷雾隐藏。另用正常公开视图检查被遗忘的部落的 6 个港礼、8 个航路奖励，以及迷雾岛屿。输出的 8 张相机图及 8 张资产四视图已查看；相机图不含完整 IMGUI，不能当成真实窗口鼠标操作或用户确认。

既有桌面图形集成回归通过 **28 配置／20,060 项检查**，包含不同模式、人数和人类席位。完整 IMGUI 自动像素验收仍为 `false`；本轮没有进行桌面鼠标操控、用户完整对局、全分辨率或长期性能检查。未重复运行全量规则套件，也不将此前 52 个完整 AI 局算成本轮重跑。

全量来源新增八个不重复哈希，当前 **143 张照片、123 个唯一资产 ID、132 个保留版本 FBX**。分类原件与未分类原位文件逐字节一致；配方引用分类原件，干净 Git 克隆可复现。可重复生成的 Blender 源文件、FBX、依赖和预览遵循既有忽略策略本地保存，分类原件、源 PNG、配方和验证证据纳入 Git。
