"""行商的两个手势。指骨控制点使用原型坐标，掌指之间沿用人体基网的连续四边面。"""
from functools import lru_cache
from math import atan2, cos, radians, sin
from pathlib import Path
import sys

import numpy as np
from mathutils import Quaternion
from mathutils.bvhtree import BVHTree

from workshop import FRONT, P, V

# 游戏的人形棋子共用同一份 HM08 手部裁片、摆姿算法和未摆姿的皮肤 UV。
HUMAN_RECIPE = Path(__file__).resolve().parents[1] / "roadside-bandits"
if str(HUMAN_RECIPE) not in sys.path:
    sys.path.append(str(HUMAN_RECIPE))
from hand_mesh import deform, hand_uvs, settle_skin, template, unit


OFFER = "12 摊掌迎客"
HOLD = "13 扶住布卷的手"
DIGITS = ("拇指", "食指", "中指", "无名指", "小指")
# HM08 的甲板内有独立的四边面环。甲根与甲沟仍使用周围的皮肤。
NAIL_FACES = ((844,845,846,847,848,849),
              (170,171,172,173,174,175,894,895),
              (356,357,358,359,360,361,932,933),
              (542,543,544,545,546,547,970,971),
              (728,729,730,731,732,733,1008,1009))


def plate_normal(vertices, faces):
    q=vertices[faces]
    return unit(np.cross(q[:,2]-q[:,0],q[:,3]-q[:,1]).sum(axis=0))


@lru_cache(maxsize=1)
def nail_uvs():
    h=template()
    result={}
    for i,ids in enumerate(NAIL_FACES):
        faces=h["faces"][list(ids)]
        vertices=h["vertices"]
        norm=plate_normal(vertices,faces)
        a,b=h["bones"][7+3*i]
        side=unit(np.cross(b-a,norm))
        length=np.cross(norm,side)
        uv=vertices@np.stack([side,length],axis=1)
        region=uv[faces].reshape(-1,2)
        uv=(uv-region.min(axis=0))/np.ptp(region,axis=0)
        result.update((index,uv) for index in ids)
    return result


def oppose_digits(vertices, faces, fingers, palm, offering_hand):
    """按甲板的实测法线校准指骨轴转。拇指可对掌，另外四指的指腹靠向掌内或织物。"""
    h=template()
    weights=h["weights"][:,5:].reshape(-1,5,3).sum(axis=2)
    original=vertices.copy()
    for i,indices in enumerate(NAIL_FACES):
        chain=np.array(fingers[i])
        bones=np.diff(chain,axis=0)
        lengths=np.linalg.norm(bones,axis=1)
        axes=bones/lengths[:,None]
        axis=axes[-1]
        normal=plate_normal(original,faces[list(indices)])
        if offering_hand:
            target=np.array((-.90,.48,.02)) if i==0 else -np.asarray(palm)
        else:
            target=np.array((-.32,-.72,.62) if i==0 else (-.57,-.83,.13-.035*i))
        old=unit(normal-axis*(normal@axis))
        new=unit(target-axis*(target@axis))
        angle=atan2(axis@np.cross(old,new),old@new)
        mask=weights[:,i]>.015
        pts=original[mask]
        longitudinal=np.clip(np.einsum("nbi,bi->nb",pts[:,None]-chain[:-1],axes)/lengths,0,1)
        pivots=chain[:-1]+longitudinal[:,:,None]*bones
        nearest=np.argmin(((pts[:,None]-pivots)**2).sum(axis=2),axis=1)
        j=np.arange(len(pts))
        t=longitudinal[j,nearest]
        length_along=(np.r_[0,np.cumsum(lengths)][nearest]+t*lengths[nearest])/lengths.sum()
        joint_axis=np.stack((axes[0],unit(axes[0]+axes[1]),unit(axes[1]+axes[2]),axes[2]))
        turn_axis=joint_axis[nearest]*(1-t[:,None])+joint_axis[nearest+1]*t[:,None]
        turn_axis/=np.linalg.norm(turn_axis,axis=1)[:,None]
        # 旋转从掌指关节渐入，到末节之前已经完成，保留天然甲板和指腹的截面。
        mix=np.clip((length_along+.08)/.78,0,1)
        bend=angle*mix*mix*(3-2*mix)*weights[mask,i]
        delta=pts-pivots[j,nearest]
        radial=delta-turn_axis*np.einsum("ij,ij->i",turn_axis,delta)[:,None]
        vertices[mask]+=radial*(np.cos(bend)-1)[:,None]+np.cross(turn_axis,radial)*np.sin(bend)[:,None]


def offering():
    wrist = P(150, 356.5, -162)
    forward = V((.555, -.785, -.275)).normalized()
    across = V((.815, .57, .1))
    across = (across - forward * across.dot(forward)).normalized()
    normal = forward.cross(across).normalized()
    def local(x, y, z):
        return wrist + across*x + forward*y + normal*z

    fingers = [[local(-12, 18, 6), P(148,343,-185), P(148,332,-201), P(146,323,-216)]]
    # 掌骨头形成浅弧。三段长度和屈曲角逐指独立，小指侧略前收。
    for x, y, z, lengths, angles, splay in (
        (-14.2, 49, 1.4, (16, 12, 10), (12, 30, 43), -3.5),
        (-1.8, 50.5, .0, (18, 14, 11), (16, 34, 48), -1.0),
        (9.7, 47.6, .8, (16.5, 12.4, 10.3), (19, 38, 53), 1.5),
        (19.7, 42, 2.2, (12.4, 9.3, 8.2), (24, 42, 58), 4.0),
    ):
        points = [local(x, y, z)]
        for length, angle in zip(lengths, angles):
            bend, spread = radians(angle), radians(splay)
            points.append(points[-1] + length * (forward*cos(bend)*cos(spread)
                                                   + across*cos(bend)*sin(spread) + normal*sin(bend)))
        fingers.append(points)
    return wrist, fingers, normal, 54.5, P(152,362,-140)


def holding(cloth):
    # 关节轴线绕过布头近侧的厚边，指腹在实际织物表面落位。
    def pad(x, row, clearance):
        hit = cloth.ray_cast(P(x,row,-240), -FRONT, 350)[0]
        if hit is None:
            raise ValueError(f"扶卷手取样超出布面：{x}, {row}")
        return P(x, row, hit.y-clearance)

    wrist = P(383.7,403,-92)
    fingers = [
        [P(385,387.5,-111), P(385,370.5,-125.5), pad(375,360.5,8.8), pad(365,357.5,6.4)],
        [P(375.7,381,-130), pad(361,380.8,8.8), pad(351.2,377.6,6.8), pad(343,374,5.1)],
        [P(376.3,392.3,-133), pad(361,391.1,8.8), pad(347.8,387.4,6.3), pad(337.6,383,4.4)],
        [P(374.5,402,-132), pad(360,400.6,7.8), pad(347.5,397,6.5), pad(337.7,392.7,4.6)],
        [P(371.2,410.1,-128), pad(360,409.6,6.8), pad(350.4,406,5.5), pad(341.6,402,4.0)],
    ]
    return wrist, fingers, V((-.72,.62,.17)), 50.5, P(376,401,-65)


def cushion_thumb(vertices,faces,chain,amount):
    h=template()
    a,b=np.asarray(chain[-2:])
    axis=unit(b-a)
    dorsal=plate_normal(vertices,faces[list(NAIL_FACES[0])])
    dorsal=unit(dorsal-axis*(axis@dorsal))
    delta=vertices-a
    t=(delta@axis)/np.linalg.norm(b-a)
    pad=np.clip(.35-delta@dorsal/6,0,1)
    roundness=np.exp(-((t-.68)/.46)**2)*pad*h["weights"][:,7]
    vertices-=np.outer(amount*roundness,dorsal)


def fit_to_wool(vertices,faces,cloth):
    # 在布头的投影平面求皮肤背缘到羊毛的余量。相同截面的掌背和指腹一起退让，
    # 保留软组织厚度；外推到布边旁的邻点，虎口跨过布边时也不会产生尖折。
    skin=BVHTree.FromPolygons(vertices.tolist(),faces.tolist())
    step=1.25
    low=np.floor(vertices[:,[0,2]].min(axis=0))-8
    high=np.ceil(vertices[:,[0,2]].max(axis=0))+8
    nx,nz=np.ceil((high-low)/step).astype(int)+1
    field=np.zeros((nz,nx))
    for j in range(nz):
        z=low[1]+j*step
        for i in range(nx):
            x=low[0]+i*step
            fabric=cloth.ray_cast(V((x,-240,z)),-FRONT,300)[0]
            if fabric is not None:
                front=skin.ray_cast(V((x,-240,z)),-FRONT,300)[0]
                if front is None or front.y>fabric.y: continue
                # 虎口可绕到布边后方。只取从相机一侧进入的这段皮肤，避免投影重叠的腕背抬起指尖。
                back=skin.ray_cast(front-FRONT*.015,-FRONT,130)[0]
                if back is not None and back.y>=fabric.y-.2:
                    limit=4.2 if z>795-370 else 2.8
                    field[j,i]=min(limit,back.y-fabric.y+.30)
    # 限制每单位长度内的位移差，接触垫自然过渡至手背。
    for _ in range(30):
        pad=np.pad(field,1,mode="edge")
        following=field.copy()
        for dz,dx in ((0,1),(1,0),(0,-1),(-1,0),(1,1),(1,-1),(-1,1),(-1,-1)):
            following=np.maximum(following,pad[1+dz:1+dz+nz,1+dx:1+dx+nx]-.71*step*np.hypot(dx,dz))
        if np.array_equal(following,field):break
        field=following
    x,z=((vertices[:,[0,2]]-low)/step).T
    ix,iz=x.astype(int),z.astype(int)
    tx,tz=x-ix,z-iz
    vertices[:,1]-=(1-tz)*((1-tx)*field[iz,ix]+tx*field[iz,ix+1])+tz*((1-tx)*field[iz+1,ix]+tx*field[iz+1,ix+1])


def add_hand(g, name, pose, cloth=None):
    wrist, fingers, palm, girth, sleeve = pose
    vertices, mirrored = deform(wrist, fingers, palm, girth, flesh=.35 if name==OFFER else .38)
    hand = template()
    length_axis=hand["bones"][11,0]-hand["bones"][0,0]
    local=((hand["vertices"]-hand["bones"][0,0])@length_axis)/(length_axis@length_axis)
    influence=np.clip((.49-local)/.49,0,1)**2
    axis=np.asarray((fingers[2][0]-wrist).normalized())
    delta=vertices-np.asarray(wrist)
    vertices+=.26*influence[:,None]*(delta-np.outer(delta@axis,axis))
    faces = hand["faces"][:, ::-1] if mirrored else hand["faces"]
    oppose_digits(vertices,faces,fingers,palm,name==OFFER)
    cushion_thumb(vertices,faces,fingers[0],1.9 if name==OFFER else 1.15)
    settle_skin(vertices, fingers)
    if cloth is not None: fit_to_wool(vertices,faces,cloth)
    paint, facing = hand_uvs()
    nails=nail_uvs()
    g.part(name)
    g.verts.extend(V(p) for p in vertices)
    for fi,(face, palmar) in enumerate(zip(faces, facing)):
        if fi in nails:
            g.face(face.tolist(),nails[fi][face],"nail")
            continue
        key = "掌面" if palmar else "手背"
        g.face(face.tolist(), [(u, 1-v) for u,v in paint[key][face]], key)

    # 同一圈腕缘接入袖内。前臂随袖轴转向，并从尺桡骨的窄处渐宽。
    rim = hand["rim"].tolist()
    origin = sum((g.verts[i] for i in rim), V((0,0,0)))/len(rim)
    rays = [g.verts[i]-origin for i in rim]
    axis = (fingers[2][0]-wrist).normalized()
    rotation = axis.rotation_difference((origin-sleeve).normalized())
    previous = rim
    for t in (.18, .42, .72, 1.0):
        centre = origin.lerp(sleeve, t)
        turn = Quaternion().slerp(rotation, t)
        current = list(range(len(g.verts), len(g.verts)+len(rim)))
        g.verts.extend(centre+(turn@v)*(1+.30*t) for v in rays)
        for j in range(len(rim)):
            k=(j+1)%len(rim)
            g.face([previous[j],previous[k],current[k],current[j]],
                   [(j/len(rim),1-t+.16),((j+1)/len(rim),1-t+.16),
                    ((j+1)/len(rim),1-t),(j/len(rim),1-t)],"skin")
        previous=current
    g.face(previous,[(.5+.45*cos(6.283*j/len(rim)),.5+.45*sin(6.283*j/len(rim)))
                     for j in range(len(rim))],"skin",False)


def cuff_sections(g,depth):
    skin=BVHTree.FromPolygons(g.verts,g.faces)
    edges={tuple(sorted((a,b))) for face in g.faces for a,b in zip(face,(*face[1:],face[0]))}
    profiles=[]
    for offset in (15,10,3,-3,-6,-5,0,8):
        y=depth+min(8,offset)
        hits=[]
        for a,b in edges:
            p,q=g.verts[a],g.verts[b]
            if (p.y-y)*(q.y-y)<0:
                hits.append(p.lerp(q,(y-p.y)/(q.y-p.y)))
        center=sum(hits,V((0,0,0)))/len(hits)
        ring=[]
        for i in range(40):
            angle=2*np.pi*i/40
            ray=V((sin(angle),0,cos(angle)))
            hit=skin.ray_cast(center,ray,90)[0]
            if hit is None: raise ValueError("袖口截面未穿过前臂")
            ring.append((hit,ray))
        profiles.append(ring)
    return profiles


def hands(g):
    # 布卷刚完成，直接用它的全部实体截面计算扶持位置。
    cloth = BVHTree.FromPolygons(g.verts,g.faces)
    add_hand(g,OFFER,offering())
    right=cuff_sections(g,-156)
    add_hand(g,HOLD,holding(cloth),cloth)
    return right,cuff_sections(g,-78)


def prepare_sources(parts):
    weights=template()["weights"][:,5:].reshape(-1,5,3).sum(axis=2)
    for obj in parts:
        if obj.name not in (OFFER,HOLD):
            continue
        for digit,label in enumerate(DIGITS):
            group=obj.vertex_groups.new(name=label)
            for idx in np.flatnonzero(weights[:,digit]>.01):
                group.add([int(idx)],float(weights[idx,digit]),"REPLACE")
        sub=obj.modifiers.new("掌指四边面细分","SUBSURF")
        sub.levels=sub.render_levels=1
        sub.uv_smooth="PRESERVE_BOUNDARIES"


def indent_wool(scene,scale):
    """用细分后的手压出布边的接触凹陷，面心和裁片边缘也参与接触取样。"""
    import bpy
    hand=scene.objects[HOLD].evaluated_get(bpy.context.evaluated_depsgraph_get()).data
    hand_points=np.array([v.co/scale for v in hand.vertices])
    skin=BVHTree.FromPolygons(hand_points.tolist(),[list(f.vertices) for f in hand.polygons])
    wool=scene.objects["14 茜红条纹羊毛布卷"].data
    points=np.array([v.co/scale for v in wool.vertices])
    lo,hi=hand_points.min(axis=0)-5,hand_points.max(axis=0)+5
    active=((points>=lo)&(points<=hi)).all(axis=1)
    probes=[[i] for i in np.flatnonzero(active)]
    probes.extend(list(e.vertices) for e in wool.edges if active[e.vertices[0]] or active[e.vertices[1]])
    probes.extend(list(f.vertices) for f in wool.polygons if any(active[i] for i in f.vertices))
    # 同一面上的样点共同带动边缘，避免薄布上只推动一个顶点后形成毛刺。
    for _ in range(18):
        move=np.zeros_like(points);count=np.zeros(len(points))
        deepest=0
        for ids in probes:
            c=points[ids].mean(axis=0)
            near,n,_,distance=skin.find_nearest(c,7)
            if near is None:continue
            signed=(V(c)-near).dot(n)
            push=.57-signed
            if push<=.015:continue
            deepest=max(deepest,push)
            move[ids]+=np.asarray(n)*min(push,2.5)
            count[ids]+=1
        if deepest<.04:break
        moving=count>0
        points[moving]+=move[moving]/count[moving,None]
    wool.vertices.foreach_set("co",np.asarray(points*scale,dtype=np.float32).ravel())
    wool.update()


def hand_paint(w,h,key):
    from hand_mesh import TILES
    from PIL import Image,ImageFilter
    from surfaces import ROOT
    study=Image.open(ROOT/"art/source/roadside-bandits/手掌手背与前臂母图.png").convert("RGB")
    study=study.resize((1448,1086),Image.Resampling.LANCZOS).crop(TILES[key])
    colour=np.asarray(study,dtype=float)
    r,g,b=colour.transpose(2,0,1)
    skin=Image.fromarray(np.uint8((r-b>25)&(r-g>11))*255)
    # 皮肤轮廓内侧才取褶皱和肤色变化，指侧与两片的缝线恢复同一肤色。
    support=skin.filter(ImageFilter.MinFilter(15)).filter(ImageFilter.GaussianBlur(5))
    mask=np.asarray(support,dtype=float)/255
    soft=np.asarray(study.filter(ImageFilter.GaussianBlur(3)),dtype=float)
    broad=np.asarray(study.filter(ImageFilter.GaussianBlur(16)),dtype=float)
    luma=np.array([.213,.715,.072])
    crease=(colour-soft)@luma
    fold=(soft-broad)@luma
    base=np.array([204.,147.,111.])
    tone=np.clip(.90*crease+.71*fold+.26*((broad@luma)-148),-27,26)
    tint=np.clip((soft-broad)*.29,-5,5)
    texture=base+mask[:,:,None]*(tone[:,:,None]+tint)
    height=mask*(np.clip(crease/29,-.56,.42)*.64+np.clip(fold/42,-.45,.35)*.24)
    gloss=.28+np.clip(crease/210,-.035,.055)*mask
    if key=="手背":
        left,top,right,bottom=TILES[key]
        yy,xx=np.indices(mask.shape,dtype=float); xx+=left; yy+=top
        for x,y,rx,ry,angle in ((872,172,13,16,-8),(967,80,16,20,-4),(1051,39,16,17,0),
                                (1153,78,16,20,3),(1290,335,14,18,47)):
            a=radians(angle)
            sx=((xx-x)*cos(a)-(yy-y)*sin(a))/rx
            sy=((xx-x)*sin(a)+(yy-y)*cos(a))/ry
            # 甲板使用基网原有的面单独展开，滤除肤色母图上对应的亮斑。
            plate=np.exp(-.65*(sx**4+sy**4))
            texture=texture*(1-plate[:,:,None])+base*plate[:,:,None]
            height*=1-plate
    def resize(field):
        img=Image.fromarray(np.asarray(field,dtype=np.float32))
        return np.asarray(img.resize((w,h),Image.Resampling.LANCZOS))
    colour=np.stack([resize(v) for v in texture.transpose(2,0,1)],axis=-1)
    height,gloss=resize(height),resize(gloss)
    gy,gx=np.gradient(height)
    normal=np.stack((-gx*.81,gy*.81,np.ones_like(gx)),axis=-1)
    normal/=np.linalg.norm(normal,axis=2,keepdims=True)
    finish=np.zeros((h,w,4));finish[:,:,3]=255*gloss
    return colour,255*(.5+.5*normal),finish
