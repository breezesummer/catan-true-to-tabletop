# 2026-10-20 — 云服务器部署与公网联机验证（1.6.12）

游戏：卡坦岛之三国争霸，内测版 1.6.12。日期采用 Asia/Shanghai。

## 连接入口与部署事实

在线地址：`http://175.178.212.142:47771`；游戏的“在线游戏 → 服务器地址”填写 `175.178.212.142:47771`。1.6.12 首次使用自动采用该地址，已有非空自定义配置优先。协议继续沿用用户已确认的 HTTP/WS，在线模式不启用局域网 UDP 发现。

服务实际运行于 Ubuntu 24.04.4 LTS / x86_64，使用自包含 Linux 构建，无需在服务器安装 .NET SDK。systemd 服务为 `catan-m7.service`，已启用开机启动及失败重启；以独立 `catan` 用户运行。程序位于 `/opt/catan-server/releases/1.6.12`，`/opt/catan-server/current` 指向该版本；权威存档位于 `/var/lib/catan-m7`，目录 `0700`、新文件 `0600`。服务采用 `UMask=0077`、`NoNewPrivileges`、只读系统及独立临时目录。部署单元见 [catan-m7.service](../../../tools/deployment/catan-m7.service)。

本轮保留默认 AI 间隔 300 毫秒和同源 300 次／10 秒限流，没有通过提高服务器上限换取验证通过。私钥 `codex.pem` 已加入忽略规则并收紧本机权限；凭据、权威存档和原始恢复材料不进入公开证据。已在停服时备份私有目录至服务器 `/var/backups/catan-m7`，备份目录 `0700`、文件 `0600`。

## 实际验证

| 项目 | 结果与范围 |
| --- | --- |
| 公网健康检查 | Windows 开发机访问真实公网 `/health` 成功，协议 2、HTTP 47771 |
| 服务端回归 | 51／51 通过 |
| 房间／协议回归 | 115／115 通过 |
| 公网 HTTP／WS 冒烟 | 12 项通过：公开房间列表、三个独立身份、逐席位投影、错误协议拒绝、重复命令、同 ID 内容冲突、伪造席位拒绝、非法指令不改状态、授权历史及真实 WS 回执与推送 |
| 实际服务重启 | systemd 停止、备份、启动后恢复原席位，旧连接失效，重复命令返回原提交回执，不再次执行 |
| 正式 Windows 客户端 | 四规则均推进至第 3 回合；真实 Submit／预览、服务器 AI、重连、直接切房及按房恢复、更名、本人投影、禁止本地权威和存档导出全部通过 |
| Android 构建 | 1.6.12／10612、ARM64 与 x86_64、API 26／36、横屏、INTERNET、允许 HTTP 明文、有效 v2 签名、非调试、无开发私有材料检查通过；未计作真机联机 |

首次公网探针曾发生一次 TCP 连接超时，未计为成功。保持程序和服务配置不变重跑后冒烟及重启恢复均通过；未为该失败指定未经证实的原因。

四规则 Unity 结果如下；传输按实际摘要记录，不将 HTTP 后备同步记作 WS。

| 规则／剧本 | 席位 | 人类指令 | 回合 | 实际传输 |
| --- | ---: | ---: | ---: | --- |
| 基础版／base | 3 | 4 | 3 | WS |
| 航海家／new-world | 4 | 6 | 3 | WS |
| 城市与骑士／base | 3 | 7 | 3 | HTTP，每秒同步 |
| 双扩展／through-the-desert | 4 | 5 | 3 | WS |

四项客户端 AI 指令均为 0。Windows 验证使用随包程序和本机 DPAPI 凭据保管，材料写入独立 `.local` 目录。后台截图均为纯黑，因此不计视觉、鼠标操作或最终外观验收。

## 独立客户端完整局

每个人类测试席位独立运行一个 .NET 客户端进程，通过实际公网访问同一云端权威房间。测试策略只读取网络返回的本人 `PlayerView`，不直接读取服务器存档。请求间隔 250 毫秒，非行动轮询间隔 500 毫秒；后续房间最多两个并行，服务器限流保持默认。

| 规则／剧本 | 独立进程 | 耗时（秒） | 成功规则指令 | 最终修订号 |
| --- | ---: | ---: | ---: | ---: |
| 基础版／base | 3 | 224.60 | 422 | 429 |
| 航海家／heading-for-new-shores | 4 | 434.60 | 508 | 517 |
| 城市与骑士／base | 4 | 383.84 | 471 | 480 |
| 双扩展组合／through-the-desert | 4 | 149.26 | 282 | 291 |

四局合计 **15 个独立客户端进程、1,683 条成功规则指令**，全部正常退出，没有过期修订重试或传输限流错误。修订号包含房间操作，不等于规则指令数。

全部结束后再次停服备份并启动，四种规则的终局均保留，修订号不回退，公开大厅为 0 个房间，没有残留测试房间。原席位及原命令去重再次通过。备份归档可完整列出；没有将此记作从备份还原演练。重启后的首次健康检查在 5 秒内未返回，重试后成功，服务加载已有存档时需容许启动时间。

## 复现与服务管理

在仓库根目录加载开发环境后运行：

```powershell
. ./tools/development/Enter-DevEnvironment.ps1
$probe = 'tests/Catan.Network.Probe/bin/Release/net10.0/Catan.Network.Probe.dll'
$cloudArgs = @('--endpoint','http://175.178.212.142:47771','--request-delay-ms','250','--poll-ms','500','--output','.local/cloud-deploy-new/probe')
& $toolchain.dotnet $probe --mode prepare @cloudArgs
# 服务重启后，使用同一目录恢复与重试。
& $toolchain.dotnet $probe --mode resume @cloudArgs
& $toolchain.dotnet $probe --mode process-game --rules base --scenario base --seats 3 @cloudArgs

pwsh -NoProfile -File tools/validation/Test-M7Player.ps1 -BuildRoot 'game version/1.6/Catan1.6.12-Cloud-Online' -Endpoint 'http://175.178.212.142:47771' -SkipLanDiscovery -OutputRoot '.local/cloud-deploy-new/player' -TimeoutSeconds 300
```

HTTPS 局域网验证仍必须提供指纹文件，错误指纹拒绝检查保留。在线 HTTP 验证不会假报执行了证书校验。

服务器管理：

```powershell
ssh -i ./codex.pem ubuntu@175.178.212.142 'sudo systemctl status catan-m7 --no-pager'
ssh -i ./codex.pem ubuntu@175.178.212.142 'sudo systemctl restart catan-m7'
ssh -i ./codex.pem ubuntu@175.178.212.142 'sudo journalctl -u catan-m7 -n 50 --no-pager'
```

升级应先停服并备份私有目录，再切换程序版本，保留 `/var/lib/catan-m7`，避免两个进程同时写入。同机生成的恢复身份必须保留，昵称不能代替凭据。

## 证据与边界

公开证据包括 `deployment.json`、`health.json`、`tests.json`、`smoke.json`、`restart.json`、`final-restart.json`、四份 `unity-*.json`、`process-*.json`、`full-games.json`、`workers.json` 及 `android-apk-verification.json`。`workers.json` 汇总每局成功指令和过期修订数；探针遇到其他传输或限流错误会失败，本轮通过的所有子进程退出码均为 0。原始 TRX、日志、私有客户端配置、回执恢复材料仅保存在 `.local/cloud-deploy-20261020`。

PC、随包 Windows 服务、Linux x64 服务和 Android APK 均位于 `game version/1.6/Catan1.6.12-Cloud-Online`；EXE、游戏窗口和 APK 名为 `Catan1.6.12`。本轮按内测补丁递增版本，不作正式发布。

本轮的多个客户端进程运行在同一 Windows 开发机，通过公网访问另一台 Linux 云主机。没有宣称多台玩家设备、跨运营商、Android 真机、用户完整鼠标对局、清洁 Windows 或长期容量已通过。没有满足完整 M7 的全部退出要求，`docs/ROADMAP.md` 保持原状态。
