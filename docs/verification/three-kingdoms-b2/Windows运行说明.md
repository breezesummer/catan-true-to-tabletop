# B2 最终 Windows 程序运行证据

完成日期：2026-10-23（Asia/Shanghai）。本次以最终冻结的 B2 核心重新构建，已包含商港整批牌流、轮换待决恢复、跳过机会游标和突袭重验额度修正。游戏版本保持内测版2.0.1；本轮为本地开发包，不是正式发布。

程序位于 `game version/2.0/Catan2.0.1-Three-Kingdoms-B2/Builds/Windows/Catan2.0.1.exe`，同目录包含自包含 Windows 房间服务。Unity 6000.3.25f1 构建成功，最后一次 .NET／Unity 客户端构建耗时67.6秒；最终程序关键文件指纹见 [Windows程序指纹.json](Windows程序指纹.json)。规则、AI、协议的发布程序集、Unity Plugins 和程序内 Managed 副本的 SHA-256 已逐一核对一致。

独立程序通过 `--three-kingdoms-verify <绝对证据目录>` 启动，使用隐藏窗口及真实产品本地入口，不注入修改后的规则存档。最终运行耗时55.4秒，正常退出，日志含 `CATAN_THREE_KINGDOMS_PLAYER_OK`，未发现运行异常。机器结果见 [result.json](result.json)。

| 验证内容 | 最终结果 |
| --- | --- |
| 城市与骑士、双扩展组合各3人／4人 | 4种配置通过 |
| 开局候选、拟登场、准备阶段、两次补将和轮换登场、技能选择前后的精确保存恢复 | 36次通过 |
| 同一合法阶段内连续主动隐退 | 8次通过 |
| 从所属玩家视图取得真实技能选项，并经产品技能提交入口执行 | 4次通过 |
| 本地重新开始保留三国开关、人数与玩法 | 4次通过 |
| 其他玩家候选、暗置身份及技能私密选项隔离 | 各配置通过 |
| 最终源码的协议／房间回归 | 137／137通过 |
| 最终源码的服务端回归 | 53／53通过 |
| 构建后清理自动资产改写，再检查Unity文本源文件 | 1915个配置／脚本通过 |

隐藏窗口未产生可用于合成的 IMGUI 重绘。结果中的 `images=0`、`guiImagesWithChanges=0`、`fullUiPixelsVerified=false`，没有界面截图或像素验收证据。`graphics=true` 仅表示存在图形设备；结果文件中的截图说明描述探针能力，不表示本次生成了截图。此探针也不模拟真实鼠标、触控、Android真机或用户完整对局试玩。

原始运行日志及权威检查点存放在本地 `.local/verification/three-kingdoms-b2/player-final/`；私密存档没有复制到公开验收目录。最终构建日志为 `.local/verification/three-kingdoms-b2/windows-build-final.log`，协议与服务结果分别为同目录的 `network-final.trx`、`server-final.trx`。三国对局仍仅支持本地，以上协议和服务回归不表示已经启用三国联机。

构建入口为 `tools/development/Build-M7.ps1 -OutputRoot 'game version/2.0/Catan2.0.1-Three-Kingdoms-B2' -SkipVerification`。该开关仅跳过构建脚本的历史通用验证；本轮规则测试、协议／服务测试、源文件检查及实际程序运行分别执行并记录，不以构建成功代替运行验证。
