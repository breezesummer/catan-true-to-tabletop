"""从声音页原型拆取无字名牌、铜轨与圆钮；伸展时固定两端及金属棱线。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[3]
MENU = ROOT / 'Unity/Catan/Assets/Resources/AcceptedMenu'
REF = Image.open(ROOT / 'docs/原型/游戏设置-声音.png').convert('RGBA')


def plaque(name, bounds, span):
    face = REF.crop(bounds)
    left, right = span
    # 逐行延续两侧漆面的明暗；不触碰上沿高光、下沿铜棱或端头。
    for y in range(10, 44):
        a, b = face.getpixel((left-5, y)), face.getpixel((right+5, y))
        for x in range(left, right):
            t = (x-left+5)/(right-left+10)
            face.putpixel((x, y), tuple(round(p+(q-p)*t) for p, q in zip(a, b)))
    w, h = face.size
    mask = Image.new('L', face.size)
    points = [(27,1),(w-28,1),(w-17,4),(w-12,12),(w-4,18),(w-1,27),
              (w-2,39),(w-10,45),(w-13,52),(w-22,h-3),(23,h-3),
              (15,h-6),(11,47),(5,43),(1,36),(1,24),(6,17),(13,13),(16,6)]
    ImageDraw.Draw(mask).polygon(points, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(.55))
    face.putalpha(mask)
    pad = 6
    canvas = Image.new('RGBA',(w+pad*2,h+pad*2))
    shadow = Image.new('L',canvas.size)
    shadow.paste(mask,(pad,pad+2))
    layer = Image.new('RGBA',canvas.size,(75,42,16))
    layer.putalpha(shadow.filter(ImageFilter.GaussianBlur(2)).point(lambda x:round(x*.32)))
    canvas.alpha_composite(layer)
    canvas.alpha_composite(face,(pad,pad))
    canvas.save(MENU / f'setup-settings-{name}.png')


plaque('wood',(248,215,470,276),(65,160))
plaque('jade',(489,215,713,276),(68,161))

# 母轨保留原型两端圆头；只用未被滑钮覆盖的木色槽底补齐内槽。
rail = REF.crop((519,400,1281,424))
for y in range(rail.height):
    color = rail.getpixel((713,y))
    for x in range(10,rail.width-10):
        rail.putpixel((x,y),color)
rail.paste(ImageOps.mirror(rail.crop((rail.width-13,0,rail.width,rail.height))),(0,0))
mask = Image.new('L',rail.size)
ImageDraw.Draw(mask).rounded_rectangle((0,1,rail.width-1,21),radius=11,fill=255)
rail.putalpha(mask.filter(ImageFilter.GaussianBlur(.5)))
rail.save(MENU / 'setup-settings-rail.png')

fill = REF.crop((555,405,619,417))
mask = Image.new('L',fill.size)
ImageDraw.Draw(mask).rounded_rectangle((0,0,63,11),radius=5,fill=255)
fill.putalpha(mask.filter(ImageFilter.GaussianBlur(.35)))
fill.save(MENU / 'setup-settings-fill.png')

knob = REF.crop((1102,390,1145,434))
mask = Image.new('L',knob.size)
ImageDraw.Draw(mask).ellipse((2,2,40,40),fill=255)
mask = mask.filter(ImageFilter.GaussianBlur(.5))
knob.putalpha(mask)
knob.save(MENU / 'setup-settings-knob.png')

# 主牌沿用原型的卷云和闭合轮廓。只替换中部文字，不重画雕花。
face = REF.crop((585,763,1088,850))
for y in range(15,70):
    a,b = face.getpixel((175,y)),face.getpixel((325,y))
    for x in range(180,325):
        t=(x-175)/150
        face.putpixel((x,y),tuple(round(p+(q-p)*t) for p,q in zip(a,b)))
mask = Image.new('L',face.size)
ImageDraw.Draw(mask).polygon([(31,1),(469,1),(481,5),(487,15),(496,20),(502,29),
    (502,58),(497,68),(488,72),(483,81),(471,86),(31,86),(20,82),
    (15,74),(6,68),(1,57),(1,29),(6,20),(15,15),(21,5)],fill=255)
face.putalpha(mask.filter(ImageFilter.GaussianBlur(.55)))
pad=10
canvas=Image.new('RGBA',(face.width+2*pad,face.height+2*pad))
shadow=Image.new('L',canvas.size);shadow.paste(face.getchannel('A'),(pad,pad+2))
layer=Image.new('RGBA',canvas.size,(93,48,13));layer.putalpha(shadow.filter(ImageFilter.GaussianBlur(2)).point(lambda x:round(x*.35)))
canvas.alpha_composite(layer);canvas.alpha_composite(face,(pad,pad))
canvas.save(MENU / 'setup-settings-done.png')
