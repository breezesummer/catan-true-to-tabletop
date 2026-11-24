"""装卸木吊机、捆索、箍桶、货箱与直接承在水中桩墩上的工作码头。"""
import math
from quay_craft import (Face, Part, R, UP, V, beam, cord, extrude, masonry, rect,
                        stone, trunk, tube)
from quay_house import (DOOR_X, EAST, FLOOR, FRONT, HALL_PEAK, HALL_RIDGE_Y, HALL_ROOF_EAVE,
                        HALL_ROOF_RUN, JOINT, LEFT, SEA_END, STORE_FRONT, TOWER_X)

QUAY = 9.65
JETTY_X, JETTY_HALF = TOWER_X,2.38
TIP = V((-2.16,10.97,8.98))
CRATE = (TIP.x,TIP.y,1.17)
TABLET_WIDTH = 11.30
TABLET_HEIGHT = TABLET_WIDTH*4.29/12.50
TABLET_FIELD,TABLET_LETTER_TOP = .27,.44
TABLET_UP = V((0,HALL_ROOF_RUN,HALL_PEAK-HALL_ROOF_EAVE)).normalized()
TABLET_NORMAL = V((0,-TABLET_UP.z,TABLET_UP.y))
TABLET_CENTER = V((DOOR_X,HALL_RIDGE_Y-.51*HALL_ROOF_RUN,HALL_PEAK-.51*(HALL_PEAK-HALL_ROOF_EAVE)))


def tablet_point(u,v,depth):
    return TABLET_CENTER+V((u,0,0))+TABLET_UP*v+TABLET_NORMAL*depth


def rivet(p,c,n,r=.060,tile="iron"):
    c,n = V(c),V(n)
    tube(p,[c-n*.018,c,c+n*r*.32],(r*.89,r,r*.67),tile,8)


def chest(p,center,size,angle=0):
    """板箱四面分别铺板，框与斜撑压在板面，钉帽落在端部搭接处。"""
    first = len(p.vertices)
    w,d,h = size
    sides = (Face(0,-d/2,1,0),Face(w/2,0,0,1),Face(0,d/2,-1,0),Face(-w/2,0,0,-1))
    for index,f in enumerate(sides):
        width = d if index%2 else w
        a = width*.5
        for k in range(4):
            u0,u1 = -a+k*width/4+.007,-a+(k+1)*width/4-.007
            s,t = R.uniform(.09,.25),R.uniform(.06,.62)
            f.plate(p,rect(u0,u1,.052,h-.052),-.024,.09,"pine",[(s,t),(s,t+.1),(s+.66,t+.1),(s+.66,t)])
        for level in (.094,h-.094):
            f.member(p,(-a,level),(a,level),.17,.09,"dock",.048,.024)
        for u in (-a+.097,a-.097):
            f.member(p,(u,.17),(u,h-.17),.16,.11,"dock",.048,.021)
        f.member(p,(-a+.16,.165),(a-.16,h-.165),.153,.094,"oak",.060,.021)
        if index in (0,1):
            f.member(p,(-a+.16,h-.165),(-.055,h/2+.065),.135,.083,"dock",.061,.019)
            f.member(p,(.055,h/2-.065),(a-.16,.165),.135,.083,"dock",.061,.019)
            for u in (-a+.105,a-.105):
                for z in (.090,h-.090):
                    rivet(p,f.at(u,z,.106),f.n,.040)
    for k in range(5):
        y = -d/2+(k+.5)*d/5
        beam(p,(-w/2+.024,y,h-.037),(w/2-.024,y,h-.037),d/5-.015,.088,"pine",.016)
    for x in (-w*.35,w*.35):
        beam(p,(x,-d/2,h+.019),(x,d/2,h+.019),.153,.078,"oak",.017)
    c,s = math.cos(angle),math.sin(angle)
    dx,dy,dz = (v*.001 for v in center)
    p.vertices[first:] = [(dx+c*x-s*y,dy+s*x+c*y,dz+z) for x,y,z in p.vertices[first:]]


def cask(p,x,y,z,scale=1):
    radius,height = .87*scale,2.17*scale
    n = 16
    spin = R.uniform(0,math.tau)
    levels = [(0,.803),(.06,.834),(.25,.95),(.50,1),(.75,.95),(.94,.834),(1,.803)]
    def point(a,h,r):
        return (x+radius*r*math.cos(a),y+radius*r*math.sin(a),z+height*h)
    for j in range(n):
        a = spin+math.tau*j/n+.008
        b = spin+math.tau*(j+1)/n-.008
        u,v = R.uniform(.05,.26),R.uniform(.06,.62)
        material = R.choice(("door","oak","door","pine"))
        rows = [p.verts([point(a,h,r),point(b,h,r)]) for h,r in levels]
        for k in range(len(levels)-1):
            lo,hi = levels[k][0],levels[k+1][0]
            p.face([rows[k][0],rows[k][1],rows[k+1][1],rows[k+1][0]],material,
                   [(u+lo*.68,v),(u+lo*.68,v+.10),(u+hi*.68,v+.10),(u+hi*.68,v)])
    def bound(h):
        for (a,r),(b,s) in zip(levels,levels[1:]):
            if a<=h<=b: return r+(s-r)*(h-a)/(b-a)
    for level,width in ((.075,.069),(.26,.077),(.74,.075),(.92,.064)):
        rows=[]
        for h,bias in ((level-width/2,.016),(level-width/2+.009,.037),(level+width/2-.010,.037),(level+width/2,.010)):
            rows.append(p.verts([point(spin+math.tau*j/n,h,bound(h)+bias) for j in range(n)]))
        for k in range(3):
            for j in range(n):
                t = (j+1)%n
                p.face([rows[k][j],rows[k][t],rows[k+1][t],rows[k+1][j]],"iron",
                       [(j/n,.21+k*.15),((j+1)/n,.21+k*.15),((j+1)/n,.36+k*.15),(j/n,.36+k*.15)],True)
        rivet(p,point(-1.15,level,bound(level)+.042),(math.cos(-1.15),math.sin(-1.15),0),.046*scale,"brass")
    # 桶盖低于凸出的桶口；板缝、桶塞与桶箍分别成形。
    rad = radius*.785
    tube(p,[(x,y,z+height*.964),(x,y,z+height*.982)],rad,"agedcut",24)
    for i in range(5):
        a,b = -rad+(2*rad)*i/5+.009,-rad+(2*rad)*(i+1)/5-.009
        xs = [a+(b-a)*k/4 for k in range(5)]
        curve = [(dx,math.sqrt(max(0,rad*rad-dx*dx))) for dx in xs]
        poly = curve+[(dx,-dy) for dx,dy in reversed(curve)]
        extrude(p,[(x+dx,y+dy,z+height*.987) for dx,dy in poly],(0,0,-.048),"pine",
                [(.47+dy/(3*rad),.37+i*.063+dx*.08) for dx,dy in poly])
    for h in (.012,.992):
        path = [point(spin+j*math.tau/n,h,.804) for j in range(n)]
        tube(p,path,.027*scale,"oak",6,True)
    tube(p,[(x+rad*.37,y-.12,z+height*.985),(x+rad*.37,y-.12,z+height*1.002)],.092*scale,"agedcut",10)


def crane():
    frame,gear,lines,load = [Part(n) for n in ("吊臂与斜撑","滑轮铁箍与绞盘","三股吊索与吊钩","悬起的货箱")]
    heel = V((-5.76,FRONT-.19,8.80))
    axis = (TIP-heel).normalized()
    beam(frame,(-5.76,FRONT-.17,5.64),(-5.76,FRONT-.17,9.18),.53,.55,"oak",.07)
    beam(frame,heel-axis*.28,TIP+axis*.36,.55,.59,"dock",.072)
    beam(frame,(-5.74,FRONT-.28,5.88),heel.lerp(TIP,.79)-UP*.19,.38,.42,"oak",.058)
    for t in (.07,.89):
        c = heel.lerp(TIP,t)
        beam(gear,c-axis*.09,c+axis*.09,.585,.630,"iron",.027,end="iron")
    for z in (5.96,8.46):
        c = V((-5.76,FRONT-.17,z))
        beam(gear,c-UP*.075,c+UP*.075,.575,.61,"iron",.023,end="iron")
    axle = V((-axis.y,axis.x,0)).normalized()
    c = TIP-UP*.26
    for side in (-1,1):
        offset = axle*(side*.19)
        beam(gear,c+offset+UP*.38,c+offset-UP*.22,.15,.14,"oak",.03)
        rivet(gear,c+offset+axle*side*.064,axle*side,.090)
    tube(gear,[c-axle*.15,c+axle*.15],.232,"brass",16)
    # 返绳通回吊楼内的卷筒，吊重沿两道滑轮绳落到四角吊索。
    drum = V((-5.88,FRONT-.51,7.31))
    tube(frame,[drum-V((.37,0,0)),drum+V((.37,0,0))],.26,"oak",14)
    for side in (-1,1):
        x = drum.x+side*.40
        tube(gear,[(x-.047,drum.y,drum.z),(x+.047,drum.y,drum.z)],.36,"iron",16)
    wrap = [drum+V((-.35+.70*i/72,.282*math.cos(i*math.tau/12),.282*math.sin(i*math.tau/12))) for i in range(73)]
    tube(lines,wrap,.050,"rope",6)
    cord(lines,[drum+UP*.28,heel-UP*.34,c+UP*.17],.061,.57)
    block = V((TIP.x,TIP.y,4.30))
    for s in (-1,1):
        cord(lines,[c+axis*(s*.15),block+axis*(s*.10)],.069,.47)
    tube(gear,[block-axle*.12,block+axle*.12],.177,"brass",14)
    for side in (-1,1):
        tip = block+axle*(side*.17)
        beam(gear,tip+UP*.18,tip-UP*.17,.19,.105,"oak",.028)
    hook = [(TIP.x+dx,TIP.y,block.z+dz) for dx,dz in ((0,-.14),(0,-.31),(-.11,-.38),(-.10,-.55),(.02,-.62),(.15,-.55),(.17,-.46))]
    tube(gear,hook,.061,"iron",8)
    w,d,h = 2.05,1.67,1.92
    chest(load,CRATE,(w,d,h))
    for side in (-1,1):
        for fore in (-1,1):
            corner = V((CRATE[0]+side*w*.43,CRATE[1]+fore*d*.43,CRATE[2]+h))
            cord(lines,[block-UP*.56,corner+UP*.10,corner],.064,.49)
            rivet(gear,corner,UP,.078)
    return [frame,gear,lines,load]


def stores():
    cases,buckets = Part("交叉钉撑木箱"),Part("橡木桶与锻铁桶箍")
    for x,y,size,angle in ((6.64,12.89,(1.82,1.62,1.62),-.047),(8.45,13.21,(1.66,1.49,1.44),.035),
                           (7.14,14.66,(1.59,1.42,1.52),.08)):
        chest(cases,(x,y,FLOOR+.02),size,angle)
    chest(cases,(6.63,12.93,FLOOR+1.72),(1.35,1.30,1.21),.084)
    chest(cases,(TOWER_X-.45,FRONT+1.74,FLOOR+.02),(1.58,1.29,1.44),-.15)
    cask(buckets,10.40,12.93,FLOOR+.025,1.02)
    cask(buckets,10.43,14.85,FLOOR+.025,.89)
    return [cases,buckets]


def deck():
    quay,boards,piles,metal = [Part(n) for n in ("迎水驳岸与条石压顶","装卸台与挑栈木板","水下桩排与穿枋","系缆柱与栈桥销钉")]
    f = Face(0,QUAY,1,0)
    masonry(quay,f,LEFT+.05,EAST,-4.90,.22,(),.81,1.72,True)
    # 只有迎水驳岸和两侧短翼墙承托木平台。后方两栋建筑各自以石墙入水。
    for side,x,stop in ((-1,LEFT+.06,FRONT),(1,EAST,STORE_FRONT)):
        face = Face(x,QUAY if side==1 else stop,0,side)
        masonry(quay,face,0,stop-QUAY,-4.88,.22,(),.79,1.53,True)
    for i in range(13):
        a,b = LEFT+(EAST-LEFT)*i/13+.019,LEFT+(EAST-LEFT)*(i+1)/13-.018
        stone(quay,f,rect(a,b,.19,FLOOR+.016),"dressed" if i%4 else "grey",.167,.87,.062)
    for x,front in ((LEFT+.14,FRONT),(EAST-.16,STORE_FRONT)):
        for i in range(3):
            y = QUAY+.70+(front-QUAY-.70)*(i+.5)/3
            half = (front-QUAY-.70)/6-.014
            beam(quay,(x,y-half,.39),(x,y+half,.39),.56,.37,"grey",.061,end="stone")
    # 沿岸的平台横板在下方承梁处错缝；左侧栈桥纵板连续对准拱门。
    for x0,x1,back,n in ((LEFT+.38,JOINT,FRONT,8),(JOINT+.02,EAST-.40,STORE_FRONT,10)):
        step = (back-QUAY-.66)/n
        for row in range(n):
            y = QUAY+.66+(row+.5)*step
            seam = x0+(x1-x0)*(.57 if row%2 else .31)
            for a,b in ((x0,seam-.013),(seam+.013,x1)):
                beam(boards,(a,y,FLOOR-.080),(b,y,FLOOR-.080),step-.016,.158,"dock",.028)
        for x in (x0+.06,(x0+x1)*.5,x1-.07):
            beam(piles,(x,QUAY+.31,.07),(x,back,.07),.38,.55,"tar",.040)
    width = JETTY_HALF*2
    for i in range(8):
        x = JETTY_X-JETTY_HALF+(i+.5)*width/8
        beam(boards,(x,SEA_END,FLOOR-.20),(x,QUAY+.85,FLOOR-.082),width/8-.025,.24,"dock",.034)
        for y in (SEA_END+.51,QUAY-.71):
            rivet(metal,(x,y,FLOOR-.05 if y<5 else FLOOR+.04),UP,.045)
    for y in (SEA_END+.52,QUAY-.75):
        beam(piles,(JETTY_X-JETTY_HALF-.05,y,.026),(JETTY_X+JETTY_HALF+.05,y,.026),.54,.48,"tar",.056)
        for delta in (-1.90,0,1.90):
            x = JETTY_X+delta
            trunk(piles,(x,y,-5.26),(x+.03,y+.03,.16),.31,"wet","agedcut",rings=4,n=13,pile=True)
    for x in (JETTY_X-1.91,JETTY_X+1.91):
        beam(piles,(x,SEA_END+.55,-1.94),(x,QUAY-.73,-.32),.19,.24,"wet",.034)
        beam(piles,(x,SEA_END+.42,-.12),(x,QUAY-.54,-.12),.31,.44,"oak",.044)
    for x,y in ((JETTY_X-1.68,SEA_END+.78),(1.57,QUAY+.46),(10.35,QUAY+.52)):
        z = FLOOR-.13 if y<6 else FLOOR
        profile = [(0,.38),(.10,.39),(.15,.28),(.63,.25),(.68,.40),(.79,.42),(.86,.35)]
        tube(metal,[(x,y,z+h) for h,r in profile],[r for h,r in profile],"bronze",16)
    return [quay,boards,piles,metal]
