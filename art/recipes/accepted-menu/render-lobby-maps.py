"""把已确认的地图插画制成列表横向缩略图，保留完整轮廓与落在纸面的阴影。"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

root = Path(__file__).resolve().parents[3]
menu = root/'Unity/Catan/Assets/Resources/AcceptedMenu'
atlas = Image.open(root/'docs/原型/选择地图.png').convert('RGB')
tiles = {
    'heading-for-new-shores': (158,194,584,431), 'four-islands': (603,193,1028,432),
    'fog-islands': (1040,194,1475,432), 'through-the-desert': (145,493,590,746),
    'forgotten-tribe': (605,494,1030,748), 'cloth-for-catan': (1037,493,1471,747)
}


def cut_paper(im):
    rgb=np.array(im,dtype=float)
    # 从边缘连通纸色剥离底纸，岛内的浅沙、雾区不参与抠色。
    paper=np.median(np.concatenate([rgb[:2].reshape(-1,3),rgb[-2:].reshape(-1,3)]),axis=0)
    distance=np.max(abs(rgb-paper),axis=2)
    wall=Image.fromarray(np.uint8(distance>42)*255).filter(ImageFilter.MaxFilter(3))
    outside=Image.new('L',(im.width+2,im.height+2))
    outside.paste(wall,(1,1))
    ImageDraw.floodfill(outside,(0,0),128)
    alpha=outside.crop((1,1,im.width+1,im.height+1)).point(lambda x:0 if x==128 else 255)
    face=im.convert('RGBA')
    face.putalpha(alpha.filter(ImageFilter.MinFilter(7)).filter(ImageFilter.GaussianBlur(.65)))
    return face


def thumbnail(key,face):
    face=face.crop(face.getbbox())
    face.thumbnail((512,278),Image.Resampling.LANCZOS)
    result=Image.new('RGBA',(face.width+28,face.height+30))
    mask=Image.new('L',result.size)
    mask.paste(face.getchannel('A'),(14,17))
    shadow=Image.new('RGBA',result.size,'#654121')
    shadow.putalpha(mask.filter(ImageFilter.GaussianBlur(6)).point(lambda a:a*.27))
    result.alpha_composite(shadow)
    result.alpha_composite(face,(14,9))
    result.save(menu/f'lobby-map-{key}.png')


for key,box in tiles.items():
    thumbnail(key,cut_paper(atlas.crop(box)))
for key in ['pirate-islands','wonders-of-catan','new-world']:
    for count in (3,4):
        thumbnail(f'{key}-{count}',Image.open(menu/f'atlas-{key}-{count}.png').convert('RGBA'))
thumbnail('standard',Image.open(menu/'map-标准岛屿.png').convert('RGBA'))
