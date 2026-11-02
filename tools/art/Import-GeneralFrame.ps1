param([string]$SkinRoot='E:/无名杀2.0/无名杀-双样式懒人包/resources/app/extension/千幻聆音/sanguoskin')
$ErrorActionPreference='Stop'
$repo=Split-Path (Split-Path $PSScriptRoot)
$dest=Join-Path $repo 'Unity/Catan/Assets/Resources/GeneralUI/v001'
New-Item -ItemType Directory -Force -Path "$dest/Portraits" | Out-Null
Copy-Item "$repo/art/generated/general-frame/v001/general-ui-atlas.png" "$dest/frame-atlas.png"
Copy-Item "$repo/art/generated/general-frame/v001/skill-buttons.png" "$dest/skill-buttons.png"
Copy-Item 'E:/无名杀2.0/无名杀-双样式懒人包/resources/app/extension/十周年UI/ui/assets/fonts/BKJT.ttf' "$dest/SkillCalligraphy.ttf"
$source=Join-Path $SkinRoot 'kanze/循循善诱.jpg'
$bytes=[IO.File]::ReadAllBytes($source)
if($bytes[0]-ne 137 -or $bytes[1]-ne 80){throw '预览皮肤应为带透明通道的PNG源文件。'}
Copy-Item -LiteralPath $source -Destination "$dest/Portraits/tk-g26.png"
[PSCustomObject]@{武将='tk-g26';姓名='阚泽';皮肤='循循善诱';来源=$source;文件='Portraits/tk-g26.png';SHA256=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash} |
    ConvertTo-Json | Set-Content "$repo/art/generated/general-frame/v001/皮肤来源.json" -Encoding utf8
Write-Output '已导入阚泽一张预览皮肤。'
