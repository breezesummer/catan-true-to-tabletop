"""错缝料石、拱券和门窗。墙面的砖带按同一条券内缘裁切，洞口保留侧壁及真实深度。"""
import math
from dataclasses import dataclass
from fabrication import ASHLAR, Plane, R, beam, facing, rectangle, tube


@dataclass(frozen=True)
class Opening:
    center: float
    half: float
    sill: float
    spring: float
    rise: float
    pointed: bool = True

    def shoulder(self,t,edge=0):
        # 尖券由相交双弧组成，宽门由半椭圆压低弓高。
        offset=.48 if self.pointed else 0
        limit=math.acos(offset/(1+offset))
        a=t*limit
        return ((self.half+edge)*((1+offset)*math.cos(a)-offset),
                self.spring+(self.rise+edge)*math.sin(a)/math.sin(limit))

    def width_at(self,z):
        if z<=self.spring: return self.half
        offset=.48 if self.pointed else 0
        limit=math.acos(offset/(1+offset))
        sine=min(1,max(0,(z-self.spring)/self.rise))*math.sin(limit)
        return max(0,self.half*((1+offset)*math.sqrt(max(0,1-sine*sine))-offset))

    def contour(self):
        arc=[self.shoulder(i/10) for i in range(11)]
        return ([(self.center-self.half,self.sill),(self.center+self.half,self.sill)]+
                [(self.center+d,z) for d,z in arc]+[(self.center-d,z) for d,z in reversed(arc[:-1])])


def clip(poly,distance):
    result=[]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        da,db=distance(*a),distance(*b)
        if da>=0: result.append(a)
        if (da<0)!=(db<0):
            t=da/(da-db)
            result.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
    return result


def carve(left,right,bottom,top,openings):
    relevant=[o for o in openings if top>o.sill and bottom<o.spring+o.rise
              and left<o.center+o.width_at(bottom) and right>o.center-o.width_at(bottom)]
    levels={bottom,top}
    for o in relevant:
        levels.update(z for z in (o.sill,o.spring,o.spring+o.rise*.55,o.spring+o.rise*.83,o.spring+o.rise)
                      if bottom<z<top)
    levels=sorted(levels)
    for lo,hi in zip(levels,levels[1:]):
        polys=[rectangle(left,right,lo,hi)]
        for o in relevant:
            if (lo+hi)/2<o.sill or (lo+hi)/2>o.spring+o.rise: continue
            width_lo,width_hi=o.width_at(lo),o.width_at(hi)
            def half(z): return width_lo+(width_hi-width_lo)*(z-lo)/(hi-lo)
            polys=[area for poly in polys for area in
                   (clip(poly,lambda u,z:o.center-half(z)-u),clip(poly,lambda u,z:u-o.center-half(z)))
                   if len(area)>=3]
        yield from polys


def cut_stone(left,right,bottom,top,openings):
    """合并裁弧时的分带，凿棱只走单块料石的外周，券肩不留水平假缝。"""
    bands=list(carve(left,right,bottom,top,openings))
    if len(bands)<2: return bands
    bands=[[(round(x,5),round(y,5)) for x,y in p] for p in bands]
    levels={}
    for poly in bands:
        for x,y in poly: levels.setdefault(y,set()).add(x)
    border={}
    for poly in bands:
        for a,b in zip(poly,poly[1:]+poly[:1]):
            stops=[a,b]
            if a[1]==b[1]:
                cuts=[(x,a[1]) for x in levels[a[1]] if min(a[0],b[0])<x<max(a[0],b[0])]
                stops=[a,*sorted(cuts,reverse=a[0]>b[0]),b]
            for s,t in zip(stops,stops[1:]):
                if s==t: continue
                if (t,s) in border: del border[t,s]
                else: border[s,t]=True
    paths={}
    for a,b in border: paths.setdefault(a,[]).append(b)
    result=[]
    while paths:
        start=next(iter(paths))
        a=start
        loop=[]
        while True:
            loop.append(a)
            b=paths[a].pop()
            if not paths[a]: del paths[a]
            a=b
            if a==start: break
        # 在同一直线上的分割点无需带入最终网格。
        corners=[]
        for i,b in enumerate(loop):
            a,c=loop[i-1],loop[(i+1)%len(loop)]
            if abs((b[0]-a[0])*(c[1]-b[1])-(b[1]-a[1])*(c[0]-b[0]))>1e-7: corners.append(b)
        if len(corners)>=3: result.append(corners)
    return result


def courses(part,plane,left,right,bottom,top,rows,openings=(),stone_width=1.35,tideline=False):
    h=(top-bottom)/rows
    for poly in carve(left,right,bottom,top,openings):
        facing(part,plane,poly,.12,.48,"lime")
    for row in range(rows):
        z=bottom+row*h
        start=left
        target=R.uniform(.44,.66)*stone_width if row%2 else stone_width
        while start<right-.07:
            end=min(right,start+target)
            if right-end<.48*stone_width: end=right
            tile=R.choice(ASHLAR)
            if tideline and z< -2.5:
                tile="tidal" if z< -3.05 or R.random()<.57 else "stone"
            for poly in cut_stone(start+.019,end-.019,z+.022,z+h-.024,openings):
                facing(part,plane,poly,R.uniform(-.022,.025),.19,tile,.043)
            start=end
            target=stone_width*R.uniform(.84,1.17)


def archivolt(part,plane,o,width,segments):
    for side in (-1,1):
        count=max(2,round((o.spring-o.sill)/.67))
        for i in range(count):
            a=o.sill+(o.spring-o.sill)*i/count+.014
            b=o.sill+(o.spring-o.sill)*(i+1)/count-.014
            inner=o.center+side*o.half
            outer=inner+side*(width+R.uniform(-.025,.025))
            facing(part,plane,rectangle(min(inner,outer),max(inner,outer),a,b),-.14,.38,
                   R.choice(("coping","ashlar","coping")),.037)
        for i in range(segments):
            t0=(i+.032)/segments
            t1=(i+.968)/segments
            tm=(t0+t1)/2
            path=[]
            for edge,ts in ((0,(t0,tm,t1)),(width,(t1,tm,t0))):
                for t in ts:
                    u,z=o.shoulder(t,edge)
                    path.append((o.center+side*u,z))
            facing(part,plane,path,-.16 if i==segments-1 else -.12,.40,
                   R.choice(("ashlar","coping","coping")),.027)


def door(part,plane,o):
    facing(part,plane,o.contour(),.29,.47,"tar")
    for j in range(12):
        a=o.center-o.half+2*o.half*j/12+.017
        b=a+2*o.half/12-.035
        def height(x):
            offset=abs(x-o.center)/o.half
            return o.spring+o.rise*math.sqrt(max(0,1-offset*offset))-.065
        outline=[(a,o.sill+.025),(b,o.sill+.025),(b,height(b)),((a+b)/2,height((a+b)/2)),(a,height(a))]
        # 板纹沿门高，两个门扇中缝略宽。
        for axis in (a,b):
            if abs(axis-o.center)<.025:
                facing(part,plane,rectangle(axis-.015,axis+.015,o.sill,height(axis)),.20,.25,"tar")
        # 板门使用纵向木纹。只裁上肩，板底与门槛贴齐。
        vertical=Plane(plane.origin,plane.v,-plane.u)
        facing(part,vertical,[(z,-x) for x,z in outline],.16,.31,R.choice(("door","door","oak")),.027)
    for s in (-1,1):
        handle=o.center+s*.36
        for z in (o.sill+.65,o.spring-.48):
            end=o.center+s*(o.half-.13)
            inner=end-s*.59
            beam(part,plane.at(end,z,.11),plane.at(inner,z,.11),.075,.12,"iron",.018,end="iron")
            for x in (end,inner):
                tube(part,[plane.at(x,z,.10),plane.at(x,z,.045)],.045,"bronze",7)
        z=o.sill+1.62
        facing(part,plane,[(handle-.13,z),(handle,z+.17),(handle+.13,z),(handle,z-.17)],.07,.17,"iron",.021)
        tube(part,[plane.at(handle+.091*math.cos(i*math.tau/14),z-.13+.127*math.sin(i*math.tau/14),.034)
                   for i in range(14)],.031,"bronze",7,True)


def window(part,plane,o):
    facing(part,plane,o.contour(),.235,.38,"tar")
    # 两扇窄尖窗与窗顶透光孔；铅条在暗玻璃前有独立厚度。
    sep=.087
    w=(o.half-sep)*.48
    for s in (-1,1):
        x=o.center+s*(w+sep*.46)
        lancet=Opening(x,w,o.sill+.092,o.spring-.055,o.rise*.65)
        facing(part,plane,lancet.contour(),.16,.218,"glass",.012)
        for flank in (-1,1):
            points=[plane.at(x+flank*w,lancet.sill,.10)]
            for i in range(7):
                u,z=lancet.shoulder(i/6)
                points.append(plane.at(x+flank*u,z,.10))
            tube(part,points,.047,"coping",7)
        for z in (o.sill+.34,o.spring-.19):
            beam(part,plane.at(x-w+.03,z,.113),plane.at(x+w-.03,z,.113),.035,.035,"iron",.006,end="iron")
    cx=o.center
    beam(part,plane.at(cx,o.sill,.075),plane.at(cx,o.spring+o.rise*.70,.075),.104,.10,"coping",.019,end="stone")
    z=o.spring+o.rise*.78
    diamond=[(cx,z+.14),(cx+.083,z),(cx,z-.14),(cx-.083,z)]
    facing(part,plane,diamond,.13,.19,"glass")
    facing(part,plane,rectangle(cx-o.half-.24,cx+o.half+.24,o.sill-.14,o.sill+.025),-.24,.22,"coping",.04)
