"""按公开布局拼排地图，从确认稿提取无字名牌，生成选图页专用素材。"""
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[3]
MENU = ROOT / 'Unity/Catan/Assets/Resources/AcceptedMenu'
REF = Image.open(ROOT / 'docs/原型/选择地图.png').convert('RGBA')


def plaque(name, bounds, blank, text_span):
    face = REF.crop(bounds)
    left, right = text_span
    patch = face.crop((blank, 0, blank + 10, face.height))
    face.paste(patch.resize((right-left, face.height)), (left, 0))
    mask = Image.new('L', face.size)
    ImageDraw.Draw(mask).rounded_rectangle((0, 0, face.width-1, face.height-1),
                                           radius=face.height//2-3, fill=255)
    face.putalpha(mask.filter(ImageFilter.GaussianBlur(.6)))
    if name == 'jade':
        # 以原漆面为模，镶边只往里走两像素；上下倒角分别接光和落影。
        enamel = Image.new('L', face.size)
        enamel.putdata([255 if g > r and g > b and a > 240 else 0
                        for r, g, b, a in face.get_flattened_data()])
        enamel = enamel.filter(ImageFilter.MedianFilter(3)).filter(ImageFilter.GaussianBlur(.55))
        inner = enamel.filter(ImageFilter.MinFilter(5))
        edge = ImageChops.subtract(enamel, inner)
        bronze = Image.new('RGB', face.size)
        brush = ImageDraw.Draw(bronze)
        for y in range(face.height):
            t = y / (face.height - 1)
            brush.line((0,y,face.width,y), fill=tuple(round(a+(b-a)*t)
                       for a,b in zip((229,177,83),(150,92,38))))
        face.paste(bronze,(0,0),edge)
        crest = ImageChops.subtract(enamel,ImageChops.offset(enamel,1,1))
        face.paste((255,231,164),(0,0,face.width,face.height),crest)
        seam = ImageChops.subtract(inner,inner.filter(ImageFilter.MinFilter(3)))
        face.paste((79,55,28),(0,0,face.width,face.height),seam)
        # 给羊皮纸上的暖光预留透明边距。近沿亮、远处柔，最后覆盖原有漆面。
        pad = 16
        canvas = Image.new('RGBA', (face.width+2*pad,face.height+2*pad))
        footprint = Image.new('L', canvas.size)
        footprint.paste(face.getchannel('A'),(pad,pad))
        for radius, color, opacity in [(6.5,(255,207,99),.95),
                                       (3.2,(255,229,150),1.0),
                                       (1.3,(255,246,199),1.0)]:
            light = Image.new('RGBA',canvas.size,color)
            light.putalpha(footprint.filter(ImageFilter.GaussianBlur(radius)).point(
                lambda value: round(min(255,value*1.8)*opacity)))
            canvas.alpha_composite(light)
        canvas.alpha_composite(face,(pad,pad))
        face = canvas
    face.save(MENU / f'setup-atlas-{name}.png')


plaque('wood', (245, 433, 505, 477), 36, (62, 198))
plaque('jade', (243, 747, 517, 798), 39, (62, 204))

# 铜轨与滑块的圆头保留原尺寸，中段复用未被滑块遮挡的一段。
rail = REF.crop((1482,196,1499,776))
rail.paste(rail.crop((0,160,17,241)),(0,48))
rail.save(MENU/'setup-atlas-rail.png')
thumb = REF.crop((1483,248,1498,318))
thumb.save(MENU/'setup-atlas-thumb.png')

# 坐标取自现有开局地图的单块顶面。各人数布局只读取公开地形。
board = Image.open(MENU / 'map-驶向新海岸.png').convert('RGBA')
mist = Image.open(MENU / 'map-迷雾岛屿.png').convert('RGBA')
centres = dict(ore=(202,99), wool=(332,99), sea=(720,99), gold=(590,99),
               brick=(655,171), wood=(331,244), wheat=(397,318),
               desert=(331,391), fog=(204,99))
outline = [(64,0),(127,26),(127,71),(64,97),(0,71),(0,26)]
mask = Image.new('L',(128,98))
ImageDraw.Draw(mask).polygon(outline,fill=255)
faces = {}
for kind,(x,y) in centres.items():
    face = (mist if kind=='fog' else board).crop((x-64,y-49,x+64,y+49))
    face.putalpha(mask)
    faces[kind] = face

layouts = json.loads((ROOT/'art/recipes/menu-previews-v001/public-layouts.json').read_text('utf-8'))
for layout in layouts['maps']:
    if layout['rule'] != 1:
        continue
    tiles = sorted(((64*(2*t['q']+t['r']),73*t['r'],t['resource'])
                    for t in layout['tiles']), key=lambda item: (item[1],item[0]))
    xs,ys,_ = zip(*tiles)
    sheet = Image.new('RGBA',(max(xs)-min(xs)+136,max(ys)-min(ys)+114))
    draw = ImageDraw.Draw(sheet)
    for x,y,kind in tiles:
        x,y = x-min(xs)+4,y-min(ys)+2
        draw.polygon([(x+dx,y+dy+6) for dx,dy in outline],fill='#875326')
        sheet.alpha_composite(faces[kind],(x,y))
        draw.line([(x+dx,y+dy) for dx,dy in outline + outline[:1]],fill='#cfa15c',width=2)
    shadow = Image.new('RGBA',sheet.size,'#513318')
    shadow.putalpha(sheet.getchannel('A').filter(ImageFilter.GaussianBlur(2)).point(lambda a:a//2))
    final = Image.new('RGBA',sheet.size)
    final.alpha_composite(shadow,(0,3))
    final.alpha_composite(sheet)
    final.thumbnail((780,480),Image.Resampling.LANCZOS)
    final.save(MENU / f"atlas-{layout['id']}-{layout['players']}.png")
