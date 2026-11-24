"""由同一片连续的掌指拓扑摆六个手势。骨骼仅用于制作，游戏读取静态网格。"""
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from gestures import gesture


TILES = {"掌面": (162, 0, 670, 690), "手背": (832, 0, 1343, 690)}


def unit(a):
    return a / np.linalg.norm(a)


def palm_axes(wrist, knuckles, palmar):
    forward = unit(knuckles[2] - wrist)
    side = knuckles[1] - knuckles[4]
    side = unit(side - forward * (side @ forward))
    normal = np.cross(side, forward)
    if normal @ palmar < 0:
        normal = -normal
    return np.column_stack((side, forward, normal))


@lru_cache(maxsize=1)
def template():
    with np.load(Path(__file__).with_name("hm08-hand.npz"), allow_pickle=False) as archive:
        return {key: archive[key] for key in archive.files}


def deform(wrist, chains, palm_normal, girth, flesh=0):
    hand = template()
    rest = hand["vertices"].astype(float)
    bones = hand["bones"].astype(float)
    weights = hand["weights"]
    wrist, chains = np.asarray(wrist), np.asarray(chains)
    roots = bones[5::3,0]
    pivot = bones[0,0]
    source = palm_axes(pivot, roots, (1,0,0))
    target = palm_axes(wrist, chains[:,0], palm_normal)
    if flesh:
        lateral,longitudinal,depth=((rest-pivot)@source).T
        heel=np.exp(-((longitudinal-.49)/.36)**4)
        thenar=np.exp(-((lateral-.18)/.22)**2-((longitudinal-.48)/.32)**2)
        hypothenar=np.exp(-((lateral+.25)/.18)**2-((longitudinal-.46)/.38)**2)
        pads=np.zeros_like(lateral)
        for u0,v0 in ((.21,.84),(.01,.84),(-.17,.79),(-.32,.72)):
            pads+=np.exp(-((lateral-u0)/.105)**2-((longitudinal-v0)/.106)**2)
        rest+=np.outer(heel*lateral*.095,source[:,0])
        rest+=np.outer((.048*thenar+.032*hypothenar+.014*heel)*np.tanh(depth/.084),source[:,2])
        rest+=np.outer(.046*pads*np.clip((depth+.01)/.10,0,1),source[:,2])
    across = abs((chains[1,0] - chains[4,0]) @ target[:,0]) / abs((roots[1]-roots[4]) @ source[:,0])
    width = np.clip(across, girth*.84, girth*1.10)
    length = np.linalg.norm(chains[2,0]-wrist) / np.linalg.norm(roots[2]-pivot)
    stretch = source @ np.diag((width, length, girth)) @ source.T
    turn = target @ source.T
    base = turn @ stretch
    ends = (bones-pivot) @ base.T + wrist
    ends[1:5,1] = chains[1:,0]
    for i in range(5):
        ends[5+3*i:8+3*i,0] = chains[i,:3]
        ends[5+3*i:8+3*i,1] = chains[i,1:]
    rotations, centers, placed = [], [], []
    for j, (a,b) in enumerate(bones):
        head, tip = ends[j]
        parent = hand["parents"][j]
        frame = rotations[parent] if parent >= 0 else turn
        axis = stretch @ (b-a)
        direction = unit(tip-head)
        rotation = Vector(frame @ axis).rotation_difference(Vector(direction)).to_matrix()
        frame = np.asarray(rotation) @ frame
        rotations.append(frame)
        local = (rest-a) @ stretch.T
        along = (local @ unit(axis)) / np.linalg.norm(axis)
        if j>=5 and flesh:
            # 留下掌骨尺度，只给骨轴两侧的软组织增加厚度；末节与小指本身更细。
            gain=flesh*(1.16 if j>=17 else 1.0)
            local+=gain*(local-np.outer(along,axis))
        placed.append(head + local @ frame.T + np.outer(along, tip-head-frame@axis))
        centers.append(a + np.outer(np.clip(along,0,1), b-a))
    # 每个顶点绕附近的指骨轴混合旋转。急弯处补回指腹和关节背侧的体积。
    vertices = np.einsum("vb,bvi->vi",weights,placed)
    rotation_mix = np.einsum("vb,bij->vij",weights,rotations)
    u,_,vh = np.linalg.svd(rotation_mix)
    center = np.einsum("vb,bvi->vi",weights,centers)
    vertices += .76*np.einsum("vij,vj->vi",u@vh-rotation_mix,(rest-center)@stretch.T)
    return vertices, np.linalg.det(turn)<0


def skin_projection(dorsal):
    # 母图使用两只不同的手。两组腕线、掌骨头和指尖分别配准。
    if dorsal:
        wx,wy = 1041,621
        shape = [
            [(1135,476),(1203,423),(1251,365),(1299,321)],
            [(1130,303),(1143,219),(1149,139),(1150,60)],
            [(1054,300),(1054,209),(1053,125),(1052,26)],
            [(979,318),(970,224),(969,153),(967,71)],
            [(907,355),(890,286),(879,222),(869,163)]]
    else:
        wx,wy = 345,623
        shape = [
            [(451,477),(513,428),(566,368),(630,323)],
            [(443,292),(454,208),(461,139),(465,65)],
            [(363,280),(366,189),(367,107),(368,26)],
            [(296,288),(294,205),(289,133),(287,58)],
            [(226,329),(216,263),(207,205),(200,151)]]
    chains = [[(x,-y,0) for x,y in finger] for finger in shape]
    vertices, mirror = deform((wx,-wy,0),chains,(0,0,1),350)
    face = template()["faces"]
    normal = np.cross(vertices[face[:,2]]-vertices[face[:,0]],vertices[face[:,3]]-vertices[face[:,1]])
    front = normal[:,2] * (-1 if mirror else 1) >= 0
    return vertices[:,:2]*[1,-1], front


@lru_cache(maxsize=1)
def hand_uvs():
    palm, facing_palm = skin_projection(False)
    back,_ = skin_projection(True)
    uv = {}
    for key,points in (("掌面",palm),("手背",back)):
        left,top,right,bottom = TILES[key]
        uv[key] = np.clip((points-[left,top])/[right-left-1,bottom-top-1],0,1)
    return uv, facing_palm


@dataclass
class HandMesh:
    vertices: np.ndarray
    faces: np.ndarray
    wrist: np.ndarray


def nearest_axis(points,chain):
    a=chain[:-1];axis=np.diff(chain,axis=0)
    t=np.clip(np.einsum("nbi,bi->nb",points[:,None]-a,axis)/(axis**2).sum(axis=1),0,1)
    on=a[None]+t[:,:,None]*axis
    which=np.argmin(((on-points[:,None])**2).sum(axis=2),axis=1)
    return on[np.arange(len(points)),which]


def settle_skin(vertices,chains):
    hand=template()
    faces=hand["faces"].astype(int)
    finger_weight=hand["weights"][:,5:].reshape(len(vertices),5,3).sum(axis=2)
    chains=np.asarray(chains)
    widths=np.array((1.08,1.00,1.02,.94,.81))
    # 靠拢的部位接触后压平两边的肉垫，而不改指骨的方向和长度。
    for i,chain in enumerate(chains):
        mask=finger_weight[:,i]>.30
        own=vertices[mask].copy()
        axis=nearest_axis(own,chain)
        mobility=np.clip((finger_weight[mask,i]-.30)/.54,0,1)
        for j,other in enumerate(chains):
            if i==j:continue
            towards=nearest_axis(axis,other)-axis
            distance=np.linalg.norm(towards,axis=1)
            normal=towards/np.maximum(distance[:,None],.001)
            limit=distance*widths[i]/(widths[i]+widths[j])-.19
            excess=np.maximum(0,np.einsum("ni,ni->n",own-axis,normal)-limit)
            own-=normal*(np.minimum(excess,1.9)*mobility)[:,None]
        vertices[mask]=own
    # 对极屈曲部位解开基网折返的面环。只移动相交的面和相邻一圈。
    sides=np.unique(np.sort(np.concatenate((faces[:,[0,1]],faces[:,[1,2]],
                         faces[:,[2,3]],faces[:,[3,0]])),axis=1),axis=0)
    a,b=np.concatenate((sides,sides[:,::-1])).T
    degree=np.bincount(a,minlength=len(vertices))
    face_sets=[set(face) for face in faces]
    for _ in range(55):
        tree=BVHTree.FromPolygons(vertices.tolist(),faces.tolist())
        hits=[(i,j) for i,j in tree.overlap(tree) if i<j and face_sets[i].isdisjoint(face_sets[j])]
        if not hits:break
        active=np.zeros(len(vertices))
        active[faces[np.unique(hits)]]=1
        adjacent=np.bincount(a,weights=active[b],minlength=len(vertices))/degree
        relax=.51*np.maximum(active,.74*adjacent)
        mean=np.column_stack([np.bincount(a,weights=v[b],minlength=len(vertices))/degree for v in vertices.T])
        vertices+=(mean-vertices)*relax[:,None]


def fit_hand(chart, charts):
    wrist, fingers, normal, girth = gesture(chart.paint, charts)
    flesh={"staff":.37,"embrace":.36,"palm":.29,"visor":.35,"knifehand":.37,"straphand":.31}
    vertices, mirrored = deform(wrist,fingers,normal,girth,flesh[chart.paint])
    settle_skin(vertices,fingers)
    hand = template()
    faces = hand["faces"][:,::-1] if mirrored else hand["faces"]
    # 用同一圈顶点缝合腕口；腕上数圈渐变到手的实测椭圆截面。
    before = np.asarray(chart.rows[-1])
    ring = hand["rim"]
    candidates = [np.roll(order,i) for order in (ring,ring[::-1]) for i in range(len(ring))]
    opening = min(candidates,key=lambda r:np.sum((vertices[r]-before)**2))
    # 掌根向现有前臂的方向展宽。只在近腕端调节，桡骨侧隆起连续进入大鱼际。
    native=vertices[opening]
    warp=np.linalg.lstsq(np.column_stack((native-native.mean(axis=0),np.ones(len(ring)))),
                         before-native,rcond=.0001)[0]
    root=hand["bones"][0,0]
    along=(hand["vertices"]-root)@unit(hand["bones"][11,0]-root)
    influence=np.clip((.47-along)/.50,0,1)
    influence=influence**2*(3-2*influence)
    delta=np.column_stack((vertices-native.mean(axis=0),np.ones(len(vertices))))@warp
    # 腕环很薄，平面外的外推只取两个主切向，防止弯腕时放大法向位移。
    _,_,axes=np.linalg.svd(native-native.mean(axis=0),full_matrices=False)
    delta-=np.outer((vertices-native.mean(axis=0))@axes[2],axes[2]@warp[:3])
    vertices+=.82*influence[:,None]*delta
    delta = vertices[opening]-before
    for i in range(4):
        chart.rows[-1-i] = [Vector(v) for v in np.asarray(chart.rows[-1-i])+delta*((4-i)/4)**2]
    chart.hand = HandMesh(vertices,faces,opening)
