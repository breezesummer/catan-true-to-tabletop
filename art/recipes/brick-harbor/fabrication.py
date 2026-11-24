"""以毫米制作砖坯、板材与五金。顺纹木作、绳股与端面共用海港的既有工艺。"""
import math
from pathlib import Path
import random
import sys

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"lumber-harbor"))
from joinery import Part as TimberPart, V, UP, beam, cord, grain, panel, tube, trunk
from kiln_atlas import PATCH

R = random.Random(211720)
ASHLAR = ("ashlar","ashlar","ashlar","stone","stone","coping")
STOCK = ("ember","ember","orange","rose","madder","ember")


class Part(TimberPart):
    def face(self,ids,tile,uv,smooth=False):
        slot,quarter=PATCH.get(tile,(tile,None))
        if quarter is not None:
            col,row=quarter%2,quarter//2
            uv=[((col+.038+.924*u)*.5,(1-row+.038+.924*v)*.5) for u,v in uv]
        super().face(ids,slot,uv,smooth)


def brick(p,center,sizes,tile,angle=0,bevel=.057,battered=True):
    """四圈断面构成手工砖的收边。斜断角与顶面缺角因砖而异。"""
    x,y,z=center
    a,b,h=(s*.5 for s in sizes)
    bevel=min(bevel,a*.33,b*.33,h*.34)
    cuts=[bevel*R.uniform(.70,1.56) for _ in range(4)]
    damaged=R.randrange(4) if battered and R.random()<.27 else None
    if damaged is not None:
        cuts[damaged]*=R.uniform(1.9,2.8)
    cuts=[min(c,a*.47,b*.47) for c in cuts]
    tilt=[R.uniform(-.008,.008) if battered else 0 for _ in range(8)]
    c,s=math.cos(angle),math.sin(angle)
    rings=[]
    coords=[]
    for inset,level in ((bevel*.72,-h),(0,-h+bevel*.78),(0,h-bevel),(bevel*.80,h)):
        ax,by=a-inset,b-inset
        k=[max(.003,v-inset*.32) for v in cuts]
        ring=[(-ax+k[0],-by),(ax-k[1],-by),(ax,-by+k[1]),(ax,by-k[2]),
              (ax-k[2],by),(-ax+k[3],by),(-ax,by-k[3]),(-ax,-by+k[0])]
        coords.append(ring)
        rings.append(p.verts([(x+c*i-s*j,y+s*i+c*j,z+level+tilt[n]) for n,(i,j) in enumerate(ring)]))
    u0,v0=R.uniform(.03,.23),R.uniform(.03,.25)
    su,sv=R.uniform(.47,.73),R.uniform(.42,.71)
    def lid(points):
        return [(u0+su*(i/(2*a)+.5),v0+sv*(j/(2*b)+.5)) for i,j in points]
    p.face(rings[0][::-1],tile,lid(coords[0])[::-1])
    p.face(rings[3],tile,lid(coords[3]))
    vf=(0,bevel*.78/(2*h),1-bevel/(2*h),1)
    for i in range(8):
        j=(i+1)%8
        width=(V(coords[1][i])-V(coords[1][j])).length
        tex=grain(width,2*h)
        for layer in range(3):
            lo,hi=vf[layer:layer+2]
            surface=tile
            if tile in STOCK and damaged is not None and i==(2*damaged+7)%8 and layer==2:
                surface="orange"
            p.face([rings[layer][i],rings[layer][j],rings[layer+1][j],rings[layer+1][i]],
                   surface,[tex(0,lo),tex(1,lo),tex(1,hi),tex(0,hi)])


def cuboid(p,center,size,tile):
    """藏在砌体内的砂浆与简洁背板使用六个面。"""
    x,y,z=center
    a,b,h=(k/2 for k in size)
    ids=p.verts([(x+a*i,y+b*j,z+h*k) for k in (-1,1) for i,j in ((-1,-1),(1,-1),(1,1),(-1,1))])
    for f in ((3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
        p.face([ids[i] for i in f],tile,[(.02,.04),(.98,.04),(.98,.96),(.02,.96)])


def rivet(p,x,y,z,r=.08):
    tube(p,[(x,y+.045,z),(x,y,z),(x,y-.029,z)],(r,r,r*.72),"iron",8)


class Plane:
    """U、V 两根单位轴张成外表面；正深度指向构件内部。"""
    def __init__(self, origin, u, v=(0,0,1)):
        self.origin,self.u,self.v=map(V,(origin,u,v))
        self.normal=self.u.cross(self.v).normalized()

    def at(self,u,v,depth=0):
        return self.origin+self.u*u+self.v*v-self.normal*depth


def facing(p,plane,polygon,front,back,tile,bevel=0):
    """轮廓在自身平面倒棱；同一色样的面内比例与真实裁切方向一致。"""
    poly=[]
    for point in polygon:
        if not poly or (V(point)-V(poly[-1])).length>.0001: poly.append(point)
    if len(poly)>1 and (V(poly[-1])-V(poly[0])).length<.0001: poly.pop()
    if len(poly)<3: return
    pairs=list(zip(poly,poly[1:]+poly[:1]))
    area=sum(a[0]*b[1]-a[1]*b[0] for a,b in pairs)
    if abs(area)<.0001: return
    if area<0: poly.reverse()
    a,b=zip(*poly)
    w,h=max(a)-min(a),max(b)-min(b)
    cx,cy=sum(a)/len(a),sum(b)/len(b)
    cut=min(bevel,w*.16,h*.16,(back-front)*.45)
    u0,v0=R.uniform(.025,.25),R.uniform(.025,.25)
    scale=R.uniform(.56,.72)/max(w,h)
    def uv(polygon): return [(u0+(x-min(a))*scale,v0+(y-min(b))*scale) for x,y in polygon]
    inset=[(cx+(x-cx)*(1-2*cut/w),cy+(y-cy)*(1-2*cut/h)) for x,y in poly]
    layers=[(inset,front),(poly,front+cut),(poly,back)] if cut else [(poly,front),(poly,back)]
    rings=[p.verts([plane.at(x,y,d) for x,y in ring]) for ring,d in layers]
    p.face(rings[0],tile,uv(layers[0][0]))
    p.face(rings[-1][::-1],tile,uv(poly)[::-1])
    for ri,(lo,hi) in enumerate(zip(rings,rings[1:])):
        for j in range(len(poly)):
            k=(j+1)%len(poly)
            p.face((lo[j],hi[j],hi[k],lo[k]),tile,[(.12,.23),(.12,.39),(.85,.39),(.85,.23)])


def wall_plane(x,y,dx,dy):
    return Plane((x,y,0),(dx,dy,0))


def rectangle(left,right,bottom,top):
    return [(left,bottom),(right,bottom),(right,top),(left,top)]
