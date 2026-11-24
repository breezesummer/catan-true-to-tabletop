"""议政宫材质。十个定向取样区共用色彩、切线法线及金属度／光滑度图集。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/ExpansionPieces/v001"
COLORS = ART.parents[1] / "PlayerColors"
SOURCE = ROOT / "art/source/coronation-palace"
BLEND = SOURCE / "冠冕议政宫.blend"
VIEWS_DIR = ROOT / "art/previews/coronation-palace"
KIND = "metro-politics"
SPAN = .033


def make():
    import sys
    import numpy as np
    from PIL import Image

    sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "gothic-royal-city"))
    from surfaces import REGIONS
    master = Image.open(ROOT / "art/source/gothic-royal-city/王城材质母图.png").convert("RGB")
    w, h = master.width//2, master.height//2
    stock = [master.crop((i*w+8,j*h+8,(i+1)*w-8,(j+1)*h-8)) for j in range(2) for i in range(2)]
    albedo = Image.new("RGB",(2048,2048))
    normal = albedo.copy()
    finish = Image.new("RGBA",albedo.size)
    mask = Image.new("L",albedo.size)
    rng = np.random.default_rng(191117)
    # 石墙、风化板岩、石柱、雕刻、勒脚、铅接玻璃、栎木、老铜、铅皮及窗膛。
    swatches = {
        "ashlar":(0,(180,171,157),.46,2.8,0,.29),
        "slate":(1,(63,65,70),.44,2.1,.012,.25),
        "limestone":(0,(184,174,157),.49,2.7,0,.26),
        "carving":(0,(210,199,179),.34,1.9,0,.34),
        "foot":(0,(157,149,137),.50,2.9,0,.25),
        "glass":(1,(65,62,61),.20,.9,.05,.64),
        "oak":(2,(82,63,48),.69,2.3,0,.34),
        "bronze":(3,(149,117,73),.64,1.6,.78,.49),
        "lead":(1,(90,86,83),.35,1.45,.63,.38),
        "shadow":(0,(123,116,108),.45,2.4,0,.22),
    }

    def pixels(a):
        return Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5))

    for index,(name,(left,top,size)) in enumerate(REGIONS.items()):
        source,tone,contrast,relief,metal,gloss = swatches[name]
        sample = np.asarray(stock[source].resize((size,size),Image.Resampling.LANCZOS),dtype=float)/255
        sample = np.roll(sample,(37*index,83*index),axis=(0,1))
        if name=="oak":
            sample = sample.transpose((1,0,2))
        y,x = (np.indices((size,size))+.5)/size
        lum = sample @ [.2126,.7152,.0722]
        grain = np.clip(lum/np.median(lum)-1,-.8,1.45)
        tint = sample-lum[:,:,None]
        tint -= np.median(tint,axis=(0,1))
        def field(cells):
            noise = np.uint8(rng.random((cells,cells))*255)
            return np.asarray(Image.fromarray(noise).resize((size,size),Image.Resampling.BICUBIC),dtype=float)/255-.5
        cloud,flecks = field(13),field(max(16,size//7))
        speck = rng.normal(0,.40,(size,size))
        rgb = np.array(tone)/255*(1+contrast*grain[:,:,None])+.29*tint
        height = .44*grain+.023*flecks+.013*speck
        smooth = gloss-.073*grain+.028*cloud
        metalness = np.full((size,size),metal)
        if name in ("ashlar","foot","slate"):
            rows,columns = {"ashlar":(13,7),"foot":(7,4),"slate":(29,19)}[name]
            yy = y*rows+.018*grain+.022*flecks+.009*np.sin(x*50)
            j = np.floor(yy).astype(int)
            offsets = rng.uniform(-.09,.09,rows)
            xx = x*columns+(j%2)*.5+offsets[j%rows]+.030*grain+.012*flecks
            i = np.floor(xx).astype(int)
            fy,fx = yy%1,xx%1
            variation = rng.normal(0,1,(rows,columns,3))[j%rows,i%columns]
            joint = np.minimum(np.minimum(fx,1-fx)*rows/columns,np.minimum(fy,1-fy))
            shade = variation[:,:,0]
            if name=="slate":
                side = np.clip(np.minimum(fx,1-fx)/.039,0,1)
                lip = np.exp(-((fy-.93)/.04)**2)
                seam = np.clip(fy/.068,0,1)*side
                rgb *= (1+.056*shade-.29*(1-seam)-.13*np.exp(-fy*6)+.09*lip)[:,:,None]
                rgb += .016*cloud[:,:,None]+.004*variation-.15*tint
                height += .33*fy+.34*seam+.13*lip+.036*cloud
                smooth += .046*shade-.075*(1-seam)
                # 边缘露出板岩本色，中部保留可辨的四色矿物涂层。
                pigment = np.clip(.76-.10*abs(cloud)-.12*(1-side)-.11*np.maximum(grain,0)-.04*abs(shade),.41,.78)
                mask.paste(pixels(pigment),(left,top))
            else:
                arris = np.clip((joint-.008)/(.05+.018*flecks),0,1)
                arris = arris*arris*(3-2*arris)
                rgb *= (1+.057*shade+.048*cloud)[:,:,None]
                rgb += variation*np.array((.009,.006,.005))
                mortar = np.array((.475,.453,.416)) if name=="ashlar" else np.array((.424,.405,.374))
                rgb = rgb*arris[:,:,None]+(mortar+.055*flecks[:,:,None])*(1-arris[:,:,None])
                height += .53*arris+.068*shade+.055*cloud
                smooth -= .078*(1-arris)
        elif name=="glass":
            a,b = x*14+y*23,x*14-y*23
            fa,fb = a%1,b%1
            seam = np.minimum.reduce((fa,1-fa,fb,1-fb))
            came = np.clip(1-seam/.075,0,1)
            panes=np.array([(56,60,61),(67,70,70),(70,68,63),(74,77,75),(55,63,66),(76,72,61)])/255
            base=panes[(np.floor(a).astype(int)*11+np.floor(b).astype(int)*5)%len(panes)]
            rgb=base*(1+.11*cloud[:,:,None]+.10*grain[:,:,None])*(1-came[:,:,None])+np.array((.16,.16,.155))*came[:,:,None]
            smooth=.69-.29*came+.035*cloud
            height=.50*came+.05*grain+.07*cloud
        elif name in ("carving","limestone","shadow"):
            cut = np.sin(size*(x*1.24+y*.47)+2.4*np.sin(27*y)+grain*6)
            tool = .5+.5*field(19)
            height += .050*cut*tool
            rgb += .009*(cut*tool+2*cloud)[:,:,None]
        elif name=="bronze":
            patina=np.clip((sample[:,:,1]-sample[:,:,0]+.046)*4.8,0,1)
            rgb+=patina[:,:,None]*np.array((-.10,.045,.035))
            smooth-=.23*patina
            metalness*=1-.45*patina
        elif name=="oak":
            height+=.095*flecks
            rgb+=.019*cloud[:,:,None]
        dy,dx=np.gradient(height)
        vectors=np.stack((-dx*relief,dy*relief,np.ones_like(dx)),axis=-1)
        vectors/=np.linalg.norm(vectors,axis=2,keepdims=True)
        packed=np.zeros((size,size,4))
        packed[:,:,0],packed[:,:,3]=metalness,smooth
        albedo.paste(pixels(rgb),(left,top))
        normal.paste(pixels(vectors*.5+.5),(left,top))
        finish.paste(pixels(packed),(left,top))
    albedo.save(ART/(KIND+"-surface.png"))
    normal.save(ART/(KIND+"-normal.png"))
    Image.merge("RGBA",[band.resize((512,512),Image.Resampling.BOX) for band in finish.split()]).save(ART/(KIND+"-finish.png"))
    mask.resize((1024,1024),Image.Resampling.BOX).save(COLORS/(KIND+"-mask.png"))


if __name__=="__main__":
    make()
