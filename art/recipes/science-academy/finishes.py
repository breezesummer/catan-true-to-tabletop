"""仪器工坊图集。刻度、星图和日晷的划线同时写入固有色、凹刻法线和粗糙度。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/ExpansionPieces/v001"
CITY = ROOT / "Unity/Catan/Assets/Art/NaturalPieces/v001"
COLORS = ROOT / "Unity/Catan/Assets/Art/PlayerColors"
SOURCE = ROOT / "art/source/science-academy"
PREVIEW = ROOT / "art/previews/science-academy"
BLEND = SOURCE / "穹顶观星学宫.blend"
COMMON = ROOT / "art/recipes/gothic-royal-city"
SIZE = 2048
# 画幅原点在左上，构件 UV 原点在左下。仪器表面的朝向由车床轴、赤道或子午圈决定。
REGIONS = {
    "copper": (0, 0, 1024, 1024), "globe": (1024, 0, 1024, 512),
    "degrees": (1024, 512, 1024, 256), "ecliptic": (1024, 768, 1024, 256),
    "brass": (0, 1024, 768, 512), "darkoak": (768,1024,256,512), "gilt": (0, 1536, 512, 512),
    "gunmetal": (512, 1536, 512, 512), "lens": (1024, 1024, 512, 512),
    "dial": (1536, 1024, 512, 512), "verdigris": (1024, 1536, 512, 512),
    "smoke": (1536, 1536, 512, 512),
}


def make_maps():
    import math
    import numpy as np
    from PIL import Image, ImageDraw, ImageFont, ImageFilter

    rng = np.random.default_rng(21171931)
    master = Image.open(ROOT / "art/source/gothic-royal-city/王城材质母图.png").convert("RGB")
    w, h = master.size
    donor = master.crop((w//2+8, h//2+8, w-8, h-8))
    maps = [Image.new(mode, (SIZE,SIZE)) for mode in ("RGB","RGB","RGBA","L")]
    # 金属固有色是反射颜色。氧化、擦亮和嵌黑的刻线分别控制金属性与高光宽度。
    settings = {
        "copper": ((83,99,97), .68,.44), "globe": ((127,102,69),.72,.45),
        "degrees": ((174,140,88),.83,.51), "ecliptic": ((140,107,65),.81,.48),
        "brass": ((150,115,66),.81,.51), "gilt": ((193,156,93),.86,.62),
        "gunmetal": ((64,62,56),.62,.38), "lens": ((43,101,117),.26,.90),
        "dial": ((164,139,92),.71,.46), "verdigris": ((87,100,86),.58,.39),
        "smoke": ((51,65,68),.08,.75), "darkoak": ((78,61,47),0,.30),
    }
    romans = ("XII","I","II","III","IV","V","VI","VII","VIII","IX","X","XI")

    def raster(a):
        return Image.fromarray((np.clip(a,0,1)*255+.5).astype("uint8"))

    def ruled(name, width, height):
        sheet=Image.new("L",(width,height))
        pen=ImageDraw.Draw(sheet)
        def line(points, value=220, breadth=1):
            pen.line([(round(x*width),round(y*height)) for x,y in points],fill=value,width=breadth)
        def label(x,y,s,size):
            pen.text((x*width,y*height),s,font=ImageFont.load_default(size=size),fill=195,anchor="mm",stroke_width=0)
        if name in ("degrees","ecliptic"):
            # 四个九十度弧段分别展开；先按真实弧长／带宽制版，刻字不会被拉成长条。
            for quarter in range(4):
                quadrant=Image.new("L",(672,96))
                d=ImageDraw.Draw(quadrant)
                for yy in (6,10,85,90):
                    d.line((0,yy,672,yy),fill=202,width=1)
                for deg in range(91):
                    u=round(deg/90*671)
                    length=21 if deg%15==0 else 13 if deg%5==0 else 5
                    d.line((u,11,u,11+length),fill=244,width=2 if deg%5==0 else 1)
                    d.line((u,84,u,84-length//2),fill=224,width=1)
                for i in range(6):
                    degree=quarter*90+i*15
                    number=str(degree) if name=="degrees" else romans[((degree//15)+1)%12]
                    d.text(((i+.50)*112,49),number,font=ImageFont.load_default(size=29),fill=247,anchor="mm")
                sheet.paste(quadrant.resize((width,height//4),Image.Resampling.LANCZOS),(0,quarter*height//4))
        if name=="globe":
            for a in range(0,361,15):
                line([(a/360,0),(a/360,1)],120,2 if a%45==0 else 1)
            for a in range(15,180,15):
                line([(0,a/180),(1,a/180)],140,2 if a in (45,90,135) else 1)
            for dz in (-.008,.008):
                line([(i/360,.5+.13*math.sin(math.radians(i))+dz) for i in range(361)],210,2)
            for x,y in [(.105,.30),(.26,.49),(.41,.66),(.59,.38),(.76,.55),(.88,.24),(.925,.72)]:
                pts=[(x+u,y+v) for u,v in ((-.042,.01),(-.02,-.055),(.012,-.043),(.04,.018),(.026,.07))]
                line(pts,215)
                for u,v in pts:
                    x0,y0=u*width,v*height
                    pen.ellipse((x0-2,y0-2,x0+2,y0+2),fill=248)
            for _ in range(380):
                u,v=rng.uniform(0,width),rng.uniform(0,height)
                r=float(rng.choice([.6,.9,1.3,1.8]))
                pen.ellipse((u-r,v-r,u+r,v+r),fill=185)
        if name=="dial":
            def polar(a,r):
                return (.5+r*math.sin(a),.5-r*math.cos(a))
            for r in (.22,.33,.375,.450,.465):
                pen.ellipse(tuple(v*width for v in (.5-r,.5-r,.5+r,.5+r)),outline=218,width=2)
            for i in range(72):
                a=i*math.tau/72
                line([polar(a,.448),polar(a,.366 if i%6==0 else .420)])
                if i%6==0:
                    line([polar(a,.057),polar(a,.30)],172)
                    label(*polar(a,.326),romans[i//6],20)
            for i in range(8):
                a=i*math.tau/8
                line([polar(a-.28,.054),polar(a,.20),polar(a+.28,.054)],235,2)
        return np.array(sheet,dtype=np.float32)/255

    for index,(key,(left,top,width,height)) in enumerate(REGIONS.items()):
        sample=np.asarray(donor.resize((width,height),Image.Resampling.LANCZOS),dtype=np.float32)/255
        sample=np.roll(sample,(43*index,61*index),axis=(0,1))
        yy,xx=np.indices((height,width),dtype=np.float32)
        x,y=(xx+.5)/width,(yy+.5)/height
        field=np.asarray(raster(rng.random((20,34))).resize((width,height),Image.Resampling.BICUBIC),
                         dtype=np.float32)/255-.5
        lum=sample @ np.array([.2126,.7152,.0722],dtype=np.float32)
        grain=np.clip(lum/np.median(lum)-1,-1,1.7)
        oxide=np.clip((sample[:,:,1]-sample[:,:,0]+.067)*3+field*.65,0,1)
        tone,metal,gloss=settings[key]
        rgb=(np.array(tone)/255)[None,None,:]*(1+.32*grain+.11*field)[:,:,None]
        rgb+=oxide[:,:,None]*np.array([-.10,.034,.033])
        noise=rng.normal(0,.17,(height,width))
        relief=.19*grain+.035*noise
        rough=gloss-.12*oxide+.034*field
        metallic=metal-.27*oxide
        mask=np.zeros((height,width))
        if key in ("degrees","ecliptic","brass","gilt"):
            turning=np.sin(yy*1.9+np.sin(x*21)*.4+.27*noise)
            relief+=.055*turning
            rough+=.032*turning
            rgb+=.010*turning[:,:,None]
        if key=="copper":
            runoff=np.clip((.38+field)*(.5+.5*np.sin(x*91+2*np.sin(x*30)))*(.2+.8*y),0,1)
            rgb+=runoff[:,:,None]*np.array([-.039,.044,.038])
            rough-=.12*runoff
            relief+=.11*field
            mask=np.clip(.52-.25*oxide-.17*runoff,.16,.54)
        if key=="lens":
            r=np.sqrt(((x-.50)*2)**2+((y-.50)*2)**2)
            angle=np.arctan2(y-.5,x-.5)
            rgb=np.stack([.105+.054*r+.030*np.cos(2.2*r+angle),
                          .26+.14*(1-r)+.062*np.sin(2.6*r+angle),
                          .34+.12*(1-r)+.058*np.sin(3*r+angle+.8)],axis=-1)
            relief=.007*noise
            rough=np.clip(.93-.08*r+.013*grain,.79,.98)
            metallic=.28
        if key=="darkoak":
            oak=master.crop((8,h//2+8,w//2-8,h-8)).transpose(Image.Transpose.TRANSPOSE)
            wood=np.asarray(oak.resize((width,height),Image.Resampling.LANCZOS),dtype=np.float32)/255
            fiber=wood @ np.array([.2126,.7152,.0722])
            fiber=np.clip(fiber/np.median(fiber)-1,-1,1)
            rgb=np.array(tone)[None,None,:]/255*(1+.58*fiber[:,:,None])
            relief=.37*fiber
            rough=.29+.075*fiber
            metallic=0
        if key=="smoke":
            # 不规则吹制小玻璃以铅条嵌成菱格。各片独立取烟蓝或橄榄灰。
            a,b=4*x+9*y,4*x-9*y
            ia,ib=np.floor(a).astype(int),np.floor(b).astype(int)
            joint=np.minimum.reduce([a%1,1-a%1,b%1,1-b%1])
            lead=np.clip((.071-joint)/.046,0,1)
            shades=np.array([(50,64,68),(55,69,69),(59,73,72),(64,75,74),(64,74,72),(68,77,73)])/255
            panes=shades[(ia*7+ib*11)%len(shades)]
            rgb=panes*(1+.07*field+.045*grain)[:,:,None]
            rgb=rgb*(1-lead[:,:,None])+np.array((.132,.142,.143))*lead[:,:,None]
            relief=.06*grain+.52*lead
            metallic=.07+.37*lead
            rough=.77-.34*lead+.048*field
        lines=ruled(key,width,height)
        if np.any(lines):
            lip=np.asarray(raster(lines).filter(ImageFilter.GaussianBlur(1.1)),dtype=np.float32)/255
            rgb*=1-(.71*lines if key!="globe" else .53*lines)[:,:,None]
            rgb+=.075*lip[:,:,None]
            relief-=.43*lines
            rough-=.19*lines
            metallic-=.23*lines
        dy,dx=np.gradient(relief)
        normals=np.stack((-dx*1.72,dy*1.72,np.ones_like(dx)),axis=2)
        normals/=np.linalg.norm(normals,axis=2,keepdims=True)
        packed=np.zeros((height,width,4))
        packed[:,:,0],packed[:,:,3]=metallic,rough
        for out,patch in zip(maps,(rgb,normals*.5+.5,packed,mask)):
            out.paste(raster(patch),(left,top))
    maps[0].save(ART/"metro-science-surface.png")
    maps[1].save(ART/"metro-science-normal.png")
    Image.merge("RGBA",[c.resize((1024,1024),Image.Resampling.BOX) for c in maps[2].split()]).save(ART/"metro-science-finish.png")
    maps[3].resize((1024,1024),Image.Resampling.BOX).save(COLORS/"metro-science-mask.png")


if __name__=="__main__":
    make_maps()
