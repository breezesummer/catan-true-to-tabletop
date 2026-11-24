"""毫米制木作、分皮砌石、拱券和窗洞。立面按沿墙距离、高度和出挑量定位。"""
import math
from pathlib import Path
import random
import sys

from mathutils import Vector
from quay_atlas import uv

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"lumber-harbor"))
from joinery import Part as MeshPart, beam, cord, grain, trunk, tube

V = Vector
UP = V((0,0,1))
R = random.Random(312117)
FIELD = ("stone","stone","honey","grey","stone","honey")


class Part(MeshPart):
    def face(self, ids, tile, coords, smooth=False):
        self.faces.append(tuple(ids))
        self.uvs.extend(uv(tile,u,v) for u,v in coords)
        self.smooth.append(smooth)


def extrude(part, outline, inward, tile, coords=None, edge=None):
    pts = list(map(V,outline))
    normal = sum(((p-pts[0]).cross(q-pts[0]) for p,q in zip(pts[1:-1],pts[2:])),V((0,0,0)))
    if normal.length_squared < 1e-13: return
    if coords is None:
        # 最长边决定贴图方向；保留不同石块、瓦片之间的取样差异。
        axis = max((pts[(i+1)%len(pts)]-p for i,p in enumerate(pts)),key=lambda d:d.length_squared).normalized()
        across = normal.normalized().cross(axis)
        sx,sy = R.uniform(.09,.66),R.uniform(.09,.65)
        coords = [(sx+(p-pts[0]).dot(axis)*.06,sy+(p-pts[0]).dot(across)*.075) for p in pts]
        coords = [(min(.98,max(.02,u)),min(.98,max(.02,v))) for u,v in coords]
    if normal.dot(V(inward))>0:
        pts.reverse()
        coords = coords[::-1]
    top = part.verts(pts)
    back = part.verts([p+V(inward) for p in pts])
    part.face(top,tile,coords)
    part.face(back[::-1],edge or tile,coords[::-1])
    for i,j in enumerate(list(range(1,len(pts)))+[0]):
        a,b = coords[i],coords[j]
        part.face((top[i],back[i],back[j],top[j]),edge or tile,
                  [a,(a[0],max(.01,a[1]-.035)),(b[0],max(.01,b[1]-.035)),b])


class Face:
    def __init__(self,x,y,tx,ty):
        self.origin = V((x,y,0))
        self.t = V((tx,ty,0))
        self.n = V((ty,-tx,0))

    def at(self,u,z,d=0):
        return self.origin+self.t*u+UP*z+self.n*d

    def plate(self,p,outline,d,thickness,tile,coords=None):
        extrude(p,[self.at(u,z,d) for u,z in outline],-self.n*thickness,tile,coords)

    def member(self,p,a,b,width,height,tile="oak",d=0,bevel=.04):
        # 宽刻画立面内的截面，高度沿立面法向。
        beam(p,self.at(*a,d),self.at(*b,d),width,height,tile,bevel,self.n)


def rect(l,r,b,t):
    return [(l,b),(r,b),(r,t),(l,t)]


def area(poly):
    return .5*sum(a[0]*b[1]-a[1]*b[0] for a,b in zip(poly,poly[1:]+poly[:1]))


def divide(poly, a, b):
    """按有向直线分割凸多边形，返回左、右两片。"""
    left,right = [],[]
    def side(p):
        return (b[0]-a[0])*(p[1]-a[1])-(b[1]-a[1])*(p[0]-a[0])
    last = poly[-1]
    old = side(last)
    for p in poly:
        new = side(p)
        if (old>1e-7 and new < -1e-7) or (old < -1e-7 and new>1e-7):
            t = old/(old-new)
            point = (last[0]+(p[0]-last[0])*t,last[1]+(p[1]-last[1])*t)
            left.append(point)
            right.append(point)
        if new>=-1e-7: left.append(p)
        if new<=1e-7: right.append(p)
        last,old = p,new
    return left,right


def clear_holes(poly, holes):
    pieces = [poly]
    for hole in holes:
        result = []
        l,r,b,t = hole.bounds
        for piece in pieces:
            if max(x for x,z in piece)<=l or min(x for x,z in piece)>=r or max(z for x,z in piece)<=b or min(z for x,z in piece)>=t:
                result.append(piece)
                continue
            inner = piece
            for a,end in zip(hole.outline,hole.outline[1:]+hole.outline[:1]):
                inner,outer = divide(inner,a,end)
                if len(outer)>2 and abs(area(outer))>.001: result.append(outer)
                if len(inner)<3 or abs(area(inner))<.001: break
        pieces = result
    return pieces


def cut_stone(shape,holes):
    """合回切窗时产生的共边，单块石面不会因拱洞裁剪而留下斜向假灰缝。"""
    pieces = clear_holes(shape,holes)
    if len(pieces)<2: return pieces
    nodes = {(round(x,6),round(y,6)) for piece in pieces for x,y in piece}
    edges = set()
    for piece in pieces:
        for a,b in zip(piece,piece[1:]+piece[:1]):
            dx,dy = b[0]-a[0],b[1]-a[1]
            d2 = dx*dx+dy*dy
            if d2<1e-10: continue
            inline = []
            for point in nodes:
                x,y = point[0]-a[0],point[1]-a[1]
                t = (x*dx+y*dy)/d2
                if abs(x*dy-y*dx)<2e-6 and -.00004<t<1.00004:
                    inline.append((t,point))
            inline.sort()
            for (_,p),(_,q) in zip(inline,inline[1:]):
                if (q,p) in edges: edges.remove((q,p))
                else: edges.add((p,q))
    boundary = dict(edges)
    if len(boundary)!=len(edges): return pieces
    loops = []
    while boundary:
        start = next(iter(boundary))
        point,loop = start,[]
        while point in boundary:
            loop.append(point)
            point = boundary.pop(point)
        if point!=start or len(loop)<3 or area(loop)<=0: return pieces
        corners = []
        for i,b in enumerate(loop):
            a,c = loop[i-1],loop[(i+1)%len(loop)]
            if abs((b[0]-a[0])*(c[1]-b[1])-(b[1]-a[1])*(c[0]-b[0]))>2e-6:
                corners.append(b)
        if len(corners)>2: loops.append(corners)
    return loops


class Opening:
    def __init__(self,x,r,bottom,spring,rise,pointed=True,n=8):
        self.x,self.r,self.bottom,self.spring,self.rise = x,r,bottom,spring,rise
        self.pointed = pointed
        if pointed:
            half = [(x+r-2*r*math.cos(math.pi*i/(3*n)),spring+rise*math.sin(math.pi*i/(3*n))/math.sin(math.pi/3)) for i in range(n+1)]
            crown = half+[(2*x-u,z) for u,z in reversed(half[:-1])]
        else:
            crown = [(x-r*math.cos(math.pi*i/(n*2)),spring+rise*math.sin(math.pi*i/(n*2))) for i in range(n*2+1)]
        self.crown = crown
        self.outline = [(x-r,bottom),(x+r,bottom)]+list(reversed(crown))
        self.bounds = (x-r,x+r,bottom,spring+rise)

    def head(self,u):
        s = min(1,abs((u-self.x)/self.r))
        factor = math.sqrt(max(0,(4-(1+s)**2)/3)) if self.pointed else math.sqrt(max(0,1-s*s))
        return self.spring+self.rise*factor


def stone(p,f,shape,surface,d=.10,depth=.58,bevel=.048):
    """前缘六至八个切面留凿痕，顶面与砂浆床之间有真实进退。"""
    if len(shape)<3 or abs(area(shape))<.002: return
    if area(shape)<0: shape = shape[::-1]
    center = sum((V(pt) for pt in shape),V((0,0)))/len(shape)
    cut = min(bevel,min((V(pt)-center).length for pt in shape)*.34)
    inner = [V(pt)+(center-V(pt)).normalized()*cut for pt in shape]
    s,t = R.uniform(.12,.49),R.uniform(.13,.49)
    def mapping(pt):
        return (max(.02,min(.98,s+(pt[0]-center.x)*.13)),max(.02,min(.98,t+(pt[1]-center.y)*.19)))
    rings = [p.verts([f.at(*v,off) for v in loop]) for loop,off in ((inner,d),(shape,d-cut*.78),(shape,d-depth))]
    p.face(rings[0],surface,[mapping(v) for v in inner])
    for i in range(len(shape)):
        j = (i+1)%len(shape)
        a,b = mapping(shape[i]),mapping(shape[j])
        for k in (0,1):
            p.face((rings[k][j],rings[k][i],rings[k+1][i],rings[k+1][j]),surface,
                   [b,a,(a[0],a[1]+.035),(b[0],b[1]+.035)])
    p.face(rings[-1][::-1],surface,[mapping(v) for v in shape[::-1]])


def masonry(p,f,l,r,b,t,holes=(),height=.75,length=1.48,marine=False):
    for patch in clear_holes(rect(l,r,b,t),holes):
        f.plate(p,patch,-.13,.42,"mortar")
    rows = max(1,round((t-b)/height))
    for k in range(rows):
        low,high = b+(t-b)*k/rows,b+(t-b)*(k+1)/rows
        columns = max(1,round((r-l)/length))
        seams = [l]+[l+(j+(.5 if k%2 else 1)+R.uniform(-.14,.14))*(r-l)/columns
                     for j in range(columns) if l+.27<l+(j+(.5 if k%2 else 1))*(r-l)/columns<r-.26]+[r]
        for start,end in zip(seams,seams[1:]):
            x0,x1,y0,y1 = start+.017,end-.017,low+.016,high-.013
            chip = min(R.uniform(.045,.13),(x1-x0)*.17)
            shape = [(x0+chip,y0),(x1-.036,y0),(x1,y0+.041),(x1,y1-.038),(x1-.056,y1),
                     (x0+.045,y1),(x0,y1-.061),(x0,y0+chip*.94)]
            tile = R.choice(FIELD)
            if marine and high < -1.3:
                tile = R.choice(("salt","tide","tide")) if high < -2.15 else R.choice(("grey","salt","salt"))
            d = R.uniform(-.029,.042)
            for patch in cut_stone(shape,holes):
                stone(p,f,patch,tile,d,.61)


def surround(p,f,hole,width,tile="dressed",d=.18):
    x,r,base,spring,_ = hole.x,hole.r,hole.bottom,hole.spring,hole.rise
    rows = max(1,round((spring-base)/(.71 if width>.33 else .53)))
    for sign in (-1,1):
        for i in range(rows):
            za = base+i*(spring-base)/rows+.012
            zb = base+(i+1)*(spring-base)/rows-.008
            ends = sorted((x+sign*r,x+sign*(r+width)))
            stone(p,f,rect(*ends,za,zb),tile,d,.68,min(.048,width*.14))
    path = hole.crown
    outer = []
    for i,pnt in enumerate(path):
        tangent = (V(path[min(i+1,len(path)-1)])-V(path[max(i-1,0)])).normalized()
        n = V((-tangent.y,tangent.x))
        outer.append(V(pnt)+n*width)
    for i in range(len(path)-1):
        a,b = V(path[i]),V(path[i+1])
        c,e = outer[i+1],outer[i]
        shape = [a.lerp(b,.021),b.lerp(a,.021),c.lerp(e,.021),e.lerp(c,.021)]
        stone(p,f,[tuple(v) for v in shape],tile,d+(.045 if i in (len(path)//2-1,len(path)//2) else 0),.66,min(.038,width*.12))


def window(p,f,hole):
    surround(p,f,hole,.19,d=.14)
    x,r,z,spring,rise = hole.x,hole.r,hole.bottom,hole.spring,hole.rise
    f.plate(p,hole.outline,-.33,.09,"glass")
    f.member(p,(x,z),(x,spring+.14),.087,.13,"dressed",-.14,.014)
    for sign in (-1,1):
        u = x+sign*r*.46
        path = [f.at(x,spring+.10,-.14),f.at(u,spring+rise*.57,-.14),f.at(x+sign*r*.87,spring+.08,-.14)]
        tube(p,path,.048,"dressed",6)
    f.member(p,(x-r,z+.48),(x+r,z+.48),.046,.05,"iron",-.27,.008)
    stone(p,f,rect(x-r-.29,x+r+.29,z-.20,z+.025),"dressed",.25,.71,.055)
