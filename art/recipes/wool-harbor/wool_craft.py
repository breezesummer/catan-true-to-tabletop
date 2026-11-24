"""毫米制构件。石砌立面和木料沿用港口工法，羊毛表面在同一张图集内取样。"""
import math
import random
from mathutils import Vector
from wool_surfaces import uv
from quay_craft import (Part as QuayPart, Face, Opening, clear_holes, extrude, masonry,
                        rect, stone, surround, window)
from joinery import beam, cord, grain, trunk, tube

V = Vector
UP = V((0,0,1))
R = random.Random(211717)


class Part(QuayPart):
    def face(self, ids, tile, coords, smooth=False):
        self.faces.append(tuple(ids))
        self.uvs.extend(uv(tile,u,v) for u,v in coords)
        self.smooth.append(smooth)


def pin(p, a, b, r=.075, tile="iron"):
    tube(p,[a,b],r,tile,8)


def ovoid(p, center, radii, tile, seg=16, rows=9, tilt=0):
    """带真极点的椭球，绕面法线转动可调眼睑、羊唇和毛锁的倾斜。"""
    cx,cy,cz=center
    ax,ay,az=radii
    c,s=math.cos(tilt),math.sin(tilt)
    def point(x,y,z):
        return (cx+c*x+s*z,cy+y,cz-s*x+c*z)
    poles=p.verts([point(0,0,-az),point(0,0,az)])
    rings=[]
    for j in range(1,rows):
        phi=math.pi*(j/rows-.5)
        rings.append(p.verts([point(ax*math.cos(phi)*math.cos(math.tau*i/seg),
            ay*math.cos(phi)*math.sin(math.tau*i/seg),az*math.sin(phi)) for i in range(seg)]))
    for i in range(seg):
        k=(i+1)%seg
        u,v=i/seg,(i+1)/seg
        p.face((poles[0],rings[0][k],rings[0][i]),tile,[((u+v)/2,0),(v,1/rows),(u,1/rows)],True)
        for j,(lo,hi) in enumerate(zip(rings,rings[1:]),1):
            p.face((lo[i],lo[k],hi[k],hi[i]),tile,[(u,j/rows),(v,j/rows),(v,(j+1)/rows),(u,(j+1)/rows)],True)
        p.face((rings[-1][i],rings[-1][k],poles[1]),tile,[(u,1-1/rows),(v,1-1/rows),((u+v)/2,1)],True)


def roof(p, cx,cy, axis, length, run, eave, ridge, rows):
    along=V((*axis,0))
    across=V((along.y,-along.x,0))
    center=V((cx,cy,ridge))
    def at(t,d,lift=0):
        return center+along*t+across*d+UP*(lift-abs(d)*(ridge-eave)/run)
    for side in (-1,1):
        corners=[at(-length/2,0),at(length/2,0),at(length/2,side*run),at(-length/2,side*run)]
        extrude(p,corners,(0,0,-.19),"tar")
        beam(p,at(-length/2,side*run,-.13),at(length/2,side*run,-.13),.21,.26,"oak",.035)
        for end in (-1,1):
            beam(p,at(end*length/2,0,-.12),at(end*length/2,side*run,-.12),.25,.32,"wall",.042)
        step=run/rows
        for row in range(rows):
            foot=run-row*step
            head=max(0,foot-1.47*step)
            cuts=[-length/2]
            q=cuts[0]+(.48 if row%2 else 1.08)
            while q<length/2-.36:
                cuts.append(q)
                q+=R.uniform(.91,1.19)
            cuts.append(length/2)
            for a,b in zip(cuts,cuts[1:]):
                l,r=a+.012,b-.011
                d=foot+R.uniform(-.043,.043)
                corners=[(l,head),(r,head),(r,d-.09),(r-.058,d-.018),
                         (r-.16,d+.005),((l+r)*.5,d+R.uniform(-.045,.024)),(l+.071,d),(l,d-.077)]
                phase=R.uniform(.01,.05)
                pts=[at(t,side*f,.044+.128*(f-head)/max(.2,d-head)+phase) for t,f in corners]
                u0,v0=R.uniform(.09,.48),R.uniform(.07,.44)
                coords=[(u0+.36*(t-l)/(r-l),v0+.46*(f-head)/max(.2,d-head)) for t,f in corners]
                extrude(p,pts,(0,0,-.095),R.choice(("slate","slate","blue")),coords)
    # 分节弧形压脊，拔高的端瓦压住交接处的木封檐。
    pieces=round(length/1.11)
    for j in range(pieces):
        a=-length/2+length*j/pieces-.015
        b=a+length/pieces+.06
        rad=.30+(.063 if j in (0,pieces-1) else 0)
        profile=[(rad*math.cos(math.pi*k/8),rad*math.sin(math.pi*k/8)) for k in range(9)]
        outline=[center+along*a+across*x+UP*(z+.085) for x,z in profile]
        extrude(p,outline,along*(b-a),"slate")
