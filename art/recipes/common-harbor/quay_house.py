"""石基吊楼、正交屋脊的仓栈与逐片搭接的板岩屋面。"""
import math
from quay_craft import (Face, Opening, Part, R, UP, V, beam, extrude, masonry, rect,
                        stone, surround, tube, window)

TOWER_X, HALF = -7.65,3.95
FRONT, REAR = 15.05,24.90
LEFT, JOINT, EAST = TOWER_X-HALF,TOWER_X+HALF,11.65
STORE_FRONT, STORE_BACK = 16.18,24.48
HALL_EAVE, HALL_PEAK = 6.40,10.81
HALL_RIDGE_Y = (STORE_FRONT+STORE_BACK)*.5
HALL_ROOF_RUN = (STORE_BACK-STORE_FRONT)*.5+.44
HALL_ROOF_EAVE = HALL_EAVE-.10
TOWER_EAVE, TOWER_PEAK = 10.86,15.62
FLOOR = .58
WATER = -3.50
# 后墙外皮压住棋盘海岸边。屋檐允许出挑，墙体和工作栈台都向海展开。
BOARD_SHIFT = 9.90 - REAR
SEA_END = 3.00
DOOR_X = 4.33


def vertical_planks(p,f,l,r,b,top,d,tile="wall",spacing=.50):
    count = max(2,round((r-l)/spacing))
    for i in range(count):
        a = l+(r-l)*i/count+.009
        c = l+(r-l)*(i+1)/count-.009
        za,zc = top(a),top(c)
        if min(za,zc)<=b: continue
        u,v = R.uniform(.04,.24),R.uniform(.05,.59)
        drift = R.uniform(-.022,.022)
        uv = [(u,v),(u,v+.085),(u+.67*(zc-b)/max(za-b,zc-b),v+.085),(u+.67*(za-b)/max(za-b,zc-b),v)]
        f.plate(p,[(a,b),(c,b),(c,zc),(a,za)],d+drift,.26,tile,uv)


def doors(p,f,hole,d,inner=False):
    x,r,z = hole.x,hole.r,hole.bottom
    vertical_planks(p,f,x-r,x+r,z,hole.head,d,"door" if not inner else "tar",.35)
    for side in (-1,1):
        hinge = x+side*r*.945
        for level in (z+.60,hole.spring-.14):
            tip = x+side*.26*r
            width = .105
            strap = [(hinge,level-width),(tip,level-.054),(tip-side*.15,level),
                     (tip,level+.054),(hinge,level+width)]
            f.plate(p,strap,d+.083,.085,"iron")
            for t in (.17,.58,.86):
                c = f.at(hinge+(tip-hinge)*t,level,d+.099)
                tube(p,[c,c+f.n*.038],(.055,.043),"brass",7)
        h = hole.bottom+(hole.spring-hole.bottom)*.58
        c = f.at(x+side*r*.30,h,d+.11)
        tube(p,[c,c+f.n*.125],(.132,.073),"iron",10)
        c += f.n*.12-UP*.13
        chain = [c+f.t*.135*math.cos(i*math.tau/16)+UP*.166*math.sin(i*math.tau/16) for i in range(16)]
        tube(p,chain,.034,"brass",6,True)


def roof(p,trim,origin,along,start,end,half,eave,ridge,cap_both=True):
    along = V(along)
    cross = V((-along.y,along.x,0))
    origin = V(origin)
    slope = (ridge-eave)/half
    def at(u,h,z):
        return origin+along*u+cross*h+UP*z
    for side in (-1,1):
        normal = (UP+cross*side*slope).normalized()
        def point(u,r,d=0):
            return at(u,side*r,ridge-slope*r)+normal*d
        extrude(trim,[point(start,0),point(end,0),point(end,half),point(start,half)],-.18*normal,"tar")
        beam(trim,point(start,half,-.09),point(end,half,-.09),.26,.39,"oak",.043)
        for edge in ((start,end) if cap_both else (end,)):
            beam(trim,point(edge,0,.025),point(edge,half,.025),.24,.37,"dock",.047,along)
        # 每片下摆略翘，压住下一皮上端。各皮错开半块，檐口保留劈口和两角缺损。
        rows = 8
        columns = max(3,round((end-start)/.77))
        step = (end-start)/columns
        for row in range(rows):
            lo = half*(1-(row+1)/rows)
            hi = half*(1-row/rows)
            cuts = [start]+[start+(i+(.5 if row%2 else 1))*step for i in range(columns)
                            if start+.07<start+(i+(.5 if row%2 else 1))*step<end-.07]+[end]
            for x0,x1 in zip(cuts,cuts[1:]):
                a,b = x0+.012,x1-.012
                high = min(half+.076,hi+R.uniform(.024,.081))
                low = max(0,lo-.115)
                left_chip = R.uniform(.046,.14)
                right_chip = R.uniform(.04,.115)
                outline = [(a,low),(b,low),(b,high-.069),(b-right_chip,high),
                           (a+left_chip,high+R.uniform(-.027,.019)),(a,high-.070)]
                ends = R.uniform(.088,.147)
                tilt = R.uniform(-.012,.017)
                pts = [point(x,r,.044+ends*(r-low)/(high-low)+tilt*(x-a)/(b-a)) for x,r in outline]
                u,v = R.uniform(.06,.55),R.uniform(.06,.51)
                coords = [(u+.36*(x-a)/(b-a),v+.39*(r-low)/(high-low)) for x,r in outline]
                extrude(p,pts,-normal*.067,R.choices(("slate","blue","heather"),(6,3,1))[0],coords)
    # 半圆筒形石脊瓦中空包住两坡，在坡顶留搭扣接缝。
    cells = max(2,round((end-start)/1.0))
    for i in range(cells):
        a,b = start+(end-start)*i/cells+.009,start+(end-start)*(i+1)/cells-.009
        for band,lo,hi,radius in ((False,a,b,.233),(True,a,a+.115,.256)):
            angles = [math.radians(-68+136*j/8) for j in range(9)]
            section = [(radius*math.sin(t),ridge+.161+radius*(math.cos(t)-.53)) for t in angles]
            section += [((radius-.062)*math.sin(t),ridge+.151+(radius-.062)*(math.cos(t)-.53)) for t in reversed(angles)]
            extrude(p,[at(lo,y,z) for y,z in section],along*(hi-lo),"blue" if band else "slate")


def buildings():
    stonework,carving,joinery,shingles,trim,hardware,inner = [Part(n) for n in (
        "两座独立入水石基与砌墙","门窗拱券与石槛","吊楼榫架与山墙板","手劈板岩瓦与合脊",
        "檐檩与椽头","双扇仓门与窗格","门洞内壁与收货间")]
    front = Face(0,FRONT,1,0)
    west = Face(LEFT,REAR,0,-1)
    back = Face(JOINT,REAR,-1,0)
    east = Face(JOINT,FRONT,0,1)
    opening = Opening(TOWER_X,1.66,FLOOR,3.30,1.69)
    store = Face(0,STORE_FRONT,1,0)
    end = Face(EAST,STORE_FRONT,0,1)
    rear = Face(EAST,STORE_BACK,-1,0)
    store_w,store_d = EAST-JOINT,STORE_BACK-STORE_FRONT
    doorspec = Opening(DOOR_X,1.94,FLOOR,3.92,.72,False)
    win1 = Opening(-.14,.56,2.74,3.88,.78,n=5)
    win2 = Opening(9.15,.56,2.74,3.88,.78,n=5)
    endwin = Opening(store_d*.5,.72,2.22,3.89,.95,n=6)
    westwin = Opening((REAR-FRONT)*.42,.52,2.34,3.72,.76,n=5)
    rearwin = Opening(store_w*.36,.62,2.45,3.93,.84,n=5)
    sections = [(front,LEFT,JOINT,(opening,),6.14),(west,0,REAR-FRONT,(westwin,),6.14),
                (back,0,2*HALF,(),6.14),(store,JOINT,EAST,(doorspec,win1,win2),6.21),
                (end,0,store_d,(endwin,),6.21),(rear,0,store_w,(rearwin,),6.21)]
    for f,l,r,holes,top in sections:
        masonry(stonework,f,l,r,-4.87,FLOOR,(),.87,1.88,True)
        masonry(stonework,f,l,r,FLOOR,top,holes,.70,1.37)
    for a,b in ((0,STORE_FRONT-FRONT),(STORE_BACK-FRONT,REAR-FRONT)):
        masonry(stonework,east,a,b,-4.87,6.14,(),.78,1.42,True)
    for f,hole in ((store,win1),(store,win2),(end,endwin),(west,westwin),(rear,rearwin)):
        window(hardware,f,hole)
    surround(carving,front,opening,.43,d=.22)
    surround(carving,store,doorspec,.36,d=.16)
    doors(hardware,store,doorspec,-.20)
    # 石拱敞开至收货间，两侧券脚与内拱圈接到同一暗部；内门和箱件可由正面窥见。
    path = [(opening.x-opening.r,FLOOR)]+opening.crown+[(opening.x+opening.r,FLOOR)]
    for a,b in zip(path,path[1:]):
        inner.face(inner.verts([front.at(*a,-.38),front.at(*a,-3.13),front.at(*b,-3.13),front.at(*b,-.38)]),
                   "shadow",[(.09,.08),(.93,.08),(.93,.42),(.09,.42)])
    front.plate(inner,opening.outline,-3.13,.16,"shadow")
    inside = Opening(TOWER_X+.18,1.12,FLOOR,2.86,.66,False)
    doors(inner,front,inside,-3.06,True)
    for i in range(4):
        y = FRONT-.15+i*.83
        for j in range(2):
            a = TOWER_X-1.68+j*1.69
            extrude(inner,[(a,y,FLOOR),(a+1.66,y,FLOOR),(a+1.66,y+.80,FLOOR),(a,y+.80,FLOOR)],(0,0,-.24),"grey")
    stone(carving,store,rect(DOOR_X-2.13,DOOR_X+2.13,FLOOR-.24,FLOOR+.025),"dressed",.44,.9)
    # 加厚转角石在平刨石带和毛石之间交错咬合。
    for f,u,sgn in ((front,LEFT,1),(front,JOINT,-1),(end,0,1),(end,store_d,-1),(store,EAST,-1),(west,REAR-FRONT,-1)):
        for i in range(7):
            z = FLOOR+i*.795
            length = .85 if i%2 else 1.15
            a,b = sorted((u,u+sgn*length))
            stone(carving,f,rect(a,b,z+.012,z+.77),"stone" if i%3 else "dressed",.092,.46)
    # 木质吊楼在石门洞之上向外挑出，两层水平系梁夹住四面木框与牛腿。
    tf = Face(0,FRONT-.19,1,0)
    tb = Face(0,REAR+.085,-1,0)
    tw = Face(LEFT-.12,REAR,0,-1)
    te = Face(JOINT+.15,FRONT,0,1)
    for f,l,r,peak in ((tf,LEFT-.10,JOINT+.10,True),(tb,-JOINT-.10,-LEFT+.10,True),
                       (tw,0,REAR-FRONT,False),(te,0,REAR-FRONT,False)):
        mid = (l+r)*.5
        def top(u):
            return TOWER_PEAK-.17-(TOWER_PEAK-TOWER_EAVE)*abs((u-mid)/((r-l)*.5)) if peak else TOWER_EAVE
        vertical_planks(joinery,f,l,r,6.13,top,0)
        for z,w in ((6.28,.39),(6.66,.34),(TOWER_EAVE-.14,.37)):
            f.member(joinery,(l-.08,z),(r+.08,z),w,.40,"oak",.13)
        bays = [l+.19,mid-(1.23 if peak else 1.85),mid+(1.23 if peak else 1.85),r-.19]
        for u in bays:
            f.member(joinery,(u,6.43),(u,top(u)-.06),.322,.359,"oak",.14)
        f.member(joinery,(bays[0]+.16,6.84),(bays[1]-.14,9.42),.27,.23,"oak",.093)
        f.member(joinery,(bays[3]-.16,6.84),(bays[2]+.14,9.42),.27,.23,"oak",.093)
        for u in (bays[0],bays[1],bays[2],bays[3]):
            beam(joinery,f.at(u,5.70,-.05),f.at(u,6.31,.30),.29,.37,"oak",.049)
            for z in (6.58,10.69):
                c = f.at(u,z,.329)
                tube(hardware,[c,c+f.n*.043],(.073,.059),"agedcut",8)
        if peak:
            for s in (-1,1):
                f.member(trim,(mid,TOWER_PEAK-.23),(mid+s*((r-l)*.5+.12),TOWER_EAVE-.13),.25,.33,"dock",.13)
    tf.plate(joinery,rect(TOWER_X-1.0,TOWER_X+1.0,6.70,9.83),.08,.11,"tar")
    vertical_planks(joinery,tf,TOWER_X-.77,TOWER_X+.77,6.81,lambda u:9.42,.15,"door",.32)
    tf.member(joinery,(TOWER_X-.88,9.51),(TOWER_X+.88,9.51),.22,.20,"oak",.18)
    for f in (store,rear):
        l,r = (JOINT,EAST) if f is store else (0,store_w)
        f.member(trim,(l,6.07),(r,6.07),.24,.34,"oak",.09)
        for i in range(10):
            u = l+(r-l)*(i+.45)/10
            beam(trim,f.at(u,5.80,-.03),f.at(u,6.35,.37),.23,.33,"oak",.037)
    mid = store_d/2
    def end_top(u):
        return HALL_PEAK-.12-(HALL_PEAK-HALL_EAVE)*abs((u-mid)/mid)
    vertical_planks(joinery,end,0,store_d,6.15,end_top,.04)
    for u in (mid-1.32,mid,mid+1.32):
        end.member(joinery,(u,6.23),(u,end_top(u)),.21,.25,"oak",.14)
    end.member(trim,(0,6.27),(store_d,6.27),.30,.29,"oak",.17)
    roof(shingles,trim,(TOWER_X,0,0),(0,1,0),FRONT-.59,REAR+.42,HALF+.49,TOWER_EAVE-.10,TOWER_PEAK)
    roof(shingles,trim,(0,HALL_RIDGE_Y,0),(1,0,0),JOINT-.39,EAST+.40,
         HALL_ROOF_RUN,HALL_ROOF_EAVE,HALL_PEAK,False)
    return [stonework,carving,joinery,shingles,trim,hardware,inner]
