"""沿用海港石木和板岩的取样，另制脂白卷毛、亚麻带、彩绘招牌与雕刻羊头。"""
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[3]
RECIPES = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(RECIPES / "common-harbor"))
from quay_atlas import REGIONS as QUAY_REGIONS, SIZE

ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/WoolHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
SOURCE = ROOT / "art/source/wool-harbor"
PREVIEW = ROOT / "art/previews/wool-harbor"
REGIONS = dict(QUAY_REGIONS, fleece=(1024,512,512), ivory=(1536,1024,256),
               horn=(1792,1024,256), web=(1536,1280,256), nose=(1792,1280,256),
               enamel=(1792,1792,256),blue=(512,512,256),roll=(768,512,256),roll2=(768,768,256))


def uv(name, u, v):
    x,y,n = REGIONS[name]
    border = 6
    return ((x+border+u*(n-2*border))/SIZE, 1-(y+border+(1-v)*(n-2*border))/SIZE)


def make_maps():
    import math
    import numpy as np
    from PIL import Image, ImageDraw, ImageFilter
    rng = np.random.default_rng(201117)
    base = ART.parent / "CommonHarbor"
    maps = {k:Image.open(base/f"common-{k}.png").copy() for k in ("albedo","normal","finish")}

    def field(n, w, h):
        seed = Image.fromarray(rng.integers(0,256,(h,w),dtype=np.uint8))
        return np.asarray(seed.resize((n,n),Image.Resampling.BICUBIC),dtype=float)/255-.5

    def put(name, rgb, relief, smooth, metal=0, strength=2.7):
        x,y,n = REGIONS[name]
        dy,dx = np.gradient(relief)
        normal = np.stack((-dx*strength,dy*strength,np.ones_like(dx)),axis=2)
        normal /= np.linalg.norm(normal,axis=2,keepdims=True)
        finish = np.zeros((n,n,4))
        finish[:,:,0],finish[:,:,3] = metal,smooth
        for key,arr in (("albedo",rgb),("normal",normal*.5+.5),("finish",finish)):
            maps[key].paste(Image.fromarray(np.uint8(255*np.clip(arr,0,1)+.5)),(x,y))

    # 绘出成簇卷曲的纤维。一簇的邻近纤维共用弯向，整张含粗毛锁、绒下阴影与散丝。
    n = 512
    silk = Image.new("L",(n*2,n*2),115)
    pen = ImageDraw.Draw(silk)
    for _ in range(2700):
        px,py = rng.uniform(-16,n+16,2)
        direction,phase = rng.uniform(-math.pi,math.pi,2)
        length,crimp,pitch = rng.uniform(5,17),rng.uniform(1.2,4.1),rng.uniform(.5,1.68)
        cs,sn = math.cos(direction),math.sin(direction)
        for strand in range(8):
            a,b = rng.normal(0,1.9,2)
            phi = phase+rng.normal(0,.22)
            tone = int(rng.integers(65,228))
            pts = []
            for t in np.linspace(0,1,21):
                angle = t*math.tau*pitch+phi
                u = a+t*length+crimp*.8*math.cos(angle)
                v = b+crimp*math.sin(angle)+2.2*t*t
                pts.append((2*(px+u*cs-v*sn),2*(py+u*sn+v*cs)))
            pen.line(pts,fill=tone,width=1+(strand==0))
    silk = silk.resize((n,n),Image.Resampling.LANCZOS)
    fibers = (np.asarray(silk,dtype=float)-128)/128
    locks = field(n,29,35)+.53*field(n,68,77)
    grease = field(n,7,8)+.42*locks
    hue = np.array([.848,.783,.660])
    col = hue + (.16*locks+.27*fibers)[:,:,None] + grease[:,:,None]*np.array([.013,-.018,-.064])
    put("fleece",col,.52*fibers+.27*locks,.10+.04*grease,strength=2.1)

    n = 256
    y,x = np.indices((n,n))/n
    for idx,key in enumerate(("roll","roll2")):
        # 卷端纤维沿螺旋梳向弯曲，内缝聚起羊毛脂和自然暗部。
        px=(x-.49+.022*idx)*2
        py=(y-.51+.015*idx)*2
        radius=np.sqrt(px*px+py*py)
        theta=np.arctan2(py,px)
        ripple=.052*np.sin(3*theta+4*radius)+.023*np.sin(7*theta)+.083*field(n,13,15)
        spiral=theta/(2*np.pi)-(1.94+idx*.18)*radius+.36*idx+ripple
        dist=spiral-np.round(spiral)
        core=np.minimum(1,radius/.12)
        groove=np.exp(-(dist/.063)**2)*core
        lip=np.exp(-((dist-.081)/.07)**2)*core
        wisps=fibers[61+idx*80:61+idx*80+n,53:53+n]
        staple=field(n,18,18)+.38*field(n,50,55)
        color=hue+(.14*staple+.23*wisps-.147*groove+.043*lip)[:,:,None]
        put(key,color,.33*wisps+.20*staple-.33*groove+.17*lip,.095+.035*staple,strength=2.15)
    noise = field(n,45,48)
    broad = field(n,12,15)
    speck = rng.normal(0,.016,(n,n))
    for key,color,gloss in (("ivory",(.828,.764,.642),.32),("horn",(.55,.429,.303),.36),
                            ("nose",(.218,.161,.127),.39)):
        vein = np.sin(17*x+8*y+2*np.sin(14*y)+1.7*broad)
        grain = .048*broad+.026*noise+.011*vein
        put(key,np.array(color)+(grain+speck*.45)[:,:,None],.10*noise+.12*broad+speck,gloss+.05*grain)
    # 经纬交错的扁带由真实束扎轮廓承托，细股通过切线法线表现。
    warp = np.cos(2*np.pi*(19*x+.10*np.sin(15*y)))
    weft = np.cos(2*np.pi*85*y)
    weave = .5*warp+.34*weft*np.sin(np.pi*19*x)**2
    selvedge = (np.exp(-((x-.10)/.016)**2)+np.exp(-((x-.90)/.016)**2))
    put("web",np.array([.56,.441,.272])+(.083*weave+.064*broad-.085*selvedge)[:,:,None],
        .19*weave+.06*noise+.03*selvedge,.16+.045*broad)
    # 深青绿油漆可见刷纹、漏漆木孔和浅划痕。金属度仅留给独立金属构件。
    brush = field(n,9,165)
    peel = np.clip((noise+.60*broad-.42)*4,0,1)
    paint = np.array([.147,.249,.238])+(.054*brush+.046*broad)[:,:,None]
    paint = paint*(1-.47*peel[:,:,None])+np.array([.385,.299,.191])*.47*peel[:,:,None]
    put("enamel",paint,.23*brush+.15*peel+.04*noise,.32+.09*broad-.14*peel)
    ART.mkdir(parents=True,exist_ok=True)
    for key,pic in maps.items():
        pic.save(ART/f"wool-{key}.png",compress_level=7)


if __name__=="__main__":
    make_maps()
