"""从加载页确认稿分离红漆主牌和备份木牌，保留原尺寸端头与反光。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parents[3]
source = Image.open(root / 'docs/原型/加载游戏-模式图标.png').convert('RGBA')
out = root / 'Unity/Catan/Assets/Resources/AcceptedMenu'


def make_plate(name, box, letters, corners, glow=False):
    face = source.crop(box)
    left, top, right, bottom = letters
    # 只在字所在高度填补底色，左右取样逐行过渡，不动两端与上下金棱。
    for y in range(top, bottom):
        l = face.getpixel((left-1, y))
        r = face.getpixel((right, y))
        for x in range(left, right):
            t = (x-left+1)/(right-left+1)
            face.putpixel((x,y), tuple(round(a+(b-a)*t) for a,b in zip(l,r)))
    mask = Image.new('L',face.size)
    ImageDraw.Draw(mask).polygon(corners,fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(.7))
    face.putalpha(mask)
    if glow:
        layer = Image.new('RGBA',(face.width+28,face.height+28))
        alpha = Image.new('L',layer.size)
        alpha.paste(mask,(14,14))
        for radius,color in [(7,(254,193,75)),(3,(255,231,145))]:
            halo = Image.new('RGBA',layer.size,color)
            halo.putalpha(alpha.filter(ImageFilter.GaussianBlur(radius)).point(lambda v:min(255,v*2)))
            layer.alpha_composite(halo)
        layer.alpha_composite(face,(14,14))
        face = layer
    face.save(out / f'setup-load-{name}.png')


make_plate('continue',(999,568,1435,674),(76,22,360,81),
    [(0,49),(5,34),(17,16),(28,14),(33,3),(42,1),(393,1),(403,4),(407,14),
     (418,17),(430,30),(435,48),(433,69),(425,83),(410,92),(402,104),(34,104),
     (25,94),(15,92),(4,78)],glow=True)
make_plate('older',(218,715,1445,769),(39,11,241,43),
    [(0,22),(4,11),(11,9),(17,2),(1209,2),(1215,9),(1222,11),(1226,20),
     (1226,35),(1221,43),(1216,45),(1211,52),(15,52),(9,45),(4,43),(0,34)])
