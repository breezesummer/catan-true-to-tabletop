# 菜单地图与扩展预览 v001

30 张 1024 × 640 静态 PNG：基础版与城市骑士各 3/4 人，航海家九剧本各 3/4 人，双扩展两个受支持剧本各 3/4 人，共 26 张地图；另有 4 张玩法封面。

图像复用项目保留的实体地形纹理与 v002 棋子透明渲染图，通过 Pillow 排版为二维桌游示意，不作为三维网格交付。原纹理不修改，所有输入 SHA-256 与生成工具版本存入导出清单。

`ExportPreviews.cs` 只读取公开剧本定义和既有基础地图夹具，显式只导出绘图需要的字段。不会创建对局、读取权威存档、读取真实随机种子或隐藏资源池。迷雾地图保留 12 个未知格，不画资源或点数。基础岛屿采用项目自编固定夹具，属于随机开局示意，不能解释为官方新手固定摆法；新世界和双扩展同样明确标示为范例。地图不画港口类别，实际港口及随机地形以开局为准。

来源：`src/Catan.Core/M3Scenarios.cs` 的公开地形定义，对应 `catan-seafarers-2025-en.pdf` 第 4–20 页（每张图的具体页码见清单）；组合支持范围对应 `catan-cities-knights-2025-en.pdf` 第 12 页。基础拓扑来自 `Unity/Catan/Assets/Resources/m1-scenario.json`，资源组件数量对应 `catan-base-2025-en.pdf` 第 3 页。地图计数另与本轮之前的独立记录 `docs/acceptance/m3-v001/scenario-source-counts.json` 核对。

从项目根目录重新生成：

```powershell
& D:/Development/dotnet/dotnet.exe run --project art/recipes/menu-previews-v001/ExportPreviews.csproj -- .
python art/recipes/menu-previews-v001/render_previews.py --install
python art/recipes/menu-previews-v001/verify_previews.py
```

Python 需 Pillow；此次使用 Codex 提供的 Python 与 Pillow。默认输出 `art/exports/menu-previews-v001`，指定 `--output` 可在干净目录重建。`--install` 将相同字节复制到 `Unity/Catan/Assets/Resources/MenuPreviews/v001`。

运行接口为 `MenuMapPreviews.RuleTexture(int)`、`ScenarioTexture(int,string,int)`、`DrawRule(Rect,int)`、`DrawScenario(Rect,int,string,int)`；小卡片可用 `DrawScenarioThumbnail(Rect,int,string,int)`，按比例裁取完整棋盘，避免图片内标题占用缩略空间。规则索引依次为基础版、航海家、城市与骑士、双扩展。`ScenarioCaption` 返回可在图片外保持清晰阅读的中文示意说明。纹理按需缓存，Unity 导入不保留 CPU 副本，不生成 mipmap。

总览见导出目录 `contact-sheet.jpg`，离线验收结果见 `verification.json`。离线验收检查覆盖集合、独立资源计数、迷雾字段、图片非空与规格、导出/Unity 字节一致和来源校验；实际菜单版式及构建加载由主任务在 Unity 验证。
