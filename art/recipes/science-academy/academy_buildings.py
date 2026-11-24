"""前置横向学厅、后部八角观测塔与右侧仪器楼。正门朝 −Y。"""
from math import cos, pi, sin, tau
from mathutils import Vector

from academy_mesh import Frame, UP, box, lathe
from architecture import buttress, cornice, foot, roof_gable, roof_plane
from openings import panel, strip, wall

TOWER=(-2.04,1.74)
ANNEX=(3.72,.24)
SPRING=8.43
DOME_R=2.49
DOME_H=2.50


def twins(x,r,bottom,spring,rise):
    return [(x+s*r*1.21,r,bottom,spring,rise,"lancet") for s in (-1,1)]


def stairs(m,f,width,landing,count,rise,run):
    for i in range(count):
        z=rise*(i+1)
        end=landing+(count-i)*run
        back=end-run if i<count-1 else -.06
        w=(width+.046*(count-i))/2+.017
        edge=[(end,z-rise),(end,z-.038),(end+.016,z-.032),(end+.022,z-.011),(end+.013,z)]
        for (a,za),(b,zb) in zip(edge,edge[1:]):
            m.face([f.at(-w,za,a),f.at(w,za,a),f.at(w,zb,b),f.at(-w,zb,b)],
                   "carving" if a!=end or b!=end else "limestone",outward=f.n)
        for s in (-1,1):
            side=[(back,0),(end,0)]+edge+[(back,z)]
            m.face([f.at(s*w,h,d) for d,h in side],"limestone",outward=f.t*s)
        m.face([f.at(-w,z,end+.013),f.at(w,z,end+.013),f.at(w,z,back),f.at(-w,z,back)],"carving",outward=UP)
        m.face([f.at(-w,0,end),f.at(w,0,end),f.at(w,0,back),f.at(-w,0,back)],"foot",outward=-UP)
        ends=[(-w,w)] if i==count-1 else [(-w,-w+.023),(w-.023,w)]
        for left,right in ends:
            panel(m,f,[(left,0),(right,0),(right,z),(left,z)],"limestone",back,-f.n)


def gable_trim(m,f,x,half,eave,peak,depth):
    for sign in (-1,1):
        pts=[(x+sign*(half+.07),eave-.035),(x,peak+.02),(x,peak+.16),(x+sign*(half+.10),eave+.075)]
        m.extrude([f.at(u,z,depth) for u,z in pts],-f.n*.15,"carving")


def porch(m,f,r,base,eave,peak):
    # 门廊是真正的突出体，开口、门扇、木纹和阶顶在同一轴线上。
    out=.30
    p=f.at(0,0,out)
    front=Frame(p.x,p.y,f.n.x,f.n.y)
    half=r*1.47
    def top(u):
        return peak-(peak-eave)*abs(u)/half
    wall(m,front,-half,half,base,top,[(None,[(0,r,base,eave-.23,.59*r,"gate")])],breaks=(0,))
    depth=-min(.24,r*.35)+.035
    for sign in (-1,1):
        middle=sign*r*.52
        for z,h in ((base+(eave-base)*.30,(eave-base)*.31),(base+(eave-base)*.74,(eave-base)*.36)):
            # 每扇门有一对嵌板，边框截面逐级退出；锁眼和门钉在木表面之前。
            w=r*.67
            sections=[]
            for inset,d in ((0,depth),(.020,depth+.028),(.043,depth+.031),(.060,depth+.007)):
                sections.append([front.at(middle+u*(w/2-inset),z+v*(h/2-inset),d)
                                 for u,v in ((-1,-1),(1,-1),(1,1),(-1,1))])
            for outer,inner in zip(sections,sections[1:]):
                for k in range(4):
                    j=(k+1)%4
                    m.face([outer[k],outer[j],inner[j],inner[k]],"darkoak",outward=front.n)
    key=front.at(.078,base+(eave-base)*.55,depth+.036)
    lathe(m,key,front.n,[(0,.031),(.009,.039),(.016,.034)],12,"brass")
    for s in (-1,1):
        edge=front.at(s*half,0)
        m.face([f.at(s*half,base,-.04),edge+UP*base,edge+UP*eave,f.at(s*half,eave,-.04)],"ashlar")
        m.band(front,s*(half-.08)-.065,s*(half-.08)+.065,base,eave-.02,-.06,.09,"limestone")
    for sign in (-1,1):
        a,b=front.at(sign*half,eave,.06),f.at(sign*half,eave,-.08)
        c,d=f.at(0,peak,-.08),front.at(0,peak,.06)
        m.face([a,b,c,d],"lead",outward=UP)
    gable_trim(m,front,0,half,eave,peak,.105)


def dormer(m,x,fy,sign):
    f=Frame(x,fy,0,sign)
    half=.337
    low,eave,peak=4.04,4.72,5.18
    back=1.12
    top=lambda u:peak-(peak-eave)*abs(u)/half
    wall(m,f,-half,half,low,top,[(None,twins(0,.103,4.21,4.55,.17))],breaks=(0,))
    m.band(f,-half-.04,half+.04,low,low+.10,-.04,.045,"carving")
    for s in (-1,1):
        m.face([f.at(s*half,low),f.at(s*half,low,-back),f.at(s*half,eave,-back),f.at(s*half,eave)],
               "ashlar",outward=f.t*s)
        a,b=f.at(s*(half+.05),eave,.055),f.at(s*(half+.05),eave,-back)
        c,d=f.at(0,peak,-back),f.at(0,peak,.055)
        roof_plane(m,a,b,c,d)
    gable_trim(m,f,0,half,eave,peak,.10)


def hall(m):
    x,y=-1.25,-1.48
    hw,hd=3.24,1.92
    eave,ridge=3.72,5.70
    front,back=Frame(x,y-hd,0,-1),Frame(x,y+hd,0,1)
    m.name="一层二层学厅"
    for j,f in enumerate((front,back)):
        lower=twins(-2.10,.204,.49,1.34,.36)+twins(2.10,.204,.49,1.34,.36)
        if j:
            lower+=twins(0,.204,.49,1.34,.36)
        upper=[spec for u in (-2.1,0,2.1) for spec in twins(u,.204,2.12,2.96,.35)]
        wall(m,f,-hw,hw,0,lambda u:eave,[(1.94,lower),(None,upper)])
        for a,b in ((-hw,-.85),(.85,hw)) if j==0 else ((-hw,hw),):
            m.band(f,a,b,.01,.34,-.03,.055,"foot")
            m.band(f,a,b,.32,.39,-.02,.078,"limestone")
        for z in (1.96,eave):
            cornice(m,f,hw,z)
        for u in (-hw+.08,-1.08,1.08,hw-.08):
            buttress(m,f,u,eave+.05,.255,.28,pinnacle=False)
            for h in (.54,1.98,3.60):
                m.band(f,u-.164,u+.164,h-.053,h+.022,.075,.27,"carving")
    for side in (-1,1):
        f=Frame(x+side*hw,y,side,0)
        top=lambda u:eave+(ridge-eave)*(1-abs(u)/hd)
        rows=[]
        for offset,limit in ((0,1.94),(1.64,None)):
            specs=[s for u in (-.94,.94) for s in twins(u,.194,.49+offset,1.32+offset,.35)]
            rows.append((limit,specs))
        wall(m,f,-hd,hd,0,top,rows,breaks=(0,))
        foot(m,f,hd)
        cornice(m,f,hd,1.96)
        cornice(m,f,hd,eave)
        for u in (-hd+.10,hd-.10):
            buttress(m,f,u,eave+.32,.28,.27,pinnacle=False)
        buttress(m,f,0,ridge+.05,.235,.20,pinnacle=False)
        m.band(f,-.12,.12,ridge-.18,ridge+.17,-.03,.16,"limestone")
        m.band(f,-.16,.16,ridge+.14,ridge+.22,-.06,.19,"carving")
    m.name="板岩大屋面与石山墙"
    roof_gable(m,Frame(x+hw,y,1,0),Frame(x-hw,y,-1,0),hd,eave,ridge,False)
    # 瓦下铺连续望板。远景并合细薄的搭接唇时，瓦口仍有实体背衬。
    for s in (-1,1):
        outer=y+s*(hd+.10)
        backing=[(x-hw-.08,outer,eave-.008),(x+hw+.08,outer,eave-.008),
                 (x+hw+.08,y,ridge-.018),(x-hw-.08,y,ridge-.018)]
        m.extrude(backing,(0,0,-.069),"lead")
    for s in (-1,1):
        f=Frame(x+hw*s,y,s,0)
        for side in (-1,1):
            edge=[(side*(hd+.08),eave-.065),(0,ridge-.045),(0,ridge+.103),(side*(hd+.11),eave+.075)]
            m.extrude([f.at(u,z,.106) for u,z in edge],-f.n*.19,"limestone")
    for j in range(14):
        left=x-hw+j*2*hw/14+.012
        right=x-hw+(j+1)*2*hw/14-.012
        section=[(y-.095,ridge-.012),(y-.095,ridge+.045),(y-.030,ridge+.117),
                 (y+.030,ridge+.117),(y+.095,ridge+.045),(y+.095,ridge-.012)]
        m.extrude([(left,u,z) for u,z in section],(right-left,0,0),"limestone")
    for side in (-1,1):
        f=Frame(x,y+hd*side,0,side)
        for dx in (-2.7,-.8,1.65):
            m.band(f,dx-.046,dx+.046,eave-.09,eave+.05,.08,.15,"lead")
    m.name="三开间老虎窗"
    for offset in (-1.95,0,1.95):
        dormer(m,x+offset,y-hd+.49,-1)
    for offset in (-2.18,2.12):
        dormer(m,x+offset,y+hd-.49,1)
    m.name="学厅门廊与石阶"
    porch(m,front,.53,.42,1.72,2.42)
    stairs(m,front,2.28,.45,6,.070,.13)


def polygon_drum(m,x,y,radius,sides,bottom,top,make_rows):
    # 每个弦面由窗洞轮廓切开。轮廓到角墩的石面保留统一水平卧缝。
    half=radius*sin(pi/sides)
    apothem=radius*cos(pi/sides)
    for i in range(sides):
        a=-pi/2+i*tau/sides
        n=Vector((cos(a),sin(a),0))
        f=Frame(x+n.x*apothem,y+n.y*apothem,n.x,n.y)
        wall(m,f,-half,half,bottom,lambda u:top,make_rows(i,half))
    outline=[(x+radius*cos(-pi/2-pi/sides+tau*i/sides),y+radius*sin(-pi/2-pi/sides+tau*i/sides),bottom) for i in range(sides)]
    m.face(outline,"foot",outward=-UP)


def round_gallery(m):
    x,y=ANNEX
    radius=1.43
    half=radius*sin(pi/12)
    for i in range(12):
        az=-pi/2+i*tau/12
        nx,ny=cos(az),sin(az)
        f=Frame(x+nx*radius*cos(pi/12),y+ny*radius*cos(pi/12),nx,ny)
        r,bottom,spring=.172,3.13,3.72
        crown=[(-r*cos(j*pi/12),spring+r*sin(j*pi/12)) for j in range(13)]
        edges=[(-r,bottom)]+crown+[(r,bottom)]
        for a,b in zip(edges,edges[1:]+edges[:1]):
            m.face([f.at(*a),f.at(*a,-.084),f.at(*b,-.084),f.at(*b)],"shadow")
        m.face([f.at(u,z,-.084) for u,z in edges],"smoke",[(.5+u,.12+(z-bottom)*.95) for u,z in edges],f.n)
        strip(m,f,edges,[(0,-.007),(.016,.026),(.040,.033),(.061,.012),(.067,0)],"carving")
        for a,b in ((-half,-r),(r,half)):
            panel(m,f,[(a,2.88),(b,2.88),(b,4.21),(a,4.21)],"ashlar")
        panel(m,f,[(-r,2.88),(r,2.88),(r,bottom),(-r,bottom)],"ashlar")
        for (a,za),(b,zb) in zip(crown,crown[1:]):
            panel(m,f,[(a,za),(b,zb),(b,4.21),(a,4.21)],"ashlar")
        m.band(f,-r-.048,r+.048,bottom-.045,bottom+.007,-.006,.053,"limestone")


def chart(m):
    x,y=TOWER
    r=2.43
    f=Frame(x,y-r*cos(pi/8),0,-1)
    # 以八芒星为主，黄道、星位和黄铜细嵌条依墙面布置。
    for u in (-.87,.87):
        m.band(f,u-.012,u+.012,6.99,8.11,0,.019,"limestone")
    def point(u,v,d=.037):
        return f.at(u,v,d)
    for coords in [[(-.83,7.48),(-.60,7.57),(-.37,7.42),(-.11,7.48),(.13,7.65),(.39,7.76),(.66,8.02)],
                   [(-.77,7.10),(-.49,7.19),(-.14,7.18),(.18,7.30),(.44,7.50),(.72,7.75)],
                   [(-.24,7.94),(.02,7.85),(.32,7.88),(.61,7.64),(.77,7.42)]]:
        for a,b in zip(coords,coords[1:]):
            m.rod(point(*a),point(*b),.010,"brass",6)
        for u,v in coords:
            m.rod(point(u,v,.033),point(u,v,.066),.029,"gilt",8,.021)
    u,v=-.46,7.56
    for i in range(8):
        a=tau*i/8
        size=.30 if i%2==0 else .19
        p=point(u+cos(a)*size,v+sin(a)*size,.042)
        m.face([point(u-sin(a)*.040,v+cos(a)*.040),p,point(u,v,.104)],"gilt",outward=f.n)
        m.face([point(u+sin(a)*.040,v-cos(a)*.040),point(u,v,.104),p],"brass",outward=f.n)


def tower(m):
    x,y=TOWER
    m.name="八角观测塔与深窗"
    for tier,(r,z0,z1) in enumerate(((2.59,0,4.80),(2.54,4.80,6.67),(2.43,6.67,8.32))):
        def rows(i,half):
            if tier==0:
                return [(2.29,twins(0,.194,.68,1.59,.35)),(None,twins(0,.194,2.77,3.88,.38))]
            if tier==2 and i==0:
                return []
            return [(None,twins(0,.211,z0+.38,z1-.66,.38))]
        polygon_drum(m,x,y,r,8,z0,z1,rows)
        for i in range(8):
            a=-pi/2-pi/8+tau*i/8
            f=Frame(x+r*cos(a),y+r*sin(a),cos(a),sin(a))
            buttress(m,f,0,z1-z0,.34 if tier==0 else .27,.18,z0,pinnacle=False)
            for z in (z0+.12,z1-.23):
                m.band(f,-.16,.16,z,z+.11,-.12,.16,"limestone")
    m.name="石塔腰线与观测层"
    for z,r in ((.32,2.61),(2.35,2.60),(4.80,2.62),(6.67,2.58),(8.30,2.54)):
        m.turned(x,y,[(z-.15,r-.13),(z-.105,r-.09),(z-.080,r-.08),
                      (z-.05,r+.035),(z+.036,r+.035),(z+.062,r-.014),
                      (z+.104,r-.018),(z+.14,r-.09)],8,"carving",-pi/2-pi/8)
    # 穹顶旋轨位于塔壁上端，不封住上方观测空间。
    m.turned(x,y,[(8.28,2.39),(8.33,2.54),(8.40,2.55),(8.47,2.50)],72,"limestone")
    lathe(m,(x,y,8.43),UP,[(0,2.48),(.016,2.52),(.079,2.52),(.105,2.45)],80,"gunmetal")
    m.name="石刻星图与黄铜星钉"
    chart(m)


def annex(m):
    x,y=ANNEX
    hw,hd,cut=1.52,1.57,.24
    m.name="星仪楼方殿与侧门"
    outline=[(-hw+cut,-hd),(hw-cut,-hd),(hw,-hd+cut),(hw,hd-cut),
             (hw-cut,hd),(-hw+cut,hd),(-hw,hd-cut),(-hw,-hd+cut)]
    for j,((ax,ay),(bx,by)) in enumerate(zip(outline,outline[1:]+outline[:1])):
        dx,dy=bx-ax,by-ay
        half=(dx*dx+dy*dy)**.5/2
        f=Frame(x+(ax+bx)/2,y+(ay+by)/2,dy,-dx)
        specs=twins(0,.224,.77,1.78,.36) if half>.8 and j!=0 else []
        wall(m,f,-half,half,0,lambda u:2.87,[(None,specs)])
        foot(m,f,half)
        cornice(m,f,half,2.83)
        if half>.8:
            for u in (-half+.045,half-.045):
                buttress(m,f,u,2.77,.238,.17,pinnacle=False)
    top=[(x+u,y+v,2.82) for u,v in outline]
    m.extrude(top,(0,0,.12),"limestone")
    f=Frame(x,y-hd,0,-1)
    porch(m,f,.45,.34,1.65,2.28)
    stairs(m,f,1.32,.36,5,.068,.115)
    m.name="环窗圆鼓楼"
    round_gallery(m)
    for z,r in ((2.93,1.48),(4.15,1.51)):
        m.turned(x,y,[(z-.09,r-.12),(z-.06,r-.06),(z-.03,r),(z+.105,r),(z+.14,r-.055)],
                 60,"carving")
    m.turned(x,y,[(4.28,1.43),(4.31,1.43),(4.35,1.24)],72,"limestone")
    m.name="连廊与日晷庭"
    # 仪器楼通过矮廊接入学厅，正面退让形成围合的小庭。
    f=Frame(2.12,-.88,0,-1)
    wall(m,f,-.48,.48,0,lambda u:2.58,[(None,twins(0,.14,.83,1.77,.28))])
    back=Frame(2.12,1.26,0,1)
    wall(m,back,-.48,.48,0,lambda u:2.58,[(None,twins(0,.14,.83,1.77,.28))])
    roof_plane(m,(1.61,-.96,2.60),(2.64,-.96,2.60),(2.64,.19,3.22),(1.61,.19,3.22))
    roof_plane(m,(2.64,1.32,2.60),(1.61,1.32,2.60),(1.61,.19,3.22),(2.64,.19,3.22))
    court=Frame(2.70,-3.04,0,-1)
    box(m,2.70,-2.60,0,1.37,.99,.25,"limestone")
    stairs(m,court,1.20,.02,4,.0625,.123)
    for wx,wy,nx,ny,length in ((2.03,-2.63,-1,0,.94),(3.40,-2.44,1,0,1.33)):
        side=Frame(wx,wy,nx,ny)
        m.band(side,-length/2,length/2,0,.32,-.085,.085,"ashlar")
        for t in (-.44,-.22,0,.22,.44):
            m.band(side,length*t-.033,length*t+.033,.28,.58,-.058,.058,"carving")
        m.band(side,-length/2-.06,length/2+.06,.57,.66,-.087,.087,"carving")
        for t in (-length/2,length/2):
            m.band(side,t-.094,t+.094,0,.72,-.09,.09,"limestone")
            m.band(side,t-.12,t+.12,.68,.76,-.12,.12,"carving")
