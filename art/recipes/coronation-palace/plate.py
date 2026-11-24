"""将四台相机的原始画面排成一张对照图。"""
import os
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

from finishes import VIEWS_DIR


def plate():
    fonts=Path(os.environ.get("WINDIR","C:/Windows"))/"Fonts"
    title=ImageFont.truetype(str(fonts/"simsun.ttc"),54)
    caption=ImageFont.truetype(str(fonts/"msyh.ttc"),27)
    paper=(245,242,236)
    sheet=Image.new("RGB",(1554,1618),paper)
    draw=ImageDraw.Draw(sheet)
    draw.text((36,18),"冠冕议政宫",font=title,fill=(48,46,42))
    draw.text((1517,46),"政治大都会",font=caption,fill=(79,76,70),anchor="rm")
    draw.line((36,89,1518,89),fill=(164,156,143),width=1)
    for i,(file,name) in enumerate((("正视","正视"),("右视","右视"),("俯视","俯视"),("冠冕议政宫","四分之三视图"))):
        left=36+(i%2)*751
        top=107+(i//2)*751
        with Image.open(VIEWS_DIR/(file+".png")) as source:
            source.thumbnail((731,700),Image.Resampling.LANCZOS)
            x=left+(731-source.width)//2
            y=top+35+(700-source.height)//2
            sheet.paste(source.convert("RGB"),(x,y))
        draw.text((left+1,top-3),name,font=caption,fill=(69,66,61))
    sheet.save(VIEWS_DIR/"议政宫四视对照.jpg",quality=94,subsampling=0)


if __name__=="__main__":
    plate()
