from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import math
import random

ROOT = Path(__file__).resolve().parents[3]
DEST = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
source = Path(__file__).with_name("flag-art")
n = 384
cloth = Image.new("RGB",(n,n))
draw = ImageDraw.Draw(cloth)
cloth.paste((228,209,175),(0,0,n,n))
# 折边以一明一暗两排短针脚压住，下方再让经纬线穿过印花。
for t in range(7,n-8,7):
    for edge in (5,n-6):
        draw.line((t,edge,t+3,edge),fill=(186,161,125))
        draw.line((edge,t,edge,t+3),fill=(186,161,125))
rng=random.Random(21016)
warp=[rng.uniform(-3,3) for _ in range(n)]
weft=[rng.uniform(-3,3) for _ in range(n)]
pix=cloth.load()
for y in range(n):
    for x in range(n):
        # 每个交点轮换浮经、浮纬，并给纱线粗细、走向和染色留下小幅不匀。
        u=x/4.2+.135*math.sin(y*.067)+.06*math.sin(y*.17)
        v=y/4.8+.145*math.sin(x*.08)
        cross=1 if (int(u)+int(v))%2 else -1
        yarn=(3.5+1.8*cross)*math.cos(math.tau*u)+(3.5-1.8*cross)*math.cos(math.tau*v)
        slub=3*math.sin(y*.12+math.sin(x*.032))+warp[x]+weft[y]+rng.uniform(-2,2)
        edge=min(x,n-1-x,y,n-1-y)
        hem= .94+.06*min(1,edge/9)
        factor=hem*(1+(yarn+slub)/240)
        pix[x,y]=tuple(min(255,round(c*factor)) for c in pix[x,y])
cloth.save(DEST/"flag-linen.png")

# 通用港字模与资源印花分开导出。
stamp=Image.new("RGBA",(n,n))
# Palatino 的楔形衬线沿用旧式印字的笔形，冒号采用同字体的紧排字距。
font=ImageFont.truetype("C:/Windows/Fonts/palab.ttf",440)
ink=Image.new("L",(1000,600))
ImageDraw.Draw(ink).text((12,10),"3:1",font=font,fill=255)
ink=ink.crop(ink.getbbox())
ink.thumbnail((252,122),Image.Resampling.LANCZOS)
letters=Image.new("RGBA",ink.size,(103,66,41,255))
letters.putalpha(ink)
stamp.alpha_composite(letters,((n-ink.width)//2,(n-ink.height)//2))
stamp.save(DEST/"flag-generic.png")

for kind in ("wood","brick","wool","wheat","ore"):
    with Image.open(source/f"{kind}.png") as original:
        alpha=original.getchannel("A")
        # 去掉透明留白；忽略近乎透明的游离像素，保留边缘抗锯齿。
        bounds=alpha.point(lambda a: 255 if a>8 else 0).getbbox()
        glyph=original.crop(bounds)
    glyph.thumbnail((720,730),Image.Resampling.LANCZOS)
    stamp=Image.new("RGBA",(1024,1024))
    stamp.alpha_composite(glyph,((1024-glyph.width)//2,(1024-glyph.height)//2))
    stamp.save(DEST/f"flag-{kind}.png")
