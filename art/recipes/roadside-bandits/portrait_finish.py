"""在人体头部原有 UV 中配准肖像、耳廓和颈部，眼球与发须按各自的表面着色。"""
from functools import lru_cache
import numpy as np
from head_form import head_template,beard_hairline
from surface_study import LUMA,sample


@lru_cache(maxsize=4)
def native_projection(w,h):
    head=head_template()
    vertices=head["vertices"];faces=head["faces"].astype(int)
    uv=head["uv"]*np.array([w-1,h-1])
    coords=np.zeros((h,w,3),dtype=np.float32)
    mask=np.zeros((h,w),dtype=bool)
    for ids in ((0,1,2),(0,2,3)):
        for p,tri in zip(uv[:,ids],vertices[faces[:,ids]]):
            a,b,c=p
            u=b-a;v=c-a;det=u[0]*v[1]-u[1]*v[0]
            if abs(det)<1e-8:continue
            x0,y0=np.maximum(0,np.floor(p.min(axis=0)).astype(int))
            x1,y1=np.minimum([w,h],np.ceil(p.max(axis=0)).astype(int)+1)
            yy,xx=np.ogrid[y0:y1,x0:x1]
            s=((xx-a[0])*v[1]-(yy-a[1])*v[0])/det
            t=(u[0]*(yy-a[1])-u[1]*(xx-a[0]))/det
            inside=(s>=-.0001)&(t>=-.0001)&(s+t<=1.0001)
            if not np.any(inside):continue
            target=coords[y0:y1,x0:x1]
            target[inside]=tri[0]+s[inside,None]*(tri[1]-tri[0])+t[inside,None]*(tri[2]-tri[0])
            mask[y0:y1,x0:x1]|=inside
    # 每个 UV 岛外扩肤色至足够的 mip 边距。
    for _ in range(11):
        total=np.zeros_like(coords); count=np.zeros((h,w),dtype=np.float32)
        for step,axis in ((1,0),(-1,0),(1,1),(-1,1)):
            neighbour=np.roll(mask,step,axis)
            total+=np.roll(coords,step,axis)*neighbour[:,:,None]
            count+=neighbour
        new=(~mask)&(count>0)
        coords[new]=total[new]/count[new,None]
        mask|=new
    coords[~mask]=(0,5.84,.65)
    return coords


PHOTO={
    "carrier":(381,284,[675,594,502,473,443,398,381,335,293,257,176,99,53]),
    "leader":(1102,271,[659,582,511,492,459,422,402,354,309,277,183,94,46]),
    "scout":(1816,266,[608,571,531,502,481,440,414,359,309,278,190,98,49]),
}
LEVELS=[6.15,6.36,6.54,6.645,6.73,6.855,6.965,7.105,7.284,7.44,7.72,8.12,8.49]


def whisker_sample(role,angle,v):
    carrier=role=="carrier"
    side=np.abs(np.sin(angle))
    x=PHOTO[role][0]+(173 if carrier else 153)*np.sin(angle)*(.94+.06*np.cos(v*np.pi))
    start=(536 if carrier else 546)-(67 if carrier else 60)*side**1.65
    end=(651 if carrier else 636)-(102 if carrier else 85)*side**1.8
    return x,start+v*(end-start)+2.5*np.sin(angle*8+v*3)


def head_tile(role,w,h,portrait,soft):
    pts=native_projection(w,h)
    x,y,z=np.moveaxis(pts,2,0);ax=np.abs(x)
    origin,width,rows=PHOTO[role]
    sx=origin+x*width*(1-.105*np.exp(-((y-6.66)/.18)**2))
    sy=np.interp(y,LEVELS,rows)
    # 睑裂内显示独立眼球，紧贴眼白的上下睑分别取睑缘外的肤色。
    near=np.clip((ax-.126)/.053,0,1)*np.clip((.485-ax)/.044,0,1)
    away=np.sign(7.285-y)
    edge=rows[8]+away*np.maximum(18,np.abs(sy-rows[8]))
    sy=sy*(1-near)+edge*near
    face=sample(portrait,sx,sy)
    medium=sample(soft,sx,sy)
    detail=(face-medium)@LUMA
    # 侧颈的颜色沿肌束走向延展到耳后，耳轮用肖像同侧耳部和附近皮肤的色差。
    neck=sample(portrait,1816+np.clip(x/.69,-1,1)*94,694-38*np.clip((y-5.5)/.96,0,1)+5*np.sin(4*x))
    neck*=np.array([.96,.90,.82])
    side=sample(portrait,origin+np.sign(x)*(119+43*np.clip((.98-z)/.72,0,1)),
                np.interp(y,[6.15,6.65,7,7.45,8.49],[452,431,397,371,197]))
    pigment=neck*.45+side*.55
    frontal=np.clip((z-.84)/.40,0,1)*np.clip((.83-ax)/.16,0,1)
    frontal*=np.clip((y-6.13)/.16,0,1)
    face=(face-.12*(face-medium))*{"carrier":(.98,.94,.87),"leader":(.95,.88,.80),"scout":(.98,.95,.90)}[role]
    rgb=pigment*(1-frontal[:,:,None])+face*frontal[:,:,None]
    ear=np.clip((ax-.637)/.096,0,1)*np.clip((.83-z)/.12,0,1)*np.clip((z-.22)/.12,0,1)
    ear*=np.clip((y-6.76)/.106,0,1)*np.clip((7.51-y)/.12,0,1)
    offset=(163+68*np.clip((.736-z)/.44,0,1))*(1 if role!="leader" else .97)
    ear_y=np.interp(y,[6.81,6.93,7.10,7.25,7.43],[452,434,398,357,312])
    ear_rgb=sample(portrait,origin+np.sign(x)*offset,ear_y)*np.array([1.00,.94,.88])
    rgb=rgb*(1-ear[:,:,None])+ear_rgb*ear[:,:,None]
    throat=np.clip((1.11-z)/.29,0,1)*np.clip((6.59-y)/.36,0,1)
    rgb=rgb*(1-throat[:,:,None])+neck*throat[:,:,None]
    if role!="scout":
        angle=np.arctan2(x,z-.56)
        edge=beard_hairline(angle)
        feather=np.clip((edge+.147-y)/.16,0,1)*np.clip((1.67-np.abs(angle))/.14,0,1)
        feather*=np.clip((y-6.17)/.16,0,1)*np.clip((z-.69)/.26,0,1)
        length=np.clip((edge-y)/.46,.0,1)
        hx,hy=whisker_sample(role,angle,length)
        roots=sample(portrait,hx,hy)*np.array([.83,.81,.75])
        rgb=rgb*(1-feather[:,:,None])+roots*feather[:,:,None]
    # 鼻唇沟、眉间折线和毛孔取平光样本的局部残差，整块颊面仍由真正的灯光塑形。
    height=np.clip(detail/40,-.60,.52)*frontal*.45
    gloss=.275+.055*np.exp(-((ax-.08)/.18)**2-((y-6.99)/.22)**2)*frontal
    gloss+=.035*np.exp(-((y-6.64)/.035)**2)*(ax<.26)+np.clip(detail/550,-.025,.025)*frontal
    return rgb,height,gloss,0,"skin"


def eye_tile(role,w,h):
    yy,xx=np.indices((h,w),dtype=np.float32)
    x=2*xx/(w-1)-1;y=2*yy/(h-1)-1
    radius=np.sqrt(x*x+y*y)/.535
    u=np.arctan2(y,x)
    iris=np.clip((1.03-radius)/.055,0,1)
    pupil=np.clip((.44-radius)/.07,0,1)
    spokes=.55*np.sin(107*u+4*np.sin(33*u)+radius*3)+.27*np.cos(218*u-radius*9)+.17*np.sin(63*u+radius*7)
    sclera=np.array([178,160,143])+(x*x+y*y)[:,:,None]*np.array([4,-7,-8])
    amber={"carrier":[91,58,32],"leader":[80,57,35],"scout":[99,74,44]}[role]
    color=np.array(amber)*(1+.23*spokes[:,:,None])
    color*=1-.48*np.exp(-((radius-.94)/.113)**2)[:,:,None]
    color=color*(1-pupil[:,:,None])+np.array([19,16,14])*pupil[:,:,None]
    return sclera*(1-iris[:,:,None])+color*iris[:,:,None],np.zeros_like(x),.63+.27*iris,0,"skin"


def fur_tile(key,w,h,portrait,soft):
    kind,role=key.split("-")
    yy,xx=np.indices((h,w),dtype=np.float32)
    u=xx/(w-1);v=yy/(h-1)
    carrier=role=="carrier"
    if kind=="whiskers":
        angle=(u-.5)*3.30
        sx,sy=whisker_sample(role,angle,v)
    else:
        angle=(u-.5)*2*np.pi
        # 正额取额前的实发，从两鬓往后接入高密度卷发，环后使用同一组褐发样本。
        side=np.sin(angle)
        sx=380+173*side+13*np.sin(3*angle+v*1.8)
        sy=30+38*side**2+v*(81+21*side**2)+7*np.sin(4*angle+.9*v)
        back=np.clip(-np.cos(angle)*1.8,0,1)
        sx=sx*(1-back)+(343+96*np.sin(2.4*angle-.5*v))*back
        sy=sy*(1-back)+(72+40*np.sin(2*angle+1.6*v))*back
    pix=sample(portrait,sx,sy)
    blurred=sample(soft,sx,sy)
    # 高频细毛连续跨过实体发绺的边界。中频保留曲线和灰须，大片亮暗仍交给真实几何。
    detail=(pix-blurred)@LUMA
    rgb=.76*blurred+.24*np.array([67,48,35])+.74*(pix-blurred)
    rgb*=np.array([.84,.83,.78]) if carrier else np.array([.79,.79,.74])
    height=np.clip(detail/45,-.58,.60)*.42
    gloss=.225+np.clip(detail/210,-.07,.085)
    return rgb,height,gloss,0,"hair"
