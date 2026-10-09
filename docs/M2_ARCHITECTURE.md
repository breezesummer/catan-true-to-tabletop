# M2 基础版架构

M2 使用 `Catan.Core.M2` 命名空间。M1 的 `Catan.Core.GameSession`、固定场景、测试及 Windows 程序保留；两种存档由各自的版本化入口识别，不能互相当作同一规则存档载入。

## 权威规则边界

`BaseGameSession` 是纯 C# / .NET Standard 2.1 规则会话。Unity、鼠标选择、模型、动画与物理组件均不参与裁决。地图沿用已固定的 19 个 Tile、54 个 Vertex、72 个 Edge 标识；M2 场景在这些拓扑坐标上生成基础版地形、数字和港口。

```csharp
var game = Catan.Core.M2.BaseGameSession.Create(topologyJson, 4, seed);
var preview = game.Preview(command);
var result = game.Execute(command);
var view = game.GetPlayerView("P1");
var authoritySave = game.Save();
var restored = Catan.Core.M2.BaseGameSession.Load(topologyJson, authoritySave);
```

`seed` 是权威新局输入，正常 UI 使用新种子；种子、随机生成器状态和发展卡牌库均不属于 `PlayerView`。`Command` 不含指定骰点或指定偷取资源类型的参数。合法人类行动与验收驱动使用相同 `Execute` 入口。

所有资源消耗、牌库变化、道路长度、奖牌归属及胜利在成功提交时完成。`Preview` 只检查可执行性。非法命令不改变存档、随机位置或事件账本。成功请求的 ID 与完整指令指纹用于去重；同一请求重试只有原回执，`NewEvents` 为空，同 ID 改内容则拒绝。

## 阶段与多步选择

常规流程为蛇形开局 → 等待掷骰 → 行动 → 下一席位。掷 7 时，超过 7 张资源的席位分别提交弃牌，然后由当前回合席位移动强盗并选择相邻玩家偷取。骑士进入同一移动/偷取规则，但不触发弃牌。道路建设牌保存剩余免费道路数；牌的效果结束后恢复打牌前的阶段，因此掷骰前打牌仍须掷骰。

玩家交易保存提出者、交易对象、双方公开的交换资源。接受时重新检查库存并原子交换；拒绝与撤销不扣资源。非当前玩家可向当前玩家提议，另外两个非当前玩家之间不能交易。

## 权限与本地换座

`PlayerView` 仅包含自己的资源明细、未使用发展卡及自己的总分；其他玩家只公开资源总数、发展卡总数、已用骑士数、公开分数和剩余棋子。公共棋盘包括城市、强盗、港口、最长道路和最大军队。胜利前他人的胜利点卡保持隐藏。

事件是公开事实的投影，不输出偷得的资源类型、弃牌构成、购得的发展卡、牌库顺序或随机状态。权威成功指令账本可包含这些私密行动的输入，因此只写入本机存档，不能用作未来的玩家同步包。

`M2LocalGameHost` 私有持有会话；`M2App` 通过它取得当前席位视图和提交命令。换座时立即取消拿起操作、丢弃旧私有视图并显示确认层。待决弃牌及交易回应会引导到需要操作的席位。存档载入后重新确认席位。

## 持久化与兼容验证

权威存档格式为 `2`，本机文件名 `m2-authority-v2.json`；内容包含规则/场景/格式版本、原拓扑内容指纹、初始种子、随机继续状态、完整手牌与牌库、有限供应、奖牌、胜者、回合、待决、交易提案及成功命令账本。载入重新生成初始场景并重放成功命令，比较权威状态；错误版本、篡改内容或无法重放的记录会被拒绝。UI 只有完整载入成功后才替换当前会话。

测试受控局面与生产载入分开。独立边界测试可构造权威夹具，但正式客户端的新局和载入不能注入任意资源或骰点。完整对局验收驱动只读取玩家有权取得的视图。

复验命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\validation\Test-M2Rules.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Build-M2.ps1 -VerifyPlayer
```

第一条包含 M1 回归和 M2 规则案例；第二条让固定版本 Unity 实际调用核心、编译客户端、构建 Windows x64 Mono 程序，再在独立 Windows 进程中执行保存与恢复验收。实际结果、证据和待验范围见 [M2 验收记录](M2_ACCEPTANCE.md)。

## 表现资源范围

M2 复用 M1 的版本化山地 FBX 与估算尺度。新增城市与强盗使用运行时基础几何作为可辨识占位；源配方在 `BoardRenderer.cs`，单位为米，摆放使用稳定逻辑坐标。这些是实际几何，未新增生成位图，也不声称为经用户确认的收藏版成品模型。完整美术生产仍须按 `ART_PIPELINE.md` 单独验收。
