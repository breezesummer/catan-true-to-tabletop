# 操作区图标 v001

八种资源/商品图标由 `OperationIconAssets.DrawGood` 对已保留的 Unity 纹理进行 UV 取样，**没有生成或重写 PNG**。代码保留取样像素的宽高比。图标是平面 UI，不是新增 3D 网格。

顺序与 `EntityCardAssets.Good`、规则玩家视图一致：木、砖、羊、麦、矿、布、币、纸。素材路径、当前 SHA-256 与从左上角计量的归一化矩形见 `sources.json`；Unity 绘制时将纵坐标转换为左下角 UV。

- 布、币、纸：使用一级市场、市政厅、修道院的费用面。这里每张仅有一份商品符号，比城市设施升级整板上的同一符号更清楚。只采集符号，不采用照片文字或印刷数字决定费用。
- 木、砖、羊、麦、矿：当前来源清单仍缺少基础版建造费用卡，不能声称这些取自费用卡。取已有手牌正面的主体插画；木头沿用已有 AI 清理样板，其他四种沿用原照片几何校正资产。
- 拥有态：保持纹理色彩，黄铜边框。缺少态：降低亮度，珊瑚红边框和右下角减号。减号让状态不只依赖颜色。
- 原始照片、已有卡面与既有导出均保持不变。此版本仅新增可复现的 UV 选择及 UI 绘制代码。

使用：`OperationIconAssets.DrawGood(rect, goodIndex, missing)`。若自定义排版，可读取 `GoodTexture(goodIndex)` 与 `GoodUv(goodIndex)`；前者返回整张来源纹理，须配合后者绘制，不要当作已经裁切的独立图标。

棋子本身继续使用现有真实网格：`PlayerComponentAssets.BuildingResource(type, playerId)`（村庄/城市/城墙/大都会）、`PlayerComponentAssets.KnightResource(playerId, level, active)`、`EntityBoardAssets.ShipResource(playerId)`、`PhotoSampleAssets.Model("road")`。

本次进一步从这些网格对应的保留 `.blend` 源文件渲染 32 张透明缩略图：四个玩家颜色 × 道路/村庄/城市/船/城墙/一至三级骑士。它们是既有网格的固定相机渲染图，不是新增模型或 AI 绘制图片。棋子保持已有几何和材质；道路与现有游戏的道路着色方法一致，应用席位颜色。骑士采用待命/未戴头盔源模型，激活状态仍由规则 UI 说明，不由图像判断。

`OperationPieceAssets.Texture(icon, seat, level = 1)` 返回透明 `Texture2D`，可用 `GUI.DrawTexture(rect, texture, ScaleMode.ScaleToFit)` 绘制。`icon` 支持 road、ship、settlement/village、city、wall、knight；seat 为 P1..P4，分别映射红、蓝、橙、米色。未知类型（如尚无实体照片的发展卡）返回 null，供 UI 使用其已有后备显示。该实现只加载缓存纹理，不创建运行时摄像机、不占用任何 Layer、不调用 Editor API。

导出目录为 `art/exports/ui-operation-v001/pieces`，清单包含逐件 `.blend` 来源哈希、PNG 哈希和几何计数。`render_pieces.py` 固定 Blender 4.5.14 LTS、Cycles CPU 24 samples、256 × 256 RGBA、正交相机与三灯；按投影边界自适应取景，模型四周保留 15% 余量。以下命令从干净输出目录重建，然后安装至版本化 Unity Resources 路径；不要覆盖已有导出目录：

```powershell
$tools = Get-Content tools/development/toolchain.json -Raw | ConvertFrom-Json
& $tools.blenderExecutable --background --python-exit-code 1 --python art/recipes/ui-operation-v001/render_pieces.py -- --output .local/ui-operation-v001-pieces-new-rebuild
./art/recipes/ui-operation-v001/install_pieces.ps1 -ExportDirectory .local/ui-operation-v001-pieces-new-rebuild
```

Unity 副本位于 `Assets/Resources/OperationUI/v001/pieces`，固定 GUID、sRGB、透明度、256 上限、双线性采样、禁用 mipmap 与纹理压缩。安装前会核对每张 PNG 的 manifest 哈希。

本轮提交包含清单指向的 29 份唯一 `.blend` 源文件及 32 张导出，可直接重建本批缩略图。图标采样所需纹理也包含在生产 Unity 资源中。此前完整照片与美术生产工程不属于此次归档范围；`sources.json` 中的原照片路径与历史配方用于追溯，部分仍只保留在原工作区，不能据此宣称全部历史美术可以从本次提交重新生产。

检查记录：已逐张查看 8 张资源/商品来源纹理，核对可见符号与作物/商品索引；文件哈希实际计算。已检查各棋子类别与四个颜色的代表渲染，32 张 PNG 的 RGBA 格式/256 尺寸、Unity 副本及干净目录二次渲染哈希均已核对，见导出目录 `verification.json`。资源符号原始图片的反光、模糊和木头样板的 AI 改绘限制仍保留。Unity 编译与集成画面检查由本轮 UI 主任务统一执行。
