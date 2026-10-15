# 原 AI 策略版本清理 v001

2026-10-15 按用户要求清理原策略版本，仅维护当前 `src/Catan.AI` 中的策略。当前两个入口 `tools/development/Play-M6.ps1`、`tools/development/Play-AiStrategy.ps1` 已统一，无需再提供策略版本选择。

可玩程序：[CatanM6.exe](../.local/m6-ui-v010/Builds/Windows/CatanM6.exe)。使用固定工具链重建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-M6.ps1 -VerifyPlayer
```

## 清理范围

- 删除 `.local/m6`、`m6-ui-v003`、`m6-ui-v004`、`m6-ui-v005`、`m6-ui-v006`、`m6-ui-v006-card-ratio-v001`、`m6-ui-v006-card-ratio-v002`、`m6-ui-v007-original-ai` 下的 `Builds`，共八份旧策略 M6 程序。
- 删除 `.local/distribution` 中旧策略内测 1.0 压缩包、v004 构建和两份解压副本，以及只用于该旧包的打包、验证脚本。
- 删除 `.local/m6-ui-v007/build-original-ai.ps1`，避免再次用旧规则／AI DLL 替换当前 Unity 插件；清理手牌比例 v001 的临时插件备份、`ui-v004-baseline` 的两份程序集及两个旧验证工程的 `bin/obj` 缓存。
- 删除 `AiHeuristics.LandEdge`：产品源码中已无调用；规则测试中的同名辅助方法属于独立规则夹具，继续保留。
- README 和策略说明改为当前统一入口；三份双版本交付文档加上历史说明，旧验收数字和原始证据不改写。

本地共清理 **22 处、2,187 个文件、9,651,577,866 字节（约 8.99 GiB）**。逐路径记录见 [清理清单](verification/ai-legacy-cleanup-v001/deleted-files.json)。执行前核对绝对路径均位于本仓库 `.local`，排除链接及疑似存档文件，再用 PowerShell `Remove-Item -LiteralPath` 删除。

当前策略的其他历史构建、独立美术图库、M1–M5 规则阶段样板、照片和素材版本不属于本次旧策略 M6 交付清理范围。保存／恢复记录、权威存档、历史验收文件和 Git 历史保留；未修改规则、策略决策、权限视图或存档格式。路线图阶段保持原状。

## 验证

120／120 项 M6 专项测试通过，失败与跳过均为 0；包含当前策略案例、交易、待决、权限与隐藏信息边界，未包含 `M6FullGameTests`。`.NET Standard 2.1` 构建零警告、零错误；Unity `6000.3.25f1` 构建与规则／存档回读通过，耗时 60.8 秒。最终 Windows 程序在两个独立进程中通过四类规则保存／恢复及继续 AI 行动，分别耗时 19.6／10.0 秒。

核对全部列明旧路径已不存在、两个启动入口同指 v010、可玩程序携带的 AI DLL 与本轮源码构建输出哈希一致。构建后逐字节恢复原有 Unity 场景、设置和插件，校验开工时的既有已跟踪改动未变；本次提交不包含这些既有改动。产品源码及正式脚本中未发现原策略切换或旧插件替换入口。

本轮未重跑全量规则测试、52 个完整 AI 局、人工鼠标操作与外观检查、全分辨率或长期性能检查。详细结果见 [验证摘要](verification/ai-legacy-cleanup-v001/summary.json)，不沿用旧版本结果作为本轮验收。
