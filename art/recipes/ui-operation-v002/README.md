# 棋子配色与局内字体 v002

棋子统一使用现有村庄的塑料颜色。红、蓝、橙、米白分别采用保留的村庄材质；运行时让道路、船、城市、城墙、大都会及骑士塑料部分共用对应材质，头盔、窗洞等细节保留独立材质。不会覆盖原始照片、模型或 v001 缩略图。

`render_pieces.py` 从原有 `.blend` 网格重新渲染四套共32张透明缩略图。线性色为红 `(0.62,0.018,0.025)`、蓝 `(0.008,0.024,0.12)`、橙 `(0.95,0.23,0.018)`、米白 `(0.82,0.70,0.43)`，与村庄来源材质一致。使用固定 Blender 4.5.14、相机、灯光和24次采样；输出与源网格哈希见 `art/exports/ui-operation-v002/pieces/manifest.json`。

复现时从仓库根目录执行，默认输出目录必须为空：

```powershell
& 'D:\Development\Blender\blender-4.5.14-windows-x64\blender.exe' --background --python-exit-code 1 --python art/recipes/ui-operation-v002/render_pieces.py
```

局内统一使用 `Noto Serif CJK SC Regular`，采用适合木桌、历史题材界面的宋体笔形；字体随 PC 与 Android 包携带。字体来自 [Noto CJK 官方仓库](https://github.com/notofonts/noto-cjk)，固定提交、来源与 SHA-256 见 `font-source.json`，SIL OFL 1.1 原始许可随字体保存在 `Unity/Catan/Assets/Resources/Fonts/NotoSerifCJKsc-OFL.txt`。界面程序不依赖系统安装字体；菜单仍保留原有字体。
