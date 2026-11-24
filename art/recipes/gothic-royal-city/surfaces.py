"""王城的米制参考路径、定向材质分区与同源 PBR 贴图。行号自图像顶边算起。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/NaturalPieces/v001"
SOURCE = ROOT / "art/source/gothic-royal-city"
PREVIEWS = ROOT / "art/previews/gothic-royal-city"
BLEND = SOURCE / "哥特王城.blend"
REGIONS = {
    "ashlar": (0, 0, 1024), "slate": (1024, 0, 1024),
    "limestone": (0, 1024, 512), "carving": (512, 1024, 512),
    "foot": (1024, 1024, 512), "glass": (1536, 1024, 512),
    "oak": (0, 1536, 512), "bronze": (512, 1536, 512),
    "lead": (1024, 1536, 512), "shadow": (1536, 1536, 512),
}
SEATS = {"red": (.85,.24,.16), "blue": (.23,.56,.78),
         "orange": (.94,.74,.30), "beige": (.79,.79,.72)}


def make_maps():
    import numpy as np
    from PIL import Image

    ART.mkdir(parents=True,exist_ok=True)
    (ART.parents[1]/"PlayerColors").mkdir(parents=True,exist_ok=True)
    master = Image.open(SOURCE / "王城材质母图.png").convert("RGB")
    half_w, half_h = master.width//2, master.height//2
    samples = [master.crop((x*half_w+7, y*half_h+7, (x+1)*half_w-7, (y+1)*half_h-7))
               for y in range(2) for x in range(2)]
    base = Image.new("RGB", (2048,2048))
    normal = base.copy()
    finish = Image.new("RGBA", base.size)
    mask = Image.new("L",base.size)
    rng = np.random.default_rng(21171902)
    # 色样、固有色中值、表面亮度起伏、切线凹凸、金属度、光滑度。
    settings = {
        "ashlar": (0,(181,171,148),.30,2.6,0,.24),
        # 屋瓦先按村庄陶瓦的中间调取值，再由席位釉色覆盖。
        "slate": (1,(145,141,135),.33,2.0,.02,.30),
        "limestone": (0,(163,151,127),.32,2.3,0,.21),
        "carving": (0,(201,190,165),.24,1.7,0,.29),
        "foot": (0,(137,133,116),.35,2.8,0,.19),
        "glass": (1,(52,74,72),.18,.75,.08,.77),
        "oak": (2,(104,67,42),.60,2.0,0,.30),
        "bronze": (3,(145,102,55),.62,1.6,.76,.50),
        "lead": (1,(67,71,72),.36,1.2,.66,.39),
        "shadow": (0,(93,87,73),.34,2.2,0,.20),
    }
    def image(a):
        return Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5))

    for index,(name,(left,top,side)) in enumerate(REGIONS.items()):
        source, tone, contrast, relief, metal, gloss = settings[name]
        sample = np.asarray(samples[source].resize((side,side),Image.Resampling.LANCZOS),dtype=float)/255
        if name == "oak":
            sample = sample.transpose(1,0,2)
        sample = np.roll(sample,(index*43,index*57),axis=(0,1))
        y,x = (np.indices((side,side))+.5)/side
        lum = sample @ [.2126,.7152,.0722]
        grain = (lum / np.median(lum)-1).clip(-1,1.8)
        chroma = sample-lum[:,:,None]
        chroma -= np.median(chroma,axis=(0,1))
        rgb = (np.array(tone)/255)*(1+contrast*grain[:,:,None]) + .21*chroma
        height = .42*grain
        smoothness = gloss-.04*grain
        if name in ("ashlar","slate","foot"):
            # 屋面每 0.17 建筑单位一道搭接；灰缝、破边和岩片起伏也进入法线。
            rows,cols = {"ashlar":(19,11),"slate":(36,22),"foot":(7,5)}[name]
            yy = y*rows + .009*np.sin(x*109+2*np.sin(y*19)) + .008*grain
            course = np.floor(yy).astype(int)
            xx = x*cols + .5*(course%2) + .025*np.sin(6.283*course/rows) + .014*grain
            block = np.floor(xx).astype(int)
            fx,fy = xx%1, yy%1
            dye = rng.normal(0,1,(rows,cols,3))
            cells = dye[course%rows,block%cols]
            shade = np.clip(cells[:,:,0],-2,2)
            joint = np.minimum(np.minimum(fx,1-fx)*rows/cols,np.minimum(fy,1-fy))
            if name == "slate":
                joint = np.minimum(np.minimum(fx,1-fx)*rows/cols,1-fy)
                relief_edge = np.clip(joint/.052,0,1)
                lip = np.exp(-((fy-.94)/.032)**2)
                rgb *= (1+.072*shade-.22*np.exp(-fy*7)-.34*(1-relief_edge)+.12*lip)[:,:,None]
                rgb += .006*cells
                height += .34*fy + .37*relief_edge + .11*lip
                smoothness += .035*shade - .09*(1-relief_edge)
                patina = np.clip((grain-.05)*.28,0,.35)
                mask.paste(image(np.clip(.90-.04*abs(shade)-patina,.53,.90)),(left,top))
            else:
                edge = np.clip(joint/(.055 if name=="ashlar" else .045),0,1)
                eased = edge*edge*(3-2*edge)
                rgb *= (1+.063*shade)[:,:,None]
                rgb += .012*cells
                mortar = np.array((.47,.444,.388)) if name=="ashlar" else np.array((.385,.373,.32))
                rgb = rgb*(.32+.68*eased[:,:,None]) + mortar*(.68-.68*eased[:,:,None])
                rgb -= (.083*np.exp(-((joint-.025)/.025)**2))[:,:,None]
                height += .58*eased + .08*shade
                smoothness -= .06*(1-eased)
        elif name=="glass":
            # 小片茬口玻璃手工铅接，菱格内有错落的钴蓝、烟绿与蜜蜡色。
            a,b = x*13 + y*26, x*13 - y*26
            ia,ib = np.floor(a).astype(int),np.floor(b).astype(int)
            fa,fb = a%1,b%1
            seam = np.minimum.reduce([fa,1-fa,fb,1-fb])
            lead = np.clip(1-seam/.091,0,1)
            choice = (ia*7+ib*11)%23
            palette = np.array([(61,79,80),(57,72,80),(73,89,87),(63,76,82),
                                (85,94,87),(50,66,76),(110,92,70),(88,72,69)])/255
            glass = palette[np.where(choice<21,choice%6,6+choice%2)]
            rgb = glass*(1+.09*grain[:,:,None]+.04*np.sin(37*x+18*y)[:,:,None])
            rgb = rgb*(1-lead[:,:,None]) + np.array((.15,.16,.16))*lead[:,:,None]
            height = .075*grain + .55*lead
            smoothness = .74-.35*lead-.04*grain
        elif name=="bronze":
            patina = np.clip((sample[:,:,1]-sample[:,:,0]+.052)*4,0,1)
            rgb += patina[:,:,None]*np.array((-.10,.058,.045))
            smoothness -= .19*patina
        elif name in ("carving","limestone"):
            chisel = np.sin((x*1.85+y*.7)*side*.55+2.2*np.sin(y*23)+2*grain)
            height += .048*chisel
            rgb += .008*chisel[:,:,None]
        dy,dx = np.gradient(height)
        vectors = np.stack((-dx*relief,dy*relief,np.ones_like(dx)),axis=-1)
        vectors /= np.linalg.norm(vectors,axis=2,keepdims=True)
        packed = np.zeros((side,side,4))
        packed[:,:,0],packed[:,:,3] = metal,smoothness
        base.paste(image(rgb),(left,top))
        normal.paste(image(vectors*.5+.5),(left,top))
        finish.paste(image(packed),(left,top))
    base.save(ART/"city-surface.png")
    normal.save(ART/"city-normal.png")
    channels = [band.resize((512,512),Image.Resampling.BOX) for band in finish.split()]
    Image.merge("RGBA",channels).save(ART/"city-finish.png")
    mask.resize((1024,1024),Image.Resampling.BOX).save(ART.parents[1]/"PlayerColors/city-mask.png")


if __name__=="__main__":
    make_maps()
