# 1.6.5 交易、卡牌交互与音效验证

日期：2026-10-18。对应 `game version/Catan1.6.5-Card-Interaction-Audio`。

## 本轮行为

- 四套规则在报价时检查支付能力，缺少报价要求的任一资源／商品即按普通拒绝处理。广播中其他人仍可接受；全部无法支付时直接结束报价，不转移手牌，不公开缺少的牌种与数量。
- 进步卡使用面板删除弃置按钮，规则入口拒绝四张及以下的主动弃置。超限处理保留。历史记录保存交互修订号，旧版本历史仍按原行为重放，恢复后的新操作采用当前规则。
- Esc 菜单增加发展卡百科，收录基础版五类发展卡和城市与骑士二十五种进步卡；进步卡文字直接复用游戏内说明。
- 工程学、医学、发明、锻造、商人、外交、谋略、征税、叛变通过地图选目标；拆城、大都会、骑士撤退与叛变后续同样直接选棋子／位置。叛变点对手骑士仅用于选中该玩家，具体移除哪名骑士仍由对手决定。锻造支持一名或两名；叛变安置可在局部气泡选择等级或放弃。
- 免费建设的海岸若同时允许道路与船，先弹局部选择卡片，再提交相应命令。悬停和取消不改变权威状态。
- 次级提示移至顶部栏下方，开局次序与次级提示分行，主要回合提示仍居中。
- v003 音效减少密集重复敲击与低频隆隆声，缩短无效尾音并保留淡出；骑士语音换为 Brandon Song 的 CC0 真人短应答／战呼。录音不是中文台词，界面不再标注“中文／英语”。[来源与授权](../../../assets/references/audio/adventurer-v001/README.md)。

## 规则依据

- `catan-base-2025-en.pdf`，PDF 第7页：交易。自动拒绝是本轮用户要求的交互策略。
- `catan-cities-knights-2025-en.pdf`，PDF 第7页：资源与商品交易；第10页：进步卡使用与超限；第11页：毁城选择；第13–16页：全部进步卡效果，其中叛变在第16页。
- `catan-cities-knights-2025-en.pdf`，PDF 第12页，及 `catan-seafarers-2025-en.pdf`，PDF 第2页：组合与道路／船。
- 原件位置与校验来源见[本地规则索引](../../../assets/references/rules/README.md)。

## 检查入口与范围

- `.NET`：`Catan.Rules.Tests`、`Catan.Audio.Tests`、`Catan.Network.Tests`、`Catan.Server.Tests`。交易专项包含非法回应无状态变化、部分／全员自动拒绝、混合商品、重复指令、预览隔离和新旧存档重放；弃牌覆盖四张及以下拒绝、五张恢复四张后继续弃置拒绝。
- 游戏交互：`--feedback-verify <项目.local内独立目录>`。运行两套扩展的直接目标输入、非法目标、预览不消耗卡牌、锻造气泡、叛变对手决定权、免费道路／船与百科返回检查。使用后台无图形模式，不能作为完整界面像素验收。
- 音频：先运行 `tools/art/Prepare-OpenVoice.cjs`，再用 `Export-TabletopAudioV3.ps1` 导出及安装，用独立输出目录重建后运行 `Verify-TabletopAudioV3.ps1`。53个文件逐项检查PCM格式、峰值、接缝、哈希与无损导入，结果在 [audio-assets.json](audio-assets.json)。
- 本轮不推进路线阶段，不代表安卓真机、真实多机、人工听感或完整视觉验收。

## 最终结果

- .NET 五组最终批次合计1355项通过，0失败，其中包含52场完整AI对局，详见 [dotnet-tests.json](dotnet-tests.json)。
- Windows 后台160条交互断言通过，含两套扩展拆城待决直接点击；0张界面截图，详见 [runtime.json](runtime.json)。
- 最终安装包及文件哈希见 [builds.json](builds.json)，Android结构、版本和签名见 [android-apk-verification.json](android-apk-verification.json)。
