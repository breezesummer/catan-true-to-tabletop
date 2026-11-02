param(
    [string]$SourceRoot='E:/无名杀2.0/无名杀-双样式懒人包/resources/app',
    [string]$PythonExecutable='D:/Development/Blender/mcp-official-env/Scripts/python.exe'
)
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
$dest=Join-Path $repo 'Unity/Catan/Assets/Resources/GeneralUI/v002'
$ui=Join-Path $SourceRoot 'extension/十周年UI'
New-Item -ItemType Directory -Force $dest | Out-Null
foreach($camp in 'wei','shu','wu','qun','jin','shen'){
    Copy-Item -LiteralPath "$ui/image/styles/shousha/name2_$camp.png" -Destination "$dest/frame-$camp.png"
}
foreach($i in 0..2){Copy-Item -LiteralPath "$ui/ui/assets/skill/shousha/btn$i.png" -Destination "$dest/skill-$i.png"}
Copy-Item -LiteralPath "$ui/image/ui/effects/sprites_shadow_green1.png" -Destination "$dest/turn-glow.png"
Copy-Item -LiteralPath "$SourceRoot/extension/如真重置版/resource/fonts/shousha.ttf" -Destination "$dest/NameCalligraphy.ttf"
# Unity 不读取 WOFF2；使用 fontTools 解封装为 TTF，保留字形。
$env:CATAN_SHOUSHA_FONT_SOURCE="$ui/ui/assets/fonts/HYZLSJ.woff2"
$env:CATAN_SHOUSHA_FONT_TARGET="$dest/SkillCalligraphy.ttf"
$env:CATAN_SHOUSHA_ASSETS=$dest
uv run --python $PythonExecutable --with fonttools --with brotli --with pillow python -c 'import os; from pathlib import Path; from PIL import Image; from fontTools.ttLib import TTFont; f=TTFont(os.environ["CATAN_SHOUSHA_FONT_SOURCE"]); f.flavor=None; f.save(os.environ["CATAN_SHOUSHA_FONT_TARGET"]); [Image.open(p).save(p,format="PNG") for p in Path(os.environ["CATAN_SHOUSHA_ASSETS"]).glob("skill-*.png")]'
if($LASTEXITCODE -ne 0){throw '手杀技能字体转换失败。'}
Write-Output '手杀势力框、三态按钮与字体已导入。'
