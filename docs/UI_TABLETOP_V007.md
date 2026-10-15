# 圆角卡纸、收牌插入与分数构成 v007

2026-10-15 更新：本文为历史验收记录，原策略程序与旧插件构建脚本已清理，下述双版本路径与构建方式已停用。当前两个启动脚本统一使用现行策略和 [CatanM6.exe](../.local/m6-ui-v010/Builds/Windows/CatanM6.exe)；详见 [清理记录](AI_LEGACY_CLEANUP_V001.md)。

2026-10-13 按本轮七项反馈更新 M6。保留优化 AI 和原策略两个入口、既有存档、原照片及历史程序。本轮不改变规则计分或 M7/M8 阶段状态。

## 本轮行为

1. 资源、商品、进步卡和程序卡面采用圆角裁切，恢复实体纸卡轮廓；仍按屏幕像素保留原图比例。卡面加入薄纸芯、柔和投影及微弱整面受光。
2. 手牌离逻辑屏幕底边留出 35 像素。悬停平滑轻抬约 10 像素、轻微摆动，点击选择仍保留额外抬升；手牌位置不受棋盘缩放影响。
3. 操作框圆角底板改为一次完整绘制，去除原九块图形在非整数缩放下产生拼接缝的来源。
4. 收牌先由银行起飞，沿弧线减速接近手牌，末段逐渐撑开对应插槽并进入相邻牌之间。骰子等待与飞行期间，新增牌不预先出现在手牌中，也不可提前按单张手牌选中。
5. 悬停分数可查看构成。本人包含获准查看的暗分，对手只显示公开来源和明分，即使终局视图披露总分也不反推暗分。支持四类规则中的建筑、道路／航线、军队、剧本奖励、布匹、大都会、保卫者、商人及胜利点卡。
6. 城市设施从 114×152 放大到 142×188 逻辑像素，并采用圆角倒角厚板网格。骰子和备件同步避让，升级翻板保持可点击。
7. 地图 6、8 点统一使用亮红色，兼顾当前桌面与保留的旧显示路径。

## 权限、素材和复现

规则仍先完成权威结算；动画仅延迟视觉显示，不控制资源、胜负或存档。飞牌来自当前 PlayerView 获准的公开历史，玩家交易、垄断和私密偷取不伪装成银行发牌。首次连接、换席位及恢复历史只建立基线；已花费资源不会被动画补回。

本人手牌使用屏幕空间的纸芯与阴影表现，不声称是可旋转的三维手牌模型。桌上银行卡、城市设施及翻板则使用真实运行时网格，厚度分别为 0.45、2.2、0.75 毫米，均为外观工作估算，未经用户实测确认。纸面微颗粒为确定性程序纹理，不消耗规则随机数。运行时网格随物件回收，避免银行库存变化造成长期积累。

本轮两张原始参考保存在 [参考清单](../assets/references/ui-v007/sources.json)，未经加工；既有正式卡面也未重绘或重新采样。参数、来源、轴向、枢轴和复现入口见 [卡纸配方](../art/recipes/ui-tabletop-v007/card-paper.json)。专用接触阴影着色器作为版本化 Resources 资产打包。

## 启动

- 根目录 `启动卡坦.cmd` 或 `Play-AiStrategy.ps1`：优化 AI，`.local/m6-ui-v007/Builds/Windows/CatanM6.exe`。
- `Play-M6.ps1`：原策略，`.local/m6-ui-v007-original-ai/Builds/Windows/CatanM6.exe`。

运行中的历史程序不会自动加载更新，应先保存退出再开新版本。原策略程序以保留版本的规则及 AI 程序集构建，构建后恢复工作区当前源码对应插件。

## 验证方法

本轮全量 **721/721 项测试通过**，失败和跳过均为 0，包含 52 个完整 AI 局。两版程序均完成 Unity 构建和四类规则的独立进程保存恢复。优化版无图形／图形各 28 配置通过，共分别 15,078／15,086 次断言。批处理完整 IMGUI 像素标记仍为 false，不能作为完整界面截图验收。

真实 1920×1080 窗口已检查圆角、悬停、本人／对手分数构成、骑士／银行操作框、放大设施和亮红点数，并实际完成三谷物换一砖。48 帧收牌记录确认落位前 13 张可见、1 张待到手，约 1.05 秒后 14 张可见、0 张待到手；末段相邻牌遮挡经逐帧查看。早段存在约 0.73 秒采样间隔，本记录不作为稳定帧率或全轨迹丝滑程度的性能验收。设施升级完整鼠标流程、全部模式／分辨率人工外观、多资源逐种录像、长期性能和用户最终体验尚未验收。

独立测试包括新增分数构成和隐藏信息非干扰案例、收牌等待／逐张抵达／花费取消／换视图案例，以及既有全量规则和完整 AI 对局。Unity 使用固定版本 `6000.3.25f1`，继续验证 47 张导入贴图和四种窗口比例的几何边界。实际运行与未验项目见 [公开验证摘要](verification/tabletop-ui-v007/summary.json)。

开发验收局通过显式 `--tabletop-v005-review` 参数启用；F8 保存单帧，F9 记录下一次操作的连续帧及可见／待到手牌数。连续画面先保存在内存，动画完成后再压缩写盘。此入口只使用独立 `.local` 验收目录，不读取或覆盖用户正常存档。带本人手牌的截图和权威验收存档仅保留本地。

复现命令：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-M6Rules.ps1 -ResultsDirectory .local/m6-ui-v007/test-results
powershell -NoProfile -ExecutionPolicy Bypass -File tools/development/Build-M6.ps1 -VerifyPlayer -OutputRoot .local/m6-ui-v007
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1 -BuildRoot .local/m6-ui-v007
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-TabletopUi.ps1 -Graphics -BuildRoot .local/m6-ui-v007
powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/Test-M6Persistence.ps1 -BuildRoot .local/m6-ui-v007
```
