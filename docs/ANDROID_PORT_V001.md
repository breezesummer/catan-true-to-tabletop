# 横屏安卓适配 v001／内测版1.4

2026-10-15，用户确认保留同一 Unity 工程，共用规则、当前 AI 和资产，新增横屏 Android APK、触控和手机界面，同时保持 PC 可玩。后续确认只发布 APK，不制作 PC 压缩包；首轮兼容范围为 Android 8.0（API 26）及以上的 64 位手机。

## 工程与规则边界

两端均使用 `Unity/Catan`、`M6.unity`、同一 `Catan.Core.dll` 与 `Catan.AI.dll`。没有复制第二套规则或 AI，规则版本、稳定格边点 ID、权威存档格式均沿用。手机操作只修改本地选择或提交原有命令；AI 继续只读同权限 `PlayerView`。

`MobileTabletopLayout.cs` 提供独立于 Unity 的安全区坐标和手势数学，`MobileTabletop.cs` 统一手机判断、坐标变换与字体。PC 正常启动保留原布局和鼠标键盘；`-catan-mobile-ui` 仅用于本机预览手机界面。

手机端采用常驻菜单、银行、玩家交易、设施与历史入口，底部资源数量选择和建造／卡牌抽屉。交易、设施、卡牌与扩展待决面板使用较大的按钮和可滚动内容。单指移动地图、双指缩放和平移，点选或拖放建造；点击骑士、船及港口调用原有操作。返回键先取消，再开菜单。安卓正常游戏切后台或退出时尝试保存，验收局关闭此自动保存入口，PC 保持原有手动存档行为。

## 安卓构建

`tools/development/Build-Android.ps1` 使用固定 Unity 6000.3.25f1 的 Android Build Support、SDK、NDK 与 JDK，通过 `M6Build.BuildAndroid` 生成 APK。默认包含 ARM64 与用于本地模拟器验证的 x86_64；`-Arm64Only` 可只构建 ARM64。使用 OpenGL ES 3、IL2CPP、API 26 下限与 API 36 目标；标识为 `com.catandevelopment.threekingdoms`，版本 `1.4`、版本码 `10400`。

安卓渲染采用单独的 URP 配置、1024 阴影、2 倍抗锯齿和 ETC2 纹理覆盖，保留 PC 管线与 Standalone 贴图设置。原图和网格资产仍共用。中文字体使用 [Noto Sans CJK SC 原字体](https://github.com/notofonts/noto-cjk/blob/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf)，SIL Open Font License 1.1 许可随资产保存。

`Assets/link.xml` 显式保留权威存档所用的 `DataContractJsonSerializer` 反射数据与序列化程序集，避免 IL2CPP 裁剪移除必要访问器；这对应 [Unity 官方记录的反射裁剪问题与保留建议](https://issuetracker.unity3d.com/issues/datacontractserializer-throws-a-nullreferenceexception-in-build-with-il2cpp-and-net-4-dot-x)。实际支持结论仍以 APK 四模式保存恢复为准。

首轮原生运行发现 `CreatePrimitive` 动态创建的 BoxCollider／CapsuleCollider 被 IL2CPP 裁剪，已依照 [Unity 6000.3 的 CreatePrimitive 说明](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/GameObject.CreatePrimitive.html) 显式保留当前所用的网格与三种具体碰撞组件，并重新构建 APK；没有保留整个引擎程序集。

内测签名密钥和 DPAPI 加密口令保存在 `.local/development/credentials/android`，不进入 Git 或发行文件。后续覆盖安装沿用该密钥；构建过程清除 Unity 设置中的本机密钥路径和口令。APK 通过 `apksigner` 校验签名，构建信息不包含私人目录或口令。

```powershell
& tools/development/Build-Android.ps1
& tools/validation/Test-AndroidApk.ps1 -ApkPath .local/m6-release-1.4/Builds/Android/Catan.apk
```

## 验证与发布边界

本轮已执行的结果、APK 校验信息和微信实际状态以 [发布摘要](verification/android-port-v001/summary.json) 为准。`MobileVerification` 仅在显式命令行参数或 Android 本地 intent 验收请求下运行，截图和权威验收局只留本地，不将隐藏手牌、牌库顺序或随机状态加入公开证据。

Windows 手机布局预览不能替代 Android IL2CPP 运行或真实触控验收；模拟器检查不能替代目标手机的长期对局、功耗与性能。M7 联机与 M8 成品验收仍未完成，本轮不更新阶段状态。
