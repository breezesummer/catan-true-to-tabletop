# B1 最终 Windows 程序运行证据

日期：2026-10-22（Asia/Shanghai）。本次在规则核心最终深复制优化、非法命令提前拒绝与旧五参数建局入口兼容修复后，重新构建开发包并重新运行独立程序。

程序：`game version/2.0/Catan2.0.1-Three-Kingdoms-B1/Builds/Windows/Catan2.0.1.exe`。验证使用 `--three-kingdoms-verify` 参数及隐藏窗口，未修改产品代码或注入规则状态。最终程序关键文件指纹见 [Windows程序指纹.json](Windows程序指纹.json)。

运行结果见 [result.json](result.json)，原始关键运行日志见 [windows-player.log](windows-player.log)。日志包含 `CATAN_THREE_KINGDOMS_PLAYER_OK` 成功标记，程序正常退出，未发现异常记录。

| 验证内容 | 最终结果 |
| --- | --- |
| 城市与骑士、双扩展组合各 3 人／4 人 | 4 种配置通过 |
| 开局候选、拟登场、准备阶段、两次补将及轮换登场的精确保存恢复 | 28 次通过 |
| 同一合法阶段内连续主动隐退 | 8 次通过 |
| 本地重新开始保留三国开关及玩法配置 | 4 次通过 |

隐藏窗口未产生可用于合成的 IMGUI 重绘。结果中的 `images=0`、`guiImagesWithChanges=0`、`fullUiPixelsVerified=false`，因此**没有界面截图，也没有界面像素验收证据**。`graphics=true` 仅表示运行时存在图形设备。结果文件中关于截图合成方式的说明描述探针能力，不表示本次生成了截图。

本次验证也不代表真实鼠标、触控、Android 真机或用户整局试玩通过。测试所需的权威存档保存在本地 `.local/three-kingdoms-b1/player-final/`，不作为公开日志内容纳入本目录。
