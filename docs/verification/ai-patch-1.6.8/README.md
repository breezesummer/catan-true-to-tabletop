# AI 逻辑 2.0 补丁验收（1.6.8）

日期：2026-10-19。最终验证在基线 `8d88206` 的独立分支 `codex/ai2-strategy-patch` 完成；原工作区同时进行的 1.6.9 联机开发未纳入此提交。运行环境为 macOS、Unity 6000.3.25f1、.NET SDK 10.0.401。

## 自动验证

| 范围 | 通过／总数 | 结果 |
| --- | --- | --- |
| 规则与展示 | 1136／1136 | 全部通过，包含下述 AI 专项和完整局，不能重复相加 |
| 网络 | 115／115 | 包含 AI 扩权字段不进入网络、不接受客户端注入的检查 |
| 音效 | 219／219 | 全部通过 |
| 服务端 | 19／23 | 四项在当前 macOS 环境失败，详见下文 |

新增 116 项 AI 专项：交易与近期获胜 62 项、蓝卡／主教／骑士战术 22 项、海岛／海盗／初设 19 项、对手手牌投影 13 项。关键案例执行真实规则指令，并检查非法席位回滚、资源与棋子守恒、视图不变以及合法获胜；没有绕过共同指令验证入口。

52 局从正常开局运行至胜利，覆盖基础、城市与骑士、航海家九个剧本、组合两个剧本，以及三／四人和种子 97／2027。共执行 **23,393 条合法指令**，结束于第 49～124 个玩家回合。逐步检查状态不变量，首次遇到每类待决时核对确定性、视图不变、保存恢复后的动作与状态一致。整局使用生产专用 AI 视图；普通人类视图继续接受原有隐藏信息边界检查。

最终代码冻结后执行上述验证和双端构建；29 个变更源码／测试输入指纹在结束时一致。源码 Release DLL、Unity 插件和 Windows 程序内 Core／AI／Network 程序集逐字节一致；Android 构建记录的 Core／AI 哈希也一致。逐局结果、失败名称、源码与产物 SHA-256 见 [summary.json](summary.json)。

## 服务端检查的限制

以下三项因 macOS 不支持使用 `EphemeralKeySet` 加载证书而失败：

- `CertificateFingerprintSurvivesRestartAndAnotherWriterIsRejected`
- `MissingCertificateSecretDoesNotSilentlyReplaceTrustedIdentity`
- `DiscoveryKeepsHealthyRoomsAndReportsTheHostWhoseHttpsListFails`

`UnansweredLocalProbeDoesNotAbortDelayedResponseFromAnotherHost` 因无法绑定测试使用的 `127.0.0.2` 地址而失败。保留原始失败记录，本轮没有修改服务端证书或地址绑定逻辑，也不将服务端整套检查记为通过。

## 构建与交付

输出目录为 `game version/1.6/Catan1.6.8-AI2-Strategy-Patch-SigningTest`，包含：

- `Builds/Windows/Catan1.6.8.exe` 与随包 win-x64 自包含房间服务。
- `Builds/Android/Catan1.6.8.apk`：IL2CPP、ARM64／x86_64，版本 1.6.8／10608，API26／36，双向横屏、非调试包。

APK 的实际清单、原生库、包内路径和 v2 签名已核对，详见 [Android 检查](android-apk.json)。使用独立临时测试签名，**不能覆盖安装此前旧签名内测版**；未执行安装或卸载。

最终构建已复制回原工作区同名版本目录，532 个构建文件逐一核对一致；TRX、测试日志和本验收记录附在产物的 `logs/verification`。原工作区现有 1.6.7、1.6.8、1.6.9 三个内测目录，未超过五版保留上限，没有删除其他版本或正式发布目录。

## 复现入口与未验范围

在本分支中设置本机 `CATAN_DEVELOPMENT_ROOT` 后，载入 `tools/development/Enter-DevEnvironment.zsh`。各测试工程使用以下相同参数执行，工程名分别为 `Catan.Rules.Tests`、`Catan.Network.Tests`、`Catan.Server.Tests`、`Catan.Audio.Tests`：

```sh
dotnet test tests/Catan.Rules.Tests/Catan.Rules.Tests.csproj --disable-build-servers -m:1 -p:UseArtifactsOutput=false -p:DirectoryBuildPropsPath="$PWD/Directory.Build.props"
./tools/development/Build-Game.command --test-signing --output-root 'game version/1.6/Catan1.6.8-AI2-Strategy-Patch-SigningTest'
```

近期胜利距离采用有限、偏保守的启发式，并非所有连招的穷举证明；本次没有测定对真人胜率。Windows 实际启动和窗口标题、Android 真机安装／触控、真实多机联机及用户对战体验仍待验证。构建成功不代替运行或视觉验收。没有满足新的阶段退出条件，ROADMAP 保持不变；本轮为内测补丁，没有创建正式发布版。
