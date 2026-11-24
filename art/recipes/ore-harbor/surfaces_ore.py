"""原矿、劈理板岩、凿面石灰岩与码头木作共用一套带边缘扩色的图集。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "art/source/ore-harbor"
PREVIEW = ROOT / "art/previews/ore-harbor"
ART = ROOT / "Unity/Catan/Assets/Art/PaintedMap/OreHarbor"
MODEL = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast"
SLOTS = ("dock", "oak", "ashlar", "dress", "boards", "slate", "ore", "ochre",
         "door", "wet", "tar", "rope", "iron", "bronze", "enamel", "agedcut")
VARIANTS = ("ashlar", "dress", "slate", "enamel")
SIZE, PAD = 512, 10


def atlas_uv(name, u, v):
    name={"enamel":"enamel0","galena":"enamel1","scour":"enamel2","coping":"enamel3"}.get(name,name)
    if name[-1].isdigit():
        q, name = int(name[-1]), name[:-1]
        u = ((q % 2) + (.034 + .932*u)) / 2
        v = ((1-q//2) + (.034 + .932*v)) / 2
    row, col = divmod(SLOTS.index(name), 4)
    return ((col+(PAD+u*(SIZE-2*PAD))/SIZE)/4, 1-(row+1-(PAD+v*(SIZE-2*PAD))/SIZE)/4)


def make_maps(output=ART):
    import numpy as np
    from PIL import Image, ImageFilter
    rng = np.random.default_rng(211721)
    timber = ART.parent / "LumberHarbor"
    src = Image.open(SOURCE/"矿脉石作板岩母图.png").convert("RGB")
    half = src.width//2
    samples = []
    for row in range(2):
        for col in range(2):
            box = (col*half+5,row*half+5,(col+1)*half-5,(row+1)*half-5)
            samples.append(np.asarray(src.crop(box).resize((SIZE,SIZE),Image.Resampling.LANCZOS),np.float32)/255.)
    kinds = ("albedo","normal","finish")
    woodmaps = {k:Image.open(timber/f"lumber-{k}.png") for k in kinds}
    out = {k:Image.new(woodmaps[k].mode,(2048,2048)) for k in kinds}
    old_slots = ("dock","oak","wall","trim","cedar","shake","bark","cut",
                 "door","wet","tar","rope","iron","shadow","sign","agedcut")
    grain_sources = {"dock":("dock",(.89,.95,1.01)),"oak":("oak",(.71,.74,.79)),
                    "boards":("wall",(1.23,1.39,1.49)),"door":("door",(.88,.89,.91)),
                    "tar":("tar",(.80,.79,.80)),"wet":("wet",(1,1,1)),"rope":("rope",(1.06,1.05,1)),
                    "iron":("iron",(1.06,1.07,1.11)),"agedcut":("agedcut",(.89,.92,.96))}
    dot = np.array([.24,.59,.17],np.float32)
    def blur(a, r):
        return np.asarray(Image.fromarray(np.uint8(np.clip(a,0,1)*255)).filter(ImageFilter.GaussianBlur(r)),np.float32)/255
    def noise(n,m):
        raw = Image.fromarray(rng.integers(0,256,(m,n),dtype=np.uint8))
        return np.asarray(raw.resize((SIZE,SIZE),Image.Resampling.BICUBIC),np.float32)/255-.5
    def fill(slot,quarter,values):
        row,col=divmod(SLOTS.index(slot),4)
        x,y=col*SIZE,row*SIZE
        side,bleed=SIZE,PAD
        if quarter is not None:
            side,bleed=(SIZE-2*PAD)//2,8
            x+=PAD+(quarter%2)*side
            y+=PAD+(quarter//2)*side
        for k,a in zip(kinds,values):
            pic=Image.fromarray(np.uint8(np.clip(a,0,1)*255+.5)).resize((side-bleed*2,side-bleed*2),Image.Resampling.LANCZOS)
            out[k].paste(Image.fromarray(np.pad(np.asarray(pic),((bleed,bleed),(bleed,bleed),(0,0)),mode="edge")),(x,y))
    for slot in SLOTS:
        if slot in grain_sources:
            original,tint=grain_sources[slot]
            r,c=divmod(old_slots.index(original),4)
            rr,cc=divmod(SLOTS.index(slot),4)
            for k in kinds:
                patch=woodmaps[k].crop((c*SIZE,r*SIZE,(c+1)*SIZE,(r+1)*SIZE))
                if k=="albedo":
                    rgb=np.array(patch,dtype=np.float32)/255
                    rgb=rgb*.73+(rgb@dot)[:,:,None]*.27
                    if slot=="boards": rgb=.74*rgb+.26*np.mean(rgb,axis=(0,1))+.018
                    patch=Image.fromarray(np.uint8(np.clip(rgb*tint,0,1)*255+.5))
                out[k].paste(patch,(cc*SIZE,rr*SIZE))
            continue
        variants=range(4) if slot in VARIANTS else (None,)
        for q in variants:
            source={"ore":0,"ochre":1,"ashlar":2,"dress":2,"slate":3,"enamel":3,"bronze":1}[slot]
            if slot=="enamel" and q==1: source=0
            if slot=="enamel" and q in (2,3): source=2
            rgb=np.roll(samples[source],tuple(rng.integers(-SIZE//3,SIZE//3,2)),axis=(0,1)).copy()
            lum=rgb@dot; med=np.median(lum)
            fine=lum-blur(lum,2)
            strata=lum-blur(lum,12)
            broad=noise(11,8)
            height=.59*strata+.32*fine+.12*lum
            metal=np.zeros_like(lum)
            if slot in ("ore","ochre"):
                oxide=np.clip((rgb[:,:,0]-rgb[:,:,2]) * 3.4+.08,0,1)
                quartz=np.clip((lum-.52)*3.9,0,1)
                metal=.49*(1-oxide)*(1-quartz)
                rough=.48+.21*oxide+.12*quartz-.24*fine
                rgb=rgb*.92+.012
            elif slot in ("ashlar","dress","slate"):
                palette={"ashlar":[(152,142,124),(141,133,119),(157,149,133),(147,137,126)],
                         "dress":[(181,172,148),(168,160,142),(161,154,138),(115,111,100)],
                         "slate":[(72,87,98),(86,97,105),(79,87,94),(91,100,104)]}
                tint=np.array(palette[slot][q])/255.
                rgb=tint*(1+((lum-med)*1.54)[:,:,None])+.39*(rgb-lum[:,:,None]-(np.median(rgb,axis=(0,1))-med))
                rough=(.72 if slot=="slate" else .84)+broad*.12-fine*.21
            elif slot=="bronze":
                tarnish=np.clip(.22+1.05*broad+noise(45,40)*.35,0,.72)
                bright=np.array([.74,.59,.36]); dark=np.array([.24,.23,.16])
                rgb=bright*(1-tarnish[:,:,None])+dark*tarnish[:,:,None]+fine[:,:,None]*.38
                metal=.82-tarnish*.45; rough=.33+.34*tarnish
                height*=.32
            elif slot=="enamel" and q==1:
                rgb=np.array([.35,.41,.44])*(1+2.1*(lum-med)[:,:,None])+.66*(rgb-lum[:,:,None])
                metal=.39-np.clip(lum-.51,0,.21)
                rough=.48+np.clip(lum-.46,0,.23)-fine*.22
            elif slot=="enamel" and q in (2,3):
                tint=np.array([.48,.485,.424]) if q==2 else np.array([.59,.56,.49])
                rgb=tint*(1+1.53*(lum-med)[:,:,None])
                if q==2:
                    salt=np.clip((strata+.45*broad-.082)*1.6,0,.38)
                    rgb+=salt[:,:,None]*np.array([.16,.17,.14])
                rough=.79+broad*.17
            else:
                worn=np.clip((broad+noise(50,48)*.47-.22)*1.45,0,.44)
                rgb=np.array([.122,.175,.186])*(1+.44*strata[:,:,None])+worn[:,:,None]*np.array([.17,.12,.071])
                height*=.26; metal=.10+.24*worn; rough=.53+.16*broad
            dy,dx=np.gradient(height)
            n=np.stack((-3.3*dx,3.3*dy,np.ones_like(lum)),axis=-1)
            n/=np.linalg.norm(n,axis=-1,keepdims=True)
            finish=np.stack((metal,np.zeros_like(lum),np.zeros_like(lum),1-rough),axis=-1)
            fill(slot,q,(rgb,n*.5+.5,finish))
    # 将每个四色块的外边也扩至图集格边，覆盖远景采样足迹。
    for name in VARIANTS:
        row,col=divmod(SLOTS.index(name),4)
        x,y=col*SIZE,row*SIZE
        for img in out.values():
            inner=np.asarray(img.crop((x+PAD,y+PAD,x+SIZE-PAD,y+SIZE-PAD)))
            img.paste(Image.fromarray(np.pad(inner,((PAD,PAD),(PAD,PAD),(0,0)),mode="edge")),(x,y))
    output.mkdir(parents=True,exist_ok=True)
    for k,img in out.items(): img.save(output/f"ore-{k}.png")

if __name__=="__main__":
    make_maps()


def surface():
    import bpy
    mat=bpy.data.materials.new("OreHarbor")
    mat.use_nodes=True
    nodes,link=mat.node_tree.nodes,mat.node_tree.links.new
    bsdf=nodes.get("Principled BSDF")
    for key,y in (("albedo",320),("normal",0),("finish",-300)):
        img=bpy.data.images.load(str(ART/f"ore-{key}.png"))
        if key!="albedo": img.colorspace_settings.name="Non-Color"
        if key=="finish": img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        tex=nodes.new("ShaderNodeTexImage"); tex.image=img; tex.location=(-730,y)
        if key=="albedo":
            color=tex
        elif key=="normal":
            normal=nodes.new("ShaderNodeNormalMap"); normal.location=(-260,y)
            normal.inputs["Strength"].default_value=.82
            link(tex.outputs["Color"],normal.inputs["Color"]); link(normal.outputs[0],bsdf.inputs["Normal"])
        else:
            split=nodes.new("ShaderNodeSeparateColor"); split.location=(-495,y)
            inv=nodes.new("ShaderNodeMath"); inv.operation="SUBTRACT"; inv.inputs[0].default_value=1; inv.location=(-250,y-96)
            link(tex.outputs[0],split.inputs[0]); link(split.outputs[0],bsdf.inputs["Metallic"])
            link(tex.outputs["Alpha"],inv.inputs[1]); link(inv.outputs[0],bsdf.inputs["Roughness"])
    uv=nodes.new("ShaderNodeUVMap"); uv.uv_map="HarborExposure"; uv.location=(-740,580)
    values=nodes.new("ShaderNodeSeparateXYZ"); values.location=(-510,580); link(uv.outputs[0],values.inputs[0])
    remap=nodes.new("ShaderNodeMath"); remap.operation="MULTIPLY_ADD"; remap.location=(-310,630)
    remap.inputs[1].default_value=.42; remap.inputs[2].default_value=.58
    link(values.outputs["X"],remap.inputs[0])
    dry=nodes.new("ShaderNodeMixRGB"); dry.blend_type="MULTIPLY"; dry.inputs[0].default_value=1
    dry.location=(-95,440); link(color.outputs["Color"],dry.inputs[1]); link(remap.outputs[0],dry.inputs[2])
    wet=nodes.new("ShaderNodeMixRGB"); wet.blend_type="MULTIPLY"; wet.location=(115,510)
    wet.inputs[2].default_value=(.34,.44,.37,1)
    link(values.outputs["Y"],wet.inputs[0]); link(dry.outputs[0],wet.inputs[1])
    link(wet.outputs[0],bsdf.inputs["Base Color"])
    nodes.active=color
    return mat
