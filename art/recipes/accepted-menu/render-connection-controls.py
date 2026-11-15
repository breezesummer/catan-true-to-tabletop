"""从连接设置确认稿拆出无字红漆主牌，保留卷云、随形包边和透明暖光。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[3]
MENU = ROOT / 'Unity/Catan/Assets/Resources/AcceptedMenu'
source = Image.open(ROOT / 'artifacts/菜单按钮原型/连接设置-20261112/连接设置-原型.png').convert('RGBA')
face = source.crop((591,722,1121,820))

# 只抹去中央字区，逐行复用两侧红漆明暗；卷云及上下金棱保持原样。
for y in range(19,79):
    left, right = face.getpixel((137,y)), face.getpixel((398,y))
    for x in range(142,395):
        t = (x-137)/261
        face.putpixel((x,y),tuple(round(a+(b-a)*t) for a,b in zip(left,right)))

mask = Image.new('L',face.size)
ImageDraw.Draw(mask).polygon(
    [(43,1),(485,1),(495,3),(505,8),(509,19),(518,23),(525,33),(529,46),
     (528,63),(522,75),(512,80),(508,89),(495,96),(42,97),(30,94),(23,85),
     (21,81),(11,77),(4,68),(0,56),(2,42),(7,31),(16,23),(22,20),(25,12),(32,5)],fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(.6))
face.putalpha(mask)

pad = 14
canvas = Image.new('RGBA',(face.width+2*pad,face.height+2*pad))
footprint = Image.new('L',canvas.size); footprint.paste(mask,(pad,pad))
for radius,color,opacity in [(6,(255,188,54),.85),(2.8,(255,222,119),1)]:
    light = Image.new('RGBA',canvas.size,color)
    light.putalpha(footprint.filter(ImageFilter.GaussianBlur(radius)).point(
        lambda v: round(min(255,v*1.8)*opacity)))
    canvas.alpha_composite(light)
canvas.alpha_composite(face,(pad,pad))
canvas.save(MENU / 'setup-connection-save.png')
