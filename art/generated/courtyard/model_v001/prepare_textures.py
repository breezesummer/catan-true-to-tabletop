"""整理ImageGen原始图集，提取材质分区并生成弱细节法线；需要Pillow。"""
from pathlib import Path
from PIL import Image
import math
P=Path(__file__).resolve().parent/'textures'
im=Image.open(P/'material_atlas.png').convert('RGB');w,h=im.size
regions=[('roof',(0,0,w//2,h//2)),('stone',(w//2,0,w,h//2)),('wood',(0,h//2,w//2,h)),('plaster',(w//2,h//2,w,h))]
for name,box in regions:
 t=im.crop(box).resize((512,512));t.save(P/(name+'_basecolor.png'))
 g=t.convert('L');out=Image.new('RGB',t.size);a=g.load();b=out.load()
 for y in range(512):
  for x in range(512):
   dx=(a[(x+1)%512,y]-a[(x-1)%512,y])/255*.7
   dy=(a[x,(y+1)%512]-a[x,(y-1)%512])/255*.7
   length=math.sqrt(dx*dx+dy*dy+1)
   b[x,y]=(round(127.5-127.5*dx/length),round(127.5+127.5*dy/length),round(127.5+127.5/length))
 out.save(P/(name+'_normal.png'))
im=Image.open(P/'joinery_atlas.png');w,h=im.size
for name,box in [('door',(0,0,w//2,h)),('window',(w//2,0,w,h))]:
 im.crop(box).resize((256,512)).save(P/(name+'_basecolor.png'))
 Image.new('RGB',(256,512),(128,128,255)).save(P/(name+'_normal.png'))
