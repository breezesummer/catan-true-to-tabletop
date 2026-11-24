"""吊楼商港的同源固有色、切线法线与金属度／光滑度图集。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art/source/common-harbor"
PREVIEW = ROOT / "art/previews/common-harbor"
ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/CommonHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
SIZE = 2048
# 图集以左上为原点。木材长纹沿 U，桩的 U 从桩底指向桩顶。
REGIONS = {
    "stone": (0,0,512), "honey": (512,0,512), "grey": (1024,0,512), "dressed": (1536,0,512),
    "slate": (0,512,512), "blue": (512,512,512), "heather": (1024,512,512),
    "tide": (1536,512,256), "salt": (1792,512,256), "mortar": (1536,768,256), "glass": (1792,768,256),
    "oak": (0,1024,512), "wall": (512,1024,512), "dock": (1024,1024,512), "pine": (1536,1024,512),
    "agedcut": (0,1536,512), "wet": (512,1536,512), "iron": (1024,1536,256),
    "brass": (1280,1536,256), "rope": (1024,1792,256), "bronze": (1280,1792,256),
    "door": (1536,1536,256), "tar": (1792,1536,256), "shadow": (1536,1792,256), "tablet": (1792,1792,256),
}


def uv(name, u, v):
    x, y, side = REGIONS[name]
    pad = 7
    return ((x+pad+u*(side-2*pad))/SIZE, 1-(y+pad+(1-v)*(side-2*pad))/SIZE)


def make_maps():
    import numpy as np
    from PIL import Image, ImageFilter

    rng = np.random.default_rng(3202117)
    def quarters(path):
        with Image.open(path) as original:
            pic = original.convert("RGB")
        w,h = pic.width//2,pic.height//2
        return [pic.crop((i*w+8,j*h+8,(i+1)*w-8,(j+1)*h-8)) for j in range(2) for i in range(2)]
    stone, slate, _, copper = quarters(ROOT/"art/source/gothic-royal-city/王城材质母图.png")
    oak, _, end, split = quarters(ROOT/"art/source/lumber-harbor/木料母图.png")
    # 底片、固有色中值、明暗起伏、光滑度。场景光与接触阴影由引擎给出。
    recipe = {
        "stone":(stone,(157,148,130),.45,.24), "honey":(stone,(164,150,126),.49,.21),
        "grey":(stone,(144,143,133),.48,.20), "dressed":(stone,(190,179,153),.35,.33),
        "slate":(slate,(74,83,88),.46,.35), "blue":(slate,(78,91,98),.49,.37),
        "heather":(slate,(88,87,91),.43,.34), "tide":(stone,(82,89,76),.55,.44),
        "salt":(stone,(127,129,109),.47,.32), "mortar":(stone,(111,105,93),.45,.14),
        "glass":(slate,(55,76,71),.32,.76), "oak":(oak,(109,78,51),.80,.32),
        "wall":(split,(118,95,73),.67,.21), "dock":(oak,(141,121,94),.78,.25),
        "pine":(oak,(163,120,76),.73,.24), "agedcut":(end,(143,110,76),.71,.22),
        "wet":(oak,(110,100,76),.64,.32), "iron":(copper,(69,68,64),.45,.45),
        "brass":(copper,(176,139,80),.39,.55), "rope":(oak,(163,135,93),.35,.16),
        "bronze":(copper,(86,73,48),.48,.44), "door":(oak,(122,83,54),.80,.34),
        "tar":(oak,(72,59,47),.62,.28), "shadow":(stone,(78,75,66),.53,.17),
        "tablet":(stone,(200,183,149),.28,.34),
    }
    maps = {k:Image.new(mode,(SIZE,SIZE)) for k,mode in (("albedo","RGB"),("normal","RGB"),("finish","RGBA"))}
    for idx,(name,(left,top,n)) in enumerate(REGIONS.items()):
        source,color,contrast,gloss = recipe[name]
        pic = source.resize((n,n),Image.Resampling.LANCZOS)
        rgb0 = np.roll(np.asarray(pic,np.float32)/255,(idx*29,idx*49),axis=(0,1))
        lum = rgb0 @ [.27,.55,.18]
        diff = np.clip(lum/np.median(lum)-1,-.86,1.02)
        y,x = (np.indices((n,n))+.5)/n
        def field(w,h):
            mosaic = Image.fromarray(rng.integers(0,256,(h,w),dtype=np.uint8))
            return np.asarray(mosaic.resize((n,n),Image.Resampling.BICUBIC),np.float32)/255-.5
        fine = rng.normal(0,.018,(n,n))
        patch = field(12,13)
        rgb = np.array(color)/255*(1+contrast*diff[:,:,None])
        rgb += .13*((rgb0-lum[:,:,None])-(rgb0-lum[:,:,None]).mean(axis=(0,1)))
        height = .42*diff+fine
        smooth = gloss + .11*patch-.065*diff
        metal = np.zeros_like(lum)
        if source is stone:
            pits = np.clip((field(n//4,n//4)+.48*field(n//2,n//2)-.35)*4,0,1)
            height -= .10*pits
            rgb -= (.042*pits)[:,:,None]
            if name in ("tide","salt"):
                weed = np.clip((patch+.4*diff+.10)*1.15,0,.55)
                rgb = rgb*(1-weed[:,:,None])+np.array([.26,.32,.19])*weed[:,:,None]
                salt = np.clip((field(38,25)+.5*patch-.19)*.15,0,.06)
                rgb += salt[:,:,None]
                smooth += .15*weed
        elif source is slate and name!="glass":
            # 板岩沿片理剥落；风化边缘略亮，层面破口更粗。
            seam = np.sin(28*y+8*x+1.4*np.sin(x*5.4)+.7*field(5,8))
            flake = np.exp(-((seam-.31)/.09)**2)*np.clip(field(16,18)+.14,0,.6)
            height += .13*flake+.045*field(22,72)
            rgb += (.069*flake+.027*patch)[:,:,None]
            smooth -= .19*flake
        elif name=="glass":
            pane = np.floor(5*x+8*y)+np.floor(5*x-8*y)*11
            cast = np.sin(pane*2.85)
            rgb += cast[:,:,None]*np.array([.034,.017,-.018])
            height = .06*field(9,11)
        elif name in ("iron","brass","bronze"):
            tarnish = np.clip((patch+.40*field(44,41)+.14)*1.35,0,.78)
            patina = np.array([.24,.31,.25]) if name!="iron" else np.array([.36,.23,.14])
            rgb = rgb*(1-tarnish[:,:,None])+patina*tarnish[:,:,None]
            metal = .89-.73*tarnish
            smooth += .13*diff-.32*tarnish
            height = .16*diff+.13*field(31,31)+.08*tarnish
        elif name=="rope":
            twist = np.cos(2*np.pi*(10*x+3*y)+.18*np.sin(13*x))
            rgb *= (1+.12*twist)[:,:,None]
            height = .13*twist+.038*field(20,80)+fine
        elif name=="wet":
            dry = np.clip((x-.28)*3.2+.38*patch,0,1)
            rgb *= (.60+.40*dry)[:,:,None]
            rgb += np.array([.005,.036,.007])*(1-dry)[:,:,None]
            smooth += .28*(1-dry)
        elif source in (oak,split):
            summerwood = np.asarray(pic.filter(ImageFilter.GaussianBlur(2.2)),np.float32)/255
            grain = (rgb0-np.roll(summerwood,(idx*29,idx*49),axis=(0,1))).mean(axis=2)
            height += .26*grain+.025*field(13,145)
            if name in ("dock","wall"):
                silver = np.clip((field(7,69)+.57*patch)*.41,0,.25)
                rgb += silver[:,:,None]*np.array([.23,.25,.24])
        dy,dx = np.gradient(height)
        normal = np.stack((-dx*2.8,dy*2.8,np.ones_like(dx)),axis=2)
        normal /= np.linalg.norm(normal,axis=2,keepdims=True)
        finish = np.stack((metal,np.zeros_like(lum),np.zeros_like(lum),np.clip(smooth,.04,.94)),axis=2)
        for key,data in (("albedo",rgb),("normal",normal*.5+.5),("finish",finish)):
            maps[key].paste(Image.fromarray(np.uint8(np.clip(data,0,1)*255+.5)),(left,top))
    ART.mkdir(parents=True,exist_ok=True)
    for name,img in maps.items():
        img.save(ART/f"common-{name}.png",compress_level=8)


if __name__=="__main__":
    make_maps()
