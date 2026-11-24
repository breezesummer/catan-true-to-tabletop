"""商人单材质图集。色图为 sRGB，法线及金属度／光滑度为线性数据。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Resources/NeutralPieces/v001"
SOURCE = ROOT / "art/source/travelling-merchant/行商.blend"
VIEWS = ROOT / "art/previews/travelling-merchant"
REFERENCE = ROOT / "assets/references/inbox/单位/商人四视建模原型.png"
SIZE = 2048
HEIGHT = .01576
PORTRAIT_BOX = (177, 115, 281, 271)
# 图集按各部件的展开方向制版。每块四周留八像素延拓，供棋盘远景使用。
REGIONS = {
    "coat": (0,0,1024), "cap": (1024,0,512), "damask": (1536,0,512),
    "harness": (1024,512,512), "boots": (1536,512,512),
    "portrait": (0,1024,512), "carpet": (512,1024,512),
    "linen": (1024,1024,512), "breeches": (1536,1024,512),
    "hair": (0,1536,256), "beard": (256,1536,256), "skin": (512,1536,256),
    "binding": (768,1536,256), "sole": (1024,1536,256), "satchel": (1280,1536,256),
    "brass": (1536,1536,256), "pewter": (1792,1536,256),
    "thread": (0,1792,256), "shade": (256,1792,256), "nail": (512,1792,256),
    "felt": (768,1792,256), "hatband": (1024,1792,256), "lining": (1280,1792,256),
    "welt": (1536,1792,256), "cord": (1792,1792,256),
}

CHARTS = {
    "袍身":(1024,0,1024,640), "迎客袖":(1024,640,512,384), "持布袖":(1536,640,512,384),
    "马甲":(0,1024,640,512), "帽冠":(640,1024,640,512),
    "左裤":(1280,1024,384,512), "右裤":(1664,1024,384,512),
    "左鞋面":(0,1536,256,512), "右鞋面":(256,1536,256,512),
    "左靴筒":(512,1536,320,512), "右靴筒":(832,1536,320,512),
    "左靴口":(1152,1536,208,512), "右靴口":(1360,1536,208,512),
    "头颅":(1568,1536,480,320),"掌面":(1568,1856,240,192),"手背":(1808,1856,240,192),
}

def atlas_uv(key, u, v):
    if key in CHARTS:
        x,y,w,h=CHARTS[key]
        return ((x+6+u*(w-12))/SIZE,1-(y+6+(1-v)*(h-12))/SIZE)
    x,y,n = REGIONS[key]
    return ((x+8+u*(n-16))/(2*SIZE), 1-(y+8+(1-v)*(n-16))/(2*SIZE))


def make_maps(charts,output):
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter

    rng = np.random.default_rng(211720)
    base = Image.new("RGB", (SIZE,SIZE), (96,52,44))
    normals = Image.new("RGB",base.size,(128,128,255))
    finish = Image.new("RGBA",base.size,(0,0,0,65))

    def noise(n, freq):
        data = rng.integers(0,256,(freq,freq),dtype=np.uint8)
        return np.asarray(Image.fromarray(data).resize((n,n),Image.Resampling.BICUBIC),dtype=float)/127.5-1

    def rgb(s):
        return np.array(list(bytes.fromhex(s)),dtype=float)

    def scars(n):
        field=Image.new("L",(n,n))
        pen=ImageDraw.Draw(field)
        for _ in range(n//2):
            x,y=rng.uniform(0,n,2)
            dx,dy=rng.uniform(1,n*.045),rng.uniform(-.012,.016)*n
            pen.line((x,y,x+dx*.6,y+dy*.38,x+dx,y+dy),fill=int(rng.uniform(25,220)),width=1)
        return np.asarray(field,dtype=float)/255

    def hide_grain(n):
        # 二近邻与最近邻之间的距离在皮纹交界处为零。
        count=max(9,n//10)
        iy,ix=np.indices((n,n),dtype=float)*count/n
        ci,cj=ix.astype(int),iy.astype(int)
        jitter=rng.uniform(.12,.88,(count,count,2))
        nearest=np.full((n,n),9.)
        second=nearest.copy()
        for dx in (-1,0,1):
            for dy in (-1,0,1):
                j=jitter[(cj+dy)%count,(ci+dx)%count]
                distance=(ci+dx+j[:,:,0]-ix)**2+(cj+dy+j[:,:,1]-iy)**2
                second=np.minimum(second,np.maximum(nearest,distance))
                nearest=np.minimum(nearest,distance)
        return np.tanh((np.sqrt(second)-np.sqrt(nearest))*4)

    def portrait(n):
        # 面部基于原型的彩色视角校准。仅保留皮肤、眉眼与面须，边缘颜色延展至鬓角接缝。
        src=Image.open(REFERENCE).convert("RGB")
        mask=Image.new("L",src.size)
        ImageDraw.Draw(mask).polygon([(198,154),(206,142),(220,133),(239,126),(254,126),
            (262,130),(267,143),(271,157),(270,169),(275,180),(270,194),(261,206),
            (252,211),(239,212),(222,207),(207,198),(200,189),(194,182),(193,168)],fill=255)
        ImageDraw.Draw(mask).polygon([(203,193),(218,199),(242,209),(258,210),(264,204),
            (264,225),(265,246),(264,264),(257,251),(243,236),(227,224),(208,214),(202,209)],fill=255)
        crop=src.crop(PORTRAIT_BOX)
        valid=np.asarray(mask.crop(PORTRAIT_BOX))>0
        raw=np.array(crop)
        for _ in range(65):
            if valid.all():
                break
            total=np.zeros(raw.shape,dtype=float)
            hits=np.zeros(valid.shape,dtype=int)
            for ax,shift in ((0,1),(0,-1),(1,1),(1,-1)):
                v=np.roll(valid,shift,axis=ax)
                if ax==0: v[0 if shift==1 else -1,:]=False
                else: v[:,0 if shift==1 else -1]=False
                total+=np.roll(raw,shift,axis=ax)*v[:,:,None]
                hits+=v
            add=(~valid)&(hits>0)
            raw[add]=(total[add]/hits[add,None]).astype(np.uint8)
            valid|=add
        corrected=Image.fromarray(raw).resize((n,n),Image.Resampling.LANCZOS)
        # 仅压低原图的大尺度光照，保留眼睑、嘴角及胡须的局部色差。
        col=np.asarray(corrected,dtype=float)
        luma=np.asarray(corrected.convert("L").filter(ImageFilter.GaussianBlur(n*.09)),dtype=float)
        return np.clip(col*((155/np.maximum(luma,50))**.20)[:,:,None],0,255)

    cloth={
        "coat":("74434a","bd884e",.16), "cap":("79505a","be8b59",.20),
        "damask":("ad7137","dba75a",.23), "linen":("d7c4a0","ece1c5",.13),
        "breeches":("71533c","9b7850",.16), "lining":("59353c","8f6151",.12),
        "felt":("ba9a70","e2c095",.16), "binding":("95653f","d5a364",.34),
        "thread":("c4a073","e0c895",.23),"cord":("866441","c7a26c",.20),
        "carpet":("d6b998","e6caaa",.20),
    }
    hides={"harness":("805237","be8656"),"boots":("69442e","aa794e"),
           "satchel":("90623e","c49760"),"hatband":("59363a","935d4f"),
           "sole":("4a352a","896846"),"welt":("654632","967555")}
    for key,(left,top,size) in REGIONS.items():
        n=size-16
        py,px=np.indices((n,n),dtype=float)
        u,v=px/(n-1),py/(n-1)
        coarse,medium,fine=(noise(n,f) for f in (6,24,max(30,n//6)))
        grain=rng.normal(0,.64,(n,n))
        metal=np.zeros_like(u)
        if key in cloth:
            ground,tip,sheen=cloth[key]
            warp=np.sin(2*np.pi*(px/4.0+.12*np.sin(py*.39)+.26*medium))
            weft=np.sin(2*np.pi*(py/4.5+.14*np.sin(px*.32)+.32*medium))
            twill=np.cos((px+2.1*py)*1.13+1.8*medium)
            pile=.46*warp+.31*weft+.23*twill
            edge=np.exp(-np.minimum(u,1-u)*53)+.72*np.exp(-np.minimum(v,1-v)*46)
            wear=np.clip((.32*coarse+.38*medium+.53*fine+.30*grain+edge*.56-.34)*1.23,0,1)
            colour=rgb(ground)[None,None,:]+(rgb(tip)-rgb(ground))*wear[:,:,None]*.61
            if key in ("coat","cap","damask","binding"):
                # 断续卷草为织入的暗花；经纬方向由每片裁片自己的 UV 决定。
                a=2*np.pi*(u*9+.11*medium)
                b=2*np.pi*(v*16+.11*coarse)
                vine=np.exp(-((np.cos(a)*.63-np.sin(b))/.21)**2)
                bud=np.exp(-((np.sin(a+.85)*.86+np.sin(b+.58))/.29)**2)
                motif=(vine+.43*bud)*np.clip(.46+.49*medium+.35*fine,0,1)
                colour+=motif[:,:,None]*np.array([17,12,3])*(1.3 if key=="damask" else .62)
                pile+=.34*motif
            if key=="carpet":
                # 横贯布幅的两组茜草红条，每圈卷层端面另用无条纹毛毡。
                stripe=((v>.18)&(v<.32))|((v>.67)&(v<.81))
                pin=((v>.135)&(v<.148))|((v>.837)&(v<.849))
                colour[stripe]=rgb("a3554e")+(wear[stripe,None])*(rgb("d1977b")-rgb("a3554e"))
                colour[pin]=rgb("a76b53")
                fleck=np.clip(1.05*fine+.65*grain-.52,0,1)
                colour+=(5.5*twill+3.5*weft+9*fleck-3)[:,:,None]
                pile+=.43*weft+.36*fleck
            elif key=="felt":
                # 只把卷层贴紧处染深。一条圆边上仍可见较亮的短绒与细股纱。
                crease=np.exp(-((v-.87)/.14)**2)
                colour*=1-.42*crease[:,:,None]
                colour+=(4.5*twill+4.3*weft+2.8*fine)[:,:,None]
            colour+=(5*coarse+3.5*medium+2.7*pile+2.8*grain)[:,:,None]
            height=.52*pile+.36*fine+.24*grain
            gloss=sheen+.04*fine+.035*wear
        elif key in hides:
            ground,tip=hides[key]
            pebble=hide_grain(n)
            cuts=scars(n)
            edge=np.exp(-np.minimum(u,1-u)*32)+.55*np.exp(-np.minimum(v,1-v)*39)
            abrasion=np.clip(.31*coarse+.20*medium+.35*fine+.20*grain+.41*edge-.25,0,1)
            colour=rgb(ground)+(rgb(tip)-rgb(ground))*(abrasion+.43*cuts)[:,:,None]
            colour+=(8*coarse+5*medium+6*(pebble-.53)+2.1*grain)[:,:,None]
            height=2*pebble+.41*fine-.86*cuts+.17*grain
            gloss=.34+.12*pebble+.07*abrasion+.04*coarse
        elif key in ("brass","pewter"):
            patina=np.clip(.45+.4*coarse+.27*medium+.22*fine,0,1)
            a,b=("936733","d7ad6c") if key=="brass" else ("74685b","c9b392")
            colour=rgb(a)+(rgb(b)-rgb(a))*patina[:,:,None]
            cuts=scars(n)
            colour+=(8*cuts+3*grain)[:,:,None]
            height=.32*fine+.11*grain-.58*cuts
            metal=.79+.16*patina
            gloss=.50+.28*patina-.12*cuts
        elif key in ("hair","beard"):
            t=u*(31 if key=="hair" else 23)+.35*np.sin(v*6+.31*coarse)+.23*medium
            strand=np.sin(t*2*np.pi)
            colour=rgb("50382d" if key=="hair" else "564035")+(11*strand+5*fine+7*coarse)[:,:,None]
            colour+=np.clip(.65*strand+.50*coarse+.35*medium-.15,0,1)[:,:,None]*np.array([37,31,23])
            height=1.3*strand+.25*fine
            gloss=.27+.11*strand+.03*medium
        elif key in ("skin","nail","portrait"):
            if key=="portrait": colour=portrait(n)
            else: colour=np.tile(rgb("d59c78" if key=="skin" else "d6a38a"),(n,n,1))
            pores=np.clip(-grain-.52,0,1)
            colour+=(2.4*medium+1.7*fine+.97*grain-2.3*pores)[:,:,None]
            height=.35*fine-.78*pores+.15*grain
            gloss=(.29 if key!="nail" else .46)+.028*medium+.045*fine
            if key=="nail":
                # 同一块 UV 从甲根走向游离缘，暖粉甲床在边缘渐薄。
                # 图像从上向下排列，UV 中的甲尖在图像上缘。
                edge=np.exp(-((v-.022-.025*(2*u-1)**2)/.047)**2)
                root=np.exp(-((1-v-.045)/.13)**2-((u-.49)/.36)**4)
                colour=np.tile(rgb("cb9b88"),(n,n,1))
                colour+=(18*edge+5*root+2.6*medium+1.3*fine)[:,:,None]
                height=.18*np.sin(80*u+medium*.8)+.10*fine
                gloss=.46+.05*(1-np.abs(2*u-1))+.030*fine-.03*edge
        else:
            colour=np.tile(rgb("47302b"),(n,n,1))+(3*medium+2*fine)[:,:,None]
            height=.43*fine
            gloss=.21+.03*medium
        gy,gx=np.gradient(height)
        vec=np.stack([-gx*.53,gy*.53,np.ones_like(gx)],axis=-1)
        vec/=np.linalg.norm(vec,axis=-1,keepdims=True)
        packed=np.stack([metal,np.zeros_like(u),np.zeros_like(u),np.clip(gloss,0,1)],axis=-1)
        for atlas,pixels in ((base,colour),(normals,(vec*.5+.5)*255),(finish,packed*255)):
            patch=np.pad(np.uint8(np.clip(pixels,0,255)+.0),((8,8),(8,8),(0,0)),mode="edge")
            atlas.paste(Image.fromarray(patch),(left,top))
    from dye import project_cuts
    final_maps=project_cuts((base,normals,finish),charts)
    for atlas,name in zip(final_maps,("surface","normal","finish")):
        atlas.save(output/f"peddler-{name}.png",compress_level=6)
