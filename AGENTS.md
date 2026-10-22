# AGENTS.md

### 项目介绍

游戏名：卡坦岛之三国争霸

版本号：内测版1.7.6

发布名：游戏名+版本号

### 云服务器

**域名:** breezesummer.cloud

**IPv4:** 175.178.212.142

**账户:** ubuntu

**SSH**：./codex.pem

### 开发约定

- 2026-10-22：Ultra 开发已搁置，本分支仅归档既有成果；恢复需用户明确指示。后续常规开发使用 dev-2.0。

- 你输出的所有文档和说明都应使用中文。
- 当我明确发布时，请先更新此处版本号二级版本号，重置三级版本号为1，然后同时发布pc版和Android版，放入./game version/release中。
- README.md 只维护项目介绍、使用入口与文档导航；每轮功能更新、修复、验证结果和发布公统一按日期与标题记录到 The Farmer's Log.md。
- 实施前阅读`docs/PROJECT_BRIEF.md` 和 `docs/ROADMAP.md` 的当前阶段内容。
- Ultra 三维资产制作前阅读并遵循 [Ultra 三维资产制作流程与提示词](docs/ULTRA_3D_ASSET_WORKFLOW.md)，按原型分析、独立生图提示词、辅助视图检查、真实建模与 Unity 验收分步执行。建模必须使用blender mcp。
- 规则开发先查 [本地规则索引](assets/references/rules/README.md)；引用注明来源文件与 PDF 页码。
- 规则核心独立于 Unity、渲染、物理、UI 和网络；使用稳定的 Tile/Vertex/Edge ID，动画不影响规则结果。
- 本地玩家、AI、远程玩家共用指令验证入口；AI 仅读取专用 `PlayerView`。
- 权威存档包含规则版本、场景版本、待处理决策和随机状态。
- 小步迭代，保持可玩。修改规则须补充独立案例、非法操作和状态不变量测试。
- 代码修改类任务完成后，只有满足阶段退出条件后才更新 docs/ROADMAP.md，然后提交并推送。
