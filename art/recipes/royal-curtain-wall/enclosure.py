"""四段卧石墙、开敞圆塔、两心尖券城门及其门旗。南门朝 −Y，棋盘表面为零。"""
from math import acos, ceil, cos, pi, sin, tau
from mathutils import Vector, geometry

X, Y, RADIUS = 11.04, 11.92, 1.98
GRADE = .77
SPRING = 2.61


def curtains(m):
    for label, center, angle, half in (("北", (0,Y), 0, X), ("西", (-X,0), pi/2, Y),
                                       ("东", (X,0), pi/2, Y), ("南", (0,-Y), 0, X)):
        m.name = label+"墙 · 交错砌体与墙顶走道"
        a, b = -half+RADIUS*.73, half-RADIUS*.73
        spans = ((a,-3.60),(3.60,b)) if label=="南" else ((a,b),)
        ca,sa=cos(angle),sin(angle)
        for left,right in spans:
            mid=(left+right)/2
            px, py = center[0]+mid*ca, center[1]+mid*sa
            m.block(px,py,-.13,4.05,right-left,1.17,"mortar",angle)
            m.courses(center,angle,left,right,-.12,.72,1,1.94,1.54,"foot")
            m.courses(center,angle,left,right,.72,3.87,6,1.39,1.29)
            m.courses(center,angle,left,right,3.87,4.14,1,1.65,1.47,"trim")
            # 走道内侧保持平整，外侧女墙上砌四组宽垛。
            outward = (0,1) if label=="北" else (0,-1) if label=="南" else (-1,0) if label=="西" else (1,0)
            rail = (center[0]+outward[0]*.41, center[1]+outward[1]*.41)
            m.courses(rail,angle,left,right,4.14,4.48,1,1.45,.62,"bed")
            merlons = 1 if label=="南" else 4
            for i in range(merlons):
                u=left+(right-left)*(i+.5)/merlons
                x,y=rail[0]+u*ca,rail[1]+u*sa
                m.block(x,y,4.47,5.13,1.66,.66,"bed",angle)
                # 两片压顶齐缝，顶面略探出垛身。
                for k in (-1,1):
                    m.block(x+k*.432*ca,y+k*.432*sa,5.12,5.32,.848,.76,"trim",angle,.033)


def towers(m):
    for sx,sy,label in ((-1,-1,"西南"),(1,-1,"东南"),(-1,1,"西北"),(1,1,"东北")):
        x,y=sx*X,sy*Y
        m.name=label+"圆塔 · 弧形石皮与收分基脚"
        m.turned(x,y,[(-.20,RADIUS*.98),(.47,RADIUS*.98),(.78,RADIUS*.92),
                      (4.36,RADIUS*.92),(4.54,1.94),(4.61,1.95)],40,"mortar")
        levels=[(-.17,.48,2.00,"foot"),(.48,.77,1.94,"aged")]
        for row in range(7):
            levels.append((.77+row*.497,.77+(row+1)*.497,1.88,"bed"))
        levels.extend([(4.245,4.42,1.93,"trim"),(4.42,4.63,1.99,"trim")])
        for row,(low,high,r,swatch) in enumerate(levels):
            pieces=9 if row<2 else 12
            for i in range(pieces):
                a=tau*(i+.5*(row%2))/pieces
                b=a+tau/pieces
                stone="aged" if swatch=="bed" and m.rng.random()<.22 else swatch
                m.wedge(x,y,a+.005,b-.005,r-.28,r,low+.008,high-.008,stone,.023)
        m.name=label+"圆塔 · 扇形铺石与开口垛圈"
        # 内向象限开向两侧走道，角塔的顶面能从墙头直接走入。
        ox,oy=-sx,-sy
        inward={(-1,-1):5*pi/4,(1,-1):7*pi/4,(-1,1):3*pi/4,(1,1):pi/4}[(ox,oy)]
        start=inward+pi*.32
        end=inward+tau-pi*.32
        for i in range(10):
            a,b=start+(end-start)*i/10,start+(end-start)*(i+1)/10
            m.wedge(x,y,a+.004,b-.004,1.44,1.98,4.62,5.11,"bed")
        for i in range(6):
            a=start+(end-start)*i/6
            b=a+(end-start)/6*.65
            m.wedge(x,y,a,b,1.43,1.98,5.10,5.84,"bed")
            m.wedge(x,y,a-.007,b+.007,1.40,2.01,5.83,6.07,"trim",.027)
        for ring,(lo,hi,num) in enumerate(((.27,.88,7),(.90,1.95,11))):
            for i in range(num):
                a=tau*(i+ring*.40)/num
                m.wedge(x,y,a+.008,a+tau/num-.008,lo,hi,4.46,4.64,"pave",.018)
        m.turned(x,y,[(4.47,.27),(4.63,.255)],12,"trim")


def gate(m):
    # 两心圆弧在拱尖相会；每枚拱石贯穿自己的进深，透空处可见券腹。
    m.name="门楼 · 贯通拱腹与双层券石"
    middle=-Y
    half,origin=2.28,.89
    def profile(t,extra):
        r=half+origin+extra
        a=t*acos(origin/r)
        return -origin+r*cos(a), SPRING+r*sin(a)

    def wafer(points,y_front,y_back,swatch):
        front=[Vector((x,y_front,z)) for x,z in points]
        m.extrude(front,(0,y_back-y_front,0),swatch)

    # 石灰灰浆退在券石之后，从门洞内仰望仍能看到连续的券腹。
    for sign in (-1,1):
        for i in range(18):
            wafer([(sign*x,z) for t,r in ((i/18,.036),((i+1)/18,.036),((i+1)/18,.62),(i/18,.62))
                   for x,z in [profile(t,r)]],middle-.93,middle+.93,"mortar")
    # 两拱圈的拱脚同圆心；砌缝错位；每道券石的迎光正棱另切细斜面。
    for ring,(inner,outer,depth,pieces) in enumerate(((0,.255,1.01,8),(.255,.62,1.15,7))):
        for sign in (-1,1):
            for i in range(pieces):
                if ring==1 and i==pieces-1:
                    continue
                lo,hi=(i+.032)/pieces,(i+1-.032)/pieces
                def outline(a,b,r0,r1):
                    xs=[profile(t,r) for r,ts in ((r0,(a,(a+b)/2,b)),(r1,(b,(a+b)/2,a))) for t in ts]
                    return [(sign*x,z) for x,z in xs]
                full=outline(lo,hi,inner,outer)
                cut=outline(lo+.004,hi-.004,inner+.024,outer-.022)
                layers=[[(x,middle+d,z) for x,z in section] for d,section in (
                    (-depth,cut),(-depth+.04,full),(depth-.04,full),(depth,cut))]
                # XZ 横断面的正向须朝进深方向。
                if geometry.normal([Vector(p) for p in layers[0]]).y < 0:
                    layers=[row[::-1] for row in layers]
                m.cut()
                m.solid(layers,"trim" if ring==0 or i%3 else "bed")
        for side in (-1,1):
            w=outer-inner
            m.block(side*(half+(inner+outer)/2),middle,-.12,GRADE,w,depth*2,"foot",0,.025)
            for i in range(5):
                m.block(side*(half+(inner+outer)/2),middle,GRADE+(SPRING-GRADE)*i/5,
                        GRADE+(SPRING-GRADE)*(i+1)/5-.012,w,depth*2,"trim",0,.025)
    # 冠部以一枚跨中锁石收合两半券，内沿跟随尖拱，上肩压入拱上砌体。
    def crown(inset):
        t=(6.03/7)+inset*.34
        a,za=profile(t,.255+inset)
        b,zb=profile(t,.62-inset)
        peak=profile(1,.255+inset)[1]
        return [(-a,za),(0,peak),(a,za),(b,zb),(.38-inset,6.48-inset),
                (-.38+inset,6.48-inset),(-b,zb)]
    layers=[[(x,middle+d,z) for x,z in crown(inset)] for d,inset in
            ((-1.205,.027),(-1.17,0),(1.17,0),(1.205,.027))]
    if geometry.normal([Vector(p) for p in layers[0]]).y<0:
        layers=[row[::-1] for row in layers]
    m.solid(layers,"trim")
    # 侧墙与拱上三角区连接，楔石脚下的净空始终连续。
    top=6.46
    # 拱肩按水平卧缝切出填充石，底边服从券背轮廓。
    def at_height(z):
        r=half+origin+.59
        return max(0,(max(0,r*r-(z-SPRING)**2))**.5-origin)
    for sign in (-1,1):
        for i in range(7):
            z0=SPRING+(top-SPRING)*i/7
            z1=SPRING+(top-SPRING)*(i+1)/7
            xm=at_height((z0+z1)/2)
            wafer([(sign*3.36,z0),(sign*at_height(z0),z0),(sign*xm,(z0+z1)/2),
                   (sign*at_height(z1),z1),(sign*3.36,z1)],middle-.80,middle+.80,"mortar")
            lo,hi=z0+.012,z1-.010
            split=max(at_height(lo)+.065,1.64+(i%2)*.63)
            left=[(sign*x,z) for x,z in ((split,lo),(at_height(lo),lo),(xm,(z0+z1)/2),
                                           (at_height(hi),hi),(split,hi))]
            wafer(left,middle-.855,middle+.855,"bed")
            if split<3.32:
                m.block(sign*(split+3.35)/2,middle,lo,hi,3.35-split-.018,1.71,"bed",edge=.024)
    m.courses((0,middle),0,-3.56,3.56,6.45,6.71,1,1.08,1.83,"trim")
    for sign in (-1,1):
        x=sign*3.26
        m.name=("西" if sign<0 else "东")+"门墩 · 券脚与门旗托座"
        m.block(x,middle,-.16,6.90,1.19,1.87,"mortar")
        m.courses((x,middle),0,-.75,.75,-.13,.79,1,1.8,2.37,"foot")
        for i in range(9):
            lo=.79+i*.614
            m.block(x,middle,lo,lo+.60,1.29,2.18,"bed" if i%4 else "aged")
        m.block(x,middle,6.30,6.57,1.48,2.31,"trim")
        m.block(x,middle,6.56,6.98,1.42,2.25,"bed")
        m.block(x,middle,6.97,7.15,1.55,2.39,"trim")


def banners(m):
    for sign in (-1,1):
        cx,cy=sign*3.26,-Y-1.24
        m.name=("西" if sign<0 else "东")+"门旗 · 亚麻、卷边与黄铜挂件"
        for side in (-1,1):
            x=cx+side*.52
            m.rod((x,cy+.20,5.37),(x,cy-.13,5.37),.050,"iron",10)
            m.turned(x,cy-.12,[(5.32,.072),(5.40,.072)],12,"brass")
        m.rod((cx-.62,cy-.11,5.38),(cx+.62,cy-.11,5.38),.057,"brass",14)
        for side in (-1,1):
            m.rod((cx+side*.61,cy-.11,5.38),(cx+side*.68,cy-.11,5.38),.087,"brass",12,end_radius=.034)
        def surface(u,v):
            x=cx+(u-.5)*.94 + .033*v*v
            tail=.62*(1-abs(u*2-1))
            z=5.35-v*(2.96-tail)
            y=cy-.108-.058*sin(u*tau*1.7+.41*v)-.044*sin(v*pi)*cos(u*pi*2+.4)-.034*v
            return Vector((x,y,z))
        nu,nv=12,16
        for j in range(nv):
            for i in range(nu):
                uv=[(a/nu,b/nv) for a,b in ((i,j),(i+1,j),(i+1,j+1),(i,j+1))]
                points=[surface(u,v) for u,v in uv]
                coords=[(.08+u*.77,.93-v*.86) for u,v in uv]
                m.face(points,"cloth",coords,Vector((0,-1,0)))
                m.face([p+Vector((0,.028,0)) for p in points],"cloth",coords,Vector((0,1,0)))
        # 布边卷缝随褶皱转折，四个挂耳缠过实心铜杆。
        for side in (0,1):
            for j in range(nv):
                m.rod(surface(side,j/nv),surface(side,(j+1)/nv),.021,"cloth",5)
        for i in range(nu):
            m.rod(surface(i/nu,1),surface((i+1)/nu,1),.021,"cloth",5)
        for u in (.10,.34,.65,.90):
            x=surface(u,0).x
            for i in range(9):
                a,b=tau*i/9,tau*(i+1)/9
                m.face([(x+w,cy-.11+.073*cos(t),5.38+.073*sin(t)) for w,t in
                        ((-.038,a),(-.038,b),(.038,b),(.038,a))],"cloth")


def yard(m):
    m.name="院地 · 夯土与临水收分石基"
    # 石基埋进海岸及规则顶点下方，庭土固定于现有棋子的落座高程。
    def rect(x,y,z):
        return [(-x,-y,z),(x,-y,z),(x,y,z),(-x,y,z)]
    m.solid([rect(X-.83,Y-.78,-4.05),rect(X+.10,Y+.10,-.32),rect(X+.12,Y+.10,.64)],"foot")
    m.block(0,0,.63,GRADE,X*2+.03,Y*2+.04,"soil",edge=.02)
    m.name="门前 · 错缝铺石与贴岸门槛"
    m.block(0,-Y-.55,-3.95,.63,4.38,3.16,"foot",edge=.09)
    for j in range(7):
        y=-Y-1.92+j*.59
        cuts=(-2.18,-.88,.66,2.18) if j%2==0 else (-2.18,-1.61,-.16,1.38,2.18)
        for i,(a,b) in enumerate(zip(cuts,cuts[1:])):
            m.block((a+b)/2,y,.63,GRADE+.014+(i%2)*.005,b-a-.028,.566,
                    "pave" if (i+j)%4 else "trim",edge=.033)


def assemble(m):
    yard(m)
    curtains(m)
    towers(m)
    gate(m)
    banners(m)
