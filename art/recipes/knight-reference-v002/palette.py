"""从参考图取局部质感，叠加连续的金属锻痕、木纹、织纹与锁甲图案。"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps

folder=Path(__file__).resolve().parent
refs=[Image.open(folder/f'参考-{i}.png').convert('RGB') for i in (1,2,3)]
cells=512
atlas=Image.new('RGB',(2048,2048))
rng=np.random.default_rng(311)
yy,xx=np.mgrid[0:1:512j,0:1:512j]
# 采自板甲、盾面、马衣、护手和马颈的实际局部。去除大尺度受光，再在模型上重新受光。
patches=[
 (1,(690,396,732,429),(154,146,130)),(0,(685,310,711,330),(210,202,177)),
 (1,(600,189,623,211),(91,87,80)),(0,(745,459,758,472),(168,123,63)),
 (0,(682,531,722,562),(76,72,64)),(1,(779,475,822,488),(99,59,36)),
 (2,(561,486,590,512),(71,48,33)),(2,(578,400,609,437),(83,59,42)),
 (2,(529,442,544,454),(34,28,25)),(1,(872,393,910,408),(144,88,49)),
 (0,(761,282,777,301),(183,137,101)),(2,(548,359,559,369),(32,25,19)),
 (2,(756,681,785,714),(223,216,198)),(0,(900,475,929,499),(232,214,174)),
 (0,(880,426,908,448),(224,215,193)),(2,(601,794,635,810),(64,53,42))]
for i,(idx,rect,col) in enumerate(patches):
    patch=ImageOps.grayscale(refs[idx].crop(rect)).resize((170,170),Image.Resampling.LANCZOS)
    tile=Image.new('L',(510,510))
    for a in range(3):
        for b in range(3):
            v=ImageOps.mirror(patch) if a%2 else patch
            tile.paste(ImageOps.flip(v) if b%2 else v,(170*a,170*b))
    p=tile.resize((512,512))
    detail=(np.asarray(p,dtype=float)-np.asarray(p.filter(ImageFilter.GaussianBlur(17)),dtype=float))/255
    grain=1+.40*detail+rng.normal(0,.023,(512,512))
    if i in (0,1,2,3):
        grain+=.055*np.sin(xx*8+yy*4)+.032*np.cos(yy*14-xx*5)
    if i in (12,13):
        grain+=.025*(np.sin(yy*710)+np.sin(xx*770))
    if i in (9,14):
        grain+=.060*np.sin(xx*177+6*np.sin(yy*8)+np.cos(xx*9))
    arr=np.uint8(np.clip(np.array(col)*grain[:,:,None],0,255))
    tile=Image.fromarray(arr); draw=ImageDraw.Draw(tile,'RGBA')
    if i in (0,1,2,3):
        for _ in range(125):
            x,y=rng.integers(1,510,2);dx=rng.integers(1,14);dy=rng.integers(-11,11)
            draw.line((x,y,x+dx,y+dy),fill=(47,39,31,62))
            draw.line((x,y+1,x+dx,y+dy+1),fill=(251,231,195,45))
    if i==4:
        for j in range(-1,34):
            for k in range(-1,35):
                x=k*16+(j%2)*8;y=j*16
                draw.ellipse((x,y,x+17,y+13),outline=(23,22,20,255),width=4)
                draw.arc((x+1,y,x+15,y+10),182,355,fill=(172,161,137,250),width=2)
                draw.arc((x+1,y+1,x+15,y+12),8,130,fill=(117,109,94,255),width=2)
    atlas.paste(tile,(i%4*cells,i//4*cells))

# 保留原画中可识别的锻纹和皮肤明暗。将各局部重新展开，背面按同种材质补全。
big=Image.new('RGB',(2048,4096));big.paste(atlas,(0,2048))
# 下列切片坐标对应当前输入的 1536×1024 原画，二维区域分别包在独立三维表面上。
def take(idx,x,y):
    im=np.asarray(refs[idx]).astype(float)
    x=np.clip(x,0,im.shape[1]-2);y=np.clip(y,0,im.shape[0]-2)
    ix=x.astype(int);iy=y.astype(int);fx=(x-ix)[:,:,None];fy=(y-iy)[:,:,None]
    return (im[iy,ix]*(1-fx)+im[iy,ix+1]*fx)*(1-fy)+(im[iy+1,ix]*(1-fx)+im[iy+1,ix+1]*fx)*fy

def map_panel(idx,top_l,top_r,low_l,low_r,wrap=False):
    u=(1+np.cos(tau*xx))/2 if wrap else xx
    def axis(a):
        return (1-yy)*((1-u)*top_l[a]+u*top_r[a])+yy*((1-u)*low_l[a]+u*low_r[a])
    return take(idx,axis(0),axis(1))
tau=np.pi*2
# 新生图层自下而上依次放置；中间部位保留原画结构，边缘可通过重复／翻转环绕。
def detail_tile(id,pic,saturation=1):
    grey=np.mean(pic,axis=2,keepdims=True)
    pic=grey+(pic-grey)*saturation
    # 半去光照，保留甲面的细纹，防止同一条高光在每层护臂上重复。
    if id in (16,17,20,21,22,23,24,27):
        raw_img=Image.fromarray(np.uint8(np.clip(pic,0,255)))
        large=np.asarray(raw_img.filter(ImageFilter.GaussianBlur(21)),dtype=float)
        base=np.array([147,139,126]) if id not in (17,22) else np.array([137,132,122])
        pic=base+.55*(large-base)+1.18*(pic-large)+rng.normal(0,2.3,(512,512,1))
    tile=Image.fromarray(np.uint8(np.clip(pic,0,255)))
    big.paste(tile,((id%4)*512,(7-id//4)*512))

# 胸甲、盔壳和面部。以 -Y 的正面为参考，在两侧连续折返。
detail_tile(16,map_panel(0,(702,351),(805,348),(704,437),(791,445),True),.8)
h=1-yy
u=np.cos(tau*xx)
w=np.interp(h,[0,.2,.52,.8,1],[44,45,38,24,4])
detail_tile(17,take(0,775+u*w,258-75*h),.76)
detail_tile(18,map_panel(0,(750,273),(801,272),(760,308),(790,308),True))
# 盾面木纹去除原队色，白绶带由独立材质槽承担。
left=[(936,663),(896,613),(862,553),(839,500),(820,452),(811,418)]
right=[(937,664),(951,612),(968,548),(978,484),(982,424),(981,359)]
# 面片 UV 第一轴为盾高，第二轴为横向。
def shield_co(a):
    return yy*np.interp(xx,np.linspace(0,1,6),[p[a] for p in left])+(1-yy)*np.interp(xx,np.linspace(0,1,6),[p[a] for p in right])
raw=take(0,shield_co(0),shield_co(1))
value=raw[:,:,0]
intensity=np.where(raw[:,:,0]/np.maximum(1,raw[:,:,1])>1.6,158+.48*(value-145),159+.47*(value-202))
detail_tile(19,np.repeat(intensity[:,:,None],3,axis=2)*np.array([1.19,1.10,1.02]))
detail_tile(20,map_panel(1,(1121,398),(1188,414),(1094,476),(1160,495)),.76)
detail_tile(21,map_panel(1,(595,210),(645,188),(644,260),(598,271),True),.75)
detail_tile(22,map_panel(0,(660,615),(699,618),(682,699),(645,695),True),.64)
detail_tile(23,map_panel(2,(500,397),(529,414),(507,489),(481,478)),.75)
detail_tile(24,map_panel(2,(600,544),(650,548),(642,608),(587,599)),.85)
detail_tile(25,map_panel(0,(637,770),(677,779),(666,802),(634,794),True),.75)
detail_tile(26,map_panel(1,(858,377),(909,389),(903,405),(863,395)),.9)
detail_tile(27,map_panel(1,(510,293),(551,301),(548,321),(506,318)),.8)
# 前 16 格在旧图集是从上到下编码的，移到新图集中仍然保持该顺序。
for i in range(16):
    x=(i%4)*512;y=(i//4)*512
    big.paste(atlas.crop((x,y,x+512,y+512)),(x,(7-i//4)*512))
big.save(folder/'表面图集.png')
