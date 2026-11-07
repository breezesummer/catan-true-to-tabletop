"""生成货物的颜色、切线空间微法线、粗糙度与金属度图集。"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

DEST = Path(__file__).resolve().parents[3] / 'Unity/Catan/Assets/Resources/PaintedMap/Coast'
N = 512
rng = np.random.default_rng(1729)
v, u = np.mgrid[0:1:complex(N), 0:1:complex(N)]
tau = np.pi*2

def noise(size):
    field = Image.fromarray(rng.normal(0, 1, (size,size)).astype('float32'))
    return np.asarray(field.resize((N,N), Image.Resampling.BICUBIC))

def mixed(a,b,t):
    return np.array(a)[None,None,:]*(1-t[:,:,None])+np.array(b)[None,None,:]*t[:,:,None]

def original_grain(kind,box):
    patch=Image.open(DEST/('port-'+kind+'.png')).convert('L').crop(box)
    patch=np.asarray(patch.resize((N//2,N//2),Image.Resampling.BICUBIC),dtype=float)
    patch-=patch.mean()
    patch=np.concatenate((patch,patch[::-1]),axis=0)
    return np.concatenate((patch,patch[:,::-1]),axis=1)/96

def fleece_nap():
    # 用第一版羊毛卷受光面的纤维作底，再铺交错卷曲的细羊绒。
    source=Image.open(DEST/'port-wool.png').convert('L').crop((142,196,195,218))
    source=source.resize((256,128),Image.Resampling.BICUBIC)
    tile=np.asarray(source,dtype=float)-np.asarray(source.filter(ImageFilter.GaussianBlur(6)),dtype=float)
    tile=np.concatenate((tile,tile[::-1]),axis=0)
    tile=np.concatenate((tile,tile[:,::-1]),axis=1)
    photo=np.tile(tile,(2,1))/43
    nap=Image.new('L',(N,N),128)
    draw=ImageDraw.Draw(nap)
    for _ in range(9700):
        x,y=rng.uniform(-12,N+8,2);length=rng.uniform(3,15)
        a=.7*np.sin(x*.021)+.5*np.cos(y*.025)+rng.normal(0,.45)
        points=[(x+j*np.cos(a+j*.045),y+j*np.sin(a+j*.045)) for j in np.linspace(0,length,6)]
        draw.line(points,fill=int(rng.choice((66,89,170,198,215))),width=1)
    return photo*.40+(np.asarray(nap,dtype=float)-128)/120

def sawn_ends(fine):
    # 各段原木分别拥有偏心髓、疏密变化的年轮、开裂方向和锯痕。
    size=N//2
    v0,u0=np.mgrid[-1:1:complex(size),-1:1:complex(size)]
    color=np.empty((N,N,3));height=np.empty((N,N))
    source=Image.open(DEST/'port-wood.png').convert('RGB')
    profiles=[(-.26,.08,9.8,(82,322,132,369)),(.15,-.24,15.4,(148,322,198,368)),
              (.22,.13,12.2,(184,279,233,323)),(-.08,-.15,8.5,(208,324,259,366))]
    for i,(px,py,count,box) in enumerate(profiles):
        patch=np.s_[(1-i//2)*size:(2-i//2)*size,(i%2)*size:(1+i%2)*size]
        x=u0-px;y=(v0-py)*(1.03+.08*np.cos(i))
        r=np.hypot(x,y);a=np.arctan2(y,x)
        phase=count*r+.48*np.sin(7*r+i)+.15*np.sin(3*a+i)*r+.42*np.sin(2*a-i)*r*r
        band=np.maximum(0,np.cos(tau*phase))**6
        core=np.exp(-3.8*r)
        sap=np.clip((np.hypot(u0,v0)-(.68+.04*np.sin(a*3+i)))*4,0,1)
        saw=np.sin((u0*.69+v0*.36)*158+i)*.018+fine[patch]*.020
        wood=np.clip(.83-.34*band-.25*core+.12*sap+saw,0,1)
        raw=np.asarray(source.crop(box).resize((size,size),Image.Resampling.BICUBIC),dtype=float)
        color[patch]=.20*raw+.80*mixed((124,63,27),(255,192,109),wood)
        height[patch]=-.19*band+.035*saw
    return color,height,.76,0

def materials():
    grain=noise(4)*.25+noise(16)*.12
    fine=noise(256)
    # 树皮的纵裂、木栓斑和隆起边缘。
    phase=(u*16 + .25*np.sin(v*8+u*16)+.64*grain+.18*noise(9))*tau
    fissure=np.maximum(0,np.cos(phase))**5
    ridge=.27*np.sin(phase)+.16*np.sin(phase*2.7+v*21)+.18*noise(35)+.08*fine
    bark=original_grain('wood',(128,241,167,277))
    peel=np.clip((noise(8)+noise(36)*.28-.46)*1.7,0,1)
    striation=(u*49+.25*np.sin(v*14+u*19)+grain*.65+noise(18)*.15)*tau
    cuts=np.maximum(0,np.cos(striation))**7*np.clip(.42+.33*noise(11),0,1)
    skin=np.clip(.61+ridge*.29-fissure*.19+bark*.43+noise(5)*.07-cuts*.39+fine*.062,0,1)
    bark_rgb=mixed((49,25,18),(157,83,45),skin)
    yield bark_rgb*(1-peel[:,:,None]*.45)+np.array([210,139,77])*peel[:,:,None]*.45,ridge*.24-fissure*.26+bark*.25-cuts*.55+fine*.11,.83,0
    yield sawn_ends(fine)
    # 烧结黏土的烧斑、孔洞和浅色砂粒。
    pores=np.clip(-noise(143)-.91,0,2); sand=np.clip(noise(197)-1.28,0,1)
    kiln=original_grain('brick',(143,190,201,246))
    clay=np.clip(.67+kiln*.63+noise(5)*.14+noise(35)*.047+fine*.044-pores*.40+sand*.29,0,1)
    yield mixed((126,40,22),(250,122,59),clay),clay+fine*.10-pores*.45,.90,0
    # 断口中的石英脉和铅灰色晶粒，片状晶粒单独响应高光。
    phase=u*3.1+v*2.4+noise(6)*.32+noise(23)*.09
    veins=np.exp(-((np.sin(phase*tau))/.20)**2)
    mica=np.clip((noise(87)-.73)*.9,0,1)
    mineral=original_grain('ore',(151,201,196,228))
    ore=np.clip(.25+mineral*.05+noise(27)*.045+veins*.09+mica*.095,0,1)
    yield mixed((19,28,39),(109,129,147),ore),ore*.45+mica*.11,.48-veins*.10-mica*.16,.64+mica*.28
    # 颖壳纵肋自底部汇向芒尖。
    husk=np.sin((u*12+.27*np.sin(v*5+u*4))*tau)
    wheat=np.clip(.62+husk*.19+noise(13)*.075+fine*.035,0,1)
    yield mixed((179,96,18),(255,228,109),wheat),husk*.34+fine*.045,.61,0
    # 纺纱表面同时保留捻股与杂乱羊绒的细纤维。
    twist=np.sin((u*4+v*9+noise(9)*.16)*tau)
    fiber=np.sin((u*54+v*28+noise(17)*.94)*tau)
    nap=fleece_nap()
    wool=np.clip(.80+twist*.07+fiber*.030+nap*.17+fine*.022,0,1)
    yield mixed((156,133,104),(255,249,228),wool),twist*.14+fiber*.08+nap*.18,.98,0
    rope=np.cos((u*6-v*15+.07*noise(18))*tau)
    yield mixed((105,61,26),(226,183,108),np.clip(.58+rope*.3+fine*.065,0,1)),rope*.43+fine*.08,.8,0
    wear=np.clip(.41+noise(4)*.17+noise(40)*.07+fine*.08,0,1)
    yield mixed((30,47,49),(154,167,151),wear),wear+fine*.09,.43+wear*.16,.76
    # 刨平的橡木桶板与树皮分开，桶盖的弦板缝由几何生成。
    lines=(u*9+noise(4)*.30+.15*np.sin(v*13+u*9))*tau
    worn_ends=np.clip((np.abs(v-.5)-.33)*6+noise(19)*.19,0,1)
    oak=np.clip(.61+.10*np.sin(lines)-.22*np.maximum(0,np.cos(lines))**8+noise(25)*.06+noise(5)*.12,0,1)
    oak_rgb=mixed((87,45,22),(231,163,90),oak)
    yield oak_rgb*(1-.41*worn_ends[:,:,None])+np.array((186,151,118))*.41*worn_ends[:,:,None],oak*.55+fine*.06,.63+.18*worn_ends,0
    quartz=np.clip(.70+noise(38)*.043+mineral*.047+mica*.10,0,1)
    yield mixed((99,119,141),(232,243,251),quartz),quartz*.17,.23,.84
    yield mixed((105,124,123),(198,207,195),wear),fine*.11,.30+wear*.11,.89
    facet=np.clip(.48+mineral*.08+noise(31)*.044+mica*.10,0,1)
    oxidation=np.clip((noise(5)-1.03)*.65,0,.42)
    steel=mixed((40,53,72),(147,166,186),facet)
    yield steel*(1-oxidation[:,:,None])+np.array([151,112,74])*oxidation[:,:,None],facet*.22,.43+oxidation*.31,.69

def build():
    atlas=np.zeros((N*3,N*4,3),dtype=np.uint8)
    surface=np.zeros((N*3,N*4,4),dtype=np.uint8)
    for i,(rgb,h,rough,metal) in enumerate(materials()):
        row, col = divmod(i,4)
        region=np.s_[row*N:(row+1)*N,col*N:(col+1)*N]
        atlas[region]=np.uint8(np.clip(rgb,0,255))
        dv,du=np.gradient(h)
        normal=np.stack((-du*2.1,dv*2.1,np.ones_like(h)),axis=-1)
        normal/=np.linalg.norm(normal,axis=-1,keepdims=True)
        surface[region]=np.uint8(np.stack(((normal[:,:,0]+1)*.5,(normal[:,:,1]+1)*.5,
            np.broadcast_to(rough,h.shape),np.broadcast_to(metal,h.shape)),axis=-1).clip(0,1)*255)
    Image.fromarray(atlas).save(DEST/'cargo-pigments.png')
    Image.fromarray(surface).save(DEST/'cargo-surface.png')
if __name__=='__main__':
    build()
