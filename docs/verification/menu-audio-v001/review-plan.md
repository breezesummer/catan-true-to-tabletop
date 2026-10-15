# 菜单与音效 v001 验证计划

本文件记录可执行入口与审查边界；不是通过证明。实际结果以本轮 `result.json`、TRX、Unity 日志和逐张检查记录为准。

## 本任务实际审查结论

已独立只读审查 `TabletopAudioModel.cs`、`TabletopAudioSynth.cs`、`TabletopAudio.cs`、`M6Audio.cs` 与菜单文件。音效运行组件只读取当前人类席位的 `M6View`、其公开 `Events` 序号和已按观看权限投影的 `HistoryEntries`；未发现读取权威存档、AI 私有视图、牌库或游戏随机状态。独立组件使用各自的 `Initialize`／`Update`，不新增 `M6App` 同名生命周期方法，未发现 partial 类生命周期冲突。新局／读档用展示组件实例 ID 与席位区分作用域，打开或返回同一菜单保留同一水位。全部成功 `CommandKind` 都经过明确分类或通用选择／卡牌反馈。

审查曾指出以下三个具体问题，音频实施方随后修改源码；本任务再次读代码确认修改已存在：

1. 原固定 1.25 秒静默无法覆盖网络历史首次失败后的 2 秒重试，可能回放迟到的旧历史。现以当前 `PlayerView.Events` 最后公开序号作为首次绑定水位，同一作用域的水位只前进，迟到的较短历史不会降低游标。
2. 原总音量／音效音量只影响新播放片段，活跃片段仍有残响。现 `ApplyVolumes` 立即更新所有活跃声部的音量及环境声；静音会停止声部并清除待播队列。
3. 海盗群岛在掷骰时自动航行侵袭，没有 `MovePirate` 指令，原分类遗漏这条声音。现读取人类 `PlayerView` 中公开海盗位置变化，为该剧本补号角和水声。

另复核混音余量：每片段规范峰值不超过 0.70；效果声部按当前活跃数平均分配 0.78 总增益，环境保留 0.30，满设置、所有样本同号时的保守总峰值上界为 `0.70 × (0.78 + 0.30) = 0.756`。最高 10 个效果声部，队列最多 32 项，同类事件有时间限流；仅处理一次补帧批次最近 8 条，避免无界排队。

本任务实际运行过一次独立纯内存 C# 数值检查：由 PowerShell `Add-Type` 编译当时的 enum 与合成器，生成 35 个片段，测得最大单片段峰值 `0.699336946`、全部首末样本为零。最初 0.62 单声部增益下，一个十种异类片段同起点的混合示例峰值为 `0.772942680`，没有复现削波；这只说明该示例，不曾将理论风险写成实际爆音。合成器当时 SHA-256 为 `9CEC059FE8958AF30F4C7EC6AC11A6A87305CF9F8F0FFA0D217986A829DFBFF6`。

音频实施方最终报告独立音频项目 202 项通过、主任务报告实际菜单图形验证结果；这些不是本审查任务独立执行的测试，最终准确统计与最终构建结果应引用相应执行者的 TRX 和本轮验证输出。音频实施方还报告 35 个效果及 1 个环境 WAV 的独立 PCM 解码与干净重建检查，见 `audio-assets.json`；本任务没有将这些复述为自己的运行结果。源码审查与数值检查不能代替实际扬声器听感、真实触控、全部鼠标操作或长时间性能验收。主任务已定位隐藏窗口截图黑色的问题，完整菜单截图应使用正常可见图形窗口，最终验收不得据黑图判通过。

最后源码复核未发现阻断本轮构建的规则权限、生命周期、历史重放或数值范围问题。主任务随后针对同帧快速按下／释放补充了 `ObserveGuiPointerUp` 的 IMGUI 释放直达路径，并保留 `Update` 兜底与统一去重；该修复的实际鼠标声音调用计数由主任务通过显式被动观察参数核验。本审查任务没有改动被动观察实现或音量偏好。发布说明仍须保留真实听感、Android 真机和长期性能未验证的边界。

## 当前基线与并行范围

已阅读 `README.md`、`docs/PROJECT_BRIEF.md`、`docs/ROADMAP.md`：当前游戏为“卡坦岛之三国争霸／内测版1.4”，四规则已本地可玩，M7 仍有真实多机、外网与清洁环境待验；M8 的完整退出条件尚未满足。本次菜单与音效不能单独作为 M7/M8 退出依据。另一对话正在调整游戏内动画与手牌，必须保留既有未提交内容。本验证任务只新增独立验证器及本计划，没有改规则、AI 或原有界面文件。

工具链实际路径检查：`.NET 10.0.401`，`D:\Development\dotnet\dotnet.exe`；Unity `6000.3.25f1`，`D:\Development\Unity\6000.3.25f1\Editor\Unity.exe`。仅检查工具存在与 SDK 版本尚不能说明本次编译通过。

## 音效接入审查

`M6History.cs` 的 `HistoryEntries` 是稳妥的共用来源。本地由 `M6Bindings.cs` 绑定 `host.History(HumanSeat)`，联机由 `M6Network.cs` 绑定 `Network.History`，已经按观看者投影。`GameHistoryEntry` 的 `Sequence`、`Kind`、`Action`、公开 `EventDie` 及 `Changes` 足以识别掷骰、产出、银行与玩家交易、建造、升级、骑士操作、强盗／海盗移动、卡牌及待决选择。可先按 `Kind` 定义声音，再通过变化识别城市降级、骑士移除等复合结果，并为所有成功命令保留通用反馈。

必须在新局、首次订阅、读档、房间切换、断线历史重建时同步水位；相同 `Sequence` 的重复轮询不得重播。联机历史有独立请求延迟，不能依赖只有 `ViewChanged` 回调才检查历史；需在常规帧中观察投影增量。对手隐藏手牌变化会投影为数量或通用卡牌名；这种情况下只使用通用声音。不要根据权威存档、AI 命令中的秘密参数、牌库次序或随机状态挑选声音。

点击反馈不能仅覆盖 `ModernOperationUI.Button`：主菜单与联机大厅仍使用 IMGUI，卡牌、地图棋子、骰子、整理、历史筛选和相机快捷键也有独立输入链路。声音系统只能表现已确认结果或本地操作意图，不延迟规则、不额外提交命令。连续 AI 操作需限制同时播放数量与峰值，资源变化应聚合，不能每张牌无限叠音。

## 编译与自动检查入口

构建由主任务统一执行，避免两个 Unity 进程同时打开同一工程。`Build-M6.ps1` 的默认输出可能被另一个对话调整，必须显式传入本轮目录：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-M6.ps1 -OutputRoot .local/menu-audio-v001
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-M6Persistence.ps1 -BuildRoot .local/menu-audio-v001
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1 -BuildRoot .local/menu-audio-v001
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1 -BuildRoot .local/menu-audio-v001 -Graphics
```

推荐相关 .NET 集合为新增音效模型测试、`GameHistoryTests`、`TabletopDiceHistoryTests`、`TabletopPresentationTests`、`VictoryPointSettingsTests`、`AndroidTouchLayoutTests`。涉及共用网络投影时补跑 `Catan.Network.Tests`；最终可以运行完整规则项目，但不要把另一对话当前失败或统计混写为本任务结果。项目环境需先加载；保存探针测试依赖 `CATAN_DOTNET` 与 `CATAN_TEST_CONFIGURATION`：

```powershell
. ./tools/development/Enter-DevEnvironment.ps1
$env:CATAN_DOTNET=$toolchain.dotnet
$env:CATAN_TEST_CONFIGURATION='Release'
& $toolchain.dotnet test tests/Catan.Rules.Tests/Catan.Rules.Tests.csproj -c Release --disable-build-servers -m:1 --results-directory .local/menu-audio-v001/test-results --logger 'trx;LogFileName=menu-audio-rules.trx'
& $toolchain.dotnet test tests/Catan.Network.Tests/Catan.Network.Tests.csproj -c Release --disable-build-servers -m:1 --results-directory .local/menu-audio-v001/test-results --logger 'trx;LogFileName=menu-audio-network.trx'
& $toolchain.dotnet test tests/Catan.Audio.Tests/Catan.Audio.Tests.csproj -c Release --disable-build-servers -m:1 --results-directory .local/menu-audio-v001/test-results --logger 'trx;LogFileName=menu-audio-audio.trx'
```

## 完整界面验证器

新增 `MenuAudioVerification.cs`，由 `M6App.Start` 通过 `Initialize(this)` 注册，只有传入 `--menu-audio-verify <项目.local子目录>` 才运行。验证器需要实际图形窗口，不接受 `-batchmode` 或 `-nographics`；`WaitForEndOfFrame` 后调用 `ScreenCapture.CaptureScreenshot` 并等待文件写完。主任务启动示例：

```powershell
$reviewExe=Join-Path (Get-Location) '.local/menu-audio-v001/Builds/Windows/CatanM6.exe'
$reviewOut=Join-Path (Get-Location) '.local/menu-audio-v001/menu-audio-verification'
$reviewLog=Join-Path $reviewOut 'player.log'
New-Item -ItemType Directory -Force -Path $reviewOut | Out-Null
Start-Process -FilePath $reviewExe -ArgumentList @('-force-d3d11','-screen-fullscreen','0','-screen-width','1440','-screen-height','900','--menu-audio-verify',('"'+$reviewOut+'"'),'-logFile',('"'+$reviewLog+'"')) -PassThru
```

成功标记为 `CATAN_MENU_AUDIO_VERIFY_OK`，结果位于指定目录 `result.json`。截图包括启动菜单、同景 1.1 秒后的动态帧、1280×720、1920×1080、四规则返回菜单；动态帧按像素变化数检查，但最终构图和元素外观仍需逐张查看。手机仅检查 2400×1080 安全区逻辑视口坐标，不能称 Android 真机通过。

配置检查覆盖非法人数与席位拒绝、3/4 人最后席位、5–20 分边界、返回菜单时的配置回填、打开／关闭菜单及非法配置不改权威。所有游戏均暂停 AI；只通过 `WriteSave(指定目录)` 输出独立验收数据，不调用默认的 `SaveGame`／`LoadGame`。

音频运行检查通过约定 API 读取实际生成的 `AudioClip`：确认音效目录非空、片段有样本、采样率不少于 16 kHz、可读取非静音样本、样本不是非数或无穷且幅度在有效范围内。该检查不改变用户音量偏好，不能代表扬声器或人工听感。

## 覆盖矩阵与人工边界

| 类别 | 必须覆盖 | 证据与风险 |
| --- | --- | --- |
| 主菜单 | 四规则、剧本前后切换、3/4 人、P4 切到 3 人、最低/最高/默认分数、AI 速度、开新局、读档、返回当前局、联机入口 | 小窗口全部选项可滚动到达；不可遮字或漂出面板 |
| 背景 | 海洋、山川、河流、村庄、麦田、羊群；至少两帧可见动态 | 动态像素检查不能代替逐项人工辨认；水流/羊群应避免遮挡文字 |
| 界面交互 | 按钮、选项、卡牌选择、拿起/放回棋子、交易草稿、骰子、历史、相机、Esc | 单次动作一次反馈；禁止 IMGUI Layout/Repaint 多次触发声音 |
| 规则声音 | 产出、投骰、强盗/海盗、海盗要塞、蛮族航程/入侵、城市降级、骑士战斗、道路/船/村庄/城市/城墙、设施/大都会/奇迹、发展/进步卡、交易、待决选择、胜利 | 检查四规则与 AI/本地/网络来源；复合结果不可被普通选择声吞没 |
| 历史与隐私 | 首次绑定、读档、重新开局、网络重连、重复 revision、空历史、被投影的秘密变化 | 不重播旧事件；没有权威存档/秘密牌型访问 |
| 音量与性能 | 静音、效果/环境独立音量、设置恢复、同时播放上限、AI 连续动作、菜单切入切出 | 片段数据检查之外仍需实际听感；环境声不能在游戏中继续重复叠加 |
| 存档隔离 | 显式目录四规则存档、打开/返回菜单局面不变 | 验收私有存档仅留 `.local`；公开文档不能复制手牌或权威数据 |

旧 `Test-TabletopUi.ps1 -Graphics` 输出的是 `camera-only` 与实验性 IMGUI 合成，结果明确 `fullUiPixelsVerified=false`；它适合补充回归，不足以单独验收新菜单完整界面。本次最终说明必须分别报告自动检查、逐张外观检查、实际操作／听感和未验证项。
