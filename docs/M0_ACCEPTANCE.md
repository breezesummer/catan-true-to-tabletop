# M0 阶段验收

日期：2026 年 10 月 8 日（Asia/Shanghai）。状态：**M0 已完成**，路线图已更新；M1 待开始。

## 范围与决定

本阶段交付项目约定、可复跑工具链、规则来源锁、素材尺度约定和 M1 的固定验收局面。尚未创建正式游戏工程；环境验证程序只显示合成测试方块。

用户本次明确确认：**先用官方 2025 规则和明确标注的估算尺寸完成 M0，实体参考后补**。据此调整 M0-04 的交付方式，原任务要求的实体版本、照片和实测尺寸转入参考后补清单，不将其标为已收到或已测量。其他工作默认值不因本次收口自动成为用户确认的事实。

## 退出条件与证据

| 路线图退出条件 | 结果 | 证据 |
| --- | --- | --- |
| .NET 可编译并测试 | 通过；`.NET Standard 2.1` 探针和 2 项 xUnit 测试 | [工具链复验记录](verification/M0_TOOLCHAIN_2026-10-08.md) |
| Unity 可导出 Windows 程序 | 通过；实际编译、Windows x64 Mono 构建与程序执行 | 同上；权威结果来自 Unity 与 Player 日志，不仅是 .NET 构建 |
| Blender 可批量生成并导入测试模型 | 通过；`.blend`、FBX、米制尺寸及底面枢轴均检查 | 同上；URP 截图已打开核对，绿色薄方块可见 |
| 版本记录完整 | 工具版本、包锁、规则来源与估算参数均已记录 | `global.json`、`tools/development/toolchain.json`、Unity 包锁、[规则来源](RULES_BASELINE.md)、[尺度基线](REFERENCE_BASELINE.md) |
| M0-04 参考基线（按本次确认调整） | 官方 2025 开发基线与估算参数已登记；实物后补 | [来源清单](../assets/references/rules/v001/sources.json)、[后补清单](REFERENCE_BASELINE.md) |
| M0-05 固定 v0.1 验收局面 | 通过数据自洽性检查与独立复核；19/54/72 拓扑、8 组摆放、6 个状态账本及开局待决检查点一致 | [人工验收流程](acceptance/V0_1_ACCEPTANCE.md)、[场景数据](acceptance/v0.1/scenario.json)、[地图定位图](acceptance/v0.1/board.svg) |

工具链曾完整运行两次，第二次包含 Renderer 复跑缺陷修复。夹具作者与独立复核代理均实际运行标准库检查脚本，输出 PASS 并明确列出尚未运行的游戏功能；独立复核还按坐标重建了拓扑，手工核对开局、产出、交易、付费建路和恢复探针的资源收支。临时篡改银行数字及相邻定居点的夹具均被检查脚本拒绝。此类负例验证的是夹具检查器，不能计入 M1 的非法游戏指令测试。

地图定位图已从 JSON 生成并渲染检查，修复顶部编号与图例重叠后再次查看通过。集成时检查了 11 份 Markdown 文件的本地链接，均可解析到现有文件。

## 重跑入口

在仓库根目录使用 Windows PowerShell：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Verify-Environment.ps1 -Unity
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\development\Fetch-RuleReferences.ps1 -VerifyOnly
& 'D:\Development\Blender\blender-4.5.14-windows-x64\4.5\python\bin\python.exe' .\tools\validation\verify_m1_fixture.py
```

规则缓存缺失时先运行 `Fetch-RuleReferences.ps1`（不带 `-VerifyOnly`）。夹具核对仅证明验收数据自洽，不能替代后续纯规则库测试或 Unity 实际操作。

## 移交 M1

按路线图建立纯规则库及稳定格/边/点拓扑，再串起命令验证、玩家视图、事件、待决选择和权威存档；接入 Unity 棋盘并制作山地样板。以固定验收流程逐项执行，不把本阶段的预期状态表当作已经运行过的游戏结果。

实体版本与尺寸、完整基础版规则、真实拿放交互、游戏存档恢复、隐藏信息保护、正式美术样板和用户试玩均尚未验收。IL2CPP、其他机器安装和目标帧率也未验证；这些不在 M0 的退出条件中。
