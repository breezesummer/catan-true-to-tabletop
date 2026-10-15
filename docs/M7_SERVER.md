# M7 权威房间服务

日期：2026-10-15。服务端使用 ASP.NET Core / .NET 10，同一代码可发布为 Windows 局域网后台服务或 Linux 在线服务。客户端共用 HTTPS `/api/request` 和 WSS `/ws`；规则、随机数、当前 AI、命令去重及权威存档均留在服务端。

本轮用户已选择持久访客凭据、掉线等待、存档不自动删除、房间公开可加入（设密码时校验密码），并要求先完成本机可验证部分。当前没有用户云服务器，本文件提供部署方法，不代表已部署在线大厅或通过跨网络验收。

## Windows 随包服务

在仓库根目录运行：

```powershell
pwsh -NoProfile -File tools/development/Build-M7Server.ps1
```

默认输出 `.local/m7-v001/Builds/Windows/Server/Catan.Server.exe`，连同该目录所有文件一起放在游戏 EXE 旁的 `Server` 目录。该发布包含 .NET 运行时；是否能在未安装 .NET 的清洁 Windows 机器运行仍须实际验证，不能用开发机成功启动代替。

手动启动示例：

```powershell
.\Server\Catan.Server.exe --data-dir "$env:LOCALAPPDATA\CatanThreeKingdoms\M7Server" --port 47771 --discovery-port 47772
```

程序仅监听 HTTPS；没有明文 HTTP 回退。默认 TCP `47771` 为 HTTPS/WSS，UDP `47772` 为局域网发现。Windows 防火墙如未允许私有网络入站，发现和其他机器加入可能失败；手动 IP 不能绕过防火墙或 Wi-Fi 客户端隔离。没有自动添加防火墙规则。

| 参数 | 默认值及用途 |
| --- | --- |
| `--data-dir` | 当前用户本地应用数据下 `CatanThreeKingdoms/M7Server`；存放私有证书、身份及房间 |
| `--port` | `47771`，HTTPS/WSS 端口 |
| `--discovery-port` | `47772`，UDP 发现端口 |
| `--disable-discovery` | 关闭发现；别名 `--no-discovery` |
| `--server-name` | `卡坦岛之三国争霸`，公开发现名称（1–64 字符） |
| `--mode` | `lan`；`online` 强制提供证书并关闭局域网发现 |
| `--certificate` | PFX 路径；密码从进程环境变量 `CATAN_CERTIFICATE_PASSWORD` 读取，不放命令行 |
| `--ai-interval-ms` | `300`；范围 10–10000，短间隔用于独立验证 |
| `--request-limit-per-10s` | 同源地址 10 秒内 `300` 次，容纳多位玩家共用一个出口的同步与交互预览；范围 1–100000，测试可显式提高，不能把该值当作实测容量 |

首次局域网启动生成两年有效的自签证书，并持久化 SHA-256 指纹到 `server-fingerprint.txt`。房主显示此指纹；加入者须用房主可信渠道核对并明确固定它。广播和 `/health` 返回的指纹只是发现提示，无法自行证明对方身份。客户端验证必须仅允许已固定指纹，在线模式则正常验证系统信任链与域名。禁止全局忽略证书验证或接受任何自签名证书。

证书、凭据和存档目录在 Windows 禁用继承并仅授权当前用户，在 Linux 设置为 `0700`。请用专用目录，不指向多人共享目录或已有其他数据的目录。局域网电脑所有者仍能读取权威存档，因此 LAN 对局信任房主。证书过期、密钥缺失或损坏时停止启动，不能静默生成新证书绕过原固定关系。

服务及房间目录分别使用排他文件锁防止重复写入。同端口或同目录重复启动会失败。停止服务前，客户端已收到成功确认的命令已保存；重新启动同一目录后凭原访客凭据重连。不自动删除全员离线房间，不提供无缝换主。丢失访客凭据不能仅凭昵称/房间号找回席位。

## 协议及限额

`GET /health` 返回状态、协议版本、服务名称、HTTPS 端口和证书指纹，不包含存档。大厅与操作统一 `POST /api/request`，请求/响应使用 `Catan.Network.Contracts.NetworkRequest/NetworkResponse` 和 `WireJson` 的字段名。口令和凭据位于加密消息体内，禁止放入 URL。客户端发送协议版本；创建、加入及重连必须传期望规则及场景版本，缺失或不兼容时拒绝。访客凭据默认有效 180 天；主动 `Revoke` 注销后不能再恢复席位，已开始的席位仍保留等待，房间文件不会随凭据失效而删除。

当前客户端在第一次创建或加入前预存随机恢复凭据和 `EnrollmentId`，提交 `EnrollmentCredential` 后成功回复不再回传长期凭据。回执丢失可重发原申请，服务从同一次持久提交的登记账本恢复原席位；同标识不同内容、过期或已吊销申请不能重新领取。账本只保存凭据哈希和使用该高熵凭据计算的 HMAC 请求指纹，避免含房间密码的申请形成额外快速猜测校验值。旧的无登记字段请求保留兼容入口，只有当前预存流程具备首次回执丢失恢复能力。

WSS 首次成功加入/重连/读取后绑定该物理连接的凭据、控制 ID 与房间；切换房间或身份应重新建立 WSS 连接。常规请求有对应 `RequestId` 回复；每 750 毫秒检查已绑定身份的最新本人视图，仅变化时推送，推送 `Code=Push`、`RequestId=null`。新连接取回控制后旧连接的鉴权读取失败并断开。客户端关闭 WSS 后服务器登记掉线；客户端不可根据动画或网络中断本地推进权威对局。

每条请求最多 64 KiB，响应最多 4 MiB；最多 64 个 WSS、128 个普通传输连接，WSS 待发队列最多 8 条，单次发送超过 5 秒即关闭慢客户端。未认证连接 10 秒内须完成首个请求，分片之间最多等待 10 秒。每源地址请求限速，WebSocket ping 每 15 秒发送，等待 pong 最多 15 秒。只接受原生客户端，浏览器 `Origin` 请求被拒绝。没有压缩私有视图，没有公开存档下载接口，不记录请求体、凭据、口令或异常原文。恢复后按原命令 ID 重试，由持久去重账本避免重复执行。

UDP 查询为 ASCII `CATAN_M7_DISCOVER_V1`。回应为 UTF-8 JSON，字段为 `game`、`protocolVersion`、`serverName`、`httpsPort`、`certificateFingerprint`、`rooms`，只含最多三个公开房间的名称/规则/人数/开始/密码标记。客户端使用响应源 IP 加 HTTPS 端口连接。发现仅响应 RFC1918、环回与链路本地源地址，每源地址 10 秒 4 次，响应不超过 1200 字节。未列出房间仍可通过房间号加入。

## Linux 在线部署准备

在开发机发布相应平台：

```powershell
pwsh -NoProfile -File tools/development/Build-M7Server.ps1 -Runtime linux-x64
```

将输出目录完整复制到服务器，例如 `/opt/catan-server`，创建专用非 root 运行用户和私有数据目录 `/var/lib/catan-m7`，给可执行文件执行权限。准备玩家域名的有效 PFX 证书，使用专用环境文件保存密码并设置仅服务用户可读；客户端只用 HTTPS 域名。

systemd 示例（用户名、路径、域名证书由实际部署者配置）：

```ini
[Unit]
Description=卡坦岛之三国争霸 M7 房间服务
After=network-online.target

[Service]
User=catan
Group=catan
WorkingDirectory=/opt/catan-server
EnvironmentFile=/etc/catan-m7/server.env
ExecStart=/opt/catan-server/Catan.Server --mode online --data-dir /var/lib/catan-m7 --port 47771 --certificate /etc/catan-m7/server.pfx
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

环境文件仅需 `CATAN_CERTIFICATE_PASSWORD=实际密码`，勿提交到 Git。公网只开放所选 TCP 端口；若反向代理终止 TLS，仍须为后端提供加密 HTTPS、正确传递 WSS Upgrade 并调整长连接超时。本实现没有信任未经配置的转发地址头，代理后的限速会共用代理地址。

升级时先停止服务、备份整个私有目录及权限，然后替换程序，再用同一目录启动。不要同时运行两个进程或实例写同一份存档，不将私有目录放入分享包或公开网站。自包含程序的运行时安全更新需要重新发布。

部署之后仍需真实跨运营商网络、多人完整局、断线与进程重启、长期内存/延迟/容量和清洁环境验收。未进行这些检查前，M7 总阶段退出条件仍未全部满足。此次不修改既有随机算法；面向陌生人的大规模开放需要单独评估随机状态推断和滥用风险。

实现参考：[微软 WebSocket 生命周期及保活说明](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0)、[Kestrel HTTPS 端点配置](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)。实际测试结果集中记录在 M7 验收文档，不用部署示例代替部署事实。
