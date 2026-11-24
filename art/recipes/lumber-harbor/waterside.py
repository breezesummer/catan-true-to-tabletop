"""石驳岸、入水木栈、吊运索具和备货。水面与岸面由棋盘直接提供。"""
import math
from joinery import Part, RNG, V, UP, beam, cord, grain, trunk, tube
from masonry import Face, lay_wall, skin
from warehouse import BED, FLOOR, STORE_FRONT, STORE_RIGHT, TOWER_FRONT, TOWER_X

SEA_END=-7.
QUAY_FRONT=-3.69
QUAY_LEFT=-12.28
QUAY_RIGHT=12.42
QUAY_BACK=2.12
PIER_LEFT=-10.49
PIER_RIGHT=-5.30
PILE_X=(PIER_LEFT+.51,PIER_RIGHT-.51)
PILE_Y=(SEA_END+.64,QUAY_FRONT-.17)
PIVOT=V((-7.31,.69,8.77))
TIP=V((-2.45,-1.88,8.77))
LOAD=TIP+(TIP-PIVOT).normalized()*.52
LOAD.z=1.27


def paving(p,x0,x1,y0,y1,z,tile):
    # 铺面有垂直石边和轻微倒角，缝底交给下层构造。
    d=.045
    corners=[(x0+d,y0),(x1-d*.83,y0),(x1,y0+d),(x1,y1-d),(x1-d,y1),(x0+d,y1),(x0,y1-d),(x0,y0+d)]
    cx,cy=(x0+x1)/2,(y0+y1)/2
    ring=p.verts([(x,y,z-.064) for x,y in corners])
    bot=p.verts([(x,y,z-.33) for x,y in corners])
    cap=p.verts([(x+(cx-x)*.041,y+(cy-y)*.07,z) for x,y in corners])
    tex=grain(x1-x0,y1-y0)
    uv=[tex((x-x0)/(x1-x0),(y-y0)/(y1-y0)) for x,y in corners]
    p.face(cap,tile,uv)
    for i in range(8):
        j=(i+1)%8
        p.face([ring[i],ring[j],cap[j],cap[i]],tile,[uv[i],uv[j],uv[j],uv[i]])
        p.face([bot[i],bot[j],ring[j],ring[i]],tile,[(.05,.21),(.95,.21),(.95,.46),(.05,.46)])


def bollard(p,x,y):
    profile=((FLOOR,.48),(FLOOR+.08,.51),(FLOOR+.19,.40),(FLOOR+.37,.29),
             (FLOOR+.86,.31),(FLOOR+.96,.43),(FLOOR+1.03,.48),(FLOOR+1.14,.46),(FLOOR+1.20,.36))
    for i,((a,r),(b,s)) in enumerate(zip(profile,profile[1:])):
        tube(p,[(x,y,a),(x,y,b)],(r,s),"iron" if i not in (1,6) else "brass",18)


def landing():
    masonry,wood,iron=map(Part,("临水驳岸与卸货铺面","纵梁、入水桩与短挑栈桥","系缆柱及甲板铁钉"))
    lay_wall(masonry,Face(0,QUAY_FRONT,(1,0)),QUAY_LEFT,QUAY_RIGHT,BED,FLOOR-.26,step=.73,block=1.58,quay=True)
    for x,s in ((QUAY_LEFT,-1),(QUAY_RIGHT,1)):
        a,b=sorted((s*QUAY_FRONT,s*QUAY_BACK))
        lay_wall(masonry,Face(x,0,(0,s)),a,b,BED,FLOOR-.26,step=.73,block=1.58,quay=True)
    # 石驳岸仅承托装卸走道。两座建筑各有自己的入水墙脚。
    skin(masonry,[(x,y,FLOOR-.103) for x,y in ((QUAY_LEFT,QUAY_FRONT),(QUAY_RIGHT,QUAY_FRONT),
         (QUAY_RIGHT,QUAY_BACK),(QUAY_LEFT,QUAY_BACK))],(0,0,-.24),"mortar",[(0,0),(1,0),(1,1),(0,1)])
    for row in range(5):
        y0=QUAY_FRONT+(QUAY_BACK-QUAY_FRONT)*row/5
        y1=QUAY_FRONT+(QUAY_BACK-QUAY_FRONT)*(row+1)/5
        count=13 if row==0 else 10
        q=[QUAY_LEFT]+[QUAY_LEFT+(QUAY_RIGHT-QUAY_LEFT)*(i-row%2*.5+RNG.uniform(-.10,.10))/count
                       for i in range(1,count+row%2)]+[QUAY_RIGHT]
        for a,b in zip(q,q[1:]):
            t=RNG.choice(("dressed","ashlar","buffStone","dressed") if row==0 else ("ashlar","buffStone","greyStone"))
            paving(masonry,a+.015,b-.015,y0+.015-(.11 if row==0 else 0),y1-.014,
                   FLOOR+RNG.uniform(-.020,.015),t)
    for x in PILE_X:
        beam(wood,(x,SEA_END+.04,FLOOR-.69),(x,QUAY_FRONT+.31,FLOOR-.69),.63,.72,"tar")
        for y in PILE_Y:
            trunk(wood,(x,y,-5.24),(x+.04,y,FLOOR-.43),.47,"dock","agedcut",5,23,pile=True)
            beam(wood,(x,y,-1.83),(x+(.72 if x<sum(PILE_X)/2 else -.72),y,-.59),.28,.34,"oak",.045)
    for y in PILE_Y:
        beam(wood,(PIER_LEFT+.09,y,FLOOR-.60),(PIER_RIGHT-.09,y,FLOOR-.60),.52,.59,"dock",.061)
    for i in range(8):
        y=SEA_END+(QUAY_FRONT+.22-SEA_END)*(i+.5)/8
        beam(wood,(PIER_LEFT,y,FLOOR-.15),(PIER_RIGHT,y,FLOOR-.15),
             (QUAY_FRONT+.22-SEA_END)/8-.025,.31,RNG.choice(("dock","dock","oak")),.034)
        for x in PILE_X:
            tube(iron,[(x,y,FLOOR+.001),(x,y,FLOOR+.021)],.053,"iron",7)
    for x in (PIER_LEFT+.13,PIER_RIGHT-.13):
        beam(wood,(x,SEA_END,FLOOR+.023),(x,QUAY_FRONT+.17,FLOOR+.023),.20,.15,"trim",.032)
    for x,y in ((-10.01,-3.51),(1.06,-3.13),(11.61,-3.02)):
        bollard(iron,x,y)
    return masonry,wood,iron


def bound_logs(wood,ropes,start,axis,length,r,stations):
    """三根带皮货木按两下、一上的公切线收束，侧面保持树皮，端面显露切口。"""
    ax=V(axis).normalized()
    cross=ax.cross(UP).normalized()
    start=V(start)
    # 断面下层中心与上层中心构成等边三角形，绳路依次绕过最外层圆弧。
    centers=[(-r*.99,0),(r*.99,0),(0,1.71*r)]
    for i,(x,z) in enumerate(centers):
        root=start+cross*x+UP*z
        a=root+ax*((.05,-.09,.12)[i]*r)
        b=root+ax*(length+(.09,.02,-.08)[i]*r)
        trunk(wood,a,b,r*(1,.98,.97)[i],rings=6,n=34)
    for t in stations:
        section=start+ax*t
        path=[]
        for index,a,b in ((1,-90,30),(2,30,150),(0,150,270)):
            x,z=centers[index]
            for k in range(15):
                f=math.radians(a+(b-a)*k/14)
                radial=r*1.035
                path.append(section+cross*(x+radial*math.cos(f))+UP*(z+radial*math.sin(f)))
        cord(ropes,path,r*.135,r*.86,closed=True)
    return ax,cross


def goods():
    cargo,rack,rope=map(Part,("带皮捆木、锯口与髓心","垫木、止滚楔与分层板材","货捆扎索"))
    start=V((8.37,-2.93,1.16))
    direction=V((.47,1,0)).normalized()
    cross=direction.cross(UP)
    for t in (.57,3.05):
        c=start+direction*t
        beam(rack,(c-cross*1.82).xy.to_3d() + UP*.31,(c+cross*1.82).xy.to_3d()+UP*.31,.53,.38,"tar",.055)
        for s in (-1,1):
            f=c+cross*s*1.50
            pts=[f-cross*s*.29+UP*(.49-c.z),f+cross*s*.20+UP*(.49-c.z),f+cross*s*.11+UP*(.94-c.z)]
            skin(rack,[v-direction*.22 for v in pts],direction*.45,"trim",[(.05,.22),(.90,.22),(.77,.54)])
    bound_logs(cargo,rope,start,direction,3.98,.70,(.68,3.07))
    # 锯木棚前备发的板材层间用隔条通风。宽面横向可见，断头略有错落。
    cx,cy=4.40,-1.91
    for tier in range(6):
        z=.44+tier*.242
        for x in (cx-1.18,cx+1.18):
            if tier%2==0:
                beam(rack,(x,cy-.82,z-.14),(x,cy+.82,z-.14),.21,.13,"tar",.016)
        for j in range(4):
            y=cy+(j-1.5)*.417+tier*.016
            left=cx-1.65+RNG.uniform(-.09,.09)
            right=cx+1.65+RNG.uniform(-.09,.09)
            beam(rack,(left,y,z),(right,y,z),.405,.181,RNG.choice(("dock","trim","door")),.022)
    return cargo,rack,rope


def hoist():
    crane,hardware,rope,logs=map(Part,("悬臂吊架、斜撑与轴座","滑轮、抱箍和吊钩","绞索、滑车与分腿吊索","凌空装卸的三根短木"))
    d=(TIP-PIVOT).normalized()
    side=d.cross(UP).normalized()
    # 枢轴托在吊楼横枋上，斜撑将吊重传回下层石承台。
    beam(crane,PIVOT-d*.48+UP*(-3.64),PIVOT-d*.48+UP*.39,.48,.60,"oak",.062)
    beam(crane,PIVOT-d*.72,TIP+d*.28,.55,.60,"trim",.073)
    beam(crane,PIVOT-d*.37-UP*3.02,TIP-d*.88-UP*.32,.40,.40,"oak",.058,up=side)
    beam(crane,PIVOT-d*.64-UP*3.05,PIVOT+d*.44-UP*3.05,.62,.54,"dressed",.073,end="dressed")
    for k in (.45,(TIP-PIVOT).length-.39):
        center=PIVOT+d*k
        for s in (-1,1):
            beam(hardware,center+side*(s*.287)-UP*.31,center+side*(s*.287)+UP*.33,
                 .19,.045,"iron",.017,up=side)
            tube(hardware,[center+side*s*.30,center+side*s*.341],.070,"brass",10)
        beam(hardware,center-side*.31+UP*.305,center+side*.31+UP*.305,.19,.062,"iron",.015)
    axle=TIP+d*.20+UP*.05
    for s in (-1,1):
        c=axle+side*s*.225
        beam(hardware,c-d*.36,c+d*.17,.42,.086,"iron",.050,up=side)
        tube(hardware,[c-side*.085,c+side*.08],.095,"brass",12)
    tube(hardware,[axle+side*t for t in (-.16,-.11,-.07,.07,.11,.16)],(.32,.33,.27,.27,.33,.32),"brass",20)
    # 绞盘设在吊臂根部。一股经顶端滑轮垂直承重，绳尾回到木卷筒。
    winch=PIVOT-UP*1.49+d*.30
    tube(crane,[winch-side*.46,winch+side*.46],.25,"oak",18)
    for s in (-1,1):
        tube(hardware,[winch+side*s*.45,winch+side*s*.54],.35,"iron",16)
    wraps=[winch+side*(-.39+.78*i/56)+d*(.26*math.cos(i*math.tau/8))+UP*(.26*math.sin(i*math.tau/8)) for i in range(57)]
    tube(rope,wraps,.052,"rope",6)
    hook=LOAD+UP*2.70
    route=[winch+UP*.24,PIVOT+UP*.37+d*.27]
    for i in range(9):
        a=i*math.pi/16
        route.append(axle+d*(.32*math.sin(a))+UP*(.32*math.cos(a)))
    cord(rope,[*route,hook+UP*.08],.097,.64)
    for s in (-1,1):
        c=hook+side*(s*.097)
        beam(crane,c-UP*.012,c+UP*.34,.23,.105,"oak",.045,up=side)
        tube(hardware,[c-side*.073+UP*.15,c+side*.073+UP*.15],.067,"brass",10)
    tube(hardware,[hook+UP*.18,hook+UP*.04,hook-UP*.09+side*.10,hook-UP*.24+side*.13,
                   hook-UP*.31,hook-UP*.26-side*.13,hook-UP*.13-side*.16],.060,"iron",9)
    axis=V((1,.09,0)).normalized()
    first=LOAD-axis*1.65
    _,cross=bound_logs(logs,rope,first,axis,3.30,.41,(.5,2.83))
    for t in (.50,2.83):
        for s in (-1,1):
            foot=first+axis*t+cross*(s*.64)+UP*.34
            cord(rope,[foot,foot.lerp(hook,.48)-UP*.050,hook-UP*.23],.072,.63)
    return crane,hardware,rope,logs
