# U0 可复用性能局面

这组夹具用于 `真实桌游` 主题的构建后性能基线。工具不改规则、玩家配置或产品资产。权威存档只保留在 `.local`，公开证据只复制 `public-manifest.json`、帧数据、统计和棋盘相机截图。

| 局面 | 规则／地图 | 人数／种子 | 选取条件 |
| --- | --- | --- | --- |
| ordinary | 基础版标准地图 | 4／97 | 合法初始摆放后的首个稳定阶段 |
| maximum | 航海家／卡坦奇迹 | 4／97 | 合法初始摆放后；49 格、124 点、172 边，是支持地图中并列最大之一 |
| crowded | 组合／穿越沙漠 | 4／2027 | 正常完整 AI 对局中，村／城／路／船／骑士总数最多且无待决的非终局；数量相同取首次 |
| fog | 航海家／迷雾岛 | 4／97 | 合法初始摆放后，保留未揭示地块 |

生成器以 `GetAiPlayerView` 供现有 AI 决策，以 `Execute` 逐条执行，并逐次检查状态不变量。每个局面从零生成两遍，比较精确存档；随后经过 `Load` 再次比较精确存档。没有注入资源、直接摆棋或读取隐藏信息来控制 AI 决策。

拥挤样本在规则与 AI 源码变化后可能改变。因此复测应保留原始 `.local` 存档；需要重新生成时，以清单中的指令数、公开棋盘摘要和棋子数量辨别变动。采样脚本默认复用已经存在的夹具，`-RegenerateFixtures` 才主动重建。

在仓库根目录生成夹具：

```powershell
. ./tools/development/Enter-DevEnvironment.ps1
& $toolchain.dotnet run --project tools/validation/UltraBaselineFixtures -c Release -- $PWD.Path "$PWD/.local/ultra/u0-fixtures"
```

在包含 `UltraBaselineProbe` 的 Windows 构建上采样：

```powershell
./tools/validation/Measure-UltraBaseline.ps1 `
  -Player 'game version/1.7/Catan1.7.0-ultra-baseline/Builds/Windows/Catan1.7.0.exe' `
  -OutputDirectory 'docs/verification/ultra/performance/round-1' `
  -SampleSeconds 30 -WarmupSeconds 8 -Width 1920 -Height 1080
```

`-Player` 应指向实际产物路径；上方目录仅展示命名方式。每一轮必须使用没有 `result.json` 的独立输出目录，防止读取旧结果。启动采用 `Hidden`，不使用 `-batchmode` 或 `-nographics`，并启用 `runInBackground`。结果仅适用于记录的后台采样条件；Android 真机与长时发热没有由此获得验证。

镜头路线版本为 v1，以每个局面的实际公开棋盘拟合中心为基准，按采样时长分成四段：

1. 全桌斜视到全桌高角度。
2. 高角度到 0.55 倍缩放近景。
3. 近景向右移动近景正交半高的 0.8 倍。
4. 回到初始全桌斜视。

每段使用相同的 `SmoothStep` 插值。路径端点在预热前由真实 `TabletopScene` 计算，采样中只更新相机坐标、旋转及正交大小，避免重复拟合代码造成额外分配。

每局先保存四张独立的 URP 相机截图，检查非黑／非纯色，再预热、采样。截图只含棋盘相机，不包含 IMGUI，不能代替全界面验收。帧采样期间要求相机至少在 90% 的采样帧上完成渲染，且采样前后完整权威存档逐字相同。规则与 AI 保持暂停。

`frames.csv` 使用单调时钟记录实际帧间隔；CPU／GPU 只采用 `FrameTimingManager` 返回的正值且时间戳不同的样本，无值记空单元格，汇总分位数为 `null`。原始帧间隔不冒充 CPU／GPU 时长。分位数采用最近秩法 `ceil(N×q)-1`；大于 50 毫秒的帧单独计数。

内存包括 Unity 分配、保留与 Mono 使用峰值、起止进程工作集、GC 次数和可用时的逐帧 GC 分配。绘制调用／批次／三角形依赖可用的 `ProfilerRecorder`，不可用时为 `null`。材质、纹理、网格及 Renderer 在采样前枚举；纹理 `RuntimeMemorySize` 不是实际显存占用。所有采样器存在观测开销。加载／截图分配、对局 CPU、人工输入、热稳定和 Android 性能须另行验证。
