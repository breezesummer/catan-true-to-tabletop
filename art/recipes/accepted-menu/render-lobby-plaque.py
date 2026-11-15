"""从局域网原型提取无字入座牌，保留端头、倒角与下沿铜色接触影。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parents[3]
original = Image.open(root / 'docs/原型/局域网联机-大厅初稿.png')
face = original.crop((1256,352,1443,416)).convert('RGBA')
for y in range(12,52):
    left,right = face.getpixel((51,y)),face.getpixel((145,y))
    for x in range(52,145):
        t = (x-51)/94
        face.putpixel((x,y),tuple(round(a+(b-a)*t) for a,b in zip(left,right)))
# 端头在肩部向内收口，外轮廓与金边同形。
edge = [(25,1),(162,1),(168,3),(172,10),(178,13),(183,19),(186,27),
        (186,38),(182,47),(176,52),(171,54),(169,60),(163,63),(24,63),
        (18,61),(15,55),(9,52),(4,47),(1,40),(1,24),(5,17),(10,12),
        (15,10),(19,4)]
mask = Image.new('L',face.size)
ImageDraw.Draw(mask).polygon(edge,fill=255)
face.putalpha(mask.filter(ImageFilter.GaussianBlur(.8)))
face.save(root/'Unity/Catan/Assets/Resources/AcceptedMenu/setup-lobby-jade.png')
