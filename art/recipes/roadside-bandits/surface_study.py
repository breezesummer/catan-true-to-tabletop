"""在裁片的真实长度上取纹样，并从几何接触、肢体部位和使用痕迹调节表面。"""
from math import pi, sin, cos, sqrt
import numpy as np
from PIL import Image, ImageFilter
from sculpt import V, C, S, BASE_ROW


LUMA=np.array([.2126,.7152,.0722],dtype=np.float32)


def sample(arr,x,y):
    x=np.clip(x,0,arr.shape[1]-1.001);y=np.clip(y,0,arr.shape[0]-1.001)
    ix=x.astype(np.int32);iy=y.astype(np.int32)
    fx=x-ix;fy=y-iy
    if arr.ndim==3:fx=fx[...,None];fy=fy[...,None]
    return (arr[iy,ix]*(1-fx)+arr[iy,ix+1]*fx)*(1-fy)+(arr[iy+1,ix]*(1-fx)+arr[iy+1,ix+1]*fx)*fy


def chart_frame(chart,grid,u,v):
    points=sample(grid,u*(grid.shape[1]-1),v*(grid.shape[0]-1))
    # 共顶的帽尖与裁片端点也使用相邻有效截面的法线。
    du=np.gradient(grid,axis=1);dv=np.gradient(grid,axis=0)
    normals=np.cross(du,dv)
    if chart.wrap:
        radial=grid-grid[:,:-1].mean(axis=1,keepdims=True)
    else:
        cx,cy={"carrier":(273,62),"leader":(395,0),"scout":(497,68)}[chart.part.split()[0]]
        radial=grid-[cx,cy,0];radial[:,:,2]=0
    reverse=(np.sum(radial*normals)<0)!=(chart.material=="lining")
    normals/=np.maximum(.00001,np.linalg.norm(normals,axis=2))[...,None]
    if reverse:normals*=-1
    normals=sample(normals,u*(grid.shape[1]-1),v*(grid.shape[0]-1))
    normals/=np.maximum(.00001,np.linalg.norm(normals,axis=2))[...,None]
    return points,normals


def contact_map(points,normals,tree):
    """短程遮蔽随模型转动。以实物距离限制射线，可区分近处缝隙与开放的外表面。"""
    h,w=points.shape[:2]
    nx=min(w,max(4,(w+5)//6));ny=min(h,max(4,(h+5)//6))
    yy,xx=np.meshgrid(np.linspace(0,h-1,ny),np.linspace(0,w-1,nx),indexing="ij")
    origins=sample(points,xx,yy).reshape(-1,3)
    surface=sample(normals,xx,yy).reshape(-1,3)
    directions=[]
    for i in range(10):
        radius=sqrt((i+.45)/10);angle=i*pi*(3-sqrt(5))
        directions.append((radius*cos(angle),radius*sin(angle),sqrt(1-radius*radius)))
    shade=np.ones(len(origins),dtype=np.float32)
    for i,(position,n) in enumerate(zip(origins,surface)):
        normal=V(n).normalized()
        if normal.length_squared<.8:continue
        axis=normal.cross(V((0,0,1)) if abs(normal.z)<.93 else V((1,0,0))).normalized()
        other=normal.cross(axis)
        start=V(position)+normal*.60
        total=0
        for x,y,z in directions:
            distance=tree.ray_cast(start,axis*x+other*y+normal*z,24.0)[3]
            if distance is not None:
                total+=.64*max(0,1-distance/12)**1.4+.36*(1-distance/24)
        shade[i]=1-.76*total/len(directions)
    plate=Image.fromarray(shade.reshape(ny,nx)).resize((w,h),Image.Resampling.BILINEAR)
    return np.asarray(plate,dtype=np.float32)


def textile_coordinates(grid,w,h):
    if grid is None:
        y,x=np.indices((h,w),dtype=np.float32)
        return x*.7,y*.7
    # 同一件衣料的小滚边与宽幅裁片共用纱线尺度，经向沿裁剪路径。
    length_u=np.linalg.norm(np.diff(grid,axis=1),axis=2)
    length_v=np.linalg.norm(np.diff(grid,axis=0),axis=2)
    weft=np.r_[0,np.cumsum(np.percentile(length_u,62,axis=0))]
    warp=np.r_[0,np.cumsum(np.percentile(length_v,62,axis=1))]
    x=np.interp(np.linspace(0,len(weft)-1,w),np.arange(len(weft)),weft)
    y=np.interp(np.linspace(0,len(warp)-1,h),np.arange(len(warp)),warp)
    return np.meshgrid(x,y)


def fibre_detail(source,destination):
    image=Image.open(source).convert("L")
    channels=[]
    # 每种纤维取实物样本的局部明暗，再镜像成为四边连续的循环。第四通道独立存圈绒。
    for col,row in ((1,1),(1,0),(2,1),(2,0)):
        x,y=col*512+109,row*512+119
        tile=image.crop((x,y,x+256,y+256))
        grey=np.asarray(tile,dtype=np.float32)
        broad=np.asarray(tile.filter(ImageFilter.GaussianBlur(5.2)),dtype=np.float32)
        residual=grey-broad
        signal=np.clip(.5+.195*residual/max(3.5,residual.std()),.045,.955)
        pair=np.concatenate((signal,signal[:,::-1]),axis=1)
        channels.append(np.concatenate((pair,pair[::-1]),axis=0))
    Image.fromarray(np.uint8(np.stack(channels,axis=2)*255+.5)).save(destination,compress_level=7)


def fabric_relief(rgb,height,gloss,kind,mat,cells,grid,rng):
    selection={"twill":"linen","bast":"sack","boucle":"boucle","fulled":"felt","wool":"felt",
               "hide":"leather","suede":"leather"}
    cell=selection.get(kind)
    if not cell:return rgb
    if mat in ("slate","cap","hatwelt"):cell="blue"
    if mat in ("hem","wrap","sackpatch"):cell="linen"
    u,v=textile_coordinates(grid,height.shape[1],height.shape[0])
    # 一个完整母片在雕塑坐标中的长度。黄麻纱束比亚麻粗，缩绒呢几乎看不到经纬格。
    span={"sack":47.0,"linen":55.0,"boucle":55.0,"felt":64.0,"blue":61.0,"leather":51.0}[cell]
    if mat=="sackpatch":span=32
    resolution=cells[cell][0].shape[0]
    dx=float(np.median(np.diff(u,axis=1)));dy=float(np.median(np.diff(v,axis=0)))
    footprint=max(1,max(dx,dy)*resolution/span)
    level=min(len(cells[cell])-1,max(0,int(np.floor(np.log2(footprint)))))
    tile=cells[cell][level]
    phase=rng.random(2)*2
    s=u/span+phase[0]+.014*np.sin(v*.11+.4)+.006*np.sin(v*.32)
    t=v/span+phase[1]+.011*np.sin(u*.13+v*.05)
    def mirror(p,n):
        return (1-np.abs(np.remainder(p,2)-1))*(n-1.002)
    swatch=sample(tile,mirror(s,tile.shape[1]),mirror(t,tile.shape[0]))
    tone,ridge=swatch[:,:,0],swatch[:,:,1]
    if cell=="leather":
        height[:]=height*.56+ridge*.24
        gloss+=tone*.13
        return rgb*(1+.32*tone[...,None])
    # 纹样用布上长度计量，小裁片和宽幅披肩的经纬应同粗。逐渐滤掉单个像素难以分辨的纱线。
    pitch={"sack":2.08,"linen":1.03,"boucle":2.10,"felt":1.35,"blue":1.42}[cell]
    warp=u/pitch+.09*np.sin(v*.15+phase[0])+.08*np.sin(v*.32+u*.024)
    weft=v/(pitch*1.12)+.095*np.sin(u*.14+phase[1])
    visible=np.clip((pitch/max(.08,dx,dy)-1.18)/1.4,0,1)
    crown_x=(.5+.5*np.cos(warp*2*np.pi))**1.55
    crown_y=(.5+.5*np.cos(weft*2*np.pi))**1.42
    if cell in ("sack","linen"):
        over=(np.cos(np.pi*(np.floor(warp+.5)+np.floor(weft+.5)))+1)*.5
        thread=over*(crown_x+.37*crown_y*(1-crown_x))+(1-over)*(crown_y+.37*crown_x*(1-crown_y))
        slub=(np.maximum(0,np.sin(2*np.pi*warp+.40))**6*
              np.maximum(0,np.sin(v*.26+3.1*np.floor(warp+.5)))**4)
        relief=(thread-.48+.53*slub)*visible
        height[:]=.14*height+.24*ridge+.49*relief
        gloss[:]=.076+.018*np.clip(tone,-1,1)+.017*thread
        return rgb*(1+.30*tone+.08*relief)[...,None]
    if cell=="boucle":
        # 小簇圈绒上有相连的亮缘和凹穴，斜光下破开大片连续的高光。
        ring=np.sqrt((np.sin(warp*np.pi))**2+(np.sin(weft*np.pi))**2)
        loops=np.exp(-((ring-.73)/.22)**2)-.36
        relief=.24*loops*visible+.46*ridge
        height[:]=.17*height+relief
        gloss[:]=.046+.018*np.clip(tone,-1,1)
        return rgb*(1+.39*tone+.08*relief)[...,None]
    nap=.47*ridge+.17*(crown_x-.4)*visible
    height[:]=.15*height+nap
    gloss[:]=.052+.026*np.clip(tone,-1,1)
    return rgb*(1+.27*tone+.03*nap)[...,None]


def hand_studies(path):
    image=Image.open(path).convert("RGB").resize((1448,1086),Image.Resampling.LANCZOS)
    plate=np.asarray(image,dtype=np.float32)
    broad=np.asarray(image.filter(ImageFilter.GaussianBlur(7)),dtype=np.float32)
    return plate,(plate-broad)@LUMA


def worked_skin(rgb,height,gloss,ch,points,u,v,hand):
    sx=1040+np.sin(2*pi*u)*87
    sy=1040-387*v
    photo,relief=hand
    color=sample(photo,sx,sy);detail=sample(relief,sx,sy)
    role=ch.part.split()[0]
    tint={"carrier":(.96,.88,.80),"leader":(1.00,.91,.81),"scout":(.97,.91,.85)}[role]
    color=(color+.54*detail[...,None])*tint
    height+=np.clip(detail/31,-.75,.65)*.43
    gloss+=np.clip(detail/240,-.065,.045)
    wrist=np.clip((v-.58)/.40,0,1)[...,None]
    neutral=np.array([177,126,94])*(1+np.clip(detail/580,-.025,.025)[...,None])
    return (rgb*.32+color*.68)*(1-wrist)+neutral*wrist


def finish_wear(rgb,height,gloss,mat,u,v,points,contact):
    x,d,z=np.moveaxis(points,2,0)
    row=BASE_ROW-C*z-S*d
    exposed=np.clip((contact-.70)/.28,0,1)
    speck=.60+.23*np.sin(1.63*x+.67*z)+.13*np.sin(.74*d-1.19*z)
    toe=np.maximum(0,-np.cos(2*pi*u))
    if mat=="boot":
        low=np.exp(-((z-12)/8.5)**2)
        wear=low*(.20+.74*toe**1.6)*exposed
        lip=np.exp(-((z-4.2)/1.9)**2)
        # 前掌弯折造成的横向油褶，避开鞋底与靴筒。
        fold=0
        for offset,width in ((12.0,.77),(15.3,.64),(18.1,.52)):
            fold+=np.exp(-((z-offset-2.1*np.sin(2*pi*u+.35))/width)**2)
        fold*=toe**1.7
        rgb*=1-.145*fold[...,None]
        rgb+=(wear+lip*.24)[...,None]*np.array([19,12,6])*speck[...,None]
        height-=fold*.55
        gloss+=wear*.14-fold*.043
    elif mat in ("jerkin","belt","strap","satchel","patch","vestpatch"):
        rim=np.exp(-(np.minimum(v,1-v)/.065)**2)
        if mat=="jerkin":rim=np.maximum(rim,np.exp(-(np.minimum(u,1-u)/.025)**2))
        oil={"belt":.82,"strap":.67,"satchel":.52,"jerkin":.46,"patch":.22,"vestpatch":.23}[mat]
        wear=exposed*(.16+.76*rim)*oil
        if mat=="satchel":wear+=.56*exposed*np.exp(-((x-534)/10.5)**2-((row-298)/6)**2)
        if mat=="belt":wear+=.32*exposed*np.exp(-((x-401)/15)**2)
        rgb+=wear[...,None]*np.array([27,16,6])*speck[...,None]
        gloss+=wear*.27
    elif mat in ("sack","sackpatch"):
        grip=np.exp(-((x-268)/24)**2-((row-251)/15)**2)*np.clip((11-d)/16,0,1)
        belly=np.exp(-((row-280)/10)**2)*(.5+.5*exposed)
        rgb*=1-(.23*grip+.10*belly)[...,None]
        gloss+=grip*.13
        height*=1-.34*grip
    elif mat in ("moss","slate","linen","russet","olive","slatepants","trousers","wrap"):
        hem=np.exp(-((1-v)/.050)**2)
        # 游离纤维只在剪口和经常磨到的褶脊发浅。
        nap=hem*exposed*(.3+.7*speck)
        rgb+=nap[...,None]*np.array([9,7,3])
        gloss+=.036*nap
    elif mat in ("steel","brass","bucklecore","edge"):
        gloss+=.16*exposed-.14*(1-exposed)
        rgb*=(.86+.14*exposed)[...,None]
    elif mat=="wood":
        grip=np.exp(-((row-314)/18)**2)
        gloss+=grip*.16
        rgb*=1-.12*grip[...,None]
    return rgb
