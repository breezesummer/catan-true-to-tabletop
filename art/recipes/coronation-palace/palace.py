"""冠冕议政宫的建筑装配。中央主殿在北，两翼廊通向南侧双塔，南阶通入庭院。"""
import random
from mathutils import Vector
from atelier import (Frame, Mason, UP, arch, arch_open, block, dentils, finial, hip, molding,
                     pairs, panel, pillar, quoin, sides, stack, strip, wall)
from ornament import heraldry

COURT=.47
TOWER_X,TOWER_Y=3.02,-3.00
HALL_Y,HALL_W,HALL_D=2.40,5.08,3.55
HALL_FRONT=HALL_Y-HALL_D/2


def court(m):
    m.name="礼仪前庭 · 逐块铺石"
    # 铺装止于两侧柱础、主殿墙脚及上一级踏步，四周没有外挑的承托盘。
    front,back=-3.69,.79
    block(m,0,(front+back)/2,4.61,back-front,.0,COURT-.042,"foot",.018)
    rng=random.Random(112319)
    row_depth=(back-front)/13
    for j in range(13):
        y=front+(j+.5)*row_depth
        x=-2.30
        while x<2.298:
            width=min(2.30-x,rng.uniform(.44,.69) if x>-2.29 or j%2==0 else rng.uniform(.21,.31))
            if .0<2.30-x-width<.16:
                width=2.30-x
            shade="carving" if rng.random()<.135 else "limestone"
            block(m,x+width/2,y,width-.009,row_depth-.009,COURT-.045,
                  COURT+rng.uniform(-.0025,.0025),shade,.0095)
            x+=width


def stairs(m,front,back,half,z0,z1,count,curve):
    # 前缘浅弧，每级有凸鼻和竖踢面。两座石阶均起自各自的实际地坪标高。
    depth=(back-front)/count
    for i in range(count):
        y=front+i*depth
        top=z0+(i+1)*(z1-z0)/count
        bottom=z0+i*(z1-z0)/count
        for j in range(12):
            a,b=(-1+j/6),(-1+(j+1)/6)
            def point(u,run,z):
                fade=max(0,1-(i+run/depth)/count)
                return Vector((u*half,y+run-curve*(1-u*u)*fade,z))
            pts=[point(a,0,top-.013),point(b,0,top-.013),
                 point(b,.013,top),point(a,.013,top)]
            m.face(pts,"carving",outward=Vector((0,-1,1)))
            m.face([point(a,.013,top),point(b,.013,top),point(b,depth,top),point(a,depth,top)],
                   "limestone",outward=UP)
            m.face([point(a,.005,bottom),point(b,.005,bottom),point(b,0,top-.013),point(a,0,top-.013)],
                   "limestone",outward=Vector((0,-1,0)))
        for s in (-1,1):
            m.face([point(s,.005,z0),point(s,depth,z0),point(s,depth,top),point(s,0,top-.013)],
                   "foot",outward=Vector((s,0,0)))
    for s in (-1,1):
        width=.235 if half>1.2 else .171
        x=s*(half+width*.48)
        ends=[(front-.028,z0+.225),(back+.035,z1+.215)]
        for offset,thick,stone in ((0,width,"ashlar"),(.015,width+.065,"carving")):
            low=z0 if offset==0 else None
            a,b=ends
            pts=[(x-thick/2,a[0],low if low is not None else a[1]),
                 (x-thick/2,b[0],low if low is not None else b[1]),
                 (x-thick/2,b[0],b[1]+offset*4),(x-thick/2,a[0],a[1]+offset*4)]
            m.extrude(pts,(thick,0,0),stone)
        for y,z in ends:
            block(m,x,y,width+.07,.19,z0,z+.066,"limestone",.018)
            block(m,x,y,width+.11,.23,z+.053,z+.106,"carving",.015)


def bell_tower(m,sign):
    x,y=sign*TOWER_X,TOWER_Y
    w,d=1.46,1.58
    label="西" if sign<0 else "东"
    m.name=label+"南门塔 · 两层成对尖窗"
    stack(m,x,y,w,d,[(0,.092,"foot"),(.092,.092,"foot"),(.125,.058,"foot"),(.58,.058,"foot"),
                     (.62,.074,"carving"),(.666,.074,"limestone"),(.698,.013,"limestone")])
    for f,length in sides(x,y,w,d):
        pairs(m,f,length/2,.67,3.53,2,[0],.124,1.00,1.42,.235)
        for z in (.74,2.06):
            m.band(f,-length/2,length/2,z-.025,z+.011,-.016,.042,"carving")
        dentils(m,f,length/2-.04,3.468,.153)
    for z,scale in ((.745,.75),(2.08,1.03),(3.57,1.18)):
        molding(m,x,y,w,d,z,scale)
    for sx in (-1,1):
        for sy in (-1,1):
            quoin(m,x+sx*(w/2-.038),y+sy*(d/2-.038),0,3.63,.245,
                  (.15,.72,1.99,2.135,3.37,3.57))
    m.name=label+"南门塔 · 四坡尖顶"
    hip(m,x,y,1.51,1.63,3.55,4.96,.138,.144)
    finial(m,x,y,4.925,.95)


def dormer(m,f,center):
    z,r=2.222,.198
    # 老虎窗的山花朝向檐口，后部直接插入主坡的板岩，垂直切边覆铅皮。
    o=f.at(center,0,.006)
    g=Frame(o.x,o.y,f.n.x,f.n.y)
    roof=lambda u:z+.36+.225*(1-abs(u)/r)
    wall(m,g,-r,r,z,roof,[(None,[(0,.093,z+.046,z+.288,.147,"lancet")])],breaks=(0,))
    for s in (-1,1):
        a=g.at(s*r,z+.316,.002)
        b=g.at(s*r,z+.316,-.49)
        k=g.at(0,z+.557,.002)
        l=g.at(0,z+.557,-.49)
        m.face([a,b,g.at(s*r,z,-.49),g.at(s*r,z)],"limestone",outward=s*g.t)
        m.face([a,k,l,b],"slate",[(.12,.12),(.12,.21),(.26,.21),(.26,.12)],UP)
    strip(m,g,[(-r-.026,z+.330),(0,z+.582),(r+.026,z+.330)],
          [(0,.016),(.014,.031),(.028,.034),(.039,.002),(.040,-.044)],"carving")
    m.rod(g.at(0,z+.601,.0),g.at(0,z+.680,.0),.026,"carving",8,.008)


def gallery(m,sign):
    x=sign*TOWER_X
    front,back=-2.32,2.57
    y=(front+back)/2
    width=1.41
    label="西" if sign<0 else "东"
    m.name=label+"翼 · 穿透式四跨石廊"
    stack(m,x,y,width,back-front,[(0,.07,"foot"),(.12,.07,"foot"),(.15,.033,"foot"),
          (COURT-.067,.033,"limestone"),(COURT-.035,.073,"carving"),(COURT,.070,"limestone")])
    for side in (-1,1):
        f=Frame(x+side*width/2,y,side,0)
        half=(back-front)*.5
        spacing=(back-front)/4
        radius=(spacing-.250)*.5
        spring=1.385
        for i in range(4):
            u=-half+(i+.5)*spacing
            arch_open(m,f,u,radius,spring,.554,COURT,2.14,.176)
            for a,b in ((u-spacing/2,u-radius),(u+radius,u+spacing/2)):
                panel(m,f,[(a,spring),(b,spring),(b,2.14),(a,2.14)],"ashlar")
                panel(m,f,[(a,spring),(b,spring),(b,2.14),(a,2.14)],"shadow",-.176,-f.n)
        for i in range(5):
            p=f.at(-half+i*spacing,0,-.079)
            pillar(m,p.x,p.y,COURT,spring)
        dentils(m,f,half-.12,2.133,.205)
    # 石券拱廊的交叉拱肋和窄拱顶从庭内可以看见，拱腹与外墙均保持通透。
    for i in range(4):
        y0=front+i*(back-front)/4
        y1=front+(i+1)*(back-front)/4
        for s in (-1,1):
            points=[Vector((x+s*(t-.5)*(width-.2),y0+t*(y1-y0),1.482+.55*4*t*(1-t)))
                    for t in (j/12 for j in range(13))]
            for a,b in zip(points,points[1:]):
                m.rod(a,b,.037,"limestone",6)
    block(m,x,y,width-.14,back-front,2.021,2.15,"shadow")
    molding(m,x,y,width,back-front,2.205,1.15)
    m.name=label+"翼 · 长脊与六座老虎窗"
    hip(m,x,y,width+.095,back-front+.12,2.21,3.32,.052,back-front-.56)
    m.rod((x,front+.27,3.326),(x,back-.27,3.326),.044,"lead",8)
    for side in (-1,1):
        f=Frame(x+side*(width+.09)/2,y,side,0)
        for t in (-1,0,1):
            dormer(m,f,t*(back-front)*.268)
    m.name=label+"翼 · 尽端扶壁"
    end=Frame(x,back,0,1)
    wall(m,end,-width/2,width/2,COURT,lambda u:2.18,
         [(None,[(0,.16,.73,1.44,.29,"lancet")])])
    for s in (-1,1):
        quoin(m,x+s*(width*.5-.04),back-.04,0,3.07,.230,(.19,.55,1.68,2.17,2.92))


def main_hall(m):
    x,y,w,d=0,HALL_Y,HALL_W,HALL_D
    m.name="主殿 · 三层灰岩立面"
    stack(m,x,y,w,d,[(0,.067,"foot"),(.135,.067,"foot"),(.16,.040,"foot"),(.837,.040,"foot"),
          (.865,.078,"limestone"),(.924,.078,"carving"),(.953,0,"ashlar")])
    for i,(f,length) in enumerate(sides(x,y,w,d)):
        if i==0:
            for s in (-1,1):
                center=s*1.78
                origin=f.at(center,0)
                sub=Frame(origin.x,origin.y,0,-1)
                half=w/2-1.78
                pairs(m,sub,half,.94,4.79,3,[-.335,.318],.09,1.055,1.53,.191)
                for z in (2.16,3.45):
                    m.band(sub,-half,half,z-.052,z+.016,-.01,.062,"limestone")
                # 门洞通向殿内，两侧墙肩延伸到门楼山花背后，屋面交线下仍有实墙。
                panel(m,f,[(s*.56,.94),(s*1.02,.94),(s*1.02,4.79),(s*.56,4.79)],"ashlar")
            panel(m,f,[(-.56,2.94),(.56,2.94),(.56,4.79),(-.56,4.79)],"ashlar")
        else:
            group=(-1.67,-.56,.56,1.67) if i==2 else (-1.05,0,1.05)
            pairs(m,f,length/2,.94,4.79,3,group,.100,1.064,1.55,.204)
            for z in (2.175,3.46):
                m.band(f,-length/2,length/2,z-.045,z+.013,-.02,.055,"limestone")
        dentils(m,f,length/2-.12,4.73,.187)
    molding(m,x,y,w,d,4.832,1.52)
    for sx in (-1,1):
        for sy in (-1,1):
            quoin(m,sx*(w/2-.035),y+sy*(d/2-.035),0,4.89,.265,
                  (.23,.94,2.16,3.45,4.63,4.84))
    m.name="主殿 · 四坡板岩与采光楼"
    hip(m,0,y,w+.165,d+.165,4.83,5.96,1.64,1.23)
    lw,ld=1.50,1.14
    molding(m,0,y,lw,ld,5.985,.8)
    for index,(f,length) in enumerate(sides(0,y,lw,ld)):
        centers=(-.435,-.145,.145,.435) if index%2==0 else (-.271,0,.271)
        specs=[(u,.088,6.015,6.195,.115,"lancet") for u in centers]
        wall(m,f,-length/2,length/2,5.97,lambda u:6.385,[(None,specs)])
    molding(m,0,y,lw,ld,6.415,.74)
    for sx in (-1,1):
        for sy in (-1,1):
            quoin(m,sx*(lw/2-.022),y+sy*(ld/2-.022),5.927,6.40,.152,(6.012,6.385))
    hip(m,0,y,lw+.046,ld+.048,6.423,6.674,.088,.081)
    finial(m,0,y,6.65,.53)


def portal(m):
    y=HALL_FRONT-.285
    f=Frame(0,y,0,-1)
    m.name="正门 · 双扇栎木与束柱门套"
    half=.94
    eave=4.39
    peak=5.29
    top=lambda u:eave+.049+(1-abs(u)/half)*(peak-eave)
    landing=1.004
    wall(m,f,-half,half,landing,top,[(None,[(0,.544,landing,2.13,.715,"gate")])],breaks=(0,))
    for s in (-1,1):
        # 深门楼两翼侧墙和山墙脊帽收回主殿前坡。
        a=f.at(s*half,landing)
        b=f.at(s*half,eave+.002)
        m.face([a,b,b-f.n*.55,a-f.n*.55],"ashlar",outward=s*f.t)
        for j in range(2):
            p=f.at(s*(.715+j*.083),0,.074)
            pillar(m,p.x,p.y,landing,2.12)
        quoin(m,s*(half+.032),y+.01,.37,4.64,.198,(1.07,2.17,3.13,4.48))
        # 门楼脊在主殿前坡上收头。两片板岩沿真实交线相接，谷底覆窄铅皮。
        ridge=f.at(0,peak+.06,.091)
        edge=f.at(s*(half+.07),eave-.028,.091)
        main_eave_y=HALL_Y-(HALL_D+.165)/2
        main_pitch=(5.96-4.83)/(HALL_Y-1.23/2-main_eave_y)
        join_z=4.83+(HALL_FRONT-main_eave_y)*main_pitch
        t=(join_z-edge.z)/(ridge.z-edge.z)
        joint=edge.lerp(ridge,t)
        front=[edge,joint,ridge]
        rear=[Vector((p.x,max(HALL_FRONT,main_eave_y+(p.z-4.83)/main_pitch)+.020,p.z)) for p in front]
        for a,b,c,d in zip(front,front[1:],rear[1:],rear):
            points=[a,b,c,d]
            m.face(points,"slate",[(p.y/3.32,(p-edge).dot((ridge-edge).normalized())/3.65) for p in points],UP)
        m.rod(rear[1]+Vector((0,-.028,.012)),rear[2]+Vector((0,-.028,.012)),.019,"lead",8)
    for d,u in ((.005,.035),(.043,.056)):
        strip(m,f,[(-half-.058,eave-.047),(0,peak+.058),(half+.058,eave-.047)],
              [(0,d),(.016,d+.018),(u,d+.027),(u+.018,-.015)],"carving")
    finial(m,0,y+.003,peak+.08,.36)
    # 成对小尖拱和中央门梃延伸至木扇前，中横铰与铜环仍可辨认。
    for s in (-1,1):
        arc=arch(s*.245,.237,2.041,.447,8)
        p=[(arc[0][0],landing)]+arc+[(arc[-1][0],landing)]
        strip(m,f,p,[(0,-.082),(.02,-.044),(.036,-.052),(.046,-.091)],"carving")
    m.band(f,-.039,.039,landing,2.062,-.16,-.043,"limestone")
    m.name="门楣 · 王冠盾徽与月桂"
    heraldry(m,f)
    m.name="主殿 · 内阶和平台"
    block(m,0,y-.105,2.036,.306,COURT,landing,"ashlar")
    molding(m,0,y-.105,2.028,.302,landing,.53)
    stairs(m,y-.260-.669,y-.260,.889,COURT,landing,7,.012)


def assemble():
    m=Mason()
    court(m)
    m.name="南入口 · 弧形阔阶与石栏"
    stairs(m,-4.495,-3.684,1.495,0,COURT,8,.083)
    for s in (-1,1):
        bell_tower(m,s)
        gallery(m,s)
    main_hall(m)
    portal(m)
    return m
