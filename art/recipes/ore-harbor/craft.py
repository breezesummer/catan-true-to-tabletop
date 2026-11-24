"""毫米制木作、砌石和原矿断口。码头梁与绳沿用同一海岸的截面和顺纹 UV。"""
import math
from pathlib import Path
import random
import sys
from mathutils import Vector
from surfaces_ore import atlas_uv

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"lumber-harbor"))
from joinery import Part as TimberPart, beam, cord, grain, panel, trunk, tube, UP

R = random.Random(211721)
V = Vector
FLOOR = .42
TOWER_X, TOWER_W, TOWER_FRONT, BACK = -7.25, 3.52, -.04, 9.42
HALL_LEFT, HALL_RIGHT, HALL_FRONT = -3.79, 10.76, .48
HALL_EAVE, HALL_PEAK = 5.88, 10.06
HALL_MID = (HALL_FRONT+BACK)*.5
HALL_SLOPE = (HALL_PEAK-HALL_EAVE)/(HALL_MID-HALL_FRONT)
TOWER_FLOOR, TOWER_EAVE, TOWER_PEAK = 6.36, 10.32, 14.45
QUAY_X, QUAY_FRONT, SHORE, FINGER_END = 11.32, -4.50, 10.18, -6.80
FINGER_X0, FINGER_X1 = -9.92, -5.22


class Part(TimberPart):
    def face(self, ids, tile, uv, smooth=False):
        self.faces.append(tuple(ids))
        self.uvs.extend(atlas_uv(tile,u,v) for u,v in uv)
        self.smooth.append(smooth)

    def object(self, scene, mat):
        obj=super().object(scene,mat)
        obj.data.uv_layers.active.name="HarborUV"
        return obj


def skin(p, points, tile, uv=None):
    if uv is None:
        uv=[(.5+.42*math.cos(i*math.tau/len(points)),.5+.42*math.sin(i*math.tau/len(points))) for i in range(len(points))]
    p.face(p.verts(points),tile,uv)


def faced_block(p, outline, outward, thickness, tile, bevel=.048):
    """一圈吃光的前倒角和向背面退入的侧边。以真实表面尺寸选择石料裁片。"""
    pts=[V(pt) for pt in outline]
    n=V(outward).normalized()
    center=sum(pts,V((0,0,0)))/len(pts)
    winding=sum(((a-center).cross(b-center) for a,b in zip(pts,pts[1:]+pts[:1])),V((0,0,0)))
    if winding.length_squared<1e-12: return
    if winding.dot(n)<0: pts.reverse()
    along=V((1,0,0)) if abs(n.x)<.8 else V((0,1,0))
    along=(along-n*along.dot(n)).normalized()
    across=n.cross(along)
    u=[(v-center).dot(along) for v in pts]; v=[(pt-center).dot(across) for pt in pts]
    w,h=max(u)-min(u),max(v)-min(v)
    if min(w,h)<.0005: return
    chamfer=min(bevel,w*.12,h*.15)
    inset=[center+along*(a*(1-2*chamfer/w))+across*(b*(1-2*chamfer/h)) for a,b in zip(u,v)]
    front=p.verts(inset); edge=p.verts([pt-n*chamfer*.83 for pt in pts]); back=p.verts([pt-n*thickness for pt in pts])
    tex=grain(max(w,h),min(w,h))
    uv=[tex((a-min(u))/w,(b-min(v))/h) for a,b in zip(u,v)]
    p.face(front,tile,uv)
    p.face(back[::-1],tile,uv[::-1])
    for i in range(len(pts)):
        j=(i+1)%len(pts)
        p.face([front[j],front[i],edge[i],edge[j]],tile,[uv[j],uv[i],(uv[i][0],uv[i][1]*.986+.006),(uv[j][0],uv[j][1]*.986+.006)])
        p.face([edge[j],edge[i],back[i],back[j]],tile,[(.05,.21),(.92,.21),(.92,.62),(.05,.62)])


def pin(p, center, normal, radius=.085, tile="iron"):
    a,n=V(center),V(normal).normalized()
    tube(p,[a-n*.038,a+n*.012,a+n*.043],(radius*.94,radius,radius*.68),tile,8)


def iron_loop(p, center, axis_a, axis_b, radius, tile="iron"):
    c,a,b=V(center),V(axis_a),V(axis_b)
    tube(p,[c+a*math.cos(i*math.tau/18)+b*math.sin(i*math.tau/18) for i in range(18)],radius,tile,7,True)


def clean(poly):
    result=[]
    for v in poly:
        if not result or (result[-1]-v).length_squared>1e-10: result.append(v)
    if len(result)>1 and (result[-1]-result[0]).length_squared<1e-10: result.pop()
    return result


def mineral(p, center, radii, patina="ore", axes=None):
    """从毛坯逐刀切出晶面，共用断棱。矿脉在同一投影方向上跨面延续。"""
    corners=[V((x,y,z)) for z in (-1,1) for y in (-1,1) for x in (-1,1)]
    faces=[[corners[i] for i in f] for f in ((0,2,3,1),(4,5,7,6),(0,1,5,4),(3,2,6,7),(2,0,4,6),(1,3,7,5))]
    phase=R.random()*math.tau
    cuts=R.randrange(12,17)
    for i in range(cuts):
        z=1-2*(i+.5)/cuts
        angle=phase+i*2.39996+R.uniform(-.22,.22)
        normal=V((math.sqrt(1-z*z)*math.cos(angle),math.sqrt(1-z*z)*math.sin(angle),z))
        distance=R.uniform(.60,.75) if i==2 else R.uniform(.73,1.05)
        clipped=[]; cut=[]
        for face in faces:
            out=[]
            for a,b in zip(face,face[1:]+face[:1]):
                da,db=a.dot(normal)-distance,b.dot(normal)-distance
                if da<=0: out.append(a)
                if (da<=0)!=(db<=0):
                    point=a.lerp(b,da/(da-db))
                    out.append(point)
                    if all((point-k).length_squared>1e-9 for k in cut): cut.append(point)
            out=clean(out)
            if len(out)>=3: clipped.append(out)
        if len(cut)>2:
            c=sum(cut,V((0,0,0)))/len(cut)
            a=(cut[0]-c).normalized(); b=normal.cross(a)
            cut.sort(key=lambda k:math.atan2((k-c).dot(b),(k-c).dot(a)))
            clipped.append(cut)
        faces=clipped
    origin=V(center)
    frame=list(map(V,axes)) if axes else [V((1,0,0)),V((0,1,0)),UP]
    u0,v0=R.uniform(.22,.59),R.uniform(.25,.65)
    def pos(pt):
        return origin+sum((frame[k]*radii[k]*pt[k] for k in range(3)),V((0,0,0)))
    for face in faces:
        c=sum(face,V((0,0,0)))/len(face)
        n=(face[1]-face[0]).cross(face[2]-face[0]).normalized()
        if n.dot(c)<0:
            face.reverse(); n=-n
        idx=max(range(3),key=lambda k:abs(n[k]))
        a,b=((1,2),(0,2),(0,1))[idx]
        tex=[(u0+pt[a]*.21,v0+pt[b]*.22) for pt in face]
        edge=p.verts([pos(pt) for pt in face])
        inner=p.verts([pos(c+(pt-c)*.927+n*.012) for pt in face])
        p.face(inner,patina,tex)
        for i in range(len(face)):
            j=(i+1)%len(face)
            p.face([edge[i],edge[j],inner[j],inner[i]],patina,
                   [tex[i],tex[j],(tex[j][0]*.96+.015,tex[j][1]*.96+.015),(tex[i][0]*.96+.015,tex[i][1]*.96+.015)])
