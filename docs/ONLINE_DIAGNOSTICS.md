# 联机通信与卡顿诊断

## 启用与日志位置

包含本功能的新客户端及服务端默认记录诊断日志，无需传入采样参数。旧的 2.0.1 游戏包和旧云端程序不会因源码更新自动获得日志；同版本号的包以本轮独立构建目录和日志中的会话信息区分。

- Windows 客户端：本轮实际构建为 `%USERPROFILE%\AppData\LocalLow\CatanDevelopment\Catan2_0_1\m7-logs`。部分历史构建的产品目录使用 `Catan2.0.1`；以 `Player.log` 中的 `CATAN_M7_LOG_DIRECTORY` 为准，收集脚本会自动查找所有 `Catan*` 版本目录。
- Android 客户端：`Application.persistentDataPath/m7-logs`，位于应用外部文件目录。设备文件访问方式取决于 Android 版本与调试授权。
- 在线服务器：`<data-dir>/diagnostics`；现有云端启动参数对应 `/var/lib/catan-m7/diagnostics`。
- 客户端文件为 `comm-*.jsonl`，最多保留 8 个、每个约 4 MiB；服务端为 `communication-*.jsonl`，按 UTC 日期或约 16 MiB 轮转，最多 8 个。文件大小限额存在单条记录的微小边界差值。

日志由有界后台队列写入，不在 Unity 每帧或权威房间锁内等待磁盘。队列满会丢弃诊断事件并计数；磁盘失败不应导致游戏断线。异常退出可能损失最后一批尚未写出的日志。诊断不是无限期审计存储，复现后应及时收集。

仅记录事件、时间、协议/版本、请求与命令编号、房间编号、修订号、传输类型、字节数、耗时、队列与错误分类。服务端连接凭据关联值仅保存摘要。不会记录口令、恢复凭据、玩家名称、手牌、权威存档或原始网络正文。

## 收集与汇总

每名玩家在复现后分别收集本机日志；可保持游戏运行，收集工具允许读取正在写入的文件。运行中的最后一行可能未完成，汇总工具会计数后跳过。

```powershell
# 自动查找本机 CatanDevelopment 下各版本最近 24 小时的诊断日志。
./tools/validation/Collect-M7Diagnostics.ps1

# 指定目录，例如单个版本的日志或已下载到本地的服务端诊断目录。
./tools/validation/Collect-M7Diagnostics.ps1 -LogDirectory 'D:/诊断/客户端','D:/诊断/服务端' -RecentHours 48

```

服务端使用独立 `catan` 用户，诊断目录可能只允许服务用户或管理员读取。维护者应先用 `sudo` 将 **diagnostics 目录内的 JSONL** 导出到临时受控目录，再通过 SCP 下载；不要打包整个 `data-dir`，也不要放开存档目录权限。

收集脚本只复制指定目录顶层的 `.jsonl`，不会复制 `m7-client`、`.credential`、房间存档或 `Player.log`。输出为 `.local/network-diagnostics/<UTC时间>-<随机编号>.zip`，不自动上传或发送。

```powershell
# 解压各客户端与服务端日志后，合并分析。需要 Python 3.10 及以上，无第三方依赖。
python tools/validation/Summarize-M7Diagnostics.py .local/network-diagnostics `
  --since '2026-10-22T22:00:00+08:00' --until '2026-10-22T23:50:00+08:00' `
  --output .local/network-diagnostics/summary.json

# 根据某次慢请求的编号查看客户端和服务端关联事件。
python tools/validation/Summarize-M7Diagnostics.py .local/network-diagnostics `
  --request-id '<requestId>' --output .local/network-diagnostics/request.json
```

工具支持客户端平铺字段和服务端 `data` 字段两种 JSONL，按事件分别计算次数、字节数和耗时分布，并保留指定请求的关联时间线。输入时间必须有时区。四名玩家与服务器的系统时间可能存在偏差，不能直接将跨机器时间戳相减称为单程网络延迟。

## 如何判断问题发生在哪里

| 现象或记录 | 可定位的环节 |
| --- | --- |
| 客户端 `request_send` 到 `response_received` 耗时高，服务端处理及排队很短 | 优先检查网络往返、带宽、客户端后台解码；该总耗时不是纯网络 RTT |
| 服务端房间处理的 `waitMs` 高 | 同房间其他操作持有锁，结合投影与持久化记录分析；成功或拒绝以 `request.dispatched` 为准 |
| 服务端 `room.projection` 或保存事件耗时高 | 可见局面生成、序列化或权威存档写盘 |
| `response.sent` 等候/发送耗时高、`response.send_timeout` | 服务端出站排队或慢连接；结合响应字节数判断 |
| 客户端 `main_queue_backlog`、`response_apply.queueWaitMs` 高 | Unity 主线程没有及时应用已经收到的消息 |
| `input_wait_*` 的 `command_pending` 或 `request_pending` | 等待命令回执或房间请求 |
| `presentation_history` | 等待完整展示历史到齐；不能跳过后直接声称操作已执行 |
| `card_animation`、`dice_animation`、`piece_animation` | 展示动画正在占用操作阶段 |
| `disconnected`、`ai_control` | 当前连接或席位控制权状态不允许输入 |
| `frame_window`、`frame_spike` | 帧间隔异常；结合网络、棋盘、GUI、GC和焦点状态进一步归因 |

每 5 秒的 `frame_window` 中，`frameMs` 为窗口最大帧间隔，`count` 为帧数，`slowFrames` 为至少 50ms 的帧数，`gcCollections` 为第 0 代 GC 次数增量。各阶段的 `*Ms` 是 **窗口累计值**；阶段存在嵌套，不可相加当作单帧成本。`workerDecodeMs` 属于后台解析，也不能加进主线程预算。至少 100ms 的尖峰最多每 5 秒单独记录一次，不能以尖峰记录条数代替慢帧总数。

日志只能给出 CPU 阶段、帧间隔和通信证据，不能单凭它证明 GPU 瓶颈、显示器实际刷新率或所有玩家的网络质量。

## 本轮稳定性与性能调整

1. WebSocket 的完整局面心跳从每秒一次调整为每 10 秒一次；有变化的局面仍由服务端原有 750ms 检查周期推送，不等待下一次心跳。
2. WebSocket 失败后继续 HTTP 同步，并自动重试恢复实时连接；每次心跳带独立回执期限，避免半断连接长期不被发现。
3. WebSocket 传输关闭不再立即剥夺玩家控制权。真正没有客户端活动时，仍由原有 45 秒宽限触发离线处理；主动离开房间仍立即生效。
4. 服务端内部推送读取不再刷新玩家活跃时间，避免服务端自己不断“保活”一个已经失联的玩家。
5. 服务端优先发送请求回执，将未发送的全量状态推送合并为最新一份；历史仍可按序号补齐，命令回执不参与合并。回执队列超过 8 条或发送超时仍会断开，已提交命令依靠原编号幂等重试。
6. HTTP 接收与响应解码、WebSocket 收发及保活从 Unity 帧调度中移出；主线程按预算应用消息，避免一次排空积压队列。单次重建棋盘仍可能超过预算，日志会保留其成本。

这些修改保持现有协议、权威命令去重、隐藏信息投影与规则结果。验证范围和昨晚调查事实见[本轮记录](verification/online-diagnostics-20261023/README.md)。
