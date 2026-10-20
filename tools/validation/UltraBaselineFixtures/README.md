# U0 可复用性能局面

这组夹具用于 `真实桌游` 主题的构建后性能基线。工具不改规则、玩家配置或产品资产。权威存档只保留在 `.local`，公开证据只复制 `public-manifest.json`、帧数据、统计和棋盘相机截图。

| 局面 | 规则／地图 | 人数／种子 | 选取条件 |
| --- | --- | --- | --- |
| ordinary | 基础版标准地图 | 4／97 | 合法初始摆放后的首个稳定阶段 |
| maximum | 航海家／卡坦奇观 | 4／97 | 合法初始摆放后；49 格、124 点、172 边，是支持地图中并列最大之一 |
| crowded | 组合／穿越沙漠 | 4／2027 | 正常完整 AI 对局中，村／城／路／船／骑士总数最多且无待决的非终局；数量相同取首次 |
| fog | 航海家／迷雾岛 | 4／97 | 合法初始摆放后，保留未揭示地块 |

生成器以 `GetAiPlayerView` 供现有 AI 决策，以 `Execute` 逐条执行，并逐次检查状态不变量。每个局面从零生成两遍，比较精确存档；随后经过 `Load` 再次比较精确存档。没有注入资源、直接摆棋或读取隐藏信息来控制 AI 决策。

拥挤样本在规则与 AI 源码变化后可能改变。因此复测应保留原始 `.local` 存档；需要重新生成时，以清单中的指令数、公开棋盘摘要和棋子数量辨别变动。每项 `fileSha256` 锁定对应合成夹具的完整文件字节，采样启动前逐一核对四个文件；缺少校验值、重复清单项、局面被替换或文件被修改时直接拒绝启动。公开校验值只针对这组人工合成基线夹具，不输出权威内容，也不处理真实玩家存档。采样脚本默认复用已经存在的夹具，`-RegenerateFixtures` 才主动重建。

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

`-Player` 应指向实际产物路径；上方目录仅展示命名方式。每一轮必须使用没有 `result.json` 的独立输出目录，防止读取旧结果。默认启动采用 `Hidden`；需要实际显示窗口时显式增加 `-VisibleWindow`，脚本才使用 `Normal` 并传入 `--ultra-baseline-visible-window`。两种模式都不使用 `-batchmode` 或 `-nographics`，并启用 `runInBackground`。`launch.json` 记录实际请求的窗口模式，运行时另记焦点与相机渲染帧数，不能仅凭可见参数推断持续前台。Android 真机与长时发热没有由此获得验证。

`launch.json` 同时保存游戏 EXE 和实际构建内的 `Assembly-CSharp.dll`、`Catan.Core.dll`、`Catan.AI.dll`、`Catan.Network.Contracts.dll` 的 SHA256。校验值从启动前的产物读取；仅比较 Unity EXE 外壳不足以确认脚本和规则实现一致。当前启动脚本要求 Windows Mono 构建的 `_Data/Managed` 目录；缺失这些程序集时拒绝采样。

镜头路线版本为 v1，以每个局面的实际公开棋盘拟合中心为基准，按采样时长分成四段：

1. 全桌斜视到全桌高角度。
2. 高角度到 0.55 倍缩放近景。
3. 近景向右移动近景正交半高的 0.8 倍。
4. 回到初始全桌斜视。

每段使用相同的 `SmoothStep` 插值。路径端点在预热前由真实 `TabletopScene` 计算，采样中只更新相机坐标、旋转及正交大小，避免重复拟合代码造成额外分配。

每局先保存四张独立的 URP 相机截图，检查非黑／非纯色，再预热、统计驻留资源并采样。截图只含棋盘相机，不包含 IMGUI，不能代替全界面验收。第 100 次采样时若实际相机渲染不足 50 帧，立即拒绝高速空转结果；结束时仍要求至少 90% 的采样帧完成渲染。隐藏窗口可能不持续渲染，即便独立截图有效也不能判定性能通过，不得通过扩大采样容量把空转当作帧率。采样前后完整权威存档必须逐字相同，规则与 AI 保持暂停。

`frames.csv` 使用单调时钟记录实际帧间隔；CPU／GPU 只采用 `FrameTimingManager` 返回的正值且时间戳不同的样本，无值记空单元格，汇总分位数为 `null`。原始帧间隔不冒充 CPU／GPU 时长。分位数采用最近秩法 `ceil(N×q)-1`；大于 50 毫秒的帧单独计数。

内存包括 Unity 分配、保留与 Mono 使用峰值、起止进程工作集、GC 次数和可用时的逐帧 GC 分配。绘制调用／批次／三角形依赖可用的 `ProfilerRecorder`，不可用时为 `null`。材质、纹理、网格及 Renderer 在采样前枚举；纹理 `RuntimeMemorySize` 不是实际显存占用。所有采样器存在观测开销。加载／截图分配、对局 CPU、人工输入、热稳定和 Android 性能须另行验证。

计数器有效句柄不等于已有数据：`ProfilerRecorder` 必须 `Valid` 且 `Count > 0` 才读取 `LastValue`，无样本在 CSV 中留空。可用性和覆盖率按真正读到的样本统计；GC 总分配只有完整覆盖时才给出数值，不完整覆盖另列已观测总量。失败日志保留阶段、采样操作、已完成帧数和异常方法栈，以便定位；不记录可能夹带存档数据的异常消息。

需要在隐藏窗口收集四局面的静态视觉和资源证据时，可显式增加 `-CaptureOnly`。该模式仍逐张验证 16 张 URP 棋盘截图，预热后登记材质／纹理／Renderer 数量及内存单次快照，并比较截图和预热前后的权威状态。它不分配帧采样缓冲、不启动计数器或 FrameTiming 采样、不输出 `frames.csv`，结果固定为 `status=captured-only`、`performanceMeasured=false`，帧时间和 CPU/GPU 时间为 `null`。资源和内存快照属于当前离屏棋盘加载状态；隐藏窗口可能不执行完整 IMGUI，不能等同正常前台完整 UI 常驻内存。单次快照也不是峰值、增长、泄漏或性能验收结论。

启动脚本仅在显式指定 `-CaptureOnly` 时接受 `captured-only` 和专用完成标记。正常性能模式仍要求 `passed`、`performanceMeasured=true` 和原有持续渲染检查；隐藏窗口的静态截图不会替代前台帧率验收。
