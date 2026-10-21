# U2 草原静态三维样板

2026年10月21日，内测版1.7.3。采用已确认的 v006 草原概念方向，交付第一轮可玩工程样板；模型细节、玉牌、三分之一分区及配色仍待用户确认。其他地形、海岸、船、玩家建筑与牌面样板继续按 U2 方案推进。

## 重建

在仓库根目录运行：

```powershell
& tools/art/Build-UltraPasture.ps1 -Install
& tools/art/Build-UltraPasture.ps1 -OutputRoot .local/ultra/u2-rebuild
# 使用本机 Python 3 执行独立几何校验：
python art/recipes/ultra/v002/pasture/verify.py --compare .local/ultra/u2-rebuild
```

Blender 版本由 `tools/development/toolchain.json` 固定为 4.5.14。生成器不使用外部插件、生成图片或网络服务；种子仅为公开的视觉配方常数，不接触地图或权威随机源。

| 输出 | 用途 |
| --- | --- |
| `art/source/ultra/v002/pasture/pasture.blend` | 米制、底面中心枢轴，打开默认显示 LOD0 原始态；其他互斥模块在 Outliner 中保留 |
| `art/exports/ultra/v002/pasture/pasture.fbx` | 显式 FBX 网格交换，包含全部模块与两级 LOD；不是单一完整地块预制件 |
| `art/exports/ultra/v002/pasture/pasture-mesh.json` | 与 Blender/FBX 同源的顶点、颜色及三角形；Unity 明确转换 Y 向上、绕序与材质，避免导入器隐式轴处理 |
| `Unity/Catan/Assets/Resources/Ultra/v002/` | 安装后的运行网格数据及 URP 顶点色材质 |
| `art/exports/ultra/v002/pasture/manifest.json` | 尺寸、LOD 三角形、版本、来源与网格 SHA-256 |
| `art/exports/ultra/v002/pasture/verification.json` | 独立几何校验及干净重建结果 |
| `art/exports/ultra/v002/pasture/blender-verification.json` | 源文件及FBX回读的38个网格、35,572三角形、顶点颜色及材质槽核对 |

## 结构与尺寸

地块半径39.5毫米；规则网格半径40毫米。地块底面为0，外缘顶面8毫米，与旧主题棋子摆放面相同。这是沿用工程估算，未声称实体实测。草坡连续覆盖中央，不预留点数空地；原始态有三只静态羊，无石头、围栏及生产设施。

每个 LOD 包含底层地形、六个60度自然模块、六个120度牧场模块、六个城市附加模块。相邻建筑所在角决定覆盖的两个自然模块。合法建筑间距保证不重叠；同一玩家的多座建筑也分别占区。城市只增加原区域内的物件，移除、降级及归属改变直接按当前公开投影还原。棚布与旗帜采用所属玩家色，地表不染玩家色。自然／开发模块不在道路中性边缘放置景观；最小规则边净空6.143毫米，最高景观16.755毫米。

每地块原始态 LOD0 为11,032三角形、LOD1为2,039三角形。以地块统一包围尺度计算屏幕占比，0.19以下使用低LOD；小物体关闭实时投影，保留主灯与环境光接收。当前为直接 LOD 切换，过渡观感仍待调校。使用顶点色和统一程序材质，没有贴图依赖，故本样板不需要纹理 UV。

开发区域面积是内部六角形投影面积的三分之一，不要求围栏本身占满该扇区。设施位于区域内部，以草地柔化边界。精确模块顶点不会跨入另一分区。网格和材质是有限共享缓存；组件不创建碰撞体，不参与规则校验。

## 验证入口

`tools/validation/Test-UltraPasture.ps1` 在实际 Windows 构建中验证布局、恢复、拾取和离屏负载，输出目录必须全新。四视图及结果见 [U2 记录](../../../../../docs/verification/ultra/U2-pasture.md)。离屏测量不等于显示器实际帧率或 Android 性能。原始 v006 图片不被修改，当前模型也不表示概念图质感已经完全还原。
