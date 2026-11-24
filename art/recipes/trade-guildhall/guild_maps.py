"""商会构件图集。石、瓦、木与铜沿用同城取样，钟面和商货另行制版。"""
from math import cos, pi, sin
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/ExpansionPieces/v001"
COLORS = ART.parents[1] / "PlayerColors"
CITY = ART.parents[1] / "NaturalPieces/v001"
SOURCE = ROOT / "art/source/trade-guildhall"
PREVIEWS = ROOT / "art/previews/trade-guildhall"
BLEND = SOURCE / "哥特商会之都.blend"
SPAN = .02714
REGIONS = {
    "ashlar": (0,0,1024), "slate": (1024,0,1024),
    "limestone": (0,1024,512), "carving": (512,1024,512),
    "foot": (1024,1024,512), "glass": (1536,1024,512),
    "oak": (0,1536,512), "bronze": (512,1536,512),
    "lead": (1024,1536,256), "shadow": (1280,1536,256),
    "linen": (1024,1792,256), "selvedge": (1280,1792,256), "dial": (1536,1536,512),
}


def clock_print(size):
    from PIL import Image, ImageDraw, ImageFont

    # 字模先在二倍分辨率排版。罗马时标沿表盘径向，四点沿用钟匠的 IIII 写法。
    scale = 2
    n = size * scale
    ink = Image.new("L",(n,n))
    draw = ImageDraw.Draw(ink)
    center = (n-1)/2
    for radius, weight in ((.477,.003),(.464,.002),(.352,.003),(.343,.0017),(.100,.003)):
        r = n*radius
        draw.ellipse((center-r,center-r,center+r,center+r),outline=194,width=max(1,round(n*weight)))
    for i in range(60):
        a = i*pi/30
        lo = .434 if i%5==0 else .451
        draw.line([(center+n*r*sin(a),center-n*r*cos(a)) for r in (lo,.464)],
                  fill=225,width=round(n*(.005 if i%5==0 else .002)))
    font = ImageFont.truetype("times.ttf",round(n*.074))
    for i, label in enumerate(("XII","I","II","III","IIII","V","VI","VII","VIII","IX","X","XI")):
        letter = Image.new("L",(round(n*.23),round(n*.14)))
        pen = ImageDraw.Draw(letter)
        pen.text((letter.width/2,letter.height/2),label,font=font,anchor="mm",fill=248)
        angle = i*pi/6
        letter = letter.rotate(-30*i if i<4 or i>8 else 180-30*i,
                               Image.Resampling.BICUBIC,expand=True)
        pos = (round(center+n*.400*sin(angle)-letter.width/2),
               round(center-n*.400*cos(angle)-letter.height/2))
        ink.paste(letter,pos,letter)
    for i in range(8):
        a = i*pi/4
        def point(r,off):
            return (center+n*(r*sin(a)+off*cos(a)),center+n*(-r*cos(a)+off*sin(a)))
        draw.polygon([point(.12,0),point(.175,.010),point(.26 if i%2==0 else .21,0),
                      point(.175,-.010)],outline=145,width=2)
    return ink.resize((size,size),Image.Resampling.LANCZOS)


def make_maps():
    import numpy as np
    from PIL import Image, ImageOps

    ART.mkdir(parents=True,exist_ok=True)
    rng = np.random.default_rng(21171907)
    master = Image.open(CITY/"city-surface.png").convert("RGB")
    relief = Image.open(CITY/"city-normal.png").convert("RGB")
    finish = Image.open(CITY/"city-finish.png").convert("RGBA")
    result = master.copy()
    normals = relief.copy()
    finish = Image.merge("RGBA",[v.resize(master.size,Image.Resampling.NEAREST) for v in finish.split()])
    original_finish = finish.copy()
    originals = {"lead":(1024,1536,512),"shadow":(1536,1536,512)}
    def bitmap(values):
        return Image.fromarray(np.uint8(np.clip(values,0,1)*255+.5))
    for label,(left,top,side) in REGIONS.items():
        if label in ("linen","selvedge","dial"):
            continue
        x,y,width = originals.get(label,(left,top,side))
        box = (x,y,x+width,y+width)
        color = master.crop(box).resize((side,side),Image.Resampling.LANCZOS)
        tint = {"ashlar":(1.035,1.033,1.015),"foot":(.98,.99,.97),
                "slate":(.779,.818,.848),"carving":(1.035,1.040,1.045),
                "oak":(.90,.87,.85),"bronze":(1.07,1.07,.99)}.get(label,(1,1,1))
        result.paste(bitmap(np.asarray(color)/255*tint),(left,top))
        normals.paste(relief.crop(box).resize((side,side),Image.Resampling.LANCZOS),(left,top))
        finish.paste(original_finish.crop(box).resize((side,side),Image.Resampling.NEAREST),(left,top))
    for label in ("linen","selvedge","dial"):
        left,top,n = REGIONS[label]
        y,x = (np.indices((n,n))+.5)/n
        grit = rng.normal(0,1,(n,n))
        broad = np.asarray(bitmap(rng.uniform(.12,.88,(13,13))).resize((n,n),Image.Resampling.BICUBIC))/255-.5
        if label=="dial":
            radius = np.hypot(x-.5,y-.5)
            printed = np.asarray(clock_print(n))/255
            age = .30*np.exp(-((radius-.484)/.030)**2)+.06*np.maximum(np.sin(x*59+y*33),0)**10
            blank = np.array((.860,.804,.641))*(1+.11*broad+.012*grit-age)[:,:,None]
            rgb = blank*(1-printed[:,:,None])+np.array((.15,.147,.122))*printed[:,:,None]
            height = .037*grit+.045*broad-.27*printed
            metal = .07+.32*printed
            gloss = .42+.10*broad-.07*printed
        else:
            # 亚麻经纬隔根交织，卷端切面对齐布边卷入的同一条螺线。
            warp = np.cos(2*pi*(x*72+.045*np.sin(2*pi*y*11)))
            weft = np.cos(2*pi*(y*76+.035*np.sin(2*pi*x*7)))
            order = np.sin(pi*x*72)*np.sin(pi*y*76)
            weave = np.where(order>0,.70*warp+.30*weft,.34*warp+.66*weft)
            if label=="linen":
                stripe = (np.exp(-((x-.080)/.009)**2)+np.exp(-((x-.100)/.004)**2)+
                          np.exp(-((x-.905)/.012)**2)+np.exp(-((x-.931)/.004)**2))
                tone = np.array((.72,.60,.42))
                rgb = tone*(1+.17*weave+.17*broad+.018*grit)[:,:,None]
                rgb *= (1-stripe[:,:,None]*np.array((.53,.33,.30)))
                height = .13*weave+.035*grit+.026*stripe
            else:
                xx,yy = 2*x-1,2*y-1
                radius = np.hypot(xx,yy)
                turn = radius*11-np.arctan2(yy,xx)/(2*pi)+.08*broad
                lip = np.exp(-(np.sin(pi*turn)/.265)**2)
                core = np.clip(radius*10,0,1)
                rgb = np.array((.68,.56,.38))*(.85+.17*core+.12*weave-.31*lip+.10*broad)[:,:,None]
                height = .09*weave-.42*lip+.05*grit
            metal,gloss = np.zeros_like(x),.15+.035*weave
        dy,dx = np.gradient(height)
        direction = np.stack((-1.6*dx,1.6*dy,np.ones_like(x)),axis=-1)
        direction /= np.linalg.norm(direction,axis=2,keepdims=True)
        packed = np.stack((metal,np.zeros_like(x),np.zeros_like(x),gloss),axis=2)
        result.paste(bitmap(rgb),(left,top))
        normals.paste(bitmap(direction*.5+.5),(left,top))
        finish.paste(bitmap(packed),(left,top))
    result.save(ART/"metro-trade-surface.png")
    normals.save(ART/"metro-trade-normal.png")
    Image.merge("RGBA",[v.resize((512,512),Image.Resampling.BOX) for v in finish.split()]).save(
        ART/"metro-trade-finish.png")
    # 限定在板岩紫外分区。盐霜、破边和石墨色瓦唇保留自然色。
    mask = Image.open(COLORS/"city-mask.png").convert("L")
    ImageOps.autocontrast(mask,cutoff=0).point(lambda v:round(v*.73)).save(COLORS/"metro-trade-mask.png")


if __name__=="__main__":
    make_maps()
