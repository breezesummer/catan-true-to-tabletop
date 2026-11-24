"""砖港的固有色、切线法线和金属度／光滑度图集。木作沿用同一海岸的木料纹向。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art/source/brick-harbor"
PREVIEW = ROOT / "art/previews/brick-harbor"
ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/BrickHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
MASTER = ROOT / "art/source/round-door-village/四材质母图.png"
TIMBER_MASTER = ROOT / "art/source/lumber-harbor/木料母图.png"
SLOTS = ("dock","oak","wall","trim","cedar","shake","bark","cut",
         "door","wet","tar","rope","iron","shadow","sign","agedcut")
# 小色样自左上依行排列，外缘各留一圈同色像素供 mip 采样。
PATCH = {
    "ashlar":("wall",None), "ember":("cedar",None),
    "madder":("shake",0), "orange":("shake",1), "rose":("shake",2), "glass":("shake",3),
    "slate":("bark",None), "smoke":("cut",0), "graphite":("cut",1),
    "tidal":("cut",2), "silver":("cut",3), "lime":("shadow",0),
    "stone":("shadow",1), "coping":("shadow",2), "bronze":("shadow",3),
    "enamel":("sign",None)
}


def make_maps():
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    size, pad = 512, 10
    rng = np.random.default_rng(21172011)
    ART.mkdir(parents=True,exist_ok=True)
    maps = {k:Image.new(mode,(4*size,4*size)) for k,mode in
            (("albedo","RGB"),("normal","RGB"),("finish","RGBA"))}
    master = Image.open(MASTER).convert("RGB")
    w,h = master.width//2,master.height//2
    samples = []
    for j in range(2):
        for i in range(2):
            crop=master.crop((i*w+6,j*h+6,(i+1)*w-6,(j+1)*h-6))
            samples.append(np.asarray(crop.resize((size,size),Image.Resampling.LANCZOS),np.float32)/255)
    def noise(n,m):
        a=Image.fromarray(rng.integers(0,256,(m,n),dtype=np.uint8))
        return np.asarray(a.resize((size,size),Image.Resampling.BICUBIC),np.float32)/255-.5
    def blur(a,r):
        a=Image.fromarray(np.uint8(np.clip(a,0,1)*255))
        return np.asarray(a.filter(ImageFilter.GaussianBlur(r)),np.float32)/255
    wood=Image.open(TIMBER_MASTER).convert("RGB")
    ww,wh=wood.width//2,wood.height//2
    for col,row in ((0,0),(0,1)):
        swatch=wood.crop((col*ww+5,row*wh+5,(col+1)*ww-5,(row+1)*wh-5))
        samples.append(np.asarray(swatch.resize((size,size),Image.Resampling.LANCZOS),np.float32)/255)
    # 原型是灰模。替灰石、石板、烧结黏土和受潮五金分别指定自然色。
    specs={
        "madder":(1,(154,68,43),.86), "ember":(1,(182,75,39),.82),
        "ashlar":(3,(167,153,130),.84), "orange":(1,(198,99,58),.81),
        "rose":(1,(173,79,54),.85), "glass":(3,(59,79,75),.23),
        "slate":(3,(87,104,110),.70), "smoke":(3,(99,111,112),.73),
        "graphite":(3,(76,86,95),.67), "tidal":(3,(87,91,72),.48),
        "silver":(3,(236,211,157),.34), "lime":(2,(117,110,97),.93),
        "stone":(3,(151,143,124),.85), "coping":(3,(186,170,144),.83),
        "bronze":(3,(176,132,74),.42), "enamel":(3,(33,56,57),.53),
        "dock":(4,(143,126,101),.76),"oak":(4,(109,82,58),.74),"trim":(4,(140,111,79),.69),
        "door":(4,(131,94,65),.73),"tar":(4,(72,58,45),.72),"wet":(4,(82,85,65),.50),
        "agedcut":(5,(153,126,92),.78),"iron":(3,(69,68,63),.49),"rope":(0,(165,143,112),.90)}
    yy,xx=np.mgrid[0:1:complex(size),0:1:complex(size)].astype(np.float32)
    for name,(source,tint,roughness) in specs.items():
        slot,quarter=PATCH.get(name,(name,None))
        sample=np.roll(samples[source],tuple(rng.integers(0,size,2)),axis=(0,1))
        lum=sample @ np.array([.25,.58,.17],np.float32)
        median=np.median(lum)
        broad=noise(9,11)
        grit=lum-blur(lum,1.5)
        salt=np.clip((grit-.038)*6,0,.72)
        fine=rng.normal(0,.012,lum.shape).astype(np.float32)
        # 彩色陶土里的石英点与气孔同时调制颜色、粗糙度和法线。
        pores=np.maximum(0,(-grit-.023)*6) + np.maximum(0,noise(127,132)-.25)*.26
        tone=(lum-median)*1.08 + broad*.14 + fine*.50
        rgb=np.asarray(tint,np.float32)/255*(1+tone[:,:,None]) + .20*(sample-lum[:,:,None] -
                         np.median(sample-lum[:,:,None],axis=(0,1)))
        height=(lum-blur(lum,8))*.75+grit*.55-pores*.22+fine*.39
        rough=roughness + broad*.09 + pores*.12 - salt*.045
        metal=np.zeros_like(lum)
        if name in ("oak","dock","trim","door","tar","wet","agedcut"):
            # 长向导管、银灰风化和锯痕各占不同尺度；防腐湿桩在纵向水线附近渐干。
            strip=noise(4,94)
            checks=np.maximum(0,noise(9,177)+strip*.34-.28)
            sun=np.clip(noise(6,29)+broad*.54-.07,0,.5)
            tone=(lum-median)*1.46+strip*.081+fine*.36
            rgb=np.array(tint)/255*(1+tone[:,:,None])-checks[:,:,None]*.074
            amount=sun*(.59 if name=="dock" else .31)
            grey=np.array([.58,.54,.46])*(1+.64*(lum-median)[:,:,None])
            rgb=rgb*(1-amount[:,:,None])+grey*amount[:,:,None]
            height=(lum-blur(lum,5))*.38+grit*.13-checks*.18+strip*.017
            rough=roughness+sun*.13-checks*.07+grit*.14
            if name=="wet":
                soak=np.clip((.48-xx+noise(11,14)*.063)*4.1,0,1)
                rgb=rgb*(1-.41*soak[:,:,None])+soak[:,:,None]*np.array([.018,.028,.018])
                rough-=.20*soak
        if name=="iron":
            oxide=np.clip((broad+noise(29,32)*.52-.21)*1.58,0,.68)
            rgb=rgb*(1-oxide[:,:,None])+np.array([.43,.25,.14])*oxide[:,:,None]
            height=grit*.28+oxide*.11
            metal=.72-.61*oxide
            rough=.41+oxide*.41+grit*.13
        if name=="rope":
            strand=np.sin(2*np.pi*(xx*12.0+yy*17.0+noise(8,11)*.15))
            fuzz=np.maximum(0,noise(30,172)-.22)
            rgb=np.array(tint)/255*(1+strand[:,:,None]*.085+fuzz[:,:,None]*.28+broad[:,:,None]*.19)
            height=strand*.075+fuzz*.09+fine*.23
        if name in ("madder","ember","orange","rose"):
            # 烧失细孔、断面砂粒与稀疏收缩裂线分三种尺度。孔沿比孔底亮，烘出微凹而非一层噪点。
            holes=Image.new("L",(size,size))
            flecks=Image.new("L",(size,size))
            dark,light=ImageDraw.Draw(holes),ImageDraw.Draw(flecks)
            for _ in range(1700):
                x,y=rng.integers(0,size,2)
                radius=rng.choice((.60,.85,1.10,1.55,2.3),p=(.26,.36,.24,.11,.03))
                width=radius*rng.uniform(.65,1.65)
                draw=dark if rng.random()<.72 else light
                draw.ellipse((x-width,y-radius,x+width,y+radius),fill=int(rng.integers(46,179)))
            for _ in range(7):
                x,y=rng.integers(5,size-5,2)
                trail=[(x,y)]
                for _ in range(rng.integers(4,9)):
                    x+=rng.uniform(1.4,3.7);y+=rng.uniform(-1.7,1.2)
                    trail.append((x,y))
                dark.line(trail,fill=int(rng.integers(50,95)),width=1)
            pits=np.asarray(holes.filter(ImageFilter.GaussianBlur(.48)),np.float32)/255
            quartz=np.asarray(flecks.filter(ImageFilter.GaussianBlur(.39)),np.float32)/255
            rim=np.clip(blur(pits,1.08)-pits,0,1)
            fired=np.clip((noise(11,8)*.71+noise(28,31)*.40-.095)*1.18,0,.46)
            rgb*=1-fired[:,:,None]*np.array([.19,.39,.34])
            rgb=rgb*(1-.72*pits[:,:,None])+quartz[:,:,None]*np.array([.25,.22,.18])+rim[:,:,None]*.36
            height+=quartz*.22-pits*.52+rim*.41
            bloom=np.clip((broad+.75*noise(22,17)-.34)*.60,0,.20)
            rgb=rgb*(1-bloom[:,:,None])+np.array([.72,.60,.50])*bloom[:,:,None]
            rgb+=salt[:,:,None]*np.array([.15,.13,.11])-pores[:,:,None]*.09
        if name in ("slate","smoke","graphite"):
            # 石板层理有长短和断续，断面显出较亮的矿物片。
            vein=np.sin(yy*163+xx*31+3.3*noise(8,16))
            flake=np.maximum(0,vein-.77)*np.maximum(0,noise(26,13)+.27)
            seam=np.maximum(0,noise(42,119)-.27)*(1+noise(9,17))
            rgb+=flake[:,:,None]*.33-seam[:,:,None]*.14
            height+=flake*.26-seam*.13
            rough-=np.maximum(0,grit)*.65
        if name in ("ashlar","stone","coping","tidal"):
            chisel=np.maximum(0,np.cos(xx*360+yy*128+noise(7,11)*5)-.68)
            chisel*=np.maximum(0,noise(18,26)+.25)
            lichen=np.clip((noise(14,16)+noise(61,45)*.58-.28)*.8,0,.35)
            rgb=rgb*(1-lichen[:,:,None])+np.array([.39,.40,.24])*lichen[:,:,None]
            rgb+=chisel[:,:,None]*.14-pores[:,:,None]*.08
            height+=chisel*.19-pores*.13
        if name in ("bronze","silver"):
            tarnish=np.clip((broad+pores*.65-.04)*1.95,0,.65)
            if name=="silver": tarnish*=.28
            rgb=rgb*(1-tarnish[:,:,None])+np.array([.31,.30,.20])*tarnish[:,:,None]
            metal=(.64 if name=="bronze" else .38)-.25*tarnish
            rough=.30+.29*tarnish-.10*grit
            height=height*.43
        if name=="enamel":
            worn=np.clip((noise(43,22)+broad*.55-.42)*.85,0,.20)
            rgb=np.asarray(tint)/255*(1+(lum-median)*.88)[:,:,None]+fine[:,:,None]*.20
            rgb=rgb*(1-worn[:,:,None])+np.array([.53,.37,.22])*worn[:,:,None]
            height=grit*.24-worn*.15
        if name=="glass":
            rgb+=noise(8,14)[:,:,None]*np.array([.075,.083,.080])
            height=grit*.07+broad*.09
            rough=.20+broad*.10
        dy,dx=np.gradient(height)
        n=np.stack((-dx*3.1,dy*3.1,np.ones_like(dx)),axis=-1)
        n/=np.linalg.norm(n,axis=-1,keepdims=True)
        finish=np.stack((metal,np.zeros_like(lum),np.zeros_like(lum),1-rough),axis=-1)
        index=SLOTS.index(slot)
        x0,y0=(index%4)*size,(index//4)*size
        for key,arr in (("albedo",rgb),("normal",n*.5+.5),("finish",finish)):
            patch=Image.fromarray(np.uint8(np.clip(arr,0,1)*255+.5))
            if quarter is None:
                side,bleed=size,pad
                pos=(x0,y0)
            else:
                side,bleed=(size-2*pad)//2,9
                pos=(x0+pad+(quarter%2)*side,y0+pad+(quarter//2)*side)
            patch=patch.resize((side-bleed*2,side-bleed*2),Image.Resampling.LANCZOS)
            guarded=np.pad(np.asarray(patch),((bleed,bleed),(bleed,bleed),(0,0)),mode="edge")
            maps[key].paste(Image.fromarray(guarded),pos)
    for key,img in maps.items():
        for slot in {slot for slot,quarter in PATCH.values() if quarter is not None}:
            index=SLOTS.index(slot)
            x,y=(index%4)*size,(index//4)*size
            inner=np.asarray(img.crop((x+pad,y+pad,x+size-pad,y+size-pad)))
            img.paste(Image.fromarray(np.pad(inner,((pad,pad),(pad,pad),(0,0)),mode="edge")),(x,y))
        img.save(ART/f"brick-{key}.png")


if __name__=="__main__":
    make_maps()
