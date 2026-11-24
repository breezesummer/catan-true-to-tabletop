"""水际作业构件、桩桥、滑轮吊盘和谷物货载。"""
import math

from grain_mesh import (Part, Face, UP, V, R, beam, cord, extrude, leaf, masonry, peg,
                        rim, slab, trunk, tube)
from granary import BOTTOM, HF, SILL, TX

SEA_END = -5.45
APRONTIP = -2.73
JET_LEFT, JET_RIGHT = -10.38, -5.78
QUAY_LEFT, QUAY_RIGHT = -3.96, 11.16
CRANE_BASE, CRANE_TIP = V((-6.14,.59,9.06)), V((-2.59,-2.47,9.23))
TRAY_X, TRAY_Y, TRAY_Z = -2.50,-2.58,1.17
BASKET_X, BASKET_Y = 3.38,-1.35


def bollard(p,x,y):
    z=SILL
    rings=[(0,.39),(.09,.40),(.17,.29),(.30,.236),(.67,.245),(.72,.36),(.82,.396),(.90,.31)]
    tube(p,[(x,y,z+h) for h,r in rings],[r for h,r in rings],"iron",16)
    rim(p,[(x+.267*math.cos(j*math.tau/20),y+.267*math.sin(j*math.tau/20),z+.20)
           for j in range(20)],.034,"brass",6)


def landing():
    stonework=Part("临潮挡墙与石铺卸粮廊")
    pier=Part("穿水木桩与左侧挑桥")
    fittings=Part("系缆墩与桥面锻钉")
    front=Face(0,APRONTIP,1,0)
    masonry(stonework,front,QUAY_LEFT,QUAY_RIGHT,BOTTOM,.32,marine=True,height=.78,length=1.79)
    for xx,yy,side in ((QUAY_RIGHT,APRONTIP,1),(QUAY_LEFT,HF+.12,-1)):
        masonry(stonework,Face(xx,yy,0,side),0,HF+.12-APRONTIP,BOTTOM,.32,marine=True,height=.78,length=1.74)
    slab(stonework,QUAY_LEFT,QUAY_RIGHT,APRONTIP+.02,HF+.12,.35,.26,"mortar")
    y0=APRONTIP-.075
    for row,y1 in enumerate((APRONTIP+.69,-.78,.38,HF+.14)):
        pitch=1.62 if row==0 else 1.98
        seams=[QUAY_LEFT]+[QUAY_LEFT+(j+(1 if row%2 else .51))*pitch+R.uniform(-.08,.08)
                           for j in range(11) if QUAY_LEFT+.29<QUAY_LEFT+(j+(1 if row%2 else .51))*pitch<QUAY_RIGHT-.24]+[QUAY_RIGHT]
        for l,r in zip(seams,seams[1:]):
            l+=.018
            r-=.018
            m,n=y0+.017,y1-.015
            c=R.uniform(.047,.096)
            loop=[(l+c,m),(r-c,m),(r,m+c),(r,n-c),(r-c,n),(l+c,n),(l,n-c),(l,m+c)]
            h=SILL+R.uniform(-.012,.012)
            extrude(stonework,[(x,y,h) for x,y in loop],(0,0,-.22),R.choice(("stone","grey","honey","dressed")))
        y0=y1
    # 桥面与仓门齐平，只有承重梁、斜撑和木桩在桥下，露出桩间水面。
    def boards(x0,x1,y0,y1):
        n=round((y1-y0)/.49)
        for i in range(n):
            y=y0+(y1-y0)*(i+.5)/n
            z=SILL-.115+R.uniform(-.010,.010)
            beam(pier,(x0+R.uniform(-.026,.026),y,z),(x1+R.uniform(-.026,.026),y,z),
                 (y1-y0)/n-.017,.23,"dock",.023)
            if i%2==0:
                for x in (x0+.29,x1-.29):
                    peg(fittings,(x,y,z+.111),UP,.038)
    boards(-11.21,QUAY_LEFT,APRONTIP,1.47)
    boards(JET_LEFT,JET_RIGHT,SEA_END,APRONTIP)
    piles=[(x,y) for x in (-9.96,-6.20) for y in (-4.97,-1.91,.93)]
    piles.extend(((-10.86,-2.22),(-4.29,-2.22)))
    for x,y in piles:
        trunk(pier,(x,y,-5.37),(x,y,.14),.28,"dock","agedcut",rings=4,n=12,pile=True)
        tube(pier,[(x,y,-.17),(x,y,-.04)],.292,"iron",12)
    for x in (-9.96,-6.20):
        beam(pier,(x,SEA_END+.04,.06),(x,1.47,.06),.35,.42,"oak",.036)
        beam(pier,(x,-4.97,-2.51),(x,-2.07,-.16),.23,.24,"wet",.033)
    for y in (-4.98,-2.18,.92):
        x0,x1=(JET_LEFT,JET_RIGHT) if y<-3 else (-11.21,QUAY_LEFT)
        beam(pier,(x0,y,-.21),(x1,y,-.21),.38,.36,"tar",.041)
    for x,y in ((-9.83,-4.88),(.11,-2.17),(10.58,-2.17)):
        bollard(fittings,x,y)
    return stonework,pier,fittings


def sack(p,base,axis,h,r,cloth,spin=0,flatten=.82):
    axis=V(axis).normalized()
    xx=(V((1,0,0))-axis*axis.x)
    if xx.length<.4: xx=V((0,1,0))-axis*axis.y
    xx.normalize()
    yy=axis.cross(xx)
    c,s=math.cos(spin),math.sin(spin)
    dx,dy=xx*c+yy*s,-xx*s+yy*c
    base=V(base)
    seed=R.uniform(0,math.tau)
    n=32
    profile=((0,.61),(.045,.80),(.12,.92),(.22,.99),(.38,1.00),(.54,.94),(.65,.80),
             (.75,.60),(.812,.39),(.858,.235),(.885,.226),(.906,.255),(.951,.38),(1,.433))
    def point(f,rr,theta):
        pleat=.095*math.cos(8*theta+seed+1.8*f)+.038*math.cos(13*theta-2.4*f)
        seam=.051*math.cos(2*theta+seed)
        weight=.37+peak(f,.83,.14)*1.62+peak(f,.13,.10)*.79
        buckle=(.055*math.cos(3*theta-16*f+seed)*peak(f,.26,.16)+
                .065*math.sin(6*theta+8*f-seed)*peak(f,.69,.19))
        radius=r*rr*(1+seam+weight*pleat+buckle+.043*math.cos(4*theta-6.3*f+seed))
        z=f*h+(.066*h*(.52+.48*math.cos(7*theta+seed)) if f>.99 else
                 .028*h*math.sin(3*theta+seed+5*f)*math.sin(f*math.pi))
        return (base+axis*z+dx*(r*.095*math.sin(f*3.1+seed)*f+math.cos(theta)*radius)
                +dy*(r*.084*f*f+math.sin(theta)*radius*flatten))
    rows=[]
    for f,rad in profile:
        rows.append(p.verts([point(f,rad,j*math.tau/n) for j in range(n)]))
    for i,(a,b) in enumerate(zip(rows,rows[1:])):
        for j in range(n):
            k=(j+1)%n
            p.face([a[j],a[k],b[k],b[j]],cloth,
                   [(j/n,profile[i][0]),((j+1)/n,profile[i][0]),((j+1)/n,profile[i+1][0]),(j/n,profile[i+1][0])],True)
    p.face(rows[0][::-1],cloth,[(.08,.05)]*n)
    # 翻边向内收口，袋嘴下方有绳扣，侧缝的细麻线贴着布面。
    outer=rows[-1]
    inner=p.verts([point(.967,.305,j*math.tau/n) for j in range(n)])
    for j in range(n):
        k=(j+1)%n
        p.face((outer[j],outer[k],inner[k],inner[j]),cloth,
               [(j/n,.99),((j+1)/n,.99),((j+1)/n,.94),(j/n,.94)],True)
    p.face(inner,"rope",[(.5+.37*math.cos(j*math.tau/n),.5+.37*math.sin(j*math.tau/n)) for j in range(n)])
    for f in (.857,.879):
        ring=[point(f,.254,math.tau*j/22) for j in range(22)]
        rim(p,ring,.043*r,"rope",6)
    knot=point(.869,.27,-1.40)
    for sign in (-1,1):
        tube(p,[knot,knot+dx*sign*.11*r-axis*.06*r,knot+dx*sign*.17*r-axis*.34*r,
                knot+dx*sign*.21*r-axis*.40*r],[.044*r,.043*r,.035*r,.018*r],"rope",6)
    for theta in (-1.20,1.96):
        seam=[point(f,rr+.009,theta) for f,rr in profile[1:10]]
        tube(p,seam,.016*r,cloth,5)
        for a,b in zip(seam,seam[1:]):
            for s0 in (.24,.71):
                c=a.lerp(b,s0)
                tube(p,[c-dx*.030*r-axis*.041*r,c+dx*.033*r+axis*.043*r],.012*r,"rope",4)


def peak(x,at,spread):
    return math.exp(-((x-at)/spread)**2)


def basket(p):
    x,y,z=BASKET_X,BASKET_Y,SILL
    # 柳条逐层穿过竖筋，每换一层，压一挑一的顺序反转。
    def radius(t): return .73+.33*math.sin(t*math.pi*.57)
    n=25
    for j in range(n):
        a=math.tau*j/n
        path=[(x+radius(t)*math.cos(a),y+.84*radius(t)*math.sin(a),z+t*1.04) for t in (0,.25,.55,.80,1)]
        tube(p,path,.040,"husk",6)
    for layer in range(9):
        f=(layer+.5)/9
        loop=[]
        for i in range(2*n):
            a=math.tau*i/(2*n)
            r=radius(f)+.050*math.cos(n*a*.5+layer*math.pi)
            loop.append((x+r*math.cos(a),y+.84*r*math.sin(a),z+f*1.02+.012*math.sin(4*a+layer)))
        rim(p,loop,.069,"willow" if layer%3 else "husk",6)
    for t,r in ((.06,.064),(1,.084)):
        ring=[(x+radius(t)*math.cos(i*math.tau/50),y+.84*radius(t)*math.sin(i*math.tau/50),z+t*1.04)
              for i in range(50)]
        rim(p,ring,r,"willow",8)
    mound=[]
    for r,h in ((1,1.00),(.83,1.14),(.59,1.28),(.32,1.36),(.045,1.40)):
        mound.append(p.verts([(x+r*math.cos(i*math.tau/28),y+.84*r*math.sin(i*math.tau/28),z+h) for i in range(28)]))
    for a,b in zip(mound,mound[1:]):
        for j in range(28):
            k=(j+1)%28
            p.face((a[j],a[k],b[k],b[j]),"grain",[(.02,.15),(.8,.18),(.82,.70),(.05,.68)],True)
    # 可见谷层以黄金角铺排，每粒小麦带长轴与胚端。
    for i in range(92):
        dist=.938*math.sqrt((i+.35)/92)
        a=i*2.399963+R.uniform(-.045,.045)
        cx,cy=x+dist*math.cos(a),y+.84*dist*math.sin(a)
        cz=z+1.40-.43*dist**1.72
        rot=R.uniform(0,math.tau)
        d=V((.161*math.cos(rot),.161*math.sin(rot),R.uniform(-.038,.038)))
        center=V((cx,cy,cz))
        leaf(p,center-d,center+d,.067,.054,UP,R.choice(("grain","grain","husk")),creased=True)


def goods():
    stock=Part("岸边托盘与三只粮袋")
    grain=Part("柳编量斗与可见麦粒")
    x,y=8.13,-.78
    for q in (-1.58,0,1.58):
        beam(stock,(x+q,y-1.46,SILL+.10),(x+q,y+1.27,SILL+.10),.27,.19,"tar",.027)
    for i in range(6):
        b=y-1.42+(i+.5)*.448
        beam(stock,(x-2.15,b,SILL+.24),(x+2.15,b,SILL+.24),.425,.19,"dock",.033)
    top=SILL+.337
    sack(stock,(x-.96,y+.36,top),(-.065,.012,1),2.75,.85,"redFlax",2.48)
    sack(stock,(x+.94,y+.35,top),(.084,.038,1),2.59,.88,"canvas",-.61)
    sack(stock,(x-1.15,y-.80,top+.53),(.994,.065,-.042),2.37,.72,"blueFlax",-1.46,.78)
    basket(grain)
    return stock,grain


def crane():
    timber=Part("悬臂吊机与绞盘")
    load=Part("三股吊索与悬挂粮盘")
    a,b=CRANE_BASE,CRANE_TIP
    dir=(b-a).normalized()
    side=V((-dir.y,dir.x,0)).normalized()
    beam(timber,(a.x,a.y,4.79),(a.x,a.y,9.42),.48,.54,"oak",.058)
    beam(timber,a-dir*.36,b+dir*.43,.49,.61,"wall",.065)
    beam(timber,(a.x,a.y,5.53),a.lerp(b,.85)-UP*.24,.34,.42,"oak",.045)
    for pos in ((a.x,a.y,5.48),(a.x,a.y,8.9)):
        peg(timber,V(pos)+side*.27,side,.144)
    # 木制滑车轮两侧有黑铁夹板，承重索沿槽落向吊环。
    wheel=b-UP*.32
    for sign in (-1,1):
        beam(timber,b+side*sign*.215+UP*.26,wheel+side*sign*.215-UP*.14,.11,.34,"iron",.028,up=side)
    tube(timber,[wheel+side*s for s in (-.22,-.17,-.12,.12,.17,.22)],
         [.22,.27,.18,.18,.27,.22],"brass",16)
    tube(timber,[wheel-side*.32,wheel+side*.32],.087,"iron",12)
    drum=V((a.x,a.y-.29,5.73))
    tube(timber,[drum+side*s for s in (-.51,-.44,-.33,.33,.44,.51)],
         [.37,.41,.275,.275,.41,.37],"oak",16)
    helix=[drum+side*(-.31+.62*i/96)+dir*(.292*math.cos(i*math.tau/14))+UP*(.292*math.sin(i*math.tau/14)) for i in range(97)]
    tube(timber,helix,.041,"rope",6)
    tube(timber,[drum-side*.64,drum+side*.63],.082,"iron",10)
    end=drum+side*.61
    beam(timber,end,end+UP*.36,.079,.115,"iron",.014)
    tube(timber,[end+UP*.36,end+UP*.36+side*.24],.063,"agedcut",8)
    cord(timber,[drum+dir*.293, a+UP*.354+dir*.09,b-dir*.16+UP*.32,b+dir*.14],.059,.42)
    hook=V((TRAY_X,TRAY_Y,4.36))
    cord(load,[wheel+dir*.16,hook+UP*.23],.083,.43)
    ring=[hook+V((.142*math.cos(j*math.tau/18),0,.18*math.sin(j*math.tau/18))) for j in range(18)]
    rim(load,ring,.058,"iron",7)
    tx,ty,tz=TRAY_X,TRAY_Y,TRAY_Z
    for y1 in (ty-.72,ty+.72):
        beam(load,(tx-1.36,y1,tz),(tx+1.36,y1,tz),.16,.19,"oak",.025)
    for j in range(6):
        x=tx-1.3+(j+.5)*2.6/6
        beam(load,(x,ty-.91,tz+.117),(x,ty+.91,tz+.117),.414,.18,"dock",.023)
    for xx in (-1.23,1.23):
        for yy in (-.79,.79):
            cord(load,[(tx+xx,ty+yy,tz+.16),(tx+xx*.97,ty+yy*.97,tz+.47),
                       hook+V((.046*xx,.035*yy,-.105))],.060,.47)
    for dx,dy,h,r,fabric in ((-.61,-.13,1.51,.55,"canvas"),(.52,-.20,1.61,.58,"redFlax"),(.01,.43,1.86,.56,"blueFlax")):
        sack(load,(tx+dx,ty+dy,tz+.212),(dx*.09,dy*.07,1),h,r,fabric,spin=dx+1.18)
    return timber,load

