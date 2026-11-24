"""按毫米组装木件、带切面树干和三股绳，各构件的几何与表面纹向共用局部坐标。"""
import math
import random

import bpy
from mathutils import Vector
from surfaces import atlas_uv

V = Vector
UP = V((0,0,1))
SIDE = V((1,0,0))
RNG = random.Random(20261120)


def grain(length, width):
    u = RNG.uniform(.02,.30)
    span = RNG.uniform(.48,.96-u)
    across = min(.85,max(.065,span*width/max(length,.05)*1.35))
    v = RNG.uniform(.02,.97-across)
    return lambda t,s:(u+span*t,v+across*s)


class Part:
    def __init__(self, name):
        self.name = name
        self.vertices, self.faces, self.uvs, self.smooth = [], [], [], []

    def verts(self, points):
        start = len(self.vertices)
        self.vertices.extend(tuple(x*.001 for x in p) for p in points)
        return list(range(start,len(self.vertices)))

    def face(self, ids, tile, uv, smooth=False):
        self.faces.append(tuple(ids))
        self.uvs.extend(atlas_uv(tile,u,v) for u,v in uv)
        self.smooth.append(smooth)

    def object(self, scene, mat):
        mesh = bpy.data.meshes.new(self.name)
        mesh.from_pydata(self.vertices,[],self.faces)
        mesh.update()
        layer = mesh.uv_layers.new(name="Timber")
        layer.data.foreach_set("uv",[val for uv in self.uvs for val in uv])
        mesh.polygons.foreach_set("use_smooth",self.smooth)
        obj = bpy.data.objects.new(self.name,mesh)
        scene.collection.objects.link(obj)
        mesh.materials.append(mat)
        return obj


def basis(a,b,up):
    axis = (V(b)-V(a)).normalized()
    if abs(axis.dot(up))>.97:
        up = V((0,1,0)) if abs(axis.y)<.97 else SIDE
    across = up.cross(axis).normalized()
    return axis,across,axis.cross(across).normalized()


def beam(p,a,b,w,h,tile="oak",bevel=.08,up=UP,end="agedcut"):
    a,b = V(a), V(b)
    axis,side,high = basis(a,b,up)
    length=(b-a).length
    c = min(bevel,w*.21,h*.21,length*.15)
    w,h = w/2,h/2
    def ring(center,shrink):
        l,t=w-shrink,h-shrink
        clip = min(c,l*.4,t*.4)
        cross = [(-l+clip,-t),(l-clip,-t),(l,-t+clip),(l,t-clip),
                 (l-clip,t),(-l+clip,t),(-l,t-clip),(-l,-t+clip)]
        return p.verts([center+side*s+high*z for s,z in cross]),cross
    rings=[]
    for center,shrink in ((a,c*.5),(a+axis*c,0),(b-axis*c,0),(b,c*.5)):
        ring_ids,cross = ring(center,shrink)
        rings.append(ring_ids)
    for ids,rev in ((rings[0],True),(rings[-1],False)):
        direction = -1 if rev else 1
        uv=[(.5+s/(2*w)*.58,.5+z/(2*h)*.58) for s,z in cross]
        p.face(ids[::direction],end,uv[::direction])
    fractions = (0,c/length,1-c/length,1)
    for j in range(8):
        k = (j+1)%8
        uv = grain(length,(V(cross[j])-V(cross[k])).length)
        for i in range(3):
            lo,hi = fractions[i:i+2]
            p.face((rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]),tile,
                   [uv(lo,0),uv(lo,1),uv(hi,1),uv(hi,0)])


def tube(p,path,r,tile="iron",n=8,closed=False,smooth=True):
    path = list(map(V,path))
    rings=[]
    distances=[0]
    for a,b in zip(path,path[1:]):
        distances.append(distances[-1]+(b-a).length)
    length=distances[-1]+((path[-1]-path[0]).length if closed else 0)
    normal=None
    for i,center in enumerate(path):
        last = path[(i-1)%len(path)] if closed or i else center
        ahead = path[(i+1)%len(path)] if closed or i+1<len(path) else center
        t = (ahead-last).normalized()
        if normal is None:
            ref = UP if abs(t.z)<.85 else SIDE
            normal = t.cross(ref).normalized()
        normal = (normal-t*normal.dot(t)).normalized()
        binormal = t.cross(normal)
        radius = r if isinstance(r,(float,int)) else r[i]
        rings.append(p.verts([center+radius*(normal*math.cos(math.tau*j/n)+binormal*math.sin(math.tau*j/n)) for j in range(n)]))
    for i in range(len(path) if closed else len(path)-1):
        k=(i+1)%len(path)
        u0=distances[i]/length
        u1=distances[k]/length if k else 1
        for j in range(n):
            h=(j+1)%n
            p.face((rings[i][j],rings[i][h],rings[k][h],rings[k][j]),tile,
                   [(u0,j/n),(u0,(j+1)/n),(u1,(j+1)/n),(u1,j/n)],smooth)
    if not closed:
        capuv=[(.5+.39*math.cos(math.tau*j/n),.5+.39*math.sin(math.tau*j/n)) for j in range(n)]
        p.face(rings[0][::-1],tile,capuv[::-1])
        p.face(rings[-1],tile,capuv)


def cord(p, path, radius, pitch, closed=False):
    """按弧长布三条真实螺旋股；闭环取整圈，绕捆转角保持股向连续。"""
    points=list(map(V,path))
    if closed:
        points.append(points[0])
    d=[0.]
    for a,b in zip(points,points[1:]):
        d.append(d[-1]+(b-a).length)
    turns=max(1,round(d[-1]/pitch))
    count=max(16,turns*8)
    centers=[]
    section=0
    for i in range(count+(0 if closed else 1)):
        offset=i/count*d[-1]
        while section+2<len(d) and d[section+1]<offset:
            section+=1
        centers.append(points[section].lerp(points[section+1],(offset-d[section])/(d[section+1]-d[section])))
    strands=[[],[],[]]
    normal=None
    for i,c in enumerate(centers):
        before=centers[(i-1)%len(centers)] if i or closed else c
        after=centers[(i+1)%len(centers)] if i+1<len(centers) or closed else c
        t=(after-before).normalized()
        if normal is None:
            normal=t.cross(UP if abs(t.z)<.88 else SIDE).normalized()
        normal=(normal-t*normal.dot(t)).normalized()
        other=t.cross(normal)
        for k in range(3):
            f=math.tau*(turns*i/count+k/3)
            strands[k].append(c+radius*.48*(math.cos(f)*normal+math.sin(f)*other))
    for strand in strands:
        tube(p,strand,radius*.52,"rope",5,closed)


def trunk(p,a,b,r,side_tile="bark",cap_tile="cut",rings=8,n=40,pile=False):
    """不规则树皮随轴向连续。两个切口由树皮、形成层和微凹的锯面分别封口。"""
    a,b=V(a),V(b)
    axis,side,up=basis(a,b,UP)
    seed=RNG.uniform(0,math.tau)
    ridge=[RNG.uniform(-.037,.035) for _ in range(n)]
    rows=[]
    for k in range(rings+1):
        t=k/rings
        center=a.lerp(b,t)+side*(.026*r*math.sin(t*math.pi+seed)*math.sin(t*math.pi))
        ring=[]
        for j in range(n):
            ang=math.tau*j/n
            rr=r*(1-.04*t+.033*math.sin(ang*3+seed)+.023*math.cos(ang*5-seed+t*.4)+ridge[j]+.012*math.sin(ang*8+t*4.1))
            ring.append(center+rr*(side*math.cos(ang)+up*math.sin(ang)))
        rows.append(ring)
    ids=[p.verts(row) for row in rows]
    first,last=(0,1) if pile else (RNG.uniform(.015,.17),RNG.uniform(.83,.985))
    if not pile and RNG.random()<.5:
        first,last=last,first
    seam=RNG.randrange(n//9,n//3)
    for k in range(rings):
        surface="wet" if pile else side_tile
        for j in range(n):
            h=(j+1)%n
            v=(j+seam)%n
            u0=first+(last-first)*k/rings
            u1=first+(last-first)*(k+1)/rings
            p.face((ids[k][j],ids[k][h],ids[k+1][h],ids[k+1][j]),surface,
                   [(u0,v/n),(u0,(v+1)/n),(u1,(v+1)/n),(u1,v/n)],True)
    for front,center,row,outer in ((True,a,rows[0],ids[0]),(False,b,rows[-1],ids[-1])):
        outward = -axis if front else axis
        lip = p.verts([center+(v-center)*.92+outward*r*.014 for v in row])
        disk = p.verts([center+(v-center)*.875+outward*r*.006 for v in row])
        spin=RNG.uniform(0,math.tau)
        def disk_uv(j,scale):
            f=math.tau*j/n+spin
            return (.5+.448*scale*math.cos(f),.5+.448*scale*math.sin(f))
        order = -1 if front else 1
        for j in range(n):
            h=(j+1)%n
            for outside,inside,sc1,sc2,tile in ((outer,lip,1,.94,side_tile),(lip,disk,.94,.89,cap_tile)):
                p.face([outside[j],outside[h],inside[h],inside[j]][::order],tile,
                       [disk_uv(j,sc1),disk_uv(h,sc1),disk_uv(h,sc2),disk_uv(j,sc2)][::order])
        p.face(disk[::order],cap_tile,[disk_uv(j,.89) for j in range(n)][::order])


def panel(p, perimeter, thickness, tile, uv, side_tile=None):
    """外表逆时针轮廓，沿指定方向向内挤出。"""
    front=p.verts(perimeter)
    back=p.verts([V(v)+V(thickness) for v in perimeter])
    p.face(front,tile,uv)
    p.face(back[::-1],side_tile or tile,uv[::-1])
    for i in range(len(front)):
        j=(i+1)%len(front)
        p.face((front[i],back[i],back[j],front[j]),side_tile or tile,
               [(0,.2),(0,.27),(1,.27),(1,.2)])
