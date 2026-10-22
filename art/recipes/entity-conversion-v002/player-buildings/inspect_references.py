"""Create read-only scaled inspection sheets, keeping original source bytes."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps

REPO = Path(__file__).resolve().parents[4]
DIRECTORY = Path(__file__).resolve().parent
KINDS = ['村庄', '城市', '大都会', '城墙', '城市+城墙', '大都会+城墙']
font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 26)
for colour in ['橙色', '米色', '蓝色']:
    sheet = Image.new('RGB', (1800, 1600), 'white')
    draw = ImageDraw.Draw(sheet)
    for index, kind in enumerate(KINDS):
        source = REPO / 'assets/references/inbox/建筑' / (colour + kind + '.jpg')
        picture = ImageOps.exif_transpose(Image.open(source))
        picture.thumbnail((600, 750))
        x, y = index % 3 * 600, index // 3 * 800
        sheet.paste(picture, (x+(600-picture.width)//2, y+40+(750-picture.height)//2))
        draw.text((x+15, y+8), source.name, fill='black', font=font)
    sheet.save(DIRECTORY / (colour+'-source-inspection.png'))
