"""按房间确认稿取无字名牌，保留随形金边；沿用开局底座生成有透明留边的暖光。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[3]
MENU = ROOT / 'Unity/Catan/Assets/Resources/AcceptedMenu'
REF = Image.open(ROOT / 'artifacts/菜单按钮原型/联机房间席位-20261112/在线游戏-房主视角.png').convert('RGBA')


def plaque(name, box, text, sample, outline):
    face = REF.crop(box)
    left, top, right, bottom = text
    # 复用同一行未印字的木纹/漆面，不动上下倒角和左右雕花端头。
    patch = face.crop((sample, top, sample+5, bottom))
    face.paste(patch.resize((right-left, bottom-top)), (left, top))
    mask = Image.new('L', face.size)
    ImageDraw.Draw(mask).polygon(outline, fill=255)
    face.putalpha(mask.filter(ImageFilter.GaussianBlur(.6)))
    face.save(MENU / f'setup-room-{name}.png')


plaque('name', (290,684,497,730), (34,9,172,36), 24,
       [(18,1),(188,1),(194,6),(201,8),(206,16),(206,31),(201,39),(193,40),(188,45),
        (18,45),(12,40),(5,38),(1,31),(1,15),(6,8),(12,7)])
plaque('host', (347,732,441,768), (20,6,76,29), 12,
       [(16,1),(77,1),(87,6),(93,15),(92,25),(84,33),(14,35),(4,29),(0,19),(4,8)])
plaque('ready', (615,731,774,769), (36,7,124,31), 24,
       [(19,1),(139,1),(151,6),(158,16),(157,26),(148,35),(18,37),(7,32),(1,24),(1,14),(7,6)])
plaque('plain', (909,731,1072,769), (48,6,118,31), 30,
       [(20,1),(141,1),(153,7),(162,17),(160,27),(151,35),(19,37),(8,32),(1,24),(1,14),(7,6)])

# 透明光晕贴图只加暖光，不重复铺原底座，避免轮廓错位和宣纸底色硬边。
seat = Image.open(MENU / 'setup-seat-active.png').convert('RGBA')
seat = seat.resize((112,36))
mask = Image.new('L',(152,76)); mask.paste(seat.getchannel('A'),(20,20))
glow = Image.new('RGBA', mask.size)
for radius, color, opacity in [(7,(255,195,65),.8),(3,(255,220,104),1),(1,(255,245,173),1)]:
    layer = Image.new('RGBA',mask.size,color)
    layer.putalpha(mask.filter(ImageFilter.GaussianBlur(radius)).point(lambda v:round(min(255,v*2.1)*opacity)))
    glow.alpha_composite(layer)
glow.save(MENU / 'setup-room-seat-glow.png')
