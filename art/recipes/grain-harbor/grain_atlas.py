"""谷物商港的同源固有色、切线法线和金属度／光滑度图集。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art/source/grain-harbor"
PREVIEW = ROOT / "art/previews/grain-harbor"
ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/GrainHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
SIZE, BLEED = 2048, 7
CELLS = (
    "stone", "honey", "grey", "dressed",
    "slate", "blue", ("heather", "tide", "salt", "mortar"), ("glass", "tar", "shadow", "plaque"),
    "oak", "wall", "dock", "agedcut",
    "canvas", ("redFlax", "blueFlax", "wet", "door"), ("iron", "brass", "gilt", "grain"),
    ("rope", "willow", "husk", "ivory"),
)
REGION = {}
for cell, names in enumerate(CELLS):
    left, top = cell % 4 * 512, cell // 4 * 512
    if isinstance(names, str):
        REGION[names] = left, top, 512
    else:
        for quarter, name in enumerate(names):
            REGION[name] = left + quarter % 2 * 256, top + quarter // 2 * 256, 256


def uv(name, u, v):
    x, y, n = REGION[name]
    return ((x + BLEED + u * (n - 2 * BLEED)) / SIZE,
            1 - (y + BLEED + (1 - v) * (n - 2 * BLEED)) / SIZE)


def make_maps():
    import numpy as np
    from PIL import Image, ImageFilter

    rng = np.random.default_rng(2171120)

    def samples(path):
        with Image.open(path) as image:
            img = image.convert("RGB")
        w, h = img.width // 2, img.height // 2
        return [img.crop((i*w+7, j*h+7, (i+1)*w-7, (j+1)*h-7)) for j in range(2) for i in range(2)]

    stone, slate, _, copper = samples(ROOT / "art/source/gothic-royal-city/王城材质母图.png")
    oak, _, cut, split = samples(ROOT / "art/source/lumber-harbor/木料母图.png")
    # 底片、干燥中间色、纹理对比度、表面光滑度。
    palette = {
        "stone": (stone,(157,146,124),.63,.22), "honey": (stone,(168,152,124),.61,.24),
        "grey": (stone,(142,139,126),.63,.24), "dressed": (stone,(191,176,147),.49,.30),
        "slate": (slate,(65,76,84),.63,.32), "blue": (slate,(73,88,98),.57,.37),
        "heather": (slate,(85,85,86),.58,.32), "tide": (stone,(77,87,73),.79,.43),
        "salt": (stone,(126,130,108),.59,.31), "mortar": (stone,(114,106,89),.52,.13),
        "glass": (slate,(48,73,68),.32,.64), "tar": (oak,(67,49,36),.67,.25),
        "shadow": (stone,(72,65,51),.57,.15), "plaque": (oak,(42,59,53),.45,.34),
        "oak": (oak,(113,79,49),.83,.32), "wall": (split,(142,111,77),.74,.23),
        "dock": (oak,(150,129,101),.80,.28), "agedcut": (cut,(139,108,73),.73,.23),
        "canvas": (None,(199,170,121),.54,.16), "redFlax": (None,(183,153,104),.53,.15),
        "blueFlax": (None,(209,185,143),.55,.17), "wet": (oak,(128,111,83),.64,.30),
        "door": (oak,(130,88,50),.79,.27), "iron": (copper,(74,69,57),.51,.44),
        "brass": (copper,(165,122,62),.48,.54), "gilt": (copper,(235,187,91),.31,.49),
        "grain": (None,(218,164,76),.52,.28), "rope": (None,(155,120,76),.53,.20),
        "willow": (oak,(159,107,55),.65,.32), "husk": (None,(198,150,74),.52,.30),
        "ivory": (stone,(245,219,156),.20,.46),
    }
    maps = {k: Image.new(mode, (SIZE,SIZE)) for k, mode in
            (("albedo","RGB"), ("normal","RGB"), ("finish","RGBA"))}
    for number,(name,(left,top,n)) in enumerate(REGION.items()):
        src, tint, contrast, gloss = palette[name]
        tint = np.array(tint) / 255
        y, x = (np.indices((n,n))+.5)/n

        def cloud(w,h):
            patch = Image.fromarray(rng.integers(0,256,(h,w),dtype=np.uint8))
            return np.asarray(patch.resize((n,n),Image.Resampling.BICUBIC),np.float32)/255-.5

        broad, freckle = cloud(8,11), cloud(85,78)
        fine = rng.normal(0,.016,(n,n))
        if src is not None:
            swatch = src.resize((n,n),Image.Resampling.LANCZOS)
            rgb0 = np.roll(np.asarray(swatch,np.float32)/255,(11*number,33*number),axis=(0,1))
            lum = rgb0 @ [.27,.57,.16]
            gain = np.clip(lum/np.median(lum)-1,-.78,.98)
        else:
            gain = .48*broad+.12*freckle
        rgb = tint * (1+contrast*gain[:,:,None])
        height = .37*gain+fine
        smooth = gloss+.09*broad-.048*gain
        metal = np.zeros_like(gain)
        if src is stone:
            pits = np.clip((freckle+.44*cloud(n//3,n//3)-.29)*2.5,0,.9)
            height -= .17*pits
            rgb -= (.057*pits)[:,:,None]
            if name in ("salt","tide"):
                algae = np.clip((broad+.60*gain+.07)*1.21,0,.57)
                rgb = rgb*(1-algae[:,:,None])+np.array([.25,.30,.16])*algae[:,:,None]
                rgb += np.maximum(0,freckle+.42*broad-.22)[:,:,None]*.19
                smooth += .18*algae
            if name=="ivory":
                # 比例字表面是抛光浅色嵌料，细孔只在逆光下轻微可见。
                height=.042*gain+.013*freckle+.002*fine
                rgb=tint*(1+.12*gain[:,:,None])
                smooth=.44+.05*broad
        elif src is slate and name!="glass":
            cleavage = np.sin(39*y+9*x+2.3*np.sin(7.6*x)+1.2*cloud(8,8))
            flakes = np.exp(-((cleavage-.22)/.10)**2)*np.clip(cloud(29,19)+.24,0,.62)
            height += .14*flakes+.040*cloud(38,97)
            rgb += (.083*flakes+.026*broad)[:,:,None]
            smooth -= .17*flakes
        elif name=="glass":
            cast = np.sin(4.72*(np.floor(5*x+8*y)+13*np.floor(5*x-8*y)))
            rgb += cast[:,:,None]*np.array([.034,.024,-.018])
            height = .06*cloud(8,12)
        elif name in ("iron","brass","gilt"):
            tarnish = np.clip((broad+.38*freckle+.07)*1.33,0,.65)
            patina = [.32,.21,.12] if name=="iron" else [.32,.30,.18]
            rgb = rgb*(1-tarnish[:,:,None])+np.array(patina)*tarnish[:,:,None]
            metal = (.80 if name=="gilt" else .87)-.65*tarnish
            smooth -= .24*tarnish
            height = .15*gain-.07*freckle+.13*cloud(26,36)
        elif name in ("canvas","redFlax","blueFlax"):
            # 经纬交替浮沉，麻节改变纱粗。褪色颜料渗入纱线，布袋鼓起时经纬随 UV 弯折。
            warp = 68*x+.32*cloud(14,8)+.14*np.sin(13*y+.8*np.sin(9*x))
            weft = 49*y+.28*cloud(9,19)+.11*np.sin(15*x)
            parity = np.cos(np.pi*np.floor(warp))*np.cos(np.pi*np.floor(weft))
            wa = np.sin(np.pi*warp)**2
            we = np.sin(np.pi*weft)**2
            weave = np.maximum(wa*(.69+.23*parity),we*(.73-.22*parity))
            thread = .045*np.sin(2*np.pi*(x*182+y*27))+.075*freckle
            dirt = np.clip(.16+broad*.38+(y-.69)*.31,0,.42)
            rgb *= (1+.36*(weave-.61)+thread-dirt*.37)[:,:,None]
            rgb += (.024*cloud(23,128))[:,:,None]
            if name!="canvas":
                band = np.minimum(np.abs(x-.30),np.abs(x-.39))
                dye = np.clip((.014-band)*105,0,1)*np.clip(.84-.26*wa+.30*broad,0,1)
                rgb = rgb*(1-dye[:,:,None])+np.array([.43,.225,.139] if name=="redFlax" else [.225,.30,.30])*dye[:,:,None]
            height = .21*weave+.07*freckle+.046*thread+.034*broad
            smooth = .145+.07*weave+.05*freckle
        elif name in ("rope","husk","grain"):
            phase = (x*15-y*7) if name=="rope" else (x*18+np.sin(y*4)*.64)
            ribs = np.cos(2*np.pi*phase+cloud(12,15)*.54)
            rgb *= (1+.093*ribs+.11*broad+.043*freckle)[:,:,None]
            height += .101*ribs+.04*freckle
        elif src in (oak,split,cut):
            softened = np.asarray(swatch.filter(ImageFilter.GaussianBlur(2)),np.float32)/255
            diff = (rgb0-np.roll(softened,(11*number,33*number),axis=(0,1))).mean(axis=2)
            height += .36*diff+.030*cloud(13,123)
            if name=="wet":
                dry = np.clip((x-.29)*2.8+.42*broad,0,1)
                rgb *= (.49+.51*dry)[:,:,None]
                rgb += (1-dry)[:,:,None]*np.array([.006,.033,.009])
                smooth += .29*(1-dry)
            if name in ("dock","wall"):
                silver = np.maximum(0,cloud(11,69)+broad*.46)*.24
                rgb += silver[:,:,None]*np.array([.25,.27,.25])
            if name=="plaque":
                height=.13*gain+.08*diff+.012*cloud(19,93)
        dy,dx = np.gradient(height)
        normal = np.stack((-dx*2.65,dy*2.65,np.ones_like(dx)),axis=-1)
        normal /= np.linalg.norm(normal,axis=2,keepdims=True)
        finish = np.stack((metal,np.zeros_like(metal),np.zeros_like(metal),smooth),axis=-1)
        for key,data in (("albedo",rgb),("normal",normal*.5+.5),("finish",finish)):
            pic = Image.fromarray(np.uint8(np.clip(data,0,1)*255+.5))
            core = np.asarray(pic.resize((n-2*BLEED,n-2*BLEED),Image.Resampling.LANCZOS))
            maps[key].paste(Image.fromarray(np.pad(core,((BLEED,BLEED),(BLEED,BLEED),(0,0)),mode="edge")),(left,top))
    ART.mkdir(parents=True,exist_ok=True)
    for name,img in maps.items():
        img.save(ART / f"grain-{name}.png", compress_level=7)


if __name__=="__main__":
    make_maps()

