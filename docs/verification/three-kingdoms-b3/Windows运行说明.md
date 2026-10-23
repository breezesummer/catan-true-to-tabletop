# B3 Windows 开发程序验证

日期：2026-10-23（Asia/Shanghai）。版本保持内测版2.0.1，本轮不是正式发布。

最终程序：`game version/2.0/Catan2.0.1-Three-Kingdoms-B3/Builds/Windows/Catan2.0.1.exe`，同目录包含自包含 Windows 房间服务。Unity 6000.3.25f1 构建成功，最后一次 .NET／Unity 客户端构建耗时33.2秒。规则、AI及协议程序集在编译输出、Unity Plugins和程序 Managed 目录的SHA-256逐一相同，见[程序指纹](Windows程序指纹.json)。

程序通过 `--three-kingdoms-verify <绝对证据目录>` 在隐藏 D3D11 窗口运行，使用产品本地开局、所属玩家视图和真实提交入口。此次约56秒完成，日志包含 `CATAN_THREE_KINGDOMS_PLAYER_OK`，无异常记录；[result.json](result.json)确认包版本为 `three-kingdoms-dice-time-16-v1`。

| 检查 | 结果 |
| --- | --- |
| 城市与骑士、双扩展组合各三人／四人 | 4种配置通过 |
| 新局使用完整16将包、候选数量与选后将池实体总数 | 各配置通过 |
| 开局候选、登场、准备阶段、两次补将／轮换、技能提交前后精确保存恢复 | 36次通过 |
| 同一合法阶段连续主动隐退 | 8次通过 |
| 从玩家投影读取技能选项并经产品入口提交 | 4次通过 |
| 重新开始保留三国开关、人数和玩法 | 4次通过 |
| 对手候选、暗将及技能私密选项隔离 | 各配置通过 |
| 清理构建自动改写后的Unity文本源文件完整性 | 1919个配置／脚本通过 |

首次运行在组合三人配置复现直谏暂挂期间补将入口冲突。修复投影优先级和挂起窗口标识后，补充两种宿主的正常会话逐点公开 `Load` 重放，再重新构建并执行上述最终运行；不以失败前通过的部分配置拼接成最终通过。

最终原始日志和私密权威检查点保存在 `.local/verification/three-kingdoms-b3/player-verified/`，构建日志为 `.local/verification/three-kingdoms-b3/windows-build-verified.log`。首次失败保留在同级 `player-final/`，不作为通过证据。公开验收目录仅复制结果及程序指纹，不包含私密存档。

本次隐藏窗口未生成可用于验收的IMGUI截图：`images=0`、`guiImagesWithChanges=0`、`fullUiPixelsVerified=false`。`graphics=true`只说明存在图形设备，结果中的截图说明描述探针能力，不代表生成了截图。没有像素、真实鼠标／触控、Android真机或用户整局试玩证据；三国联机继续关闭。
