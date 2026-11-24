"""木材港共用图集。各木面以顺纹为 U，端面以髓心为原点，数据图采用线性采样。"""
from pathlib import Path
import math


ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art/source/lumber-harbor"
PREVIEW = ROOT / "art/previews/lumber-harbor"
ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/LumberHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
SIZE, PAD = 512, 10
TILES = ("dock", "oak", "wall", "trim", "cedar", "shake", "bark", "cut",
         "door", "wet", "tar", "rope", "iron", "shadow", "sign", "agedcut")
# 旧港口配方也按这十六格展开木作。石工、瓦作和饰件使用各自格内带独立扩边的色样。
PATCHES = {
    "slate": ("cedar",0), "blueSlate": ("cedar",1), "heather": ("cedar",2), "ridge": ("cedar",3),
    "ashlar": ("shake",0), "greyStone": ("shake",1), "buffStone": ("shake",2), "tideStone": ("shake",3),
    "dressed": ("sign",0), "tablet": ("sign",1), "brass": ("sign",2), "glass": ("sign",3),
    "mortar": ("shadow",None)
}


def atlas_uv(tile, u, v):
    if tile in PATCHES:
        tile,quarter=PATCHES[tile]
        if quarter is not None:
            side=(SIZE-2*PAD)//2
            u=(quarter%2+(7+u*(side-14))/side)*.5
            v=(1-quarter//2+(7+v*(side-14))/side)*.5
    row, col = divmod(TILES.index(tile), 4)
    return ((col + (PAD + u*(SIZE-2*PAD))/SIZE)/4,
            1 - (row + 1 - (PAD + v*(SIZE-2*PAD))/SIZE)/4)


def make_maps():
    import numpy as np
    from PIL import Image, ImageFilter
    ART.mkdir(parents=True, exist_ok=True)
    master = Image.open(SOURCE/"木料母图.png").convert("RGB")
    w, h = master.size
    def sample(col, row):
        x, y = col*w//2, row*h//2
        crop = master.crop((x+4,y+4,x+w//2-4,y+h//2-4))
        return np.asarray(crop.resize((SIZE,SIZE),Image.Resampling.LANCZOS),np.float32)/255
    oak, bark, cut, cedar = sample(0,0), sample(1,0), sample(0,1), sample(1,1)
    # 树皮沿纵向生长，旋转底片后，所有杆件都可以沿 U 展开。
    bark = np.rot90(bark)
    y, x = np.mgrid[0:1:complex(SIZE),0:1:complex(SIZE)].astype(np.float32)
    rng = np.random.default_rng(21720)
    def noise(nx, ny):
        pic = Image.fromarray(rng.integers(0,256,(ny,nx),dtype=np.uint8))
        return np.asarray(pic.resize((SIZE,SIZE),Image.Resampling.BICUBIC),np.float32)/255-.5
    specs = {
        "dock":(oak,(153,113,76),.68), "oak":(oak,(139,86,46),.71),
        "wall":(cedar,(106,66,40),.76), "trim":(oak,(168,108,58),.64),
        "cedar":(cedar,(134,61,34),.74), "shake":(cedar,(108,49,29),.79),
        "bark":(bark,(108,65,38),.86), "cut":(cut,(218,162,92),.73),
        "door":(oak,(150,91,47),.72), "wet":(oak,(78,77,49),.46),
        "tar":(cedar,(84,55,38),.59), "rope":(oak,(181,139,89),.89),
        "iron":(bark,(72,66,58),.48), "shadow":(cedar,(61,40,29),.84),
        "sign":(oak,(201,150,90),.64), "agedcut":(cut,(162,119,75),.71)
    }
    atlas = {key:Image.new(mode,(SIZE*4,SIZE*4)) for key,mode in
             (("albedo","RGB"),("normal","RGB"),("finish","RGBA"))}
    for index, name in enumerate(TILES):
        source, tint, roughness = specs[name]
        fine = rng.normal(0,.012,(SIZE,SIZE)).astype(np.float32)
        grain = source @ np.array([.28,.55,.17],np.float32)
        middle = np.median(grain)
        contrast = 1.32 if name in ("cut","agedcut") else 1.06
        tone = .90*contrast*(grain-middle)/max(.23,middle)
        rgb = (1+tone[:,:,None])*np.array(tint)/255
        rgb += (source-source.mean(axis=2,keepdims=True) -
                (source-source.mean(axis=2,keepdims=True)).mean(axis=(0,1)))*.22
        broad = noise(16,18)
        height = grain*.43 + fine*.47
        rough = roughness + .13*broad - (grain-middle)*.18
        metal = np.zeros_like(grain)
        if name in ("cut","agedcut"):
            height = grain*.38 + .009*np.sin(y*830+x*17)
            soft = np.asarray(Image.fromarray(np.uint8(grain*255)).filter(ImageFilter.GaussianBlur(2.1)),np.float32)/255
            rgb += .70*(grain-soft)[:,:,None]
            # 保留底片髓心的位置，以缓慢变化的晚材带加强小比例下的年轮读形。
            angle=np.arctan2(y-.46,x-.70)
            radius=np.hypot((x-.70)*.97,(y-.46)*1.06)
            growth=radius*(1+.026*np.sin(3*angle)+.032*np.sin(angle+.6))
            phase=growth*29+.18*np.sin(growth*24+np.sin(angle))+.065*broad
            latewood=np.exp(-((np.mod(phase,1)-.72)/.14)**2)
            rgb *= (1-.19*latewood)[:,:,None]
            height -= .042*latewood
        if name == "bark":
            # 老松的鳞片凸部经盐雾漂灰，纵沟留下褐色软木层。
            silver=np.clip(.14+(grain-middle)*1.04+.28*broad,0,.46)
            rgb=rgb*(1-silver[:,:,None])+np.array([.47,.42,.34])*silver[:,:,None]
            height = grain*.78 + .03*noise(28,48)
        if name=="cut":
            # 宽窄不同的生长带在棋盘视距仍可见，近看保留锯痕、晚材纤维和径向干缩裂纹。
            heart=np.hypot((x-.70)*.97,(y-.46)*1.06)
            ang=np.arctan2(y-.46,x-.70)
            growth=heart*(1+.031*np.sin(3*ang+.7)+.048*np.cos(ang))
            phase=growth*17.6+.13*np.sin(24*growth)+.07*np.sin(8*ang)*growth
            band=np.exp(-(np.sin(np.pi*phase)/.47)**2)
            early=np.clip(.18+.82*growth,0,.7)
            rgb=(rgb*(.89+.13*early[:,:,None]))*(1-band[:,:,None]*np.array([.43,.52,.59]))
            height-=.105*band
            for heading,start,length in ((-2.15,.27,.37),(1.72,.14,.46),(-.19,.19,.3)):
                drift=heading+.13*growth+.032*np.sin(35*growth)
                distance=np.abs(np.sin(ang-drift))*heart
                check=np.exp(-(distance/.0039)**2)*(np.cos(ang-drift)>0)*np.clip((heart-start)*28,0,1)*np.clip((start+length-heart)*28,0,1)
                rgb*=1-.60*check[:,:,None]
                height-=.09*check
        if name == "wet":
            moss = np.clip((.24+broad+noise(9,86)*.4)*.67,0,.42)
            rgb = rgb*(1-moss[:,:,None])+np.array([.25,.29,.12])*moss[:,:,None]
            dry = np.clip((x-.26)*3.45+.43*broad+.18*noise(26,44),0,1)
            rgb = rgb*(1-dry[:,:,None]) + (1+tone[:,:,None])*np.array([153,113,76])/255*dry[:,:,None]
            rough += .22*dry
        if name == "iron":
            hammer = noise(31,42)
            pits = np.clip((noise(110,116)-.24)*3,0,1)
            oxide = np.clip((broad-.13)*2.9+pits*.49,0,.85)
            rgb = np.array([.27,.264,.24])*(1+hammer[:,:,None]*.56) + fine[:,:,None]
            rgb = rgb*(1-oxide[:,:,None])+np.array([.31,.177,.097])*oxide[:,:,None]
            height = .21*hammer - .13*pits + fine
            rough = .39 + .36*oxide + .14*hammer
            metal = .81 - .68*oxide
        if name == "rope":
            twist = np.cos(2*math.pi*(x*21-y*9 + .1*noise(12,12)))
            fibres = np.sin(2*math.pi*(x*96-y*37))*.04 + fine
            rgb = np.array(tint)/255 * (1 + .10*twist + fibres + .16*broad)[:,:,None]
            height = .11*twist + fibres*.46
        dy, dx = np.gradient(height)
        strength = 3.6 if name=="bark" else 2.35
        normal = np.stack((-dx*strength,dy*strength,np.ones_like(dx)),axis=-1)
        normal /= np.linalg.norm(normal,axis=-1,keepdims=True)
        finish = np.stack((metal, np.zeros_like(metal),np.zeros_like(metal),1-rough),axis=-1)
        row,col = divmod(index,4)
        for key,arr in (("albedo",rgb),("normal",normal*.5+.5),("finish",finish)):
            pic = Image.fromarray(np.uint8(np.clip(arr,0,1)*255+.5))
            # 保护边界 mip；有符号的数据通道必须与颜色使用同一个内框。
            inside = pic.resize((SIZE-2*PAD,SIZE-2*PAD),Image.Resampling.LANCZOS)
            padded = np.pad(np.array(inside),((PAD,PAD),(PAD,PAD),(0,0)),mode="edge")
            atlas[key].paste(Image.fromarray(padded),(col*SIZE,row*SIZE))
    quay_surfaces(atlas, oak)
    for kind, pic in atlas.items():
        pic.save(ART/("lumber-"+kind+".png"))


def quay_surfaces(atlas, oak):
    master=Image.open(ROOT/"art/source/gothic-royal-city/王城材质母图.png").convert("RGB")
    w,h=master.width//2,master.height//2
    def sample(col,row):
        area=(col*w+7,row*h+7,(col+1)*w-7,(row+1)*h-7)
        return np.asarray(master.crop(area).resize((SIZE,SIZE),Image.Resampling.LANCZOS),np.float32)/255
    stone,slate,metal=sample(0,0),sample(1,0),sample(1,1)
    specs={
        "ashlar":(stone,(150,144,126),.78), "greyStone":(stone,(137,139,132),.80),
        "buffStone":(stone,(161,149,126),.83), "tideStone":(stone,(91,96,80),.57),
        "dressed":(stone,(193,180,151),.74), "mortar":(stone,(109,103,88),.93),
        "slate":(slate,(70,82,87),.65), "blueSlate":(slate,(74,90,100),.60),
        "heather":(slate,(87,89,92),.66), "ridge":(slate,(95,97,95),.58),
        "tablet":(oak,(54,66,62),.65), "brass":(metal,(228,188,114),.34),
        "glass":(slate,(42,67,65),.22)
    }
    rng=np.random.default_rng(21172026)
    yy,xx=np.mgrid[0:1:complex(SIZE),0:1:complex(SIZE)]
    def field(nx,ny):
        image=Image.fromarray(rng.integers(0,256,(ny,nx),dtype=np.uint8))
        return np.asarray(image.resize((SIZE,SIZE),Image.Resampling.BICUBIC),np.float32)/255-.5
    for name,(source,tint,roughness) in specs.items():
        lum=np.roll(source,tuple(rng.integers(0,SIZE,2)),axis=(0,1)) @ [.27,.57,.16]
        contrast=np.clip(lum/np.median(lum)-1,-.83,1.12)
        patina=field(12,15)
        fine=field(160,165)
        rgb=np.array(tint)/255*(1+.49*contrast[:,:,None])
        height=.37*contrast+.048*fine
        rough=roughness+.10*patina-.08*contrast
        metallic=np.zeros_like(lum)
        if source is stone:
            pores=np.clip((fine+.46*field(44,47)-.30)*2.6,0,.90)
            quartz=np.maximum(0,field(209,210)-.25)
            rgb=rgb*(1-.32*pores[:,:,None])+quartz[:,:,None]*.22
            height+=.17*quartz-.28*pores
            if name=="dressed":
                comb=np.sin(132*xx+yy*265+2*field(22,17))
                height+=.040*comb*np.maximum(0,patina+.28)
            if name=="tideStone":
                algae=np.clip((patina+.35*field(34,39)+.20)*1.1,0,.65)
                rgb=rgb*(1-algae[:,:,None])+np.array([.20,.25,.155])*algae[:,:,None]
                rough-=.22*algae
        elif source is slate and name!="glass":
            fissure=np.sin(yy*47+xx*17+1.5*np.sin(7*xx)+.8*field(9,16))
            lamina=np.exp(-((fissure-.17)/.10)**2)*np.clip(field(15,21)+.25,0,.8)
            scale=np.clip((patina+.40*field(49,47)-.28)*.62,0,.25)
            rgb+=lamina[:,:,None]*.069+scale[:,:,None]*np.array([.16,.15,.09])
            height+=.15*lamina+.038*field(24,73)
            rough+=.11*lamina
        elif name=="brass":
            oxide=np.clip((patina+.36*fine+.02)*1.28,0,.61)
            rgb=rgb*(1-oxide[:,:,None])+np.array([.31,.32,.21])*oxide[:,:,None]
            metallic=.72-.47*oxide
            height=.12*contrast+.055*fine
            rough+=.30*oxide
        elif name=="glass":
            height=.073*field(7,9)
            rgb+=patina[:,:,None]*np.array([.05,.12,.10])
        elif name=="tablet":
            height=.28*contrast
            rgb+=patina[:,:,None]*.026
        dy,dx=np.gradient(height)
        norm=np.stack((-dx*3.5,dy*3.5,np.ones_like(dx)),axis=-1)
        norm/=np.linalg.norm(norm,axis=-1,keepdims=True)
        finish=np.stack((metallic,np.zeros_like(lum),np.zeros_like(lum),1-rough),axis=-1)
        slot,quarter=PATCHES[name]
        row,col=divmod(TILES.index(slot),4)
        side,pad=(SIZE,PAD) if quarter is None else ((SIZE-2*PAD)//2,7)
        x=col*SIZE+(0 if quarter is None else PAD+quarter%2*side)
        y=row*SIZE+(0 if quarter is None else PAD+quarter//2*side)
        for key,arr in (("albedo",rgb),("normal",norm*.5+.5),("finish",finish)):
            img=Image.fromarray(np.uint8(np.clip(arr,0,1)*255+.5))
            inner=img.resize((side-2*pad,side-2*pad),Image.Resampling.LANCZOS)
            expanded=np.pad(np.asarray(inner),((pad,pad),(pad,pad),(0,0)),mode="edge")
            atlas[key].paste(Image.fromarray(expanded),(x,y))


if __name__ == "__main__":
    make_maps()
