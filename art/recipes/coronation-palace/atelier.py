"""石作、屋面和开口构件。立面以水平切线、向外深度和垂直高度定位。"""
from math import cos, pi, sin, tau
from pathlib import Path
import sys

from mathutils import Vector

shared = str(Path(__file__).resolve().parent.parent / "gothic-royal-city")
if shared not in sys.path:
    sys.path.insert(0,shared)
from openings import arch, panel, strip, wall
from stonework import Frame, Mason, UP


def rect(x,y,w,d,z,clip):
    a,b=w*.5,d*.5
    c=min(clip,a*.6,b*.6)
    return [Vector((x+u,y+v,z)) for u,v in
            ((-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c))]


def stack(m,x,y,w,d,profile,cut=.034):
    """截面每一行给出该层底高、出挑和向上延伸的石种。"""
    rings=[rect(x,y,w+2*delta,d+2*delta,z,cut) for z,delta,_ in profile]
    m.face(rings[0][::-1],profile[0][2])
    for (_,_,stone),low,high in zip(profile,rings,rings[1:]):
        for i in range(8):
            j=(i+1)%8
            m.face([low[i],low[j],high[j],high[i]],stone)
    m.face(rings[-1],profile[-1][2])


def block(m,x,y,w,d,z0,z1,stone="limestone",bevel=.012):
    b=min(bevel,(z1-z0)*.33,w*.16,d*.16)
    stack(m,x,y,w,d,[(z0,-b,stone),(z0+b,0,stone),(z1-b,0,stone),(z1,-b,stone)],max(.004,b))


def molding(m,x,y,w,d,z,size=1):
    profile=[(-.142,-.015,"limestone"),(-.120,.025,"carving"),(-.099,.031,"shadow"),
             (-.082,.008,"limestone"),(-.061,.022,"carving"),(-.039,.057,"carving"),
             (-.026,.079,"carving"),(.010,.079,"carving"),(.023,.067,"limestone")]
    stack(m,x,y,w,d,[(z+h*size,o*size,s) for h,o,s in profile])


def sides(x,y,w,d):
    return [(Frame(x,y-d/2,0,-1),w),(Frame(x+w/2,y,1,0),d),
            (Frame(x,y+d/2,0,1),w),(Frame(x-w/2,y,-1,0),d)]


def cord(m,f,points,radius,stone="carving",sides=7):
    """连续线条共用断面，避免逐段圆柱在捏尖、转角处出现裂口。"""
    pts=[f.at(*p) for p in points]
    sections=[]
    for i,p in enumerate(pts):
        a=(p-pts[max(0,i-1)]).normalized()
        b=(pts[min(len(pts)-1,i+1)]-p).normalized()
        tangent=(a+b).normalized()
        right=tangent.cross(f.n).normalized()
        forward=right.cross(tangent).normalized()
        sections.append([p+radius*(right*cos(tau*k/sides)+forward*sin(tau*k/sides)) for k in range(sides)])
    for a,b in zip(sections,sections[1:]):
        for i in range(sides):
            j=(i+1)%sides
            m.face([a[i],b[i],b[j],a[j]],stone)


def finial(m,x,y,z,size=1):
    # 短方座、叠涩颈圈和石雕橡实，是四视图中各角相同的收头。
    stack(m,x,y,.205*size,.205*size,[(z+h*size,d*size,s) for h,d,s in
          ((0,.022,"limestone"),(.044,.022,"carving"),(.062,.001,"limestone"),
           (.153,.001,"carving"),(.177,.032,"carving"),(.209,.032,"carving"),(.226,0,"carving"))])
    m.turned(x,y,[(z+h*size,r*size) for h,r in
             ((.222,.082),(.236,.097),(.254,.085),(.267,.057),(.287,.070),
              (.32,.076),(.351,.064),(.384,.034),(.406,.009))],10,"carving")


def quoin(m,x,y,bottom,top,size,bands,cap=True):
    block(m,x,y,size,size,bottom,top,"limestone",.019)
    # 角墩腰身的水平石缝只在相邻石块间退入，砖层与墙面尺度一致。
    z=bottom+.22
    while z<top-.09:
        stack(m,x,y,size+.007,size+.007,[(z-.008,0,"shadow"),(z+.006,0,"limestone")],.017)
        z+=.30
    for h in bands:
        molding(m,x,y,size+.024,size+.024,h,.77)
    if cap:
        finial(m,x,y,top,size/.254)


def hip(m,x,y,w,d,eave,top,upper_w,upper_d):
    """四坡沿等距瓦行铺板岩，瓦口、四条铅包斜脊和屋面各有独立轮廓。"""
    def corners(ww,dd,z):
        return [Vector((x+u*ww/2,y+v*dd/2,z)) for u,v in ((-1,-1),(1,-1),(1,1),(-1,1))]
    lo,hi=corners(w,d,eave),corners(upper_w,upper_d,top)
    m.face(lo[::-1],"shadow")
    for i in range(4):
        j=(i+1)%4
        a,b,c,d0=lo[i],lo[j],hi[j],hi[i]
        n=(b-a).cross(d0-a).normalized()
        length=((a+b-c-d0)*.5).length
        count=max(2,round(length/.126))
        for k in range(count):
            t,v=k/count,(k+1)/count
            p,q=a.lerp(d0,t),b.lerp(c,t)
            s,r=a.lerp(d0,v),b.lerp(c,v)
            # 物理下唇对齐图集的一道瓦缝，每一坡维持一致的板瓦宽度。
            uv=[(.47-(q-p).length/6.65,k/29),(.47+(q-p).length/6.65,k/29),
                (.47+(r-s).length/6.65,(k+1)/29),(.47-(r-s).length/6.65,(k+1)/29)]
            lift=n*.012
            m.face([p+lift,q+lift,r,s],"slate",uv,n)
            if (q-p).length>.012:
                m.face([q+lift,p+lift,p,q],"lead",outward=Vector((n.x,n.y,-.20)))
        if (hi[i]-lo[i]).length>.05:
            m.rod(lo[i]+UP*.015,hi[i]+UP*.015,.033,"lead",7)
        m.rod(a,b,.031,"lead",7)
    if min(upper_w,upper_d)>.01:
        m.face(hi,"lead",outward=UP)


def pairs(m,f,half,z0,z1,tiers,centers,r,base,spring,rise):
    rows=[]
    for k in range(tiers):
        dz=k*(z1-z0)/tiers
        windows=[(u+offset,r,base+dz,spring+dz,rise,"lancet")
                 for u in centers for offset in (-r*1.16,r*1.16)]
        rows.append((z0+(k+1)*(z1-z0)/tiers if k+1<tiers else None,windows))
    wall(m,f,-half,half,z0,lambda u:z1,rows)
    for k in range(tiers):
        dz=k*(z1-z0)/tiers
        for u in centers:
            m.band(f,u-2.54*r,u+2.54*r,base+dz-.071,base+dz-.039,-.015,.074,"carving")
            # 双灯窗共用一圈折返石框，内侧保留凹槽，窗玻璃从石梃后面透出。
            middle=spring+dz+.016
            left=arch(u-r*1.16,r*1.16,middle,rise+.034,6)
            right=arch(u+r*1.16,r*1.16,middle,rise+.034,6)
            outline=[(left[0][0],base+dz-.015)]+left+right[1:]+[(right[-1][0],base+dz-.015)]
            width=r*(.46 if tiers<3 else .33)
            strip(m,f,outline,[(0,.010),(.12*width,.050),(.34*width,.062),(.54*width,.041),
                               (.74*width,.034),(width,.066),(1.25*width,.054),(1.28*width,-.008)],"carving")


def dentils(m,f,half,z,spacing=.213):
    n=max(2,round(2*half/spacing))
    for i in range(n):
        u=-half+(i+.5)*2*half/n
        m.band(f,u-.040,u+.040,z-.064,z-.004,-.006,.067,"limestone")


def arch_open(m,f,x,r,spring,rise,base,top,depth):
    """通透回廊。拱腹厚度、拱肩填石和各块楔石均参与投影。"""
    points=arch(x,r,spring,rise,7)
    strip(m,f,points,[(0,-.010),(.011,.025),(.032,.041),(.048,.026),
                     (.059,.012),(.089,.017),(.104,.040)],"carving")
    contour=list(map(Vector,points))
    miters=[]
    for i,p in enumerate(contour):
        previous=(p-contour[max(0,i-1)]).normalized()
        following=(contour[min(i+1,len(contour)-1)]-p).normalized()
        tangent=(previous+following).normalized()
        normal=Vector((-tangent.y,tangent.x))
        miters.append(normal/max(.65,abs(tangent.dot(previous if i+1==len(contour) else following))))
    for i,(a,b) in enumerate(zip(points,points[1:])):
        panel(m,f,[a,b,(b[0],top),(a[0],top)],"ashlar")
        m.face([f.at(*a),f.at(*a,-depth),f.at(*b,-depth),f.at(*b)],"limestone",
               outward=Vector((0,0,-1)))
        panel(m,f,[a,b,(b[0],top),(a[0],top)],"shadow",-depth,-f.n)
        # 楔石在相邻段共用的角平分线上合缝，越过拱顶也保持等宽窄缝。
        p,q=contour[i:i+2]
        sections=[]
        for t in (.012,.988):
            o=p.lerp(q,t)
            n=miters[i].lerp(miters[i+1],t)
            sections.append([f.at(*(o+n*w),d) for w,d in
                             ((.092,.016),(.101,.052),(.122,.061),(.145,.048),(.148,-.005))])
        stone="limestone" if i%4==0 else "carving"
        for k in range(4):
            m.face([sections[0][k],sections[1][k],sections[1][k+1],sections[0][k+1]],stone,outward=f.n)
    for s in (-1,1):
        u=x+s*r
        m.face([f.at(u,base),f.at(u,base,-depth),f.at(u,spring,-depth),f.at(u,spring)],"shadow",
               outward=-s*f.t)


def pillar(m,x,y,bottom,spring):
    """回廊石柱的方形础座、鼓形柱身、柱颈圆饰与承拱顶板。"""
    block(m,x,y,.335,.335,bottom,bottom+.085,"limestone")
    z=bottom
    profile=[(z+.074,.148),(z+.112,.151),(z+.135,.121),(z+.155,.128),
             (z+.178,.124),(z+.198,.100),(spring-.14,.088),(spring-.117,.110),
             (spring-.089,.112),(spring-.073,.128),(spring-.043,.139)]
    m.turned(x,y,profile,14,"carving")
    block(m,x,y,.286,.286,spring-.048,spring+.016,"limestone")
    block(m,x,y,.331,.331,spring+.010,spring+.062,"carving")
