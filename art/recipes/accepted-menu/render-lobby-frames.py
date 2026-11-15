"""生成闭合表头卡片与代码内槽，四角和包边可九宫格伸展。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

out=Path(__file__).resolve().parents[3]/'Unity/Catan/Assets/Resources/AcceptedMenu'
scale=4


def frame(name,field):
    width,height=160,64
    image=Image.new('RGBA',(width*scale,height*scale))
    draw=ImageDraw.Draw(image)
    if field:
        # 从外侧接触影过渡至槽口暗边，内壁下缘接光。
        for inset,radius,color in [(1,14,'#b39062'),(2,13,'#f8e0b4'),(4,11,'#996b3c'),
                                   (5,10,'#d2b48a'),(7,8,'#f2d5a6'),(9,6,'#f7dfb6')]:
            draw.rounded_rectangle((inset*scale,inset*scale,(width-inset)*scale,(height-inset)*scale),
                                   radius*scale,fill=color)
    else:
        # 浅纸色底仅轻盖原纸，单层闭合铜线收口。
        points=[(11,2),(149,2),(152,3),(154,7),(158,9),(159,12),
                (159,52),(158,55),(154,57),(152,61),(149,62),(11,62),
                (8,61),(6,57),(2,55),(1,52),(1,12),(2,9),(6,7),(8,3)]
        points=[(x*scale,y*scale) for x,y in points]
        draw.polygon(points,fill=(198,143,77,24))
        draw.line(points+points[:1],fill=(147,99,48,190),width=scale,joint='curve')
    image.resize((width,height),Image.Resampling.LANCZOS).save(out/f'setup-lobby-{name}.png')


frame('card',False)
frame('field',True)
