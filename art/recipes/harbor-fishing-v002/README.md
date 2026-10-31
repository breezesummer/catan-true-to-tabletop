# 商人扩展实体原照 v002

2026-10-31。按用户提供的 `assets/references/inbox/商人扩展/` 原照转换六块V形渔港、湖泊、臭靴和一／二／三鱼筹码，共11件。原照内容不变，仅按用户要求重命名；原文件名、新文件名、SHA256与校正角点见[参考清单](参考清单.json)。

原照仅做几何投影，保留插画、数字、颗粒与反光，不生成或改写印刷内容。六渔港采用左右两片四边形投影，保留凹口；湖泊采用六片三角形投影；方片采用四角透视校正。背面和实际毫米尺寸未提供，背面沿用v001公开入口；模型厚边不宣称还原未知的隐藏面。

通过Blender MCP生成单网格、两材质的米制厚板，底面中心为枢轴。渔港估算42×30.31×1.5毫米，筹码24×24×1.5毫米；湖泊68.416×79×8毫米，与现有尖顶六边形地块拼接。11件共420三角面。Unity只在桌面生成六渔港与湖泊；筹码与臭靴在手牌、头像等原有入口显示同源图标。

制作文件：

- `prepare.py`：Python、Pillow、NumPy校正原照，校验原件SHA256。
- `build.py`：必须通过Blender MCP执行；新增独立场景，不保存或覆盖传入的旧场景。
- `verify.py`：通过Blender MCP回读FBX，检查尺寸、底面、网格、材质和UV，并与干净目录重建比较。
- [Blender验证](Blender验证.json)：最终11件回读及重建结果。
- [运行资产清单](../../../Unity/Catan/Assets/Resources/HarborFishing/v002/资产清单.json)：原照、导出文件、几何、UV、贴图和图标校验值。
- [Unity与游戏截图记录](../../../docs/verification/fishing-photo-v002/验收记录.md)：正式入口和四模式检查。

本地原件、源文件与预览遵循[仓库存储约定](../../../docs/REPOSITORY_STORAGE.md)。原照校正输出在 `art/source/harbor-fishing-v002/textures/`，Blender源在同级 `捕鱼实体原照.blend`，每件顶视、侧视和默认相机预览在 `previews/`。实际相邻组件、拥挤节点和手牌检查使用Unity游戏截图，不用Blender预览替代实机证据。

复现顺序：

1. 运行 `prepare.py`；检查原图校验值后生成校正贴图。
2. 在Blender MCP的后台执行接口选择已有 `.blend` 作为启动文件，把 `__file__` 设为本目录 `build.py` 路径后执行脚本。脚本新建场景，默认输出到Unity的 `HarborFishing/v002/`；源文件独立保存。
3. 在空目录重新运行 `prepare.py`，再通过Blender MCP执行 `build.py`，设置 `OUTPUT_ROOT`、`SOURCE_ROOT` 和 `TEXTURE_ROOT` 分别指向干净运行目录、源目录和校正贴图目录。本轮干净重建位于 `.local/fishing-photo-rebuild/`。
4. 通过Blender MCP执行 `verify.py`。几何、UV和贴图按校验值比较；图标比较解码像素，因为Blender PNG含日期、路径与耗时元数据。最终图标像素完全相同。
5. Unity执行菜单 `Catan/美术/验收捕鱼实体原照`，或在启动参数中指定 `-executeMethod FishingPhotoEditorVerification.Run --fishing-photo-verify`。检查临时本地对局，不访问服务器、不写入用户存档；命令行模式完成后退出编辑器。

仅替换美术资源与装饰摆放；规则身份、渔港关联顶点、湖泊号码、随机状态、隐藏信息和网络格式不变。原照资产与图标继续缓存，静止帧不建模；重排时复用渔港并刷新朝向。游戏尺寸与外观仍等待用户实测及截图确认，不推进F6、M8的产品验收状态。
