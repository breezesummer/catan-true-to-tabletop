# 桌面与实体摆放 v004

2026-10-12 按用户六项反馈调整当前 M6 的四类规则模式。本轮仅修改显示、选择和操作入口布局，规则版本、场景版本、权威存档和 PlayerView 权限不变。原照片、既有模型、旧贴图和旧程序保留；不改变路线图阶段。

## 已交付行为

| 用户要求 | 当前实现 |
| --- | --- |
| 手牌整齐摆成一行 | 完整卡面横排、固定间隔、不旋转、不相互遮挡。选择所需资源时只向前抽出对应卡片。单类资源最多一页 12 张；与特殊牌并存时资源 8 张、特殊牌 4 张，超出容量翻页或在手牌区滚轮换页。容量属于本轮工作设计，未记录为用户确认。 |
| 三骰三角摆放 | 事件骰位于顶角，红骰在左下角，白骰在右下角。直接点击骰子掷骰，权威结果显示在顶面；动画和其他骰面仅供显示。 |
| 结束回合在右下角 | 文案改为“结束回合”，按钮固定在屏幕右下角；“放回手中”移开，避免重叠。 |
| 三张完整城市设施卡 | 贸易、政治、科学各一张完整建设卡，使用现有照片资产。卡内五个小翻板保持各自位置，点击下一等级提交升级，成功后翻为已建面。跳级或点击已建等级只提示。 |
| 棋子和资源随地图缩放 | 本人备件、对手备件和牌背、手牌、银行卡片、设施卡与骰子采用桌面世界坐标，和地图、蛮族板共用相机。滚轮和视角切换改变相机，不重排物件。命中区域由实时投影与相机射线决定。 |
| 极高清桌面 | 新生成原生 8192×8192 橡木色彩贴图；Windows 保留 8K 上限、BC7 高质量、MipMap、三线性和 16 倍各向异性，独立渲染管线为 1:1 渲染比例及 4 倍 MSAA。实际渲染尺寸跟随窗口，最大化后不继续放大 1440×900 的旧画面。 |

备件使用既有实际三维网格，纸牌为带薄纸边的三维面，底面贴在桌面。桌面和纸边尺寸为工作估算；8K 位图本身不是三维模型。现有卡面照片的细节仍受原照分辨率限制。来源、轴心点和复现步骤见 [v004 美术配方](../art/recipes/ui-tabletop-v004/README.md)。

## 实际验证

- 独立 .NET 全量测试：**575/575 通过**，失败／跳过均为 0，包含既有 52 个正常 AI 完整局，以及横排手牌与选择不改变权威状态的测试。原扇排的四个重复参数用例调整为三个横排容量用例，故总数比 v003 少一项；没有删除规则测试。
- 固定 Unity **6000.3.25f1**：实际执行 `.NET Standard 2.1` 核心与 AI、权威存档回读、Windows x64 Mono 构建，最终构建通过。
- Unity 无图形集成：四类模式 × 3／4 人 × 每个人类席位，共 **28 配置、14,831 次断言通过**。
- Unity 图形集成：同样 **28 配置、14,839 次断言通过**，输出 8 张相机图。检查包含整桌缩放比例与地图一致、物件世界位置不变、缩放／俯视后的命中、设施五个小翻板位于完整板内，以及运行时纹理实际为 8192×8192。
- 两个独立 Windows 进程：四类规则、混合席位、暂停、待决交易、权威状态和随机继续状态的保存／恢复标记通过。
- 原生 8K 贴图重新生成：固定种子两次结果 SHA-256 一致；Unity 导入副本与源图逐字节相同。BC7 全 Mip 链的纹理空间约 85.3 MiB，未完成低显存设备和长期性能验收。
- 人工窗口检查：城市与骑士四人新局，点击单枚定居点、道路和城市并落在地图；本人三张及后续八张手牌完整横排；点击事件骰掷骰；滚轮缩放时棋子、手牌、银行、设施和骰子与地图同步。点击贸易板内第一级小翻板，等级 0→1、商品扣减、翻板变为已建面；点击第五级提示先建第二级，无越级升级。切换俯视后物件仍摆在桌面，卡面和拾取正常。最终构建另行检查完整窗口中的右下按钮、分数标签和骰子不同侧面。

可公开的验证摘要位于 [v004 证据目录](verification/tabletop-ui-v004/summary.json)。权威测试局仅留在 `.local`，截图只来自新建开发测试局，不输出其他玩家的完整手牌、牌库顺序、隐藏地图或随机状态。

收尾窗口画面：[桌边视角](verification/tabletop-ui-v004/manual-city-window.png)、[俯视角](verification/tabletop-ui-v004/manual-city-top-window.png)。截图按 Windows 逻辑像素保存为 2194×1234，包含窗口标题栏，未把它们描述为 8K 或原生 4K 截图；8K 指桌面材质本身。两张图来自新建四人开发局的第二次开局阶段，展示完整手牌、完整设施板和三角骰子。事件骰侧面分配的最后修正不改变图中初始船面与摆放。

图形批处理的完整 IMGUI 合成仍未产生有效 Repaint，`fullUiPixelsVerified` 保持 `false`；相机图不作为完整界面验收。人工检查覆盖本次城市与骑士操作链，尚未逐项人工检查所有模式、分辨率、真实按住拖动、用户整局体验、最终外观和长期性能。这些边界不由自动测试替代。

## 启动与复现

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Play-M6.ps1
```

启动脚本默认使用 `.local/m6-ui-v004/Builds/Windows/CatanM6.exe`，旧 v003 保留。构建与检查：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-M6.ps1 -VerifyPlayer
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-M6Rules.ps1 -ResultsDirectory .local/m6-ui-v004/test-results
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1 -Graphics
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-M6Persistence.ps1
```

不宣布 M7／M8 或用户视觉验收完成。原工作区其他美术修改继续保留，本轮按明确文件清单提交推送。
