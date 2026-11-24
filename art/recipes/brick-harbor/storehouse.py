"""临海双仓。高仓山墙朝海，低仓屋脊平行岸线；立体交易牌顺向海屋坡安装。"""
import math
from fabrication import Part, Plane, R, V, UP, beam, brick, cuboid, facing, rectangle, tube, wall_plane
from ashlar import Opening, archivolt, courses, door, window

FLOOR=.26
TOWER_X,TOWER_HALF=-7.17,3.38
TOWER_FRONT,TOWER_BACK=1.12,8.94
GALLERY,TOWER_EAVE,TOWER_RIDGE=5.88,10.29,14.04
HALL_LEFT,HALL_RIGHT=-3.91,10.76
HALL_FRONT,HALL_BACK=1.83,9.19
HALL_EAVE,HALL_RIDGE=5.64,9.14
TOWER_GATE=Opening(TOWER_X,1.56,FLOOR,2.68,1.80)
MAIN_DOOR=Opening(3.40,1.96,FLOOR,3.32,.85,False)
FRONT_WINDOWS=(Opening(-1.24,.52,1.97,3.17,.75),Opening(7.84,.52,1.97,3.17,.75))
SIDE_WINDOW=Opening((HALL_FRONT+HALL_BACK)*.5,.63,1.77,3.12,.86)


def member(part,plane,a,b,width,depth,projection,tile="oak"):
    beam(part,plane.at(*a,-projection),plane.at(*b,-projection),width,depth,tile,
         min(.053,width*.15),up=plane.normal,end="agedcut")


def peg(part,plane,u,z,depth,r=.071):
    tube(part,[plane.at(u,z,-depth+.04),plane.at(u,z,-depth-.017)],(r,r*.86),"iron",9)


def framed_gable(part,plane,center,half,bottom,eave,ridge,tower=False):
    # 竖铺板伸入椽下，每片单独按斜度截断，山面由外露橡木大梁咬合。
    box=Plane(plane.origin,plane.v,-plane.u)
    def roof(u): return ridge-(ridge-eave)*abs(u-center)/half
    count=round(2*half/.46)
    for i in range(count):
        a=center-half+(2*half)*i/count+.008
        b=center-half+(2*half)*(i+1)/count-.008
        pa=[(a,bottom),(b,bottom),(b,roof(b)),(a,roof(a))]
        facing(part,box,[(z,-u) for u,z in pa],.065,.27,R.choice(("oak","door","oak","dock")),.015)
    for u in (center-half+.10,center+half-.10,center):
        member(part,plane,(u,bottom),(u,roof(u)),.34 if tower else .25,.29,.044)
    for z in ((bottom+.23,bottom+1.24,eave-.08) if tower else (eave+.07,)):
        member(part,plane,(center-half-.09,z),(center+half+.09,z),.38 if tower else .27,.34,.18,"trim")
    for s in (-1,1):
        u=center+s*half
        member(part,plane,(u,eave),(center,ridge),.30,.31,.16,"trim")
        if tower:
            member(part,plane,(u-s*.12,bottom+1.45),(center+s*.42,eave-.29),.27,.24,.10)
            for z in (bottom+.23,bottom+1.24,eave-.08):
                peg(part,plane,u-s*.22,z,.36)
                peg(part,plane,center+s*.28,z,.36,.056)
    if tower:
        for u in (center-half+.31,center-1.12,center+1.12,center+half-.31):
            member(part,plane,(u,bottom-.57),(u,bottom+.22),.29,.51,.16)
            member(part,plane,(u,bottom-.52),(u,bottom+.04),.26,.69,.10,"trim")


def roof(part,center,axis,half_run,eave,ridge,length,overhang,skip_plaque=False):
    axis=V(axis)
    across=UP.cross(axis)
    center=V((center[0],center[1],0))
    slope=(ridge-eave)/half_run
    run=half_run+overhang
    low=eave-overhang*slope
    length+=2*overhang
    pitch=math.atan(slope)
    distance=run/math.cos(pitch)
    for s in (-1,1):
        u=-s*axis
        v=-s*across*math.cos(pitch)+UP*math.sin(pitch)
        base=center+s*across*run+UP*low
        plane=Plane(base,u,v)
        facing(part,plane,rectangle(-length/2,length/2,0,distance),.045,.27,"tar")
        beam(part,plane.at(-length/2,-.025,.13),plane.at(length/2,-.025,.13),.16,.25,"tar",.03)
        n=max(5,round(distance/.65))
        lift=distance/n
        width=.81 if skip_plaque else .85
        for j in range(n):
            start=-length*.5
            v0=j*lift
            step=width*(.51 if j%2 else 1)
            while start<length*.5-.035:
                end=min(length*.5,start+step)
                if length*.5-end<.26: end=length*.5
                a,b=start+.014,end-.014
                tip=R.uniform(-.035,.035)
                h=min(lift+.22,distance-v0+.035)
                # 屋牌遮住的完整瓦片不计入网格，四缘保留搭接与投影。
                hidden=skip_plaque and s<0 and a>-5.86 and b<5.86 and v0>.92 and v0+h<4.83
                if not hidden:
                    chip=min(R.uniform(.045,.12),(b-a)*.16)
                    outline=[(a,tip+chip),(a+chip,tip+.018),(b-chip*1.18,tip),
                             (b,tip+chip*.66),(b,h),(a,h)]
                    layer=Plane(plane.at(0,v0,-R.uniform(.123,.151)),u,v-plane.normal*.119)
                    tile=R.choice(("slate","slate","slate","smoke","graphite"))
                    facing(part,layer,outline,0,.091,tile,.018)
                start=end
                step=width*R.uniform(.93,1.07)
        for end in (-length*.5,length*.5):
            beam(part,plane.at(end,-.10,-.07),plane.at(end,distance,-.06),.18,.23,
                 "graphite",.044,up=plane.normal,end="smoke")
    # 脊件逐节套接，端头有挡水帽，临檐可见切面与包边。
    n=max(5,round(length/1.13))
    for i in range(n):
        a=-length*.5+i*length/n
        b=a+length/n-.012
        p=center+UP*(ridge+.065)
        tube(part,[p+axis*a,p+axis*(a+.067),p+axis*(b-.08),p+axis*b],
             (.22,.25,.20,.20),R.choice(("smoke","slate","graphite")),14)
    for s in (-1,1):
        p=center+UP*(ridge+.10)+s*axis*length*.5
        tube(part,[p-s*axis*.10,p+s*axis*.07,p+s*axis*.11],(.23,.255,.205),"smoke",14)


def storehouse():
    walls,join,slates,hardware,stockroom=map(Part,("灰岩砌体与拱券","高仓木架及山墙板","两向搭接板岩屋面",
                                               "仓门铅条尖窗与五金","入库通廊及货箱"))
    tower_face=wall_plane(0,TOWER_FRONT,1,0)
    main_face=wall_plane(0,HALL_FRONT,1,0)
    gable=wall_plane(HALL_RIGHT,0,0,1)
    left,right=TOWER_X-TOWER_HALF,TOWER_X+TOWER_HALF
    courses(walls,tower_face,left,right,FLOOR,GALLERY,9,(TOWER_GATE,))
    archivolt(walls,tower_face,TOWER_GATE,.39,5)
    for plane,a,b in ((wall_plane(0,TOWER_BACK,-1,0),-right,-left),
                      (wall_plane(left,0,0,-1),-TOWER_BACK,-TOWER_FRONT),
                      (wall_plane(right,0,0,1),TOWER_FRONT,TOWER_BACK)):
        courses(walls,plane,a,b,FLOOR,GALLERY,9)
    # 两侧略收分的扶壁及挑檐石，压住拱洞两边的荷载。
    for x in (left+.28,right-.28):
        for j in range(7):
            z=FLOOR+(j+.5)*.74
            brick(walls,(x,TOWER_FRONT-.105+(j/7)*.10,z),(.69-.13*j/7,.83-.26*j/7,.71),
                  "coping" if j in (0,6) else R.choice(("ashlar","stone")),.0,.051)
    for y in (TOWER_FRONT,TOWER_BACK):
        front=y==TOWER_FRONT
        plane=tower_face if front else wall_plane(0,TOWER_BACK,-1,0)
        framed_gable(join,plane,TOWER_X if front else -TOWER_X,TOWER_HALF+.10,GALLERY-.03,TOWER_EAVE,TOWER_RIDGE,True)
    for x in (left,right):
        front=x==right
        plane=wall_plane(x,0,0,1 if front else -1)
        a,b=(TOWER_FRONT,TOWER_BACK) if front else (-TOWER_BACK,-TOWER_FRONT)
        for j in range(16):
            u=a+(b-a)*(j+.5)/16
            member(join,plane,(u,GALLERY),(u,TOWER_EAVE), (b-a)/16-.016,.19,-.13,
                   R.choice(("door","oak","dock")))
        for z in (GALLERY+.17,GALLERY+1.18,TOWER_EAVE-.12):
            member(join,plane,(a,z),(b,z),.30,.26,.094,"trim")
        for u in (a+.15,(a+b)*.5,b-.15):
            member(join,plane,(u,GALLERY),(u,TOWER_EAVE),.28,.26,.084)
        member(join,plane,(a+.26,GALLERY+1.33),((a+b)*.5-.20,TOWER_EAVE-.26),.25,.24,.07)
    roof(slates,(TOWER_X,(TOWER_FRONT+TOWER_BACK)/2),(0,1,0),TOWER_HALF,TOWER_EAVE,TOWER_RIDGE,
         TOWER_BACK-TOWER_FRONT,.43)
    openings=(MAIN_DOOR,*FRONT_WINDOWS)
    courses(walls,main_face,HALL_LEFT,HALL_RIGHT,FLOOR,HALL_EAVE+.06,9,openings)
    courses(walls,gable,HALL_FRONT,HALL_BACK,FLOOR,HALL_EAVE+.06,9,(SIDE_WINDOW,))
    courses(walls,wall_plane(0,HALL_BACK,-1,0),-HALL_RIGHT,-HALL_LEFT,FLOOR,HALL_EAVE+.06,9)
    # 贴接高仓的端墙在其外廓后，低仓背面仍有完整横梁与山墙。
    framed_gable(join,gable,(HALL_FRONT+HALL_BACK)/2,(HALL_BACK-HALL_FRONT)/2,HALL_EAVE-.16,HALL_EAVE,HALL_RIDGE)
    for x in (HALL_LEFT+.22,HALL_RIGHT-.22):
        for j in range(8):
            z=FLOOR+(j+.5)*(HALL_EAVE-FLOOR)/8
            brick(walls,(x,HALL_FRONT+.11,z),(.49 if j%2 else .76,.59 if j%2 else .44,
                  (HALL_EAVE-FLOOR)/8-.027),R.choice(("stone","coping")),.0,.04)
    for o in openings:
        archivolt(walls,main_face,o,.32 if o==MAIN_DOOR else .195,5 if o==MAIN_DOOR else 3)
    door(hardware,main_face,MAIN_DOOR)
    for o in FRONT_WINDOWS: window(hardware,main_face,o)
    archivolt(walls,gable,SIDE_WINDOW,.21,4)
    window(hardware,gable,SIDE_WINDOW)
    for i in range(14):
        x=HALL_LEFT+(i+.5)*(HALL_RIGHT-HALL_LEFT)/14
        beam(join,(x,HALL_FRONT-.36,HALL_EAVE-.38),(x,HALL_FRONT+.17,HALL_EAVE-.055),
             .18,.23,"tar",.025)
    roof(slates,((HALL_LEFT+HALL_RIGHT)/2,(HALL_FRONT+HALL_BACK)/2),(1,0,0),
         (HALL_BACK-HALL_FRONT)/2,HALL_EAVE,HALL_RIDGE,HALL_RIGHT-HALL_LEFT,.45,True)
    # 高仓内院有进深、仓底和背墙，正向洞口可看见内墙前的木箱。
    cuboid(stockroom,(TOWER_X,TOWER_FRONT+2.94,2.84),(5.92,.22,5.22),"tar")
    cuboid(stockroom,(TOWER_X,TOWER_FRONT+1.44,FLOOR-.027),(5.8,3.20,.19),"stone")
    cuboid(stockroom,(TOWER_X,TOWER_FRONT+1.54,GALLERY-.19),(6.5,3.36,.22),"tar")
    for x,y,w,h in ((TOWER_X+.70,3.05,1.08,.94),(TOWER_X+.48,3.99,1.18,1.01),(TOWER_X-.80,3.62,.88,.79)):
        z=FLOOR+.06
        cuboid(stockroom,(x,y,z+h/2),(w,w*.84,h),"tar")
        for j in range(5):
            u=x-w/2+(j+.5)*w/5
            beam(stockroom,(u,y-w*.43,z+.04),(u,y-w*.43,z+h-.04),w/5-.012,.07,"dock",.014)
        for level in (z+.09,z+h-.09):
            beam(stockroom,(x-w/2,y-w*.46,level),(x+w/2,y-w*.46,level),.085,.13,"trim",.016)
        beam(stockroom,(x-w*.40,y-w*.475,z+.16),(x+w*.42,y-w*.475,z+h-.16),.105,.07,"oak",.015,up=V((0,-1,0)))
    return walls,join,slates,hardware,stockroom


HALL_PITCH=math.atan((HALL_RIDGE-HALL_EAVE)/((HALL_BACK-HALL_FRONT)/2))
SIGN=Plane(((HALL_LEFT+HALL_RIGHT)/2,3.27,HALL_EAVE+(3.27-HALL_FRONT)*math.tan(HALL_PITCH)),
           (1,0,0),(0,math.cos(HALL_PITCH),math.sin(HALL_PITCH)))
SIGN_W,SIGN_H=12.50,4.29


def rounded(w,h,r):
    pts=[]
    for x,y,a in ((w/2-r,h/2-r,0),(-w/2+r,h/2-r,90),(-w/2+r,-h/2+r,180),(w/2-r,-h/2+r,270)):
        for i in range(6):
            t=math.radians(a+90*i/5)
            pts.append((x+r*math.cos(t),y+r*math.sin(t)))
    return pts


def emblem():
    p=Part("六砖浮雕")
    # 叠砖图记也是六件有厚度的烧陶，顶缘与断面随屋坡受光。
    for row in range(3):
        n=3-row
        for col in range(n):
            w,h=1.49,.78
            x=3.28+(col-(n-1)/2)*(w+.09)
            y=-.99+row*(h+.088)
            points=[(x+a,y+b) for a,b in rounded(w,h,.079)]
            facing(p,SIGN,[(x+a,y+b) for a,b in rounded(w+.055,h+.055,.091)],-.55,-.46,"bronze",.023)
            facing(p,SIGN,points,-.674,-.465,("orange","rose","ember")[(col+row)%3],.065)
    return p
