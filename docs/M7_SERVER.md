# M7 权威房间服务

日期：2026-10-15；当前行为更新于 2026-10-19。服务端使用 ASP.NET Core / .NET 10，同一代码可发布为 Windows 局域网后台服务或 Linux 在线服务。按用户要求，在线模式直接通过公网 IP 使用 HTTP `/api/request` 和 WS `/ws`，不要求域名和证书；局域网模式保留 HTTPS/WSS 与自动证书发现。规则、随机数、当前 AI、命令去重及权威存档均留在服务端。

当前用户要求持久访客凭据、存档不自动删除、房间公开可加入（设密码时校验密码）；2026-10-17 更新为离线 AI 接管、重连下一次本人行动时交还控制。服务运行时，即使全体人类离线也继续推进至完局；本轮仍以本机可验证部分为范围。当前没有用户云服务器，本文件提供部署方法，不代表已部署在线大厅或通过跨网络验收。

## Windows 随包服务

在仓库根目录运行：

```powershell
pwsh -NoProfile -File tools/development/Build-M7Server.ps1
```

默认输出 `game version/1.6/Catan1.6.9-Online-Ip-Lobby/Builds/Windows/Server/Catan.Server.exe`，连同该目录所有文件一起放在游戏 EXE 旁的 `Server` 目录。该发布包含 .NET 运行时；是否能在未安装 .NET 的清洁 Windows 机器运行仍须实际验证，不能用开发机成功启动代替。

手动启动示例：

```powershell
.\Server\Catan.Server.exe --data-dir "$env:LOCALAPPDATA\CatanThreeKingdoms\M7Server" --port 47771 --discovery-port 47772
```

默认局域网模式监听 HTTPS/WSS，在线模式监听 HTTP/WS；两者默认 TCP 端口均为 `47771`，每个进程只使用所选模式。UDP `47772` 仅用于局域网发现。Windows 防火墙如未允许私有网络入站，发现和其他机器加入可能失败；手动 IP 不能绕过防火墙或 Wi-Fi 客户端隔离。没有自动添加防火墙规则。

| 参数 | 默认值及用途 |
| --- | --- |
| `--data-dir` | 当前用户本地应用数据下 `CatanThreeKingdoms/M7Server`；存放私有证书、身份及房间 |
| `--port` | `47771`，在线 HTTP/WS 或局域网 HTTPS/WSS 端口 |
| `--discovery-port` | `47772`，UDP 发现端口 |
| `--disable-discovery` | 关闭发现；别名 `--no-discovery` |
| `--server-name` | `卡坦岛之三国争霸`，公开发现名称（1–64 字符） |
| `--mode` | `lan`；`online` 使用 HTTP/WS，不要求证书并关闭局域网发现 |
| `--certificate` | 仅局域网模式可指定 PFX 路径；在线模式应移除此参数。密码从进程环境变量 `CATAN_CERTIFICATE_PASSWORD` 读取，不放命令行 |
| `--ai-interval-ms` | `300`；范围 10–10000，短间隔用于独立验证 |
| `--request-limit-per-10s` | 同源地址 10 秒内 `300` 次，容纳多位玩家共用一个出口的同步与交互预览；范围 1–100000，测试可显式提高，不能把该值当作实测容量 |

首次局域网启动生成两年有效的自签证书，并持久化 SHA-256 指纹到 `server-fingerprint.txt`。客户端进入“房间列表”后自动搜索，支持重新搜索和点击加入；按 2026-10-17 用户要求，不再显示手动核对指纹步骤，而是自动绑定所选发现结果中的证书。TLS 必须匹配该证书；发现广播依赖可信局域网环境，不构成带外身份核验。在线模式不读取也不生成局域网证书文件。

证书、凭据和存档目录在 Windows 禁用继承并仅授权当前用户，在 Linux 设置为 `0700`。请用专用目录，不指向多人共享目录或已有其他数据的目录。局域网电脑所有者仍能读取权威存档，因此 LAN 对局信任房主。证书过期、密钥缺失或损坏时停止启动，不能静默生成新证书绕过原固定关系。

服务及房间目录分别使用排他文件锁防止重复写入。同端口或同目录重复启动会失败。停止服务前，客户端已收到成功确认的命令已保存；重新启动同一目录后凭原访客凭据重连。全员离线的已开局房间由 AI 继续推进，前提是服务进程持续运行且未因故障或命令上限暂停；不自动删除私有存档，不提供无缝换主。未开局空大厅关闭，无在线人类或已结束房间不出现在公开列表。丢失访客凭据不能仅凭昵称/房间号找回席位。

Android 只能作为加入端使用现有房间服务，不能启动 Windows 随包服务；AndroidKeyStore 凭据保管及真实手机连接、重连行为仍需真机验收。玩家恢复材料按房间保存：LAN 使用服务器证书与房间号作为稳定键，同证书服务器更换 IP 后保留原身份；在线按服务地址与房间号区分。Windows 客户端使用当前用户 DPAPI，账号和跨设备找回尚未提供。

## 协议及限额

`GET /health` 返回状态、协议版本、服务名称和传输端口，不包含存档。在线模式返回 `transport=http`、`httpPort`、兼容字段 `httpsPort=0` 及空指纹；局域网模式返回 HTTPS 端口和证书指纹。大厅与操作统一 `POST /api/request`，请求/响应使用 `Catan.Network.Contracts.NetworkRequest/NetworkResponse` 和 `WireJson` 的字段名。口令和凭据只放消息体，禁止放入 URL；在线消息体按用户要求明文传输。客户端发送协议版本；创建、加入及重连必须传期望规则及场景版本，缺失或不兼容时拒绝。访客凭据默认有效 180 天；主动 `Revoke` 注销后不能再恢复席位，已开始的席位由 AI 接管，房间文件不会随凭据失效而删除。

当前客户端在第一次创建或加入前预存随机恢复凭据和 `EnrollmentId`，提交 `EnrollmentCredential` 后成功回复不再回传长期凭据。回执丢失可重发原申请，服务从同一次持久提交的登记账本恢复原席位；同标识不同内容、过期或已吊销申请不能重新领取。账本只保存凭据哈希和使用该高熵凭据计算的 HMAC 请求指纹，避免含房间密码的申请形成额外快速猜测校验值。旧的无登记字段请求保留兼容入口，只有当前预存流程具备首次回执丢失恢复能力。

WS/WSS 首次成功加入/重连/读取后绑定该物理连接的凭据、控制 ID 与房间；切换房间或身份应重新建立 WS/WSS 连接。常规请求有对应 `RequestId` 回复；每 750 毫秒检查已绑定身份的最新本人视图，仅变化时推送，推送 `Code=Push`、`RequestId=null`。新连接取回控制后旧连接的鉴权读取失败并断开。客户端关闭 WS/WSS 后服务器登记掉线并由 AI 接管；HTTP/HTTPS 同步连接的失联由心跳超时识别。客户端不可根据动画或网络中断本地推进权威对局。`HumanControlAvailable` 表示真人能否操作，`ControlledByAi`／`RejoinPending` 公开托管与等待交接状态。重连先恢复视图，下一次本人的行动边界再交还控制；开局使用摆放轮次识别边界，仍在线连接的替换不额外触发托管等待。

每条请求最多 64 KiB，响应最多 4 MiB；最多 64 个 WS/WSS、128 个普通传输连接，WS/WSS 待发队列最多 8 条，单次发送超过 5 秒即关闭慢客户端。未认证连接 10 秒内须完成首个请求，分片之间最多等待 10 秒。每源地址请求限速，WebSocket ping 每 15 秒发送，等待 pong 最多 15 秒。只接受原生客户端，浏览器 `Origin` 请求被拒绝。没有压缩私有视图，没有公开存档下载接口，不记录请求体、凭据、口令或异常原文。恢复后按原命令 ID 重试，由持久去重账本避免重复执行。

UDP 查询为 ASCII `CATAN_M7_DISCOVER_V1`。回应为 UTF-8 JSON，字段为 `game`、`protocolVersion`、`serverName`、`httpsPort`、`certificateFingerprint`、`rooms`，只含最多三个公开房间的名称/规则/人数/开始/密码标记。客户端使用响应源 IP 加 HTTPS 端口连接。发现仅响应 RFC1918、环回与链路本地源地址，每源地址 10 秒 4 次，响应不超过 1200 字节。客户端收到服务发现后，会通过绑定该证书的 HTTPS 查询完整当前房间列表；无在线人类、已关闭和已结束的房间不列出。未列出但未关闭的大厅可按房间号加入；已开局房间只允许原身份恢复。

## Linux 在线部署准备

在开发机发布相应平台：

```powershell
pwsh -NoProfile -File tools/development/Build-M7Server.ps1 -Runtime linux-x64
```

将输出目录完整复制到服务器，例如 `/opt/catan-server`，创建专用非 root 用户 `catan` 与私有数据目录 `/var/lib/catan-m7`，给可执行文件执行权限，并让服务用户能写数据目录。无需准备域名、PFX、证书密码或反向代理。

直接启动：

```bash
/opt/catan-server/Catan.Server --mode online --data-dir /var/lib/catan-m7 --port 47771
```

systemd 示例：

```ini
[Unit]
Description=卡坦岛之三国争霸在线房间服务
After=network-online.target
Wants=network-online.target

[Service]
User=catan
Group=catan
WorkingDirectory=/opt/catan-server
ExecStart=/opt/catan-server/Catan.Server --mode online --data-dir /var/lib/catan-m7 --port 47771
Restart=on-failure
RestartSec=5
UMask=0077
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=strict
ReadWritePaths=/var/lib/catan-m7

[Install]
WantedBy=multi-user.target
```

在云安全组和系统防火墙放行所选 TCP 端口（默认 `47771`），不要为在线大厅开放 UDP 发现端口。使用固定公网 IP，从外部网络访问 `http://公网IP:47771/health`，应返回正常状态；再在游戏的服务器设置中填写 `公网IP:47771`。同一服务器内的房间共用此地址，创建和加入房间无需玩家配置端口或证书。

HTTP/WS 按用户选择明文传输，房间密码、玩家恢复凭据及所属玩家视图没有传输加密；身份认证、指令校验、按席位投影和本机私有文件权限仍保留。旧在线部署应移除 `--certificate` 与证书密码环境配置，并将客户端地址从 `https://` 改为 `http://`。

升级时先停止服务、备份整个私有目录及权限，然后替换程序，再用同一目录启动。不要同时运行两个进程或实例写同一份存档，不将私有目录放入分享包或公开网站。自包含程序的运行时安全更新需要重新发布。

部署之后仍需真实跨运营商网络、多人完整局、断线与进程重启、长期内存/延迟/容量和清洁环境验收。未进行这些检查前，M7 总阶段退出条件仍未全部满足。此次不修改既有随机算法；面向陌生人的大规模开放需要单独评估随机状态推断和滥用风险。

实现参考：[微软 WebSocket 生命周期及保活说明](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)、[Kestrel HTTPS 端点配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)。实际测试结果集中记录在 M7 验收文档，不用部署示例代替部署事实。
