"""通过 Blender MCP 在项目根目录执行。尺寸单位为米，船首朝 +X。"""
from pathlib import Path
from math import pi, sin, cos, exp, atan2
import bpy
import numpy as np
from mathutils import Vector

root = Path.cwd()
art = root / "Unity/Catan/Assets/Art/Andalucia"
source = root / "art/source/安达卢西亚号.blend"
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.data.orphans_purge(do_recursive=True)
for m in list(bpy.data.materials):
    if m.users==0: bpy.data.materials.remove(m)
rng = np.random.default_rng(1607)
size = 1024
y,x = np.mgrid[:size,:size] * (2*pi/size)


def png(name, rgb, data=False, alpha=None):
    height,width=rgb.shape[:2]
    image = bpy.data.images.new(name, width=width, height=height, alpha=alpha is not None, is_data=data)
    if data:
        image.colorspace_settings.name="Non-Color"
    pixels = np.ones((height,width,4), np.float32)
    pixels[:,:,:3] = np.clip(rgb,0,1)
    if alpha is not None:
        image.alpha_mode="CHANNEL_PACKED"
        pixels[:,:,3]=alpha
    image.pixels.foreach_set(pixels.ravel())
    image.filepath_raw = str(art / (name+".png"))
    image.file_format="PNG"
    image.save()
    return image


def normal(name, height, strength):
    dx=(np.roll(height,1,1)-np.roll(height,-1,1))*strength
    dy=(np.roll(height,1,0)-np.roll(height,-1,0))*strength
    normals=np.stack((dx,dy,np.ones_like(dx)),axis=-1)
    return png(name, .5+.5*normals/np.linalg.norm(normals,axis=-1,keepdims=True),True)


grain = y + .052*np.sin(x+2*np.sin(y)) + .022*np.sin(3*x-3*y)
fibres = sum(np.sin(k*grain+rng.uniform(0,6.28)+.5*np.sin(2*x+k*y*.0))/k**.45 for k in range(13,145,4))
weather = .55*np.sin(3*grain+.7*np.sin(x))+.23*np.cos(9*grain)+.17*rng.normal(size=(size,size))
wood = png("橡木顺纹", np.exp((.17*fibres+.24*weather)[:,:,None])*np.array([.39,.225,.096]))
wood_normal = normal("橡木细纹法线",fibres+.18*weather,.32)
weave = np.cos(96*x)*(.75+.25*np.cos(48*y))+np.cos(96*y)*(.75-.25*np.cos(48*x))
linen = png("亚麻原布",np.array([.88,.85,.76])*(.97+.017*weave+.018*np.sin(3*x+np.sin(2*y))+.014*rng.normal(size=(size,size)))[:,:,None])
linen_normal = normal("亚麻经纬法线",weave,.19)

# 一张图容纳八块独立取样的船板，每块有自己的底色、年轮弯曲和小范围灰化。
board_tones=np.array([[.27,.105,.041],[.44,.205,.074],[.34,.155,.072],[.54,.285,.116],
                      [.38,.163,.054],[.48,.24,.106],[.31,.139,.066],[.60,.328,.133]])
lane=np.minimum((y/(2*pi)*len(board_tones)).astype(int),len(board_tones)-1)
local_y=(y/(2*pi)*len(board_tones))%1
knots=np.cos(x*2+lane*1.27)+np.sin(x+lane*2.19)
growth=local_y+.075*knots*np.sin(pi*local_y)+.022*np.sin(4*x+lane*.7)
bands=np.sin(2*pi*(5.9*growth+.27*np.sin(2*x+lane)))
pore=np.maximum(0,np.cos(2*pi*24*growth+.75*np.sin(2*x+lane)))**8
bleach=np.clip(.18+.17*knots+.13*bands,0,.50)
plank_rgb=board_tones[lane]*np.exp((.115*bands-.09*pore+.095*weather)[:,:,None])
plank_rgb+=bleach[:,:,None]*np.array([.073,.063,.049])
plank_color=png("舷板分色顺纹",plank_rgb)
plank_normal=normal("舷板导管法线",.55*bands-.48*pore+.12*weather,.44)

# 护舷方木截面两侧上缘最先磨亮，沿船长间断露出蜂蜜色木质。
perimeter=y/(2*pi)
edge=np.maximum(np.exp(-((perimeter-.385)/.084)**2),np.exp(-((perimeter-.737)/.084)**2))
contact=.50+.24*np.sin(x+2*grain)+.15*np.sin(3*x-2*grain)+.085*fibres
wear=np.clip(edge*(.51+.63*contact)+.14*contact,0,1)
rail_rgb=np.array([.19,.091,.043])+wear[:,:,None]*np.array([.45,.28,.105])
rail_color=png("护舷磨亮木面",rail_rgb*np.exp((.14*fibres+.10*weather)[:,:,None]))
rail_gloss=png("护舷磨耗光滑度",np.zeros((size,size,3)),True,np.clip(.23+.43*wear,.18,.66))

# 贴图定位于整张横帆，细折痕从帆脚向内展开；中部只保留细密织纹。
sv,su=np.mgrid[0:2048,0:2048].astype(np.float32)/2047
fine=np.zeros_like(sv)
for side in [0,1]:
    across=np.abs(su-side)*1.6
    above=1-sv
    radius=np.hypot(across,above)
    theta=np.arctan2(above,across+.005)
    taper=np.exp(-3.9*radius)*np.minimum(1,radius*21)
    phase=77*theta+13*radius+1.7*np.sin(27*radius+4*theta+side)+.65*np.sin(47*radius-6*theta)
    fine+=taper*(np.sin(phase)+.40*np.sin(phase*1.84+5*radius))
fine+=.17*sv**4*np.sin(174*sv+24*su+3*np.sin(15*su))
cloth_height=3.9*fine+.11*np.cos(2*pi*298*su)+.10*np.cos(2*pi*213*sv)
clew_normal=normal("帆脚张力法线",cloth_height,.84)
panel=np.floor(8*su)
loom=.008*np.cos(2*pi*298*su)+.007*np.cos(2*pi*213*sv)
tint=.98+.016*np.sin(panel*3.6)+.013*np.sin(19*su+4*np.sin(10*sv))+loom
canvas_color=png("横帆裁片原色",np.array([.90,.88,.81])*tint[:,:,None])


def material(name, color, rough, texture=None, bump=None):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    bs=m.node_tree.nodes.get("Principled BSDF")
    bs.inputs["Base Color"].default_value=(*color,1)
    bs.inputs["Roughness"].default_value=rough
    if texture:
        tex=m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image=texture
        mul=m.node_tree.nodes.new("ShaderNodeMixRGB")
        mul.blend_type="MULTIPLY"
        mul.inputs[0].default_value=1
        mul.inputs[2].default_value=(*color,1)
        m.node_tree.links.new(tex.outputs["Color"],mul.inputs[1])
        m.node_tree.links.new(mul.outputs[0],bs.inputs["Base Color"])
    if bump:
        tex=m.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image=bump
        n=m.node_tree.nodes.new("ShaderNodeNormalMap")
        n.inputs["Strength"].default_value=.52
        m.node_tree.links.new(tex.outputs[0],n.inputs["Color"])
        m.node_tree.links.new(n.outputs[0],bs.inputs["Normal"])
    return m

oak=material("船壳橡木",(.65,.59,.49),.65,wood,wood_normal)
plank=material("分色舷板",(.82,.79,.74),.68,plank_color,plank_normal)
rubrail=material("磨亮护舷",(.94,.90,.79),.45,rail_color,wood_normal)
gloss_node=rubrail.node_tree.nodes.new("ShaderNodeTexImage")
gloss_node.image=rail_gloss
invert=rubrail.node_tree.nodes.new("ShaderNodeMath")
invert.operation="SUBTRACT"
invert.inputs[0].default_value=1
rubrail.node_tree.links.new(gloss_node.outputs["Alpha"],invert.inputs[1])
rubrail.node_tree.links.new(invert.outputs[0],rubrail.node_tree.nodes.get("Principled BSDF").inputs["Roughness"])
wale=material("焦油护舷",(.23,.20,.19),.84,wood,wood_normal)
trim=material("磨亮扶栏",(1,.92,.62),.56,wood,wood_normal)
deck=material("晒旧甲板",(.96,1,1),.88,wood,wood_normal)
stern=material("艉楼雕木",(.70,.54,.42),.75,wood,wood_normal)
cloth=material("本白亚麻",(1,1,1),.94,linen,linen_normal)
course_cloth=material("受风亚麻",(1,1,1),.92,canvas_color,clew_normal)
thread=material("帆边缝线",(.42,.32,.20),.93)
seat=material("归属旗布",(.62,.045,.024),.89,None,linen_normal)
rope=material("焦油索具",(.064,.044,.028),.92)
hemp=material("浅麻绳",(.32,.22,.13),.93)
iron=material("锻铁",(.055,.049,.045),.63)
shadow=material("炮窗内壁",(.022,.015,.009),.95)

# 分组保留可编辑部件；游戏导出时合并为单个网格。
groups={}
group=""
def geometry(points, faces, uvs, mat, smooth=False):
    verts,polys,loops,mats,sm=groups.setdefault(group,([],[],[],[],[]))
    start=len(verts)
    verts.extend(points)
    polys.extend([tuple(i+start for i in f) for f in faces])
    loops.extend(uvs)
    mats.extend([mat]*len(faces))
    sm.extend([smooth]*len(faces))


def grid(rows, uvrows, mat, reverse=False, smooth=True):
    count=len(rows[0])
    faces=[]
    for i in range(len(rows)-1):
        for j in range(count-1):
            a=i*count+j
            f=(a,a+count,a+count+1,a+1)
            faces.append(f[::-1] if reverse else f)
    flat=[v for row in uvrows for v in row]
    geometry([v for row in rows for v in row],faces,[flat[i] for f in faces for i in f],mat,smooth)


def spar(a,b,r,s,mat,n=10,flat=False):
    a,b=Vector(a),Vector(b)
    axis=(b-a).normalized()
    p=axis.cross(Vector((0,0,1)) if abs(axis.z)<.9 else Vector((0,1,0))).normalized()
    q=axis.cross(p)
    pts=[]
    for center,radius in ((a,r),(b,s)):
        for j in range(n):
            angle=2*pi*j/n
            pts.append(center+float(radius)*(p*cos(angle)+q*sin(angle)))
    faces=[(j,(j+1)%n,(j+1)%n+n,j+n) for j in range(n)]
    uv=[v for j in range(n) for v in [(0,j/n), (0,(j+1)/n),((b-a).length/5,(j+1)/n),((b-a).length/5,j/n)]]
    for offset in [0,n]:
        f=list(range(offset,offset+n))
        faces.append(tuple(f if offset else reversed(f)))
        uv.extend([(.5+.48*cos(2*pi*i/n),.5+.48*sin(2*pi*i/n)) for i in faces[-1]])
    geometry(pts,faces,uv,mat,not flat)


def line(points,r,mat,n=6):
    for a,b in zip(points,points[1:]):
        spar(a,b,r,r,mat,n)


def timber(path, breadth, depth, mat):
    """沿构件中心线扫掠方木，转角切棱，木纹沿构件连续。"""
    points=list(map(Vector,path))
    d=min(breadth,depth)*.11
    w,h=breadth/2,depth/2
    section=[(-w+d,-h),(w-d,-h),(w,-h+d),(w,h-d),(w-d,h),(-w+d,h),(-w,h-d),(-w,-h+d)]
    rows=[];uv=[];distance=0
    for i,p in enumerate(points):
        direction=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        across=Vector((0,1,0))
        if abs(direction.dot(across))>.95: across=Vector((1,0,0))
        across=(across-direction*direction.dot(across)).normalized()
        up=direction.cross(across)
        if i: distance+=(p-points[i-1]).length
        rows.append([p+across*u+up*v for u,v in section+[section[0]]])
        uv.append([(distance/5,k/8) for k in range(9)])
    grid(rows,uv,mat,True,False)
    for i in [0,-1]:
        f=tuple(reversed(range(8))) if i==0 else tuple(range(8))
        geometry(rows[i][:8],[f],[section[j] for j in f],mat)


def sheave_block(center, scale):
    """两片椭圆木颊围住槽轮，外侧可见销头和深色绳槽。"""
    p=Vector(center)
    contour=[Vector((cos(i*pi/8)*.68,0,sin(i*pi/8)))*scale for i in range(16)]
    for side in [-1,1]:
        axis=Vector((0,side*scale,0))
        rings=[[p+axis*t+v*k for v in contour+[contour[0]]] for t,k in [(.10,.86),(.22,1),(.40,.82)]]
        grid(rings,[[(j/16,t) for j in range(17)] for t in [0,.5,1]],trim,side<0)
        f=tuple(range(16)) if side<0 else tuple(reversed(range(16)))
        geometry(rings[-1][:16],[f],[(contour[j].x,contour[j].z) for j in f],trim)
    axis=Vector((0,scale,0))
    spar(p-axis*.14,p+axis*.14,scale*.55,scale*.55,wale,12)
    spar(p-axis*.43,p+axis*.43,scale*.14,scale*.14,iron,8)


def slack(a,b,r,mat,sag):
    a,b=Vector(a),Vector(b)
    line([a.lerp(b,i/6)+Vector((0,0,-sag*4*(i/6)*(1-i/6))) for i in range(7)],r,mat)


def box(center, dims, mat, bevel=.045):
    # 八角截面并在端面切出倒角，沿长轴展开木纹。
    cx,cy,cz=center
    length,width,height=dims
    d=min(bevel,width*.2,height*.2,length*.12)
    w,h=width/2,height/2
    contour=[(-w+d,-h),(w-d,-h),(w,-h+d),(w,h-d),(w-d,h),(-w+d,h),(-w,h-d),(-w,-h+d)]
    rows=[];uv=[]
    for xx,k in [(-length/2,.93),(-length/2+d,1),(length/2-d,1),(length/2,.93)]:
        rows.append([(cx+xx,cy+t*k,cz+v*k) for t,v in contour+[contour[0]]])
        uv.append([(xx/5,j*.125) for j in range(9)])
    grid(rows,uv,mat,True,False)
    for ix in [0,3]:
        face=tuple(range(8)) if ix else tuple(reversed(range(8)))
        geometry(rows[ix][:8],[face],[(contour[j][0],contour[j][1]) for j in face],mat)


def width(x):
    return float(np.interp(x,[-20,-18,-15,-10,-4,2,7,11,14,17,19,20],[3.15,3.9,4.55,4.94,5.045,5.04,4.83,4.4,3.75,2.58,1.1,.16]))


def sheer(x):
    return 4.2+1.48*(max(-x,0)/20)**2+.95*(max(x,0)/20)**2


def hull(x,t,side,offset=0):
    shape=float(np.interp(t,[0,.08,.22,.43,.64,.83,1],[.07,.46,.77,.955,1,.985,.946]))
    lift=-2.1+1.6*(max(x-11,0)/9)**2+.9*(max(-x-12,0)/8)**2
    rake=max(0,(x-13)/7)**2*(1-t)*2.55
    return (x-rake,side*(width(x)*shape+offset),lift+(sheer(x)-lift)*t)


group="外板、护舷与炮列"
gun_stations=[-16.6,-13.3,-10,-6.7,-3.4,-.1,3.2,6.5,9.8,13.1]
for side in [-1,1]:
    group="外板、护舷与舷侧加强材"
    for row in range(21):
        # 连续船壳在板边内收成细缝；开炮窗的四层才需要按窗缘分段。
        ports=gun_stations if 13<=row<=16 else []
        joints=[-20]+list(np.arange(-19+(row%3)*1.65,20,5.4))+[20]
        breaks=sorted(set(joints+
                          [v for c in ports for v in [c-.49,c+.49]]))
        for a,b in zip(breaks,breaks[1:]):
            if any(abs((a+b)/2-c)<.49 for c in ports):continue
            board_idx=int(np.searchsorted(joints,(a+b)*.5)-1)
            sample=np.random.default_rng((side+1)*3000+row*83+board_idx)
            swatch=int(sample.integers(len(board_tones)))
            offset=sample.uniform(0,1)
            rows=[];uv=[]
            for xx in np.linspace(a,b,max(2,int((b-a)*2)+1)):
                ts=[row/21,(row+.035)/21,(row+.97)/21,(row+1)/21]
                rows.append([hull(xx,t,side,o) for t,o in zip(ts,[-.008,.003,.003,-.008])])
                if row>=7:
                    uv.append([(xx/6.5+offset,(swatch+.10+.80*(t*21-row))/len(board_tones)) for t in ts])
                else:
                    uv.append([(xx/5.8+row*.37,t*2.4+row*.13) for t in ts])
            grid(rows,uv,plank if row>=7 else wale,side>0,False)
    for t,d in [(.34,.28),(.56,.19),(.86,.23),(1,.14)]:
        timber([hull(xx,t,side,.07) for xx in np.linspace(-20,20,60)],.18,d,rubrail)
    for xx in [-18.1,-14.95,-11.65,-8.35,-5.05,8.15]:
        timber([hull(xx+.24*t,t,side,.081) for t in np.linspace(.22,1,17)],.19,.24,oak)
    for idx,c in enumerate(gun_stations):
        group=("右舷炮" if side<0 else "左舷炮")+f"{idx+1:02d}"
        xx,yy,zz=hull(c,.715,side)
        # 窗框沿弯曲船壳开口，四面内衬一直通往暗舱背板。
        mouth=[Vector(hull(c+dx,t,side)) for dx,t in
               [(-.49,13/21),(.49,13/21),(.49,17/21),(-.49,17/21)]]
        recess=[p+Vector((0,-side*.91,0)) for p in mouth]
        for j in range(4):
            k=(j+1)%4
            f=(3,2,1,0) if side>0 else (0,1,2,3)
            geometry([mouth[j],mouth[k],recess[k],recess[j]],[f],[(0,0),(1,0),(1,1),(0,1)],wale)
        geometry(recess,[(3,2,1,0) if side>0 else (0,1,2,3)],[(0,0),(1,0),(1,1),(0,1)],shadow)
        closed=idx in [0,3,5,8]
        muzzle=yy+side*(-.29 if closed else .31)
        spar((xx,yy-side*.84,zz),(xx,muzzle,zz),.20,.145,iron,16)
        spar((xx,muzzle-side*.08,zz),(xx,muzzle,zz),.169,.169,iron,16)
        spar((xx,muzzle,zz),(xx,muzzle+side*.008,zz),.105,.105,shadow,16)
        for dx in [-.53,.53]:
            timber([hull(c+dx,t,side,.018) for t in [.613,.822]],.10,.085,oak)
        ztop=hull(c,17/21,side)[2]
        if closed:
            corners=[p+Vector((0,side*.018,0)) for p in mouth]
            geometry(corners,[(3,2,1,0) if side>0 else (0,1,2,3)],[(0,0),(.2,0),(.2,.32),(0,.32)],oak)
        else:
            box((xx,yy+side*.46,ztop+.07),(1.07,.91,.12),oak)
            slack((xx,yy+side*.91,ztop+.13),(xx,yy,ztop+.67),.024,hemp,.04)
group="龙骨与船端封板"
for end in [-20,20]:
    rows=[[hull(end,t,s) for s in (-1,1)] for t in np.linspace(0,1,24)]
    grid(rows,[[(s*width(end)/5,t) for s in (-1,1)] for t in np.linspace(0,1,24)],stern if end<0 else oak,end>0)
line([hull(xx,0,0) for xx in np.linspace(-20,20,48)],.36,wale,12)
group="甲板与阶梯艉楼"
# 甲板沿纵向铺设，逐站随舷宽收尖。
for a,b,level,mat in [(-20,20,0,deck),(-20,-7,1.85,stern),(-20,-13.4,3.65,stern),(10.8,20,1.25,oak)]:
    stations=np.linspace(a,b,int((b-a)*1.6)+2)
    for band in range(27):
        rows=[];uv=[]
        for xx in stations:
            yz=[width(xx)*.946*(2*(band+t)/27-1) for t in [.018,.982]]
            rows.append([(xx,yy,sheer(xx)+level+.065*(1-(yy/width(xx))**2)) for yy in yz])
            uv.append([(xx/5+band*.41,yy/1.9) for yy in yz])
        grid(rows,uv,mat)
    for side in [-1,1]:
        def edge(xx,z):return (xx,side*width(xx)*.946,sheer(xx)+z)
        if level:
            base=1.85 if level>2 else 0
            if a<0:
                for row in range(7):
                    rows=[[edge(xx,base+(level-base)*(row+t)/7) for t in [.006,.994]] for xx in stations]
                    grid(rows,[[(xx/5,row*.27+t*.2) for t in [0,1]] for xx in stations],mat,side>0)
            for end in [a,b]:
                z=sheer(end)
                box((end,0,z+(base+level)/2),(.13,width(end)*1.89,level-base),mat)
            if a>0:continue
        # 舷墙上部留出扶手透空，下面保留连续挡浪板。
        for z1,z2 in [(0,.24),(.244,.49)]:
            rows=[[edge(xx,level+z1),edge(xx,level+z2)] for xx in stations]
            grid(rows,[[(xx/5,z1+.13),(xx/5,z2+.13)] for xx in stations],mat if level else oak,side>0)
        for xx in np.linspace(a,b,int((b-a)/1.12)+2):
            spar(edge(xx,level),edge(xx,level+.85),.066,.062,trim,8)
        for off in [.39,.85]:
            line([edge(xx,level+off) for xx in stations],.061 if off<.5 else .085,trim,8)
# 艉封板横廊、侧廊窗与立柱。
for side in [-1,1]:
    for xx in np.arange(-19.4,-13.5,.93):
        yy=side*(width(xx)*.946+.04); zz=sheer(xx)+2.76
        box((xx,yy,zz),(.67,.14,.86),shadow)
        for d in [-.34,0,.34]:box((xx+d,yy+side*.09,zz),(.067,.11,.93),trim)
        box((xx,yy+side*.09,zz),(.75,.09,.075),trim)
for yy in np.linspace(-2.64,2.64,7):
    box((-20.11,yy,7.95),(.2,.64,.91),shadow)
    for delta in [-.32,0,.32]:box((-20.23,yy+delta,7.95),(.1,.065,.98),trim)
for zz in [7.35,8.51,9.24]:box((-20.29,0,zz),(.44,6.42,.13),trim)
# 舵楼门、窗格、阶梯和艉廊绕栏。
for front,h in [(-7,0),(-13.4,1.85)]:
    floor=sheer(front)+h
    box((front+.09,.65,floor+.84),(.16,1.03,1.66),oak)
    for yy in [.15,1.15]:
        spar((front+.22,yy,floor),(front+.22,yy,floor+1.7),.069,.069,trim,8)
    for side in [-1,1]:
        for j in range(9):
            box((front+2.46-j*.29,side*2.56,floor+(j+1)*.206),(.40,1.18,.13),deck)
        for yy in [side*2.56-.61,side*2.56+.61]:
            spar((front+2.52,yy,floor+.72),(front-.04,yy,floor+2.54),.067,.067,trim)
            for j in [0,4,8]:
                xx=front+2.46-j*.29;zz=floor+(j+1)*.206
                spar((xx,yy,zz),(xx,yy,zz+.59),.046,.046,trim)
box((-20.64,0,7.28),(1.22,6.98,.2),stern)
for yy in np.linspace(-3.35,3.35,15):
    spar((-21.16,yy,7.35),(-21.16,yy,8.18),.051,.067,trim)
for zz in [7.70,8.18]:
    spar((-21.16,-3.42,zz),(-21.16,3.42,zz),.074,.074,trim)
for side in [-1,1]:
    for yy in [side*3.4]:
        spar((-21.16,yy,8.18),(-19.85,yy,8.18),.073,.073,trim)
for xx in [-3.4,3.4]:
    box((xx,0,sheer(xx)+.14),(2.5,1.8,.23),wale)
    for t in np.arange(-1.14,1.18,.21):box((xx+t,0,sheer(xx)+.29),(.088,1.7,.10),trim)
    for t in np.arange(-.75,.8,.24):box((xx,t,sheer(xx)+.29),(2.4,.09,.10),trim)

group="艏楼两舷与系索台"
for side in [-1,1]:
    # 艏楼外侧挡板在前桅附近最高，向格栅平台逐渐落低。
    stations=np.linspace(6.4,19.8,35)
    def parapet(x,t):
        rise=float(np.interp(x,[6.4,10.8,12,16,19.8],[.45,2.75,2.65,1.63,1.32]))
        return (x,side*(width(x)*.946+.048-.16*t),sheer(x)+rise*t)
    for row in range(7):
        ts=[row/7,(row+1)/7]
        joints=[6.4]+list(np.arange(7.5+row%3*1.32,19.8,4.95))+[19.8]
        for i,(start,end) in enumerate(zip(joints,joints[1:])):
            xs=sorted({start,end,*[x for x in stations if start<x<end]})
            lane=(row*5+i*3+(side+1))%len(board_tones)
            rows=[[parapet(x,t) for t in ts] for x in xs]
            grid(rows,[[(x/6+row*.31,(lane+v)/len(board_tones)) for v in [.12,.88]] for x in xs],plank,side>0)
    timber([parapet(x,1) for x in stations],.15,.16,rubrail)
    for x in np.linspace(6.9,18.9,7):
        timber([parapet(x,t) for t in [0,1]],.14,.12,oak)
    # 侧支索的外伸系索台与下方铁链板。
    for x,span in [(-4.2,4.2),(9.35,3.25)]:
        y=side*(width(x)*.95+.23);z=sheer(x)+.13
        box((x,y,z),(span,.70,.24),wale)
        for station in np.linspace(x-span*.36,x+span*.36,3):
            timber([(station,side*width(station)*.96,z-.67),(station,y,z-.11)],.16,.19,oak)

group="镂空艏楼与斜桅"
# 中央艏柱向上弯至平台前端；左右托膝从艏柱展开，托住格栅横梁。
timber([(17.4,0,-.8),(18.7,0,1.1),(20.1,0,2.55),(22,0,3.8),
        (24.3,0,5.12),(26.4,0,6.57)],.58,.72,wale)
for side in [-1,1]:
    for h in [0,.53,1.05]:
        timber([(16,side*3.15,5.28+h),(20.5,side*2.04,5.62+h),
                (23.7,side*1.47,6.12+h),(26.4,side*.89,6.7+h)],.22,.21 if h else .34,rubrail)
        if side>0:timber([(26.4,-.89,6.7+h),(26.4,.89,6.7+h)],.22,.21,rubrail)
    for t in np.linspace(0,1,12):
        xx=16+10.4*t; yy=side*(3.15-2.26*t); zz=5.23+1.47*t**1.4
        timber([(xx,yy,zz),(xx+.12,yy,zz+1.10)],.15,.17,oak)
    timber([(14,side*3.7,2.7),(17,side*2.68,3.16),(20,side*1.9,3.94),
            (23.4,side*1.16,5.1),(26.4,side*.89,6.58)],.30,.41,oak)
    for t in np.linspace(0,1,8):
        xx=17.3+8.1*t
        beam=3.0-2.01*t
        z=5.33+1.20*t
        timber([(xx-.92,side*.19,1.52+3.95*t),(xx-.65,side*beam*.51,z-.99),
                (xx,side*beam,z-.13)],.30,.42,oak)
        if side>0:timber([(xx,-beam,z-.13),(xx,beam,z-.13)],.23,.27,wale)
        for shift in [-.065,.065]:
            p=Vector((xx+shift,side*(beam+.13),z-.16))
            spar(p,p+Vector((0,side*.018,0)),.044,.041,iron,8)
for xx in np.linspace(18.8,26.2,19):
    w=2.36-(xx-18.8)*.19
    box((xx,0,5.46+(xx-18.8)*.16),(.22,2*w,.12),oak)
spar((13.5,0,5.3),(34.5,0,16.7),.44,.13,trim,18)
for t in np.linspace(.12,.93,14):
    p=Vector((13.5+21*t,0,5.3+11.4*t))
    spar(p,p+Vector((.13,0,.07)),.46-.30*t,.46-.30*t,rope,14)

def sail(xx,top,wide,drop,angle,mark=False):
    # 横桁略斜，帆脚下垂，腹部顺风鼓起，边角由帆脚索拉紧。
    def pt(u,v):
        # 两个受力帆角各自产生扇形褶；兜风重心向下风舷偏移。
        phase=xx*.19+top*.27
        belly=(sin(pi*v*.86)**.92)*(1-.74*u*u)*(1+.15*u)
        fold=0
        for sign in [-1,1]:
            dx=(1-sign*u)*wide
            dz=(1-v)*drop
            radius=(dx*dx+dz*dz)**.5
            fold+=.105*exp(-radius/(drop*.68))*sin(24*atan2(dz,dx+.18)+radius*.87+sign*phase)
            # 各条褶脊有不同倾角和消退距离，帆角收紧、向内逐渐变宽。
            for k,slope in enumerate((.12,.28,.49,.79,1.25,1.92)):
                spread=.055+.025*dx+.014*(k%3)
                distance=(dz-slope*dx-.12*sin(.75*dx+phase+k)*min(1,dx))/spread
                if abs(distance)<4:
                    fold+=(.057+.012*(k%3))*(1-2*distance**2)*exp(-distance**2-dx/(wide*.51))*min(1,radius*2)
        fold+=.033*sin(11*u+2.7*v+phase)*sin(18*v-5*u+phase)
        # 帆腹下半部的短斜褶随风和帆脚张力变化，避开规则的整幅波纹。
        fold+=.063*v**1.4*sin(53*v+15*u+3*sin(6*u+phase))*sin(5*u+3*v+phase)**2
        yy=u*wide*(1+.065*v)+.19*sin(pi*v)*(1-u*u)
        depth=drop*.235*belly+fold*min(1,9*v)
        foot=drop*v**2*(.195*(1-u*u)*(1+.25*u)+.033*u)
        return (xx+angle*yy+depth,yy,top-drop*v+foot-.12*u*u)
    us=np.linspace(-1,1,113)
    vs=np.unique(np.r_[np.linspace(0,.625,21),np.linspace(.625,1,55)])
    rows=[[pt(u,v) for v in vs] for u in us]
    first_face=len(groups.get(group,([],[]))[1])
    grid(rows,[[((u+1)*.5,v) for v in vs] for u in us],course_cloth,True)
    if mark:
        # 同一网格的上帆包边使用席位色，保持双面弯曲处垂顺贴合。
        for i in range(len(us)-1):
            for j in np.flatnonzero((vs[:-1]>=.936)&(vs[:-1]<.967)):
                groups[group][3][first_face+i*(len(vs)-1)+j]=seat
    for u in us[::14]:
        line([pt(u,v) for v in vs[::2]],.012,thread,5)
    for v in [0,14/32,22/32,1]:
        line([pt(u,v) for u in us],.032 if v in [0,1] else .014,hemp if v in [0,1] else thread,6)
    # 缝在两处帆脚加强布上的斜缝，沿受力方向分散拉力。
    for side in [-1,1]:
        for reach in [.10,.17]:
            line([pt(side*(1-reach*t),1-reach*(1-t)) for t in np.linspace(0,1,12)],.014,thread,5)
    for u in np.linspace(-.90,.90,10):
        p=Vector(pt(u,.44))
        line([p,p+Vector((.13,.02,-.21)),p+Vector((.17,.06,-.43))],.021,hemp,5)
    for sign in [-1,1]:
        a=Vector(pt(0,0))+Vector((-.17,0,.12))
        b=Vector(pt(sign,0))+Vector((-.17,sign*.36,.13))
        spar(a,b,.19 if wide>5 else .14,.073,trim,12)
        for t in np.linspace(.09,.93,8):
            p=a.lerp(b,t)
            spar(p,p+(b-a).normalized()*.10,.19-.094*t,.19-.094*t,hemp,8)
        if xx<20:
            # 两侧千斤索上提帆桁，转桁索和帆脚索分别回到甲板上的系索栏。
            head=(xx-.35,0,top+min(3.1,drop*.48))
            slack(head,b,.038,hemp,.08)
            for frac in [.38,.71]:
                line([head,a.lerp(b,frac)],.026,rope)
            land=(max(-17,xx-4.6),sign*(width(xx)*.88),sheer(xx)+.8)
            lead=Vector(land)+Vector((0,0,.32))
            cheek=b+Vector((-.11,0,-.29))
            sheave_block(cheek,.24)
            sheave_block(lead,.22)
            for dx in [-.084,.084]:
                delta=Vector((dx,0,0))
                slack(cheek+delta,lead+delta,.031,hemp,.14)
            clew=Vector(pt(sign,1))
            sheave_block(clew,.18)
            slack(clew,(land[0]-1.0,land[1],land[2]),.044,hemp,.09)
            # 收帆索经过横桁内侧滑车，下引到桅脚前的系索栏。
            lead_yard=a.lerp(b,.31)
            sheave_block(lead_yard,.16)
            slack(clew,lead_yard,.026,hemp,.11)
            slack(lead_yard,(xx-.8,sign*1.5,sheer(xx)+1),.026,hemp,.13)
        else:
            tie=(33.7,0,23.35) if top>17 else (32.7,0,16.0)
            sheave_block(b,.16)
            slack(b,tie,.034,hemp,.07)
            slack(pt(sign,1),(20.5,sign*1.8,6.0),.039,hemp,.16)
    return pt


for xx,height,base,topdeck,name in [(-14.6,22,8,16.8,"后桅"),(-2.5,35.5,4.35,20.5,"主桅"),(11.1,30.7,5.9,18.6,"前桅")]:
    group=name+"桅杆与支索"
    spar((xx,0,base),(xx-.32,0,topdeck+.9),.41,.28,trim,20)
    spar((xx-.31,0,topdeck-.7),(xx-.62,0,height),.245,.092,trim,16)
    for z in np.arange(base+.7,topdeck,1.28):
        sx=xx-.32*(z-base)/(topdeck+.9-base)
        spar((sx,0,z),(sx,0,z+.12),.433-.13*(z-base)/(topdeck-base),.433-.13*(z-base)/(topdeck-base),rope,14)
    spar((xx-.3,0,topdeck-.44),(xx-.3,0,topdeck),.78,1.20,oak,22)
    spar((xx-.3,0,topdeck),(xx-.3,0,topdeck+.13),1.25,1.25,trim,24)
    for side in [-1,1]:
        foot=[];head=[]
        for j in range(5):
            px=xx-3.6+j*.72; py=side*width(px)*.983
            start=Vector((px,py,sheer(px)+(.96 if xx<-10 else .34)))
            end=Vector((xx-.55+j*.095,side*.51,topdeck-.05))
            upper=start+Vector((-.07,-side*.11,.72))
            foot.append(upper);head.append(end)
            yy=side*width(px)*.945
            spar((px,yy,sheer(px)-.92),start,.066,.061,iron,8)
            for p in [start,upper]:
                sheave_block(p,.20)
            for dx in [-.083,0,.083]:
                line([start+Vector((dx,0,.08)),upper+Vector((dx,0,-.08))],.024,hemp)
            line([upper,end],.056,rope)
        for k in np.arange(.043,.95,.046):
            spar(foot[0].lerp(head[0],k),foot[-1].lerp(head[-1],k),.032,.032,hemp,6)
        box((xx-1.7,side*width(xx)*.85,sheer(xx)+.8),(3.45,.25,.15),trim)
        for px in np.linspace(xx-3.1,xx-.3,8):
            ypin=side*width(xx)*.85
            spar((px,ypin,sheer(xx)+.57),(px,ypin,sheer(xx)+1.01),.045,.065,trim,8)
        line([(xx-.60,side*.08,height-.43),(xx-.36,side*1.13,topdeck+.14),(xx-4,side*width(xx)*.96,sheer(xx)+.4)],.046,rope)
        if name!="后桅":
            high=Vector((xx-.58,side*.14,height-1.1))
            low=Vector((xx-.35,side*1.06,topdeck+.08))
            for delta in [-.55,0,.55]:
                slack(low+Vector((delta,0,0)),high+Vector((delta*.13,0,0)),.032,rope,.04)
            for t in np.linspace(.055,.78,13):
                mid=low.lerp(high,t)
                half=.53*(1-.87*t)
                spar(mid-Vector((half,0,0)),mid+Vector((half,0,0)),.023,.023,hemp,5)
    # 席位色长燕尾旗
    rows=[]; uv=[]
    for t in np.linspace(0,1,15):
        xx1=xx-.62-3.35*t; yy=.26*sin(8*t)
        rows.append([(xx1,yy,height-.18-.24*t),(xx1+.39*t*t,yy,height-.6+.04*sin(7*t)),(xx1,yy,height-1.07+.28*t)])
        uv.append([(t,0),(t,.5),(t,1)])
    grid(rows,uv,seat)
    if xx>0:
        group="帆片－前桅下横帆"
        sail(xx+.23,18.21,8.50,10.15,.20)
        group="帆片－前桅上横帆"
        sail(xx-.02,27.5,5.10,7.65,.17,True)
    elif xx>-10:
        group="帆片－主桅下横帆"
        sail(xx+.18,18.0,8.85,11.65,.23)
        group="帆片－主桅上横帆"
        sail(xx-.10,32.15,5.95,9.80,.19,True)
    else:
        group="帆片－后桅三角帆"
        a=Vector((-9.6,.22,12.2));b=Vector((-20.6,.22,23.3));c=Vector((-19.9,1.22,10.2))
        spar(a,b,.18,.092,trim,14)
        def lateen(t,v):
            tension=v*v*(1-v)*sin(pi*t)
            gathers=tension*(.38*sin(32*t+3*v)+.17*sin(73*t-7*v))
            return a.lerp(b,t).lerp(c,v)+Vector((0,.94*sin(pi*v)*(1-.45*t)+gathers,0))
        ts=np.linspace(0,1,57);vs=np.linspace(0,.996,35)
        grid([[lateen(t,v) for v in vs] for t in ts],[[(t*4,v*4) for v in vs] for t in ts],cloth)
        for t in ts[::7]:line([lateen(t,v) for v in vs],.022,thread)
        line([a,c,b],.041,hemp)
group="艏帆与首尾纵索"
spar((33.55,0,16.0),(33.7,0,23.75),.18,.076,trim,16)
timber([(32.8,0,16.03),(34.16,0,16.63)],.62,.34,wale)
group="帆片－下艏帆"
sail(28.7,13.45,4.65,5.58,-.13)
group="帆片－上艏帆"
sail(33.82,22.95,3.12,5.12,-.11)
group="艏帆与首尾纵索"
for side in [-1,1]:
    slack((33.7,0,23.55),(27.1,side*1.1,12.65),.040,rope,.07)
    # 艏斜桅横向支索经两舷张紧，前支索在桅根前分叉向两侧。
    lead=Vector((16.4,side*2.85,5.07))
    tip=Vector((30.7,side*.15,14.66))
    for i in [0,.16]:
        slack(tip,lead+Vector((i,0,0)),.045,rope,.10)
    sheave_block(lead,.24)
    for tip,base in [((10.7,side*.18,18.9),(23.3,side*1.1,10.6)),
                     ((-2.9,side*.17,20.6),(10.6,side*2.45,6.38)),
                     ((-14.9,side*.15,16.7),(-3.2,side*1.65,5.22))]:
        slack(tip,base,.039,hemp,.12)
for a,b in [((-15.2,0,21.6),(-3,0,20.8)),((-3,0,35),(10.8,0,19.8)),((-2.8,0,20.8),(11.1,0,7.6)),((10.5,0,30.5),(34.4,0,16.6)),((10.8,0,19),(28,0,13.2)),((33.4,0,16.1),(19.6,0,3.4))]:
    line([a,b],.068,rope)
# 艏锚，横杆、锚爪与锚索。
for side in [-1,1]:
    a=Vector((14.1,side*4.0,5.6));b=a+Vector((-1.15,side*.3,-2.05))
    spar(a,b,.12,.14,iron)
    line([b+Vector((-.68,0,.72)),b+Vector((-.53,0,.18)),b,b+Vector((.62,0,-.18)),b+Vector((.99,0,.23))],.14,iron)
    spar(a+Vector((-.62,0,.29)),a+Vector((.64,0,-.34)),.16,.13,trim)
    line([(16,side*3.35,6.95),a],.068,hemp)

objects=[]
for label,(points,faces,uvlist,materials,smooth) in groups.items():
    mesh=bpy.data.meshes.new(label)
    mesh.from_pydata(points,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(label,mesh)
    bpy.context.collection.objects.link(obj)
    slots=list(dict.fromkeys(materials))
    for m in slots:mesh.materials.append(m)
    mesh.polygons.foreach_set("material_index",[slots.index(m) for m in materials])
    mesh.polygons.foreach_set("use_smooth",smooth)
    layer=mesh.uv_layers.new(name="顺纹与裁片")
    layer.data.foreach_set("uv",np.asarray(uvlist,dtype=np.float32).ravel())
    objects.append(obj)
for obj in objects:obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
assert sum(label.startswith("帆片－") for label in groups)==7
assert all(sum(label.startswith(side+"舷炮") for label in groups)==10 for side in ["左","右"])
bpy.context.preferences.filepaths.save_version=0
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=="VIEW_3D":
            space=area.spaces.active
            space.region_3d.view_rotation=Vector((-74,72,-19)).to_track_quat("-Z","Y")
            space.region_3d.view_distance=77
            space.region_3d.view_location=(4,0,15)
            space.shading.color_type="MATERIAL"
            space.overlay.show_overlays=False
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(source),compress=True)
bpy.ops.object.join()
obj=bpy.context.object
obj.name="安达卢西亚号"
obj.data.calc_loop_triangles()
bpy.ops.export_scene.fbx(filepath=str(art/"安达卢西亚号.fbx"),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",add_leaf_bones=False,bake_anim=False,path_mode="STRIP",use_tspace=False)
result={"三角面":len(obj.data.loop_triangles),"网格分组":len(groups),"材质槽":len(obj.data.materials),"尺寸":list(obj.dimensions)}





