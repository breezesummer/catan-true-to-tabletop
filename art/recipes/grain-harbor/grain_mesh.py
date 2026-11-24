"""谷仓木石构件与屋面。尺寸均为毫米，+Y 朝岸，Z=0 对齐岸面。"""
import math
from pathlib import Path
import random
import sys

from grain_atlas import uv

sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"common-harbor"))
from quay_craft import (MeshPart, Face, Opening, UP, V, beam, cord, extrude, masonry,
                        rect, stone, surround, trunk, tube, window)

R = random.Random(21720)


class Part(MeshPart):
    def face(self, ids, tile, coords, smooth=False):
        self.faces.append(tuple(ids))
        self.uvs.extend(uv(tile,u,v) for u,v in coords)
        self.smooth.append(smooth)


def slab(p,x0,x1,y0,y1,z,depth,tile):
    extrude(p,[(x0,y0,z),(x1,y0,z),(x1,y1,z),(x0,y1,z)],(0,0,-depth),tile,
            [(.04,.11),(.96,.11),(.96,.86),(.04,.86)])


def rim(p,path,r,tile,n=7):
    tube(p,path,r,tile,n,closed=True)


def peg(p,point,normal,r=.070,tile="iron"):
    pos,axis=V(point),V(normal)
    tube(p,[pos-axis*.034,pos,pos+axis*.031],[r*.9,r,r*.66],tile,8)


def leaf(p,base,tip,width,depth,normal,tile,creased=False):
    """两端收尖的颖壳，中脊抬高，根端比尖端圆钝。"""
    a,b=V(base),V(tip)
    axis=(b-a).normalized()
    side=normal.cross(axis).normalized()
    face=axis.cross(side).normalized()
    rings=[]
    n=12 if creased else 8
    for f,scale in ((0,.07),(.19,.76),(.43,1),(.65,.85),(.84,.47),(1,.035)):
        center=a.lerp(b,f)+face*(.19*depth*math.sin(f*math.pi))
        coords=[]
        for j in range(n):
            k=math.tau*j/n
            c,s=math.cos(k),math.sin(k)
            rib=1+.18*max(s,0)**4
            if creased and s>0:
                rib=1-.59*max(0,1-abs(c)*2.9)
            coords.append(center+scale*(side*(width*c)+face*(depth*s*rib)))
        rings.append(p.verts(coords))
    for i,(a,b) in enumerate(zip(rings,rings[1:])):
        for j in range(n):
            k=(j+1)%n
            p.face((a[j],a[k],b[k],b[j]),tile,
                   [(j/n,i/5),((j+1)/n,i/5),((j+1)/n,(i+1)/5),(j/n,(i+1)/5)],True)
    for ring,reverse in ((rings[0],True),(rings[-1],False)):
        p.face(ring[::-1] if reverse else ring,tile,[(.5,.5)]*n)


def roof(frame,tiles,center,axis,across,length,run,eave,ridge):
    center,axis,across=V((*center,0)),V(axis),V(across)
    pitch=(ridge-eave)/run
    for side in (-1,1):
        normal=(UP+across*(side*pitch)).normalized()

        def pos(s,t,depth=0):
            return center+axis*s+across*(side*(run-t))+UP*(eave+t*pitch)+normal*depth

        lo,hi=-length/2,length/2
        outline=[pos(lo,0),pos(hi,0),pos(hi,run),pos(lo,run)]
        extrude(frame,outline,-normal*.24,"tar")
        beam(frame,pos(lo,-.02,-.13),pos(hi,-.02,-.13),.28,.34,"oak",.037)
        # 檐椽端面可在近水低机位看见。靠脊贴瓦，檐口略翘以压住下一排。
        for i in range(round(length/1.23)+1):
            s=lo+.11+(length-.22)*i/round(length/1.23)
            beam(frame,pos(s,.04,-.34),pos(s,run-.05,-.34),.19,.27,"wall",.025,up=normal)
        courses=round(run/.60)
        for row in range(courses):
            step=run/courses
            bottom=row*step
            top=min(run+.025,bottom+step+.22)
            count=round(length/.82)
            width=length/count
            cuts=[lo]+[lo+(i+(.53 if row%2 else 1))*width+R.uniform(-.062,.062)
                       for i in range(count) if lo+.17<lo+(i+(.53 if row%2 else 1))*width<hi-.16]+[hi]
            for start,end in zip(cuts,cuts[1:]):
                x0,x1=start+.013,end-.013
                t=bottom+R.uniform(-.041,.034)
                chip=min(R.uniform(.035,.12),(x1-x0)*.22)
                # 稍错开的薄片尾缘、断角和露白的板岩边。
                loop=[(x0,t+chip),(x0+chip*.85,t),(x1-chip*.61,t-.013),
                      (x1,t+chip*.6),(x1,top),(x0,top)]
                lift=R.uniform(.108,.15)
                points=[pos(x,y,.035+lift*(top-y)/(top-t)) for x,y in loop]
                ox,oy=R.uniform(.05,.49),R.uniform(.04,.43)
                coords=[(ox+.44*(x-x0)/(x1-x0),oy+.50*(y-t)/(top-t)) for x,y in loop]
                tile=R.choices(("slate","blue","heather"),(6,4,2))[0]
                extrude(tiles,points,-normal*.082,tile,coords)
        for end in (lo-.017,hi+.017):
            for i in range(5):
                a=run*i/5-.07
                b=run*(i+1)/5+.055
                beam(tiles,pos(end,a,.17),pos(end,b,.17),.23,.19,
                     "heather" if i%3 else "blue",.040,up=normal)
    # 屋脊逐节压扣，断面为拱形，两侧薄唇覆住最上一排瓦。
    count=round(length/1.09)
    for i in range(count):
        s0=-length/2+i*length/count-.065
        s1=-length/2+(i+1)*length/count+.055
        rows=[]
        for s,r in ((s0,.274),(s0+.095,.300),(s0+.14,.259),(s1,.247)):
            rows.append([center+axis*s+UP*(ridge+.075+r*math.sin(k*math.pi/8))
                         +across*(r*math.cos(k*math.pi/8)) for k in range(9)])
        for i0 in range(3):
            for j in range(8):
                vs=[rows[i0][j],rows[i0+1][j],rows[i0+1][j+1],rows[i0][j+1]]
                mid=(j+.5)*math.pi/8
                norm=UP*math.sin(mid)+across*math.cos(mid)
                if (vs[1]-vs[0]).cross(vs[2]-vs[0]).dot(norm)<0: vs.reverse()
                tiles.face(tiles.verts(vs),"blue",[(.09,.1),(.85,.1),(.85,.32),(.09,.32)])
        for row,norm in ((rows[0],-axis),(rows[-1],axis)):
            extrude(tiles,row,-norm*.045,"heather")

