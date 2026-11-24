"""依据正、侧和俯视投影装配三联商馆、后部商会大厅与两座商塔。"""
from math import cos, pi, sin, tau

from mathutils import Vector

from architecture import buttress, cornice, foot, pinnacle_at
from openings import arch, panel, strip, wall
from stonework import Frame, UP
from guild_details import clock, goods, hoist, shield, timber

FRONT, REAR = -3.18, .02
BAY = 2.58
HALF = BAY/2
EAVE = 2.57
STALL = -1.98


def roof_plane(m,a,b,c,d):
    """一排片岩一条下唇，屋脊和锥尖处收窄，瓦坯的错缝跟随铺设方向。"""
    a,b,c,d=map(Vector,(a,b,c,d))
    n=(b-a).cross(d-a).normalized()
    if n.z<0:
        n.negate()
    length=((a+b-c-d)*.5).length
    count=max(2,round(length/.124))
    for i in range(count):
        lo,hi=i/count,(i+1)/count
        p,q=a.lerp(d,lo),b.lerp(c,lo)
        s,r=a.lerp(d,hi),b.lerp(c,hi)
        left=(q-p).length/6.40
        right=(r-s).length/6.40
        lip=n*(.0072*(1-lo) if (c-d).length<.02 else .0072)
        uv=[(.47-left,i/36),(.47+left,i/36),(.47+right,(i+1)/36),(.47-right,(i+1)/36)]
        m.face([p+lip,q+lip,r,s],"slate",uv,n)
        m.face([q+lip,p+lip,p,q],"lead")


def rounded(x,r,spring,rise,steps=16):
    return [(x-r*cos(pi*i/steps),spring+rise*sin(pi*i/steps)) for i in range(steps+1)]


def opening_wall(m,f,left,right,bottom,top,holes,material="ashlar"):
    """为每个洞口留空；背墙、窗玻璃、拱腹在各自的进深处另建。"""
    cursor=left
    for head,base in holes:
        a,b=head[0][0],head[-1][0]
        if a>cursor:
            panel(m,f,[(cursor,bottom),(a,bottom),(a,top),(cursor,top)],material)
        if base>bottom:
            panel(m,f,[(a,bottom),(b,bottom),(b,base),(a,base)],material)
        for (u,z),(v,h) in zip(head,head[1:]):
            panel(m,f,[(u,z),(v,h),(v,top),(u,top)],material)
        cursor=b
    if cursor<right:
        panel(m,f,[(cursor,bottom),(right,bottom),(right,top),(cursor,top)],material)


def reveal(m,f,path,depth,material="limestone"):
    for a,b in zip(path,path[1:]):
        m.face([f.at(*a),f.at(*a,-depth),f.at(*b,-depth),f.at(*b)],material)


def archway(m,f,x,r,spring,rise,base,thickness,rich=False,pointed=False):
    count=22 if rich else 12
    head=arch(x,r,spring,rise,6) if pointed else rounded(x,r,spring,rise,count)
    path=[(x-r,base)]+head+[(x+r,base)]
    reveal(m,f,path,thickness,"limestone" if rich else "shadow")
    # 拱腹、半圆拱券和径向楔石在开口内外分别承重。竖向拱脚在枕石处收头。
    profile=[(0,-.020),(.016,.030),(.040,.053),(.063,.038),(.071,.012),
             (.086,.012),(.099,.051),(.120,.069),(.142,.044),(.155,.037)]
    if not rich:
        profile=[(v*.52,d*.62) for v,d in profile[::2]]
    strip(m,f,path,profile,"carving")
    if rich:
        for i,(a,b) in enumerate(zip(head,head[1:])):
            a,b=Vector(a),Vector(b)
            key=i in (count//2-1,count//2)
            strip(m,f,[a.lerp(b,.026),a.lerp(b,.974)],
                  [(.121,.049),(.146,.075),(.225 if key else .207,.065),(.236 if key else .210,.001)],
                  "carving" if key or i%4==0 else "limestone")
        for sign in (-1,1):
            u=x+sign*(r+.098)
            for i in range(5):
                z=(spring-base)*i/5+base
                m.band(f,u-.089,u+.089,z+.005,z+(spring-base)/5-.005,-.012,.074,
                       "carving" if i==4 else "limestone")
            m.band(f,u-.112,u+.112,spring-.055,spring+.019,-.013,.096)
    return head


def window(m,f,x,r,base,spring,rise,divided=True):
    head=rounded(x,r,spring,rise,11)
    path=[(x-r,base)]+head+[(x+r,base)]
    depth=.098
    reveal(m,f,path+[path[0]],depth,"shadow")
    coords=[(.5+(u-x)/(3*r),.09+.76*(z-base)/(spring+rise-base)) for u,z in path]
    m.face([f.at(*p,-depth) for p in path],"glass",coords,f.n)
    strip(m,f,path,[(0,-.013),(.017,.029),(.033,.045),(.048,.020),
                    (.055,.018),(.070,.047),(.083,.045),(.088,.001)],"carving")
    if divided:
        strip(m,f,[(x,base),(x,spring+rise-.018)],[(-.017,-.063),(.001,-.044),(.018,-.062)],
              "limestone")
        m.band(f,x-r,x+r,spring-.065,spring-.032,-.085,-.056,"carving")
    m.band(f,x-r-.093,x+r+.093,base-.050,base+.006,-.011,.075,"limestone")
    return head,base


def cap(m,f,left,right,z,depth=.23):
    # 山墙石帽的滴水边略微伸出，正背面都能读到阶梯折线。
    m.band(f,left-.023,right+.023,z-.063,z-.012,-depth-.024,.037,"limestone")
    m.band(f,left-.041,right+.041,z-.016,z+.055,-depth-.043,.061,"carving")


def stepped_gable(m,f,center_bay):
    rise=.29 if center_bay else 0
    windows=[window(m,f,s*.355,.246,2.07,2.56,.234) for s in (-1,1)]
    opening_wall(m,f,-HALF,HALF,1.84,2.94,windows)
    m.band(f,-HALF,HALF,1.859,1.922,-.012,.081,"limestone")
    m.band(f,-HALF,HALF,1.925,1.984,-.011,.105,"carving")
    # 最外层台阶始于窗层上缘。上层阁窗只切穿相应的立面宽度。
    steps=[(1.235,2.94),(.988,3.13),(.783,3.35),(.579,3.58),
           (.372,3.81+rise),(.164,4.065+rise)]
    for i,(half,z) in enumerate(steps):
        next_half=steps[i+1][0] if i+1<len(steps) else 0
        for sign in (-1,1):
            l,r=sorted((sign*next_half,sign*half))
            if not next_half and sign==1:
                l,r=-half,half
            elif not next_half:
                continue
            cap(m,f,l,r,z)
            if next_half:
                m.face([f.at(sign*next_half,z),f.at(sign*next_half,z,-.23),
                        f.at(sign*next_half,steps[i+1][1],-.23),f.at(sign*next_half,steps[i+1][1])],
                       "limestone",outward=f.t*sign)
    w=.372
    upper=3.81+rise
    attic=[window(m,f,u,.088 if center_bay else .099,3.039,3.307,.086,False)
           for u in ((-.151,.151) if center_bay else (0,))]
    opening_wall(m,f,-w,w,2.94,upper,attic)
    for i,(half,z) in enumerate(steps[:-2]):
        inner=steps[i+1][0]
        for sign in (-1,1):
            a,b=sorted((sign*inner,sign*half))
            if z>2.94:
                panel(m,f,[(a,2.94),(b,2.94),(b,z),(a,z)],"ashlar")
    peak=steps[-1][0]
    panel(m,f,[(-peak,upper),(peak,upper),(peak,steps[-1][1]),(-peak,steps[-1][1])],"limestone")
    # 屋面上方两侧和背面的阶梯山墙也有石料厚度。
    for i,(half,z) in enumerate(steps):
        inner=steps[i+1][0] if i+1<len(steps) else 0
        for sign in (-1,1):
            a,b=sorted((sign*inner,sign*half))
            m.face([f.at(a,EAVE,-.23),f.at(b,EAVE,-.23),f.at(b,z,-.23),f.at(a,z,-.23)],
                   "ashlar",outward=-f.n)


def shop(m,x,index):
    m.name=("西商馆","中央商馆","东商馆")[index]+" · 阶梯山墙与敞廊"
    front,back=Frame(x,FRONT,0,-1),Frame(x,REAR,0,1)
    r=.900 if index!=1 else .935
    head=archway(m,front,0,r,.850,.752,0,.35,True)
    opening_wall(m,front,-HALF,HALF,0,1.86,[(head,0)])
    inside=Frame(x,FRONT+.35,0,1)
    opening_wall(m,inside,-HALF,HALF,0,1.86,[(head,0)],"shadow")
    stepped_gable(m,front,index==1)
    # 内廊门扇退至后一跨；柱身间和拱腹之后保留真实空间。
    rear=Frame(x,STALL,0,-1)
    wall(m,rear,-HALF,HALF,0,lambda _:2.15,[(None,[(0,.395,.038,.946,.39,"gate")])])
    for offset in (-.785,.785):
        m.band(rear,offset-.220,offset+.220,.30,1.14,-.008,.011,"shadow")
        for k in range(6):
            u=offset-.171+k*.067
            timber(m,rear.at(u,.349,.022),rear.at(u,1.088,.022),.063,.027)
        for h in (.45,.96):
            timber(m,rear.at(offset-.205,h,.054),rear.at(offset+.205,h,.054),.037,.066)
        m.band(rear,offset-.244,offset+.244,.295,.340,-.01,.096,"limestone")
    for u in (-.605,.605):
        center=rear.at(u,0,.165)
        m.turned(center.x,center.y,[(.006,.090),(.114,.090),(.149,.061),
                 (.990,.061),(1.031,.088),(1.111,.094)],8,"limestone")
    for dx in (-.76,.76):
        timber(m,rear.at(dx,1.62,.021),rear.at(dx,1.69,.40),.08,.111)
    timber(m,rear.at(-HALF+.04,1.68,.34),rear.at(HALF-.04,1.68,.34),.091,.131)
    for side in (-1,1):
        wing=Frame(x+side*(HALF-.106),(FRONT+STALL)/2,-side,0)
        span=(STALL-FRONT)/2
        panel(m,wing,[(-span,0),(span,0),(span,1.90),(-span,1.90)],"ashlar")
        for y in (FRONT+.35,STALL-.045):
            m.turned(x+side*1.052,y,[(0,.147),(.109,.147),(.151,.106),(.80,.104),
                     (.85,.146),(.90,.138)],8,"limestone")
    floor=[(x-HALF+.085,FRONT+.03,.009),(x+HALF-.085,FRONT+.03,.009),
           (x+HALF-.085,STALL+.01,.009),(x-HALF+.085,STALL+.01,.009)]
    m.face(floor,"foot",outward=UP)
    m.face([Vector(p)+UP*1.843 for p in floor],"shadow",outward=-UP)
    ridge=3.94 if index==1 else 3.70
    ridge_front,ridge_back=(x,FRONT+.178,ridge),(x,REAR+.06,ridge)
    for s in (-1,1):
        eave_x=x+s*(HALF+.015)
        a=(eave_x,FRONT+.178,EAVE)
        b=(eave_x,REAR+.06,EAVE)
        roof_plane(m,a,b,ridge_back,ridge_front)
        m.rod(a,b,.041,"lead",7)
    m.rod(ridge_front,ridge_back,.042,"lead",8)
    wall(m,back,-HALF,HALF,0,lambda u:EAVE+(ridge-EAVE)*(1-abs(u)/HALF),
         [(None,[(0,.196,.73,1.70,.35,"lancet")])],breaks=(0,))
    for s in (-1,1):
        m.rod(back.at(s*HALF,EAVE,.065),back.at(0,ridge,.065),.054,"limestone",5)
    if index==1:
        shield(m,front)


def shop_street(m):
    for i,x in enumerate((-BAY,0,BAY)):
        shop(m,x,i)
    m.name="四座临街石墩与东侧货门"
    f=Frame(0,FRONT,0,-1)
    for x in (-3*HALF,-HALF,HALF,3*HALF):
        buttress(m,f,x,2.91,.302,.29,pinnacle=False)
        m.band(f,x-.181,x+.181,.015,.327,-.12,.346,"foot")
        m.band(f,x-.176,x+.176,1.385,1.490,-.06,.242,"carving")
        cap(m,f,x-.13,x+.13,2.91,.17)
    mid=(FRONT+REAR)/2
    half=(REAR-FRONT)/2
    for s in (-1,1):
        f=Frame(s*3*HALF,mid,s,0)
        wall(m,f,-half,half,0,lambda _:EAVE,
             [(None,[(.14*s,.417,.018,1.10,.51,"gate")])])
        cornice(m,f,half,EAVE)
        for sign in (-1,1):
            buttress(m,f,sign*(half-.10),2.62,.252,.17,pinnacle=False)
        for a,b in ((-half,-.72),(.78,half)):
            m.band(f,a,b,0,.242,-.02,.053,"foot")


def dormer(m,x,y,z):
    f=Frame(x,y,0,-1)
    r=.151
    head,_=window(m,f,0,.072,z+.008,z+.28,.082,False)
    opening_wall(m,f,-r,r,z,z+.376,[(head,z+.008)],"limestone")
    panel(m,f,[(-r,z+.376),(r,z+.376),(0,z+.577)],"carving")
    for s in (-1,1):
        a=f.at(s*(r+.036),z+.343,.043)
        b=f.at(s*(r+.036),z+.343,-.37)
        top,back=f.at(0,z+.616,.043),f.at(0,z+.616,-.40)
        roof_plane(m,a,b,back,top)
        m.rod(a,top,.029,"carving",5)
        sx=s*r
        m.face([f.at(sx,z),f.at(sx,z+.361),f.at(sx,z+.361,-.40),
                f.at(sx,z,-.40)],"ashlar",outward=f.t*s)
    m.rod(f.at(0,z+.60,.022),f.at(0,z+.67,.022),.020,"bronze",8,.005)


def great_hall(m):
    m.name="商会大厅 · 内院柱廊与高侧窗"
    front_y,back_y=1.09,3.23
    mid=(front_y+back_y)/2
    center=-.265
    half_width,half_depth=3.102,(back_y-front_y)/2
    west,east=center-half_width,center+half_width
    eave,ridge=4.83,6.16
    f=Frame(center,front_y,0,-1)
    gallery=[]
    for u in (-2.58,-1.55,-.52,.52,1.55,2.58):
        gallery.append((archway(m,f,u,.405,2.72,.435,0,.25),0))
        for sign in (-1,1):
            x=center+u+sign*.448
            m.turned(x,front_y-.024,[(0,.112),(.17,.112),(.214,.083),(2.59,.082),
                     (2.63,.108),(2.72,.126)],8,"limestone")
    opening_wall(m,f,-half_width,half_width,0,3.31,gallery)
    wall(m,Frame(center,front_y+.67,0,-1),-half_width,half_width,0,lambda _:3.36,
         [(None,[(u,.244,.12,1.43,.392,"gate") for u in (-2.25,0,2.25)])])
    for side,y in ((-1,front_y),(1,back_y)):
        f=Frame(center,y,0,side)
        openings=[(u,.130,3.57,4.185,.31,"tracery" if side==-1 else "lancet") for u in
                  (-2.88,-2.47,-2.06,-1.65,-1.24,-.83,-.42,0,.42,.83,1.24,1.65,2.06,2.47,2.88)]
        rows=[(None,openings)]
        if side==1:
            rows=[(3.30,[(u,.202,1.00,2.10,.40,"lancet") for u in (-2.4,-1.2,0,1.2,2.4)])]+rows
        wall(m,f,-half_width,half_width,3.30 if side==-1 else 0,lambda _:eave,rows)
        for z in (3.40,4.82):
            cornice(m,f,half_width,z)
    for side in (-1,1):
        f=Frame(center+half_width*side,mid,side,0)
        wall(m,f,-half_depth,half_depth,0,lambda u:eave+(ridge-eave)*(1-abs(u)/half_depth),
             [(None,[(u,.166,3.60,4.17,.32,"lancet") for u in (-.57,.05,.59)])],breaks=(0,))
        for s in (-1,1):
            m.rod(f.at(s*(half_depth+.065),eave,.075),f.at(0,ridge+.09,.075),.080,"carving",5)
    for y in (front_y-.072,back_y+.072):
        a,b=(west-.045,y,eave),(east+.045,y,eave)
        roof_plane(m,a,b,(b[0],mid,ridge),(a[0],mid,ridge))
        m.rod(a,b,.043,"lead",8)
    m.rod((west-.05,mid,ridge),(east+.05,mid,ridge),.052,"lead",8)
    m.name="商会屋面四扇老虎窗"
    for x in (-1.12,-.04,1.04,2.12):
        dormer(m,x,front_y+.083,5.018)
    # 北面窄附廊接后坡，东、西外廊连通前排商馆。中央露天。
    m.name="后侧库廊与两翼连接廊"
    rear=Frame(0,3.715,0,1)
    wall(m,rear,-3.23,3.23,0,lambda _:2.80,[(None,[(u,.18,.65,1.65,.31,"lancet")
         for u in (-2.65,-1.65,-.65,.65,1.65,2.65)])])
    cornice(m,rear,3.23,2.80)
    roof_plane(m,(-3.27,3.77,2.83),(3.27,3.77,2.83),(3.27,3.19,3.44),(-3.27,3.19,3.44))
    for s in (-1,1):
        end=Frame(s*3.23,3.47,s,0)
        panel(m,end,[(-.25,0),(.25,0),(.25,3.12-.30*s),(-.25,3.12+.30*s)],"ashlar")
        y=.73
        for edge in (2.34,3.49):
            side=s if edge>3 else -s
            f=Frame(s*edge,y,side,0)
            wall(m,f,-.75,.75,0,lambda _:2.82,[(1.45,[(u,.145,.30,.96,.29,"lancet")
                 for u in (-.45,0,.45)]),(None,[(u,.14,1.67,2.28,.28,"lancet") for u in (-.45,0,.45)])])
            cornice(m,f,.75,1.47)
            cornice(m,f,.75,2.82)
        for edge in (2.31,3.53):
            roof_plane(m,(s*edge,-.015,2.84),(s*edge,1.53,2.84),
                       (s*2.92,1.53,3.59),(s*2.92,-.015,3.59))
        m.rod((s*2.92,0,3.59),(s*2.92,1.51,3.59),.041,"lead")
        gable=Frame(s*2.92,1.49,0,1)
        wall(m,gable,-.576,.576,0,lambda u:3.56-.74*abs(u)/.576,
             [(None,[(0,.204,.08,1.05,.27,"gate")])],breaks=(0,))


def clock_tower(m):
    m.name="西北方钟楼 · 钟室与钟盘"
    x,y,r=-2.64,1.59,.729
    sill,top,tip=6.46,7.51,9.41
    for nx,ny in ((0,-1),(1,0),(0,1),(-1,0)):
        f=Frame(x+nx*r,y+ny*r,nx,ny)
        rows=[(5.26,[(u,.091,4.30,4.85,.23,"slit") for u in (-.218,.218)])]
        if ny==1 or nx==-1:
            rows.insert(0,(4.15,[(u,.093,2.84,3.59,.25,"slit") for u in (-.231,.231)]))
        wall(m,f,-r,r,0,lambda _:sill,rows)
        for level in (2.65,5.28,sill,top):
            cornice(m,f,r+.014,level)
        for sign in (-1,1):
            m.band(f,sign*r-.061,sign*r+.061,.08,top,-.063,.046,"limestone")
        if ny==-1:
            clock(m,f,5.86,.451)
        # 四壁镂空，每面两拱；铜钟封在横梁上，立柱之间能看见对侧天光。
        holes=[(archway(m,f,u,.193,7.018,.258,sill+.085,.129),sill+.085) for u in (-.252,.252)]
        opening_wall(m,f,-r,r,sill,top,holes,"limestone")
        for u in (-.505,0,.505):
            m.rod(f.at(u,sill+.068,.034),f.at(u,7.032,.034),.032,"carving",8)
            m.band(f,u-.057,u+.057,6.995,7.060,.001,.069,"carving")
    for i in range(9):
        xx=x+(i-4)*.15
        timber(m,(xx,y-r+.03,sill+.051),(xx,y+r-.03,sill+.051),.145,.048)
    # 西连廊与钟楼交接处的阶梯泛水埋入灰缝，裙边压住板岩的上口。
    for a,b in ((-3.35,-2.92),(-2.92,-2.31)):
        def z(xx):
            return 3.59-.75*abs(xx+2.92)/.61
        yy=y-r-.017
        m.face([(a,yy,z(a)+.082),(b,yy,z(b)+.082),(b,yy-.033,z(b)+.014),
                (a,yy-.033,z(a)+.014)],"lead",outward=Vector((0,-1,1)))
    for sx in (-1,1):
        for sy in (-1,1):
            cx,cy=x+sx*r,y+sy*r
            m.turned(cx,cy,[(.012,.114),(1.90,.109),(sill-.17,.089),(sill+.012,.129),
                            (top+.058,.096)],4,"limestone",pi/4)
            pinnacle_at(m,cx,cy,top+.006,.141,.672)
    for dx in (-.253,.253):
        cx=x+dx
        m.turned(cx,y-.22,[(6.71,.145),(6.736,.166),(6.779,.164),(6.81,.139),
                 (6.90,.110),(7.028,.089),(7.075,.062),(7.098,.032)],24,"bronze")
        m.rod((cx,y-.22,6.675),(cx,y-.22,6.803),.022,"lead",8)
        m.rod((cx,y-.22,7.075),(cx,y-.22,7.226),.026,"lead",8)
    timber(m,(x-r+.05,y-.22,7.24),(x+r-.05,y-.22,7.24),.102,.12)
    roof_tip(m,x,y,r*2**.5*1.035,top+.078,tip,4,pi/4)


def roof_tip(m,x,y,r,base,tip,sides,angle):
    outline=[Vector((x+r*cos(angle+i*tau/sides),y+r*sin(angle+i*tau/sides),base)) for i in range(sides)]
    crown=Vector((x,y,tip-.206))
    for a,b in zip(outline,outline[1:]+outline[:1]):
        roof_plane(m,a,b,crown,crown)
        m.rod(a,crown,.025,"lead",7,.008)
        m.rod(a,b,.035,"lead",6)
    m.turned(x,y,[(tip-.26,.040),(tip-.18,.052),(tip-.155,.027),
             (tip-.117,.029),(tip-.091,.052),(tip-.051,.031),(tip,.006)],10,"bronze")


def east_tower(m):
    m.name="东北八角货栈塔"
    x,y,r=3.44,2.53,.638
    angle=pi/8
    top=5.51
    corners=[Vector((x+r*cos(angle+i*tau/8),y+r*sin(angle+i*tau/8),0)) for i in range(8)]
    for a,b in zip(corners,corners[1:]+corners[:1]):
        mid=(a+b)/2
        n=(b-a).cross(UP).normalized()
        f=Frame(mid.x,mid.y,n.x,n.y)
        half=(b-a).length*.5
        wall(m,f,-half,half,0,lambda _:top,[(2.30,[(0,.061,.78,1.65,.166,"slit")]),
             (4.05,[(0,.072,2.78,3.49,.201,"slit")]),(None,[(0,.084,4.53,5.02,.210,"lancet")])])
        foot(m,f,half)
    for level in (2.34,4.15,top-.10):
        m.turned(x,y,[(level-.06,r*1.004),(level-.003,r*1.08),
                 (level+.065,r*1.08),(level+.094,r*1.012)],8,"carving",angle)
    roof_tip(m,x,y,r*1.10,top+.035,7.11,8,angle)


def build_guild(m):
    shop_street(m)
    great_hall(m)
    clock_tower(m)
    east_tower(m)
    hoist(m)
    goods(m)
