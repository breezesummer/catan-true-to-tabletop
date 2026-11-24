"""两座垂直相交的仓屋。砌体沿真实拱洞收口，门扇、券石和窗棂各有进深。"""
import math
from craft import (BACK,FLOOR,HALL_EAVE,HALL_FRONT,HALL_LEFT,HALL_MID,HALL_PEAK,HALL_RIGHT,
                   HALL_SLOPE,TOWER_EAVE,TOWER_FLOOR,TOWER_FRONT,TOWER_PEAK,TOWER_W,TOWER_X,
                   Part,R,V,UP,beam,faced_block,iron_loop,pin,skin,tube)
from joinery import grain,panel


class Wall:
    def __init__(self,x,y,along):
        self.base=V((x,y,0))
        self.u=V((*along,0))
        self.inward=UP.cross(self.u)

    def pt(self,u,d,z):
        return self.base+self.u*u+self.inward*d+UP*z

    def block(self,p,poly,d,thick,tile,bevel=.046):
        faced_block(p,[self.pt(u,d,z) for u,z in poly],-self.inward,thick,tile,bevel)

    def timber(self,p,a,b,d,width,depth,tile="oak"):
        beam(p,self.pt(a[0],d,a[1]),self.pt(b[0],d,b[1]),width,depth,tile,
             min(.061,width*.13),up=-self.inward)


class Opening:
    def __init__(self,x,r,sill,spring,rise,trim):
        self.x,self.r,self.sill,self.spring,self.rise,self.trim=x,r,sill,spring,rise,trim
        self.top=spring+rise

    def head(self,t):
        a,b=self.r,self.rise
        if b>=a:
            k=(b*b-a*a)/(2*a)
            angle=(1-abs(2*t-1))*math.atan2(b,k)
            x=(k-(a+k)*math.cos(angle))*(1 if t<=.5 else -1)
            return V((self.x+x,self.spring+(a+k)*math.sin(angle)))
        d=(a*a-b*b)/(2*b)
        angle=(2*t-1)*math.atan2(a,d)
        return V((self.x+(b+d)*math.sin(angle),self.spring-d+(b+d)*math.cos(angle)))

    def half_at(self,z):
        if z<=self.spring: return self.r
        a,b=self.r,self.rise
        h=min(b,z-self.spring)
        if b>=a:
            k=(b*b-a*a)/(2*a)
            return max(0,math.sqrt(max(0,(a+k)**2-h*h))-k)
        d=(a*a-b*b)/(2*b)
        return math.sqrt(max(0,(b+d)**2-(d+h)**2))

    def crown_at(self,x):
        a,b=self.r,self.rise
        x=min(a,abs(x-self.x))
        if b>=a:
            k=(b*b-a*a)/(2*a)
            return self.spring+math.sqrt(max(0,(a+k)**2-(k+x)**2))
        d=(a*a-b*b)/(2*b)
        return self.spring-d+math.sqrt(max(0,(b+d)**2-x*x))


def clip_u(points,limit,keep_right):
    output=[]
    for a,b in zip(points,points[1:]+points[:1]):
        da,db=a[0]-limit,b[0]-limit
        if (da>=0 if keep_right else da<=0): output.append(a)
        if da*db<0:
            output.append((limit,a[1]+(b[1]-a[1])*da/(da-db)))
    return output


def masonry_spans(x0,x1,z0,z1,openings):
    holes=[h for h in openings if h.x+h.r>x0 and h.x-h.r<x1 and h.top>z0 and h.sill<z1]
    if not holes:
        yield [(x0,z0),(x1,z0),(x1,z1),(x0,z1)]
        return
    levels=sorted({z0,z1,*[z for h in holes for z in (h.sill,h.top) if z0<z<z1]})
    for lo,hi in zip(levels,levels[1:]):
        active=[h for h in holes if h.sill<(lo+hi)*.5<h.top]
        heights=sorted({lo,hi,*[h.head(t/16).y for h in active for t in range(9) if lo<h.head(t/16).y<hi]})
        sides=[[(x0,z) for z in heights]]
        for h in active:
            sides.extend([[(h.x-h.half_at(z),z) for z in heights],[(h.x+h.half_at(z),z) for z in heights]])
        sides.append([(x1,z) for z in heights])
        for left,right in zip(sides[::2],sides[1::2]):
            poly=clip_u(clip_u(right+list(reversed(left)),x0,True),x1,False)
            if len(poly)>2 and max(p[0] for p in poly)-min(p[0] for p in poly)>.002:
                # 直边中间点与共点会让侧边在 FBX 三角化时退化。
                clean=[]
                for k,pt in enumerate(poly):
                    prev=poly[k-1]; nxt=poly[(k+1)%len(poly)]
                    if abs((pt[0]-prev[0])*(nxt[1]-pt[1])-(pt[1]-prev[1])*(nxt[0]-pt[0]))>1e-7:
                        clean.append(pt)
                if len(clean)>2: yield clean


def ashlar(p,w,half,height,holes,rows=7):
    h=(height-FLOOR)/rows
    holes=sorted(holes,key=lambda v:v.x)
    for poly in masonry_spans(-half,half,FLOOR,height,holes):
        w.block(p,poly,.13,.38,"dress3",.012)
    for row in range(rows):
        z0=FLOOR+row*h+.020
        cuts=[-half]; cursor=-half-(.76 if row%2 else 0)
        while cursor<half:
            cursor+=R.uniform(1.40,1.98) if row else R.uniform(1.65,2.21)
            if cursor<half-.42: cuts.append(cursor)
        cuts.append(half)
        for a,b in zip(cuts,cuts[1:]):
            tile=R.choice(("ashlar0","ashlar0","ashlar1","ashlar2","ashlar3"))
            d=R.uniform(-.026,.026)
            for poly in masonry_spans(a+.022,b-.022,z0,z0+h-.042,holes):
                w.block(p,poly,d,.43,tile,R.uniform(.027,.060))


def surround(p,w,h):
    t=h.trim
    count=max(2,round((h.spring-h.sill)/(.72 if t>.3 else .56)))
    for sign in (-1,1):
        inner=h.x+sign*h.r
        for i in range(count):
            a=h.sill+(h.spring-h.sill)*i/count+.015
            b=h.sill+(h.spring-h.sill)*(i+1)/count-.013
            outer=inner+sign*t*(1.11 if i==0 else 1)
            w.block(p,[(inner,a),(outer,a),(outer,b),(inner,b)],-.15,.59,"dress"+str(R.randrange(3)),.037)
    def outer(t0,thick):
        v=h.head(t0)
        tangent=(h.head(min(1,t0+.002))-h.head(max(0,t0-.002))).normalized()
        return v+V((-tangent.y,tangent.x))*thick
    n=11 if h.r>1 else 7
    for i in range(n):
        start,end=(i+.022)/n,(i+.978)/n
        mid=(start+end)*.5
        keystone=i==n//2
        thick=t*(1.19 if keystone else 1)
        arc=[h.head(start),h.head(mid),h.head(end),outer(end,thick),outer(mid,thick),outer(start,thick)]
        w.block(p,arc,-.19 if keystone else -.15,.59,"dress"+str(R.randrange(3)),.033)


def window(p,w,h):
    outline=[(h.x-h.r,h.sill),(h.x+h.r,h.sill)]+[h.head(i/12) for i in range(12,-1,-1)]
    w.block(p,outline,.30,.12,"enamel",.014)
    sill=h.sill-.035
    w.timber(p,(h.x-h.r-h.trim-.10,sill),(h.x+h.r+h.trim+.10,sill),-.083,.20,.53,"dress1")
    split=h.spring+h.rise*.13
    w.timber(p,(h.x,h.sill+.05),(h.x,split),.043,.115,.15,"dress2")
    for side in (-1,1):
        a=w.pt(h.x,.046,split)
        b=w.pt(h.x+side*h.r*.32,.046,split+h.rise*.34)
        c=w.pt(h.x+side*h.r*.49,.046,h.spring+h.rise*.60)
        tube(p,[a,b,c],(.069,.059,.056),"dress2",7)
        w.timber(p,(h.x+side*h.r*.52,h.sill+.10),(h.x+side*h.r*.52,h.spring),.17,.045,.052,"iron")


def doors(p,w,h):
    for i in range(12):
        a=h.x-h.r+2*h.r*(i/12)+.018
        b=h.x-h.r+2*h.r*((i+1)/12)-.018
        outline=[(a,h.sill+.02),(b,h.sill+.02),(b,h.crown_at(b)-.016),(a,h.crown_at(a)-.016)]
        # 纵板正面沿竖轴取木纹，端面藏于券石内。
        points=[w.pt(u,.10,z) for u,z in outline]
        tex=grain(h.top-h.sill,b-a)
        uv=[tex((z-h.sill)/(h.top-h.sill),(u-a)/(b-a)) for u,z in outline]
        panel(p,points,w.inward*.21,"door" if i%4!=1 else "boards",uv,"agedcut")
    for sign in (-1,1):
        for z in (h.sill+.72,h.spring-.22):
            a=h.x+sign*(h.r-.08); b=h.x+sign*(h.r-.81)
            w.timber(p,(a,z),(b,z),.046,.118,.073,"iron")
            for u in (a,b): pin(p,w.pt(u,-.001,z),-w.inward,.049)
        c=w.pt(h.x+sign*.39,-.004,h.sill+1.60)
        pin(p,c,-w.inward,.12)
        iron_loop(p,c-w.inward*.055-UP*.12,w.u*.112,UP*.165,.034,"bronze")


def infill(p,w,half,base,eave,peak,front=True):
    def top(x): return peak-(peak-eave)*abs(x)/half
    n=math.ceil(half*2/.55)
    for j in range(n):
        x0=-half+2*half*j/n+.012; x1=-half+2*half*(j+1)/n-.012
        h0,h1=top(x0),top(x1)
        if min(h0,h1)<base: continue
        poly=[(x0,base-.035),(x1,base-.035),(x1,h1),(x0,h0)]
        sample=grain(max(h0,h1)-base+.04,x1-x0)
        uv=[sample((z-base+.035)/(max(h0,h1)-base+.04),(x-x0)/(x1-x0)) for x,z in poly]
        panel(p,[w.pt(x,.025,z) for x,z in poly],w.inward*.22,R.choice(("boards","boards","door")),uv,"agedcut")
    for z in (base+.12,eave):
        if z>base+.3 or peak==eave or front:
            w.timber(p,(-half-.08,z),(half+.08,z),-.15,.34,.36)
    for x in (-half+.17,0,half-.17):
        w.timber(p,(x,base),(x,max(base+.16,top(x)-.03)),-.15,.33,.34)
    if peak>eave+.1:
        for side in (-1,1):
            w.timber(p,(0,peak-.05),(side*half,eave-.02),-.16,.26,.35)
    if eave>base+1.5:
        for side in (-1,1):
            x=side*half*.46
            w.timber(p,(x,base+.16),(x,eave),-.15,.26,.33)
            w.timber(p,(side*(half-.23),base+.30),(side*half*.48,eave-.19),-.14,.32,.29)
        for x in (-half+.2,-half*.46,0,half*.46,half-.2):
            for z in (base+.12,eave): pin(p,w.pt(x,-.334,z),-w.inward,.073,"iron")


def houses():
    stone,carving,wood,fenestration=[Part(n) for n in ("两仓错缝砌石与灰缝","券石窗套与斜撑墩","檐托楼枋与山墙竖板","双板门与双联窗")]
    left,right=TOWER_X-TOWER_W,TOWER_X+TOWER_W
    front=Wall(TOWER_X,TOWER_FRONT,(1,0))
    rear=Wall(TOWER_X,BACK,(-1,0))
    side_mid=(TOWER_FRONT+BACK)*.5
    gate=Opening(0,1.53,FLOOR,2.69,1.74,.43)
    ashlar(stone,front,TOWER_W,TOWER_FLOOR,[gate],8)
    surround(carving,front,gate)
    ashlar(stone,rear,TOWER_W,TOWER_FLOOR,[],8)
    infill(wood,front,TOWER_W+.06,TOWER_FLOOR,TOWER_EAVE,TOWER_PEAK)
    infill(wood,rear,TOWER_W+.06,TOWER_FLOOR,TOWER_EAVE,TOWER_PEAK)
    for i in range(7):
        u=(i-3)*.95
        beam(wood,front.pt(u,.03,TOWER_FLOOR-.36),front.pt(u,-.32,TOWER_FLOOR-.11),
             .24,.26,"oak",.041)
    for i in range(9):
        x=left+.095+(i+.5)*(right-left-.19)/9
        beam(wood,(x,TOWER_FRONT+.10,TOWER_FLOOR-.13),(x,BACK-.10,TOWER_FLOOR-.13),
             (right-left-.19)/9-.018,.19,"tar",.026)
    for x,direction in ((left,(0,-1)),(right,(0,1))):
        wall=Wall(x,side_mid,direction)
        half=(BACK-TOWER_FRONT)*.5-.24
        ashlar(stone,wall,half,TOWER_FLOOR,[],8)
        infill(wood,wall,half+.09,TOWER_FLOOR,TOWER_EAVE,TOWER_EAVE)
    # 门洞可见仓内背墙和承重梁，海侧门槛接到桩承地板上。
    for x in (left+.29,right-.29):
        beam(wood,(x,TOWER_FRONT+.2,TOWER_FLOOR-.13),(x,BACK-.25,TOWER_FLOOR-.13),.31,.34,"tar")
    for z in (FLOOR+.12,TOWER_FLOOR-.16):
        beam(wood,(left+.12,BACK-.36,z),(right-.12,BACK-.36,z),.3,.29,"tar")
    for x in (-TOWER_W+.37,TOWER_W-.37):
        for k in range(6):
            z=FLOOR+k*.68
            depth=.77-.058*k
            front.block(carving,[(x-.36,z),(x+.36,z),(x+.34,z+.657),(x-.34,z+.657)],-depth,depth+.29,
                        "dress"+str(1+k%2),.044)
    a,b=HALL_LEFT,HALL_RIGHT
    cx=(a+b)*.5; half=(b-a)*.5
    fac=Wall(cx,HALL_FRONT,(1,0))
    door=Opening(.49,1.83,FLOOR,3.64,.72,.40)
    windows=[Opening(x,.50,2.00,3.18,.78,.215) for x in (-3.72,4.82)]
    ashlar(stone,fac,half,HALL_EAVE,[door,*windows])
    for h in (door,*windows): surround(carving,fac,h)
    for h in windows: window(fenestration,fac,h)
    doors(fenestration,fac,door)
    for side,x,along in ((1,b,(0,1)),(-1,a,(0,-1))):
        wall=Wall(x,HALL_MID,along)
        side_window=Opening(.14,.61,1.98,3.35,.88,.23)
        ashlar(stone,wall,(BACK-HALL_FRONT)*.5-.13,HALL_EAVE,[side_window] if side>0 else [])
        if side>0:
            surround(carving,wall,side_window); window(fenestration,wall,side_window)
        infill(wood,wall,(BACK-HALL_FRONT)*.5,HALL_EAVE,HALL_EAVE,HALL_PEAK)
    far=Wall(cx,BACK,(-1,0))
    back_windows=[Opening(x,.49,2.1,3.06,.73,.21) for x in (-3.85,3.18)]
    ashlar(stone,far,half,HALL_EAVE,back_windows)
    for h in back_windows: surround(carving,far,h); window(fenestration,far,h)
    for w in (fac,far):
        w.timber(wood,(-half,HALL_EAVE-.22),(half,HALL_EAVE-.22),-.085,.27,.35,"oak")
        for j in range(8):
            x=-half+.39+j*(2*half-.78)/7
            beam(wood,w.pt(x,.1,HALL_EAVE-.37),w.pt(x,-.43,HALL_EAVE-.18),.24,.24,"oak",.043)
    return stone,carving,wood,fenestration
