"""拆取局内菜单确认稿的纸面、无字木牌与红漆主牌，保留随形金边与接触影。"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter, ImageStat

ROOT = Path(__file__).resolve().parents[3]
MENU = ROOT / 'Unity/Catan/Assets/Resources/AcceptedMenu'
REF = Image.open(ROOT / 'docs/原型/游戏内菜单.png').convert('RGBA')

# 纸面的边框和山峦直接取自原型。用两边空白纸的逐行底色与真实细纹补齐控件区。
paper = REF.crop((555,98,1109,845))
grain = REF.crop((568,113,591,782)).convert('RGB')
grain_smooth = grain.filter(ImageFilter.GaussianBlur(5))
for y in range(12,690):
    a = ImageStat.Stat(paper.crop((13,y,30,y+1))).mean
    b = ImageStat.Stat(paper.crop((paper.width-26,y,paper.width-13,y+1))).mean
    row = y % grain.height
    for x in range(9,paper.width-9):
        t=(x-9)/(paper.width-18)
        # 镜像平铺保留纸纹原始尺度，不将窄条纹理横向拉粗。
        gx=x%(grain.width*2)
        if gx>=grain.width:gx=grain.width*2-1-gx
        sample=grain.getpixel((gx,row))
        smooth=grain_smooth.getpixel((gx,row))
        rgb=tuple(max(0,min(255,round(a[c]+(b[c]-a[c])*t+(sample[c]-smooth[c])*.7))) for c in range(3))
        if y>674:
            original=paper.getpixel((x,y));blend=(y-674)/16
            rgb=tuple(round(p+(q-p)*blend) for p,q in zip(rgb,original))
        paper.putpixel((x,y),rgb+(255,))
mask=Image.new('L',paper.size)
ImageDraw.Draw(mask).rounded_rectangle((0,0,paper.width-1,paper.height-1),radius=9,fill=255)
paper.putalpha(mask.filter(ImageFilter.GaussianBlur(.4)))
paper.save(MENU/'setup-pause-sheet.png')


def blank_face(name,box,text,samples,outline,pad):
    face=REF.crop(box)
    left,top,right,bottom=text
    for y in range(top,bottom):
        a,b=face.getpixel((samples[0],y)),face.getpixel((samples[1],y))
        for x in range(left,right):
            t=(x-samples[0])/(samples[1]-samples[0])
            face.putpixel((x,y),tuple(round(p+(q-p)*t) for p,q in zip(a,b)))
    mask=Image.new('L',face.size)
    if name=='resume':
        # 红牌肩部弧线比通用切角更外扩，按原图铜色外缘逐行取形，避免削掉金棱。
        draw=ImageDraw.Draw(mask)
        for y in range(face.height):
            xs=[x for x in range(face.width) if face.getpixel((x,y))[1]<170 and face.getpixel((x,y))[2]<125]
            if xs:draw.line((min(xs),y,max(xs),y),fill=255)
        mask=mask.filter(ImageFilter.MaxFilter(3))
    else:
        ImageDraw.Draw(mask).polygon(outline,fill=255)
    mask=mask.filter(ImageFilter.GaussianBlur(.55))
    face.putalpha(mask)
    canvas=Image.new('RGBA',(face.width+2*pad,face.height+2*pad))
    footprint=Image.new('L',canvas.size);footprint.paste(mask,(pad,pad+2))
    shadow=Image.new('RGBA',canvas.size,(74,37,12))
    shadow.putalpha(footprint.filter(ImageFilter.GaussianBlur(2)).point(lambda a:round(a*.4)))
    canvas.alpha_composite(shadow);canvas.alpha_composite(face,(pad,pad))
    canvas.save(MENU/f'setup-pause-{name}.png')


blank_face('wood',(626,334,1042,398),(105,10,304,50),(91,322),
    [(30,1),(384,1),(395,4),(402,11),(410,16),(415,25),(415,40),(410,50),
     (401,54),(395,60),(384,63),(29,63),(18,60),(12,54),(5,50),(1,40),
     (1,25),(5,16),(14,11),(19,4)],8)
blank_face('resume',(610,219,1057,316),(120,20,330,73),(108,342),
    [(39,2),(405,2),(418,5),(426,16),(427,22),(439,28),(445,39),
     (446,66),(439,78),(429,82),(423,90),(411,96),(36,96),(24,90),
     (19,82),(9,77),(2,66),(1,40),(7,29),(18,23),(22,13),(29,5)],12)
