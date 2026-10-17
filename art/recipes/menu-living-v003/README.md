# 五景动态主页 v003

五张独立原画由内置 imagegen 生成，原件保存在 `art/source/menu-living-v003`，Unity 使用的同字节副本在 `Unity/Catan/Assets/Resources/PastoralMenu/v003`。提示词见 `prompts.json`，原件校验值见 `source-checksums.json`。旧版海港原画保留。

| 主页风景 | 实时动态 | 配套环境声 |
| --- | --- | --- |
| 晨光港湾 | 局部水波与掠过的海鸟 | 海浪、海鸟 |
| 金色麦田 | 麦浪、飞鸟与浮动光尘 | 田野微风、田鸟 |
| 林间溪谷 | 溪流、树叶与飘落叶片 | 流水、林鸟 |
| 雪山城堡 | 三层飘雪与暖窗灯光 | 轻柔山风 |
| 月夜灯港 | 水面倒影、灯火与星光 | 夜风、虫鸣 |

动态由 `PastoralMenuScene.cs` 与 `LivingLandscape.shader` 实时绘制，不是预录视频，也不作为三维模型资产交付。只使用展示时间与固定参数，既不读取权威随机数，也不修改规则状态。设置中的「主页风景」保存主题与动态开关；关闭动态后显示对应原画，环境声可独立调节。

五张压缩纹理按需缓存，共用一张离屏渲染纹理；PC 动态帧为 1600×1000，Android 为 1024×640。退出主页后不执行背景绘制。所有主题共用柔和的左侧阅读渐层，保留原画比例。纹理导入规则与源图一同保存在工程内。

实际构建的画面、设置与逐主题双帧验证由 `tools/validation/Test-AudioVisual.ps1` 执行。原画完整不代表动画已通过运行验收；本轮实际结果见 `The Farmer's Log.md` 与 `docs/verification/audio-visual-v002`。
