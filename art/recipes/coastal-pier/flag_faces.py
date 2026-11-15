from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageFilter
import math
import random

ROOT = Path(__file__).resolve().parents[3]
DEST = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
sheet = Image.open(ROOT / "Unity/Catan/Assets/Art/ResourceRedraw/v001/approved-sheet.png").convert("RGB")
# 在两倍放大的卡角坐标上描轮廓，蒙版以原图采样。
icons = {
    "wood": (30, [(15,66),(22,61),(22,55),(87,18),(96,17),(103,23),(115,23),(122,31),(121,39),(131,48),(132,57),(72,101),(63,103),(55,98),(47,101),(39,96),(35,88),(30,85),(23,84),(15,78)]),
    "brick": (380, [(20,55),(37,51),(41,40),(79,20),(87,23),(100,23),(105,30),(124,30),(139,38),(142,51),(137,57),(124,59),(132,68),(132,74),(104,92),(91,79),(66,90),(54,84),(49,74),(32,78),(20,66)]),
    "wool": (730, [(26,76),(26,55),(29,44),(36,37),(47,34),(70,36),(88,34),(102,24),(114,23),(123,28),(127,36),(135,41),(134,46),(125,49),(122,60),(119,75),(112,83),(108,89),(108,103),(100,104),(97,90),(92,89),(91,104),(82,104),(81,86),(59,85),(53,89),(56,99),(52,104),(45,103),(39,94),(37,87),(33,93),(35,101),(28,102),(25,97),(25,88),(29,83)]),
    "wheat": (1080, [(25,36),(28,27),(41,23),(54,19),(75,19),(88,23),(96,33),(95,41),(82,47),(72,57),(67,69),(69,79),(84,101),(74,107),(61,110),(44,109),(30,104),(33,95),(45,77),(48,66),(40,52),(29,44)]),
    "ore": (1425, [(24,78),(28,59),(39,43),(61,31),(90,21),(102,25),(116,27),(125,34),(125,48),(130,63),(127,70),(91,86),(70,90),(45,87),(28,85)])
}
for kind in [*icons, "generic"]:
    n = 384
    cloth = Image.new("RGB",(n,n))
    draw = ImageDraw.Draw(cloth)
    cloth.paste((228,209,175),(0,0,n,n))
    # 折边以一明一暗两排短针脚压住，下方再让经纬线穿过印花。
    for t in range(7,n-8,7):
        for edge in (5,n-6):
            draw.line((t,edge,t+3,edge),fill=(186,161,125))
            draw.line((edge,t,edge,t+3),fill=(186,161,125))
    if kind=="generic":
        font=ImageFont.truetype("C:/Windows/Fonts/msyhbd.ttc",136)
        ink=Image.new("L",(500,200))
        ImageDraw.Draw(ink).text((0,0),"3：1",font=font,fill=255)
        ink=ink.crop(ink.getbbox())
        cloth.paste((77,48,29),((n-ink.width)//2,(n-ink.height)//2),ink)
    else:
        x, outline=icons[kind]
        glyph=sheet.crop((x,170,x+75,245)).resize((150,150),Image.Resampling.LANCZOS)
        mask=Image.new("L",glyph.size)
        ImageDraw.Draw(mask).polygon(outline,fill=255)
        glyph.putalpha(mask.filter(ImageFilter.GaussianBlur(.65)))
        glyph=glyph.crop(glyph.getbbox())
        scale=min(270/glyph.width,274/glyph.height)
        glyph=glyph.resize((round(glyph.width*scale),round(glyph.height*scale)),Image.Resampling.LANCZOS)
        cloth.paste(glyph,((n-glyph.width)//2,(n-glyph.height)//2),glyph)
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
    cloth.save(DEST/f"flag-{kind}.png")
