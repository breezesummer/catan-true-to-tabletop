# 2026-10-19 — 公网 IP 在线大厅（1.6.9）

本轮按用户要求将在线联机改为 HTTP/WS 公网 IP 直连，无需域名、PFX 或证书密码。局域网原有 HTTPS/WSS、自签名证书与自动发现保留。未提供真实云服务器，本轮没有部署公网服务。

## 功能与界面

- 在线入口直接显示分层大厅，包含房间列表、创建房间、当前房间和服务器地址。支持裸 IP、IP:端口和 http://IP:端口，默认端口 47771；保存后再次打开自动连接。
- 大厅中央分别显示尚未配置、连接中和“联机服务器不在线”。在线空大厅显示“还没有开放房间”及创建入口；离线清除旧列表并禁止创建、加入，提供修改地址和立即重试。
- 列表每五秒刷新。普通房间 Get/Push 不清空列表，也不延后大厅刷新时钟；列表回执只证明服务器可达，不确认席位连接。服务器重启后，席位恢复与列表刷新可独立完成；旧地址和旧连接回执不能覆盖新会话。
- 保留房间密码、玩家身份与控制权限、按席位视图、指令幂等、限流和私有文件保管。HTTP/WS 按用户明确选择明文传输，不声称仍有传输加密。

## 实际检查

| 检查 | 结果与范围 |
| --- | --- |
| 服务端及正式客户端 | 51/51，通过真实 HTTP/WS、IP 正规化、创建/加入、身份拒绝、停服/恢复、跨连接回执与 LAN 回归 |
| 房间与协议回归 | 115/115 通过 |
| 音效与展示模型回归 | 219/219 通过 |
| 真实 Unity 图形界面 | 32 条断言、14 张非黑帧，通过创建、直接加入、密码弹窗/错误/成功、停服清列表、恢复和保存地址后自动连接 |
| 构建脚本保护 | 9/9 通过 |
| Windows、Android 与服务端产物 | 构建及包检查结果见同目录 summary.json |

图形检查使用 Unity 6000.3.25f1 的 macOS 编辑器实际 Play 模式与独立 .NET 服务，截图直接读取运行帧。macOS 编辑器不支持项目原有的 Windows DPAPI / AndroidKeyStore 保管，因此仅在 UNITY_EDITOR、显式验收参数和项目 .local 目录同时满足时，对合成凭据使用内存替身；磁盘仅保存无凭据占位。不将此项视为 Windows 或 Android 平台凭据保管验证，正式包不包含替身。

### 界面证据

- [未配置服务器](01-unconfigured-lobby.png)
- [服务器离线时的中央提示](03-server-offline.png)
- [服务器在线但没有房间](05-online-empty-lobby.png)
- [创建房间](06-create-room.png)
- [真实服务的三房间列表](08-room-list.png)
- [加入密码房间后的当前房间](11-protected-current-room.png)
- [停服后重新上线](13-server-recovered.png)
- [图形检查摘要](ui-summary.json)

## 复验与部署

```zsh
source tools/development/Enter-DevEnvironment.zsh
dotnet test tests/Catan.Server.Tests/Catan.Server.Tests.csproj
python3 tools/validation/verify_online_lobby_macos.py
```

复验脚本会启动自己的服务进程和图形编辑器，使用独立 .local 目录，自动配合服务停止与恢复，不读取玩家身份。图形编辑器仍需有效许可及用户接受其软件条款。

云端将 Server/linux-x64 的完整目录复制到目标服务器后，以专用用户启动：

```bash
./Catan.Server --mode online --data-dir /var/lib/catan-m7 --port 47771
```

开放所选 TCP 端口后，在客户端保存公网 IP:端口。完整说明见 [服务端部署](../../M7_SERVER.md)。

Android 最终包已核对版本 1.6.9 / 10609、API26 / 36、ARM64 / x86_64、INTERNET、usesCleartextTraffic=true、非调试与 v2 签名。打包回调显式写入 Android 清单，不能仅依赖 Unity 的 HTTP 选项。由于原签名未备份，本轮沿用独立测试签名构建方式，不能覆盖安装旧内测版。

构建以当前工作树生成，包含原有未提交 AI 修改；这些代码不纳入本轮提交。额外规则与 AI 长局全量回归已主动取消，不计入通过项。

本轮不作正式发布，也不更新 ROADMAP 的阶段状态。真实云服务器、跨运营商多机、Windows 原生游戏运行、Android 真机及长期容量仍待验；本机编辑器和打包结果不替代这些检查。原工作树中已有的 AI 与素材修改不纳入本轮提交。
