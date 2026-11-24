"""双圆心尖拱、深窗洞、石刻窗花与双扇橡木正门。"""
from math import cos, pi, sin, tau

from mathutils import Vector


def arch(x,r,spring,rise,steps):
    left=[(x+r-2*r*cos(pi*i/(3*steps)),spring+rise*sin(pi*i/(3*steps))/sin(pi/3))
          for i in range(steps+1)]
    return left+[(2*x-u,z) for u,z in reversed(left[:-1])]


def strip(m,f,path,profile,swatch):
    """沿尖拱截面扫过石线脚。截面首坐标向拱外，次坐标向立面外。"""
    path=list(map(Vector,path))
    sections=[]
    for i,p in enumerate(path):
        before=(p-path[max(0,i-1)]).normalized()
        after=(path[min(i+1,len(path)-1)]-p).normalized()
        tangent=(before+after).normalized()
        outer=Vector((-tangent.y,tangent.x))
        offset=outer/max(.55,abs(outer.dot(Vector((-after.y,after.x))))) if i<len(path)-1 else outer
        sections.append([f.at(*(p+offset*w),d) for w,d in profile])
    for row,nxt in zip(sections,sections[1:]):
        for i in range(len(profile)-1):
            m.face([row[i],nxt[i],nxt[i+1],row[i+1]],swatch)


def panel(m,f,outline,material,d=0,out=None):
    m.face([f.at(x,z,d) for x,z in outline],material,outward=f.n if out is None else out)


def rosette(m,f,x,z,r,depth):
    # 四瓣石窗花与真实的前后空间；窗玻璃处在花瓣之后。
    pts=[(x+r*cos(tau*i/20),z+r*sin(tau*i/20)) for i in range(20)]
    panel(m,f,pts,"glass",depth-.023)
    for lobed in (False,True):
        rings=[]
        for i in range(25):
            angle=tau*i/24
            radius=r*((.71+.18*cos(4*angle)) if lobed else .97)
            rings.append([f.at(x+(radius+dr*r)*cos(angle),z+(radius+dr*r)*sin(angle),depth+r*relief)
                          for dr,relief in ((-.055,0),(-.016,.078),(.042,.052),(.065,0))])
        for a,b in zip(rings,rings[1:]):
            for i in range(3):
                m.face([a[i],b[i],b[i+1],a[i+1]],"carving",outward=f.n)


def door(m,f,path,x,r,bottom,spring,rise,depth):
    def head(u):
        return spring+rise*max(0,(4-(1+abs((u-x)/r))**2)/3)**.5
    for i in range(12):
        a=x-r+2*r*i/12+.004
        b=x-r+2*r*(i+1)/12-.004
        if i in (5,6):
            a+=.004
            b-=.004
        outline=[(a,bottom),(b,bottom),(b,head(b)),(a,head(a))]
        back=depth+.014
        m.face([f.at(u,z,back) for u,z in outline],"oak",
               [(.07+i*.068+(u-a)*.75,.075+(z-bottom)/(spring+rise-bottom)*.84) for u,z in outline],
               f.n)
    for side in (-1,1):
        hinge=x+side*r*.88
        for h in (.21,.68,1.03):
            z=bottom+(spring-bottom)*h
            length=r*(.68 if h!=.68 else .81)
            pts=[(hinge,z-.027),(hinge-side*length,z-.018),(hinge-side*(length+.055),z),
                 (hinge-side*length,z+.018),(hinge,z+.027)]
            m.extrude([f.at(u,v,depth+.045) for u,v in pts],-f.n*.018,"lead")
            for j in (.15,.54,.85):
                u=hinge-side*length*j
                m.rod(f.at(u,z,depth+.044),f.at(u,z,depth+.059),.018,"bronze",6,.011)
        center=x+side*.105
        for j in range(8):
            def loop(k):
                return f.at(center+.037*cos(tau*k/8),bottom+.64+.048*sin(tau*k/8),depth+.070)
            m.rod(loop(j),loop(j+1),.009,"bronze",5)


def opening(m,f,spec):
    x,r,bottom,spring,rise,kind=spec
    steps=7 if kind in ("gate","tracery") else 4
    head=arch(x,r,spring,rise,steps)
    path=[(x-r,bottom)]+head+[(x+r,bottom)]
    gate=kind=="gate"
    scale=r*(.33 if gate else .26)
    depth=-min(.24,r*(.35 if gate else .42))
    # 内侧归暗的石质窗膛，窗和门板实际退入墙体。
    contour=path+[path[0]]
    for a,b in zip(contour,contour[1:]):
        m.face([f.at(*a),f.at(*a,depth),f.at(*b,depth),f.at(*b)],"shadow")
    if gate:
        panel(m,f,path,"shadow",depth)
        door(m,f,path,x,r,bottom,spring,rise,depth)
        profile=[(0,-.012),(.08*scale,.038),(.17*scale,.056),(.28*scale,.045),
                 (.34*scale,.015),(.50*scale,.015),(.53*scale,.085),(.65*scale,.12),
                 (.77*scale,.115),(.86*scale,.072),(scale,.07),(scale,0)]
    else:
        # 定向取玻璃坯料，大小不同的窗具有可读的铅接花纹。
        glass_uv=[(.5+(u-x)/max(.74,r*3.0),.13+(z-bottom)/max(1.85,(spring+rise-bottom)*1.30))
                  for u,z in path]
        m.face([f.at(*p,depth) for p in path],"glass",glass_uv,f.n)
        profile=[(0,-.018),(.14*scale,.025),(.37*scale,.038),(.54*scale,.015),
                 (.70*scale,.016),(.82*scale,.048),(scale,.044),(scale,0)]
    strip(m,f,path,profile,"carving" if kind!="slit" else "limestone")
    if not gate:
        m.band(f,x-r-scale,x+r+scale,bottom-.052,bottom+.008,-.02,.065,"limestone")
    if kind=="tracery":
        # 内窗分为成对小尖拱，饰有中央石梃和高处的四叶花。
        start=spring-r*.47
        for offset in (-.5,.5):
            tracery=arch(x+r*offset,r*.48,start,r*.73,5)
            strip(m,f,tracery,[(-r*.055,depth*.48),(0,depth*.48+.027),
                               (r*.055,depth*.48)],"limestone")
        m.rod(f.at(x,bottom,depth*.45),f.at(x,start+.022,depth*.45),r*.064,"carving",6)
        flower=spring+rise*.48
        rosette(m,f,x,flower,r*.30,depth*.44)
        for k in (-1,1):
            m.rod(f.at(x+k*.48*r,start+r*.73,depth*.48),
                  f.at(x+k*.22*r,flower-.21*r,depth*.44),r*.045,"limestone",5)
        m.band(f,x-r,x+r,bottom+(spring-bottom)*.44,bottom+(spring-bottom)*.44+.027,
               depth,depth+.038,"lead")
    elif kind=="belfry":
        for dz in (.23,.46,.69):
            z=bottom+(spring-bottom)*dz
            m.band(f,x-r*.94,x+r*.94,z,z+.027,depth+.002,depth+.043,"lead")
    if gate:
        for side in (-1,1):
            for course in range(5):
                z=bottom+(spring-bottom)*(course+.5)/5
                u=x+side*(r+scale*1.13)
                m.band(f,u-scale*.21,u+scale*.21,z-(spring-bottom)*.095,z+(spring-bottom)*.095,
                       -.01,.095,"limestone")
        # 分段楔石在外拱带上留下窄灰缝。
        for i,(a,b) in enumerate(zip(head,head[1:])):
            a,b=Vector(a),Vector(b)
            strip(m,f,[a.lerp(b,.035),a.lerp(b,.965)],
                  [(scale*.96,.077),(scale*1.26,.098),(scale*1.31,.021)],"limestone" if i%3==0 else "carving")
    return head


def wall(m,f,left,right,bottom,top,rows,swatch="ashlar",breaks=()):
    """逐洞构成立面，开口处没有挡住窗膛的实心盒。top 可为山墙折线。"""
    def fill(a,b,low,high):
        if b-a<.0001:
            return
        cuts=[a]+sorted(v for v in breaks if a+.0001<v<b-.0001)+[b]
        for l,r in zip(cuts,cuts[1:]):
            if min(high(l)-low(l),high(r)-low(r)) < -.0001:
                raise ValueError("洞口高于墙顶")
            panel(m,f,[(l,low(l)),(r,low(r)),(r,high(r)),(l,high(l))],swatch)
    last=bottom
    for limit,specs in rows:
        high=top if limit is None else lambda u,z=limit:z
        low=lambda u,z=last:z
        prev=left
        for s in sorted(specs):
            x,r,base,spring,rise,kind=s
            fill(prev,x-r,low,high)
            fill(x-r,x+r,low,lambda u,z=base:z)
            head=opening(m,f,s)
            for (a,za),(b,zb) in zip(head,head[1:]):
                fill(a,b,lambda u: za+(zb-za)*(u-a)/(b-a),high)
            prev=x+r
        fill(prev,right,low,high)
        if limit is None:
            return
        last=limit
    fill(left,right,lambda u:last,top)
