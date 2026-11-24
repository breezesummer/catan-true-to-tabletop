"""双扇铜穹顶、带物镜的折射镜、可读刻度浑仪与庭院日晷。"""
from math import atan2, cos, pi, sin, tau
from mathutils import Vector

from academy_buildings import ANNEX, DOME_H, DOME_R, SPRING, TOWER
from academy_mesh import UP, band_ring, lathe, orb


def dome(m):
    x,y=TOWER
    center=Vector((x,y,SPRING+.06))
    gap=.403
    crown=1.88
    r,h=DOME_R,DOME_H
    def opening(t):
        return gap*(1-.27*(max(0,sin(t))**1.35))
    def shell(lat,t,d=0):
        edge=opening(t)
        offset=abs(lat)
        spread=offset*edge/gap if offset<=gap else edge+(offset-gap)*(pi/2-edge)/(pi/2-gap)
        lat=spread if lat>=0 else -spread
        return center+Vector(((r+d)*sin(lat),-(r+d)*cos(lat)*cos(t),(h+d)*cos(lat)*sin(t)))
    def sheet(l0,l1,t0,t1,cols,rows,d,swatch,inside=False):
        for j in range(rows):
            for i in range(cols):
                u0,u1=i/cols,(i+1)/cols
                v0,v1=j/rows,(j+1)/rows
                uv=[(u0,v0),(u1,v0),(u1,v1),(u0,v1)]
                points=[shell(l0+(l1-l0)*u,t0+(t1-t0)*v,d) for u,v in uv]
                out=sum(points,Vector())/4-center
                m.face(points,swatch,uv,-out if inside else out,True)
    def roll(l0,l1,t0,t1,width,material,d,steps):
        last=shell(l0,t0,d)
        for i in range(1,steps+1):
            s=i/steps
            point=shell(l0+(l1-l0)*s,t0+(t1-t0)*s,d)
            m.rod(last,point,width,material,6)
            last=point
    m.name="铜穹顶外板与咬边"
    for sign in (-1,1):
        # 两扇壳体保留完整的纵向观测开口。
        for a in range(7):
            la=sign*(gap+(pi/2-gap)*a/7)
            lb=sign*(gap+(pi/2-gap)*(a+1)/7)
            for b in range(10):
                ta=pi*b/10
                tb=pi*(b+1)/10
                sheet(la,lb,ta,tb,3,4,0,"copper")
    sheet(-gap,gap,crown,pi,12,18,0,"copper")
    def seam(coords,width,metal):
        last=None
        for az,el in coords:
            vx,vy,vz=cos(el)*sin(az),-cos(el)*cos(az),sin(el)
            t=atan2(vz,-vy)
            if abs(vx)<sin(opening(t)+.045) and t<crown+.008:
                last=None
                continue
            point=center+Vector((vx*(r+.012),vy*(r+.012),vz*(h+.012)))
            if last is not None:
                m.rod(last,point,width,metal,5)
            last=point
    for i in range(28):
        az=tau*(i+.5)/28
        seam([(az,pi*.5*j/48) for j in range(49)],.018,"verdigris")
    for j in range(1,7):
        el=j*pi/14
        seam([(tau*i/152,el) for i in range(153)],.009,"gunmetal")
    m.name="穹顶内衬与快门轨道"
    for s in (-1,1):
        sheet(s*gap,s*pi/2,0,pi,16,28,-.107,"gunmetal",True)
    sheet(-gap,gap,crown,pi,10,16,-.107,"gunmetal",True)
    for s in (-1,1):
        # 双轨的断面有外翻边、凹槽和内唇，能从正面读出板厚。
        section=[(-.032,-.105),(-.050,.008),(-.046,.078),(-.032,.105),
                 (.050,.105),(.065,.081),(.069,.030),(.081,.021),(.084,-.009)]
        for t in range(52):
            ta,tb=pi*t/52,pi*(t+1)/52
            for j,((la,ha),(lb,hb)) in enumerate(zip(section,section[1:])):
                points=[shell(s*(gap+la),ta,ha),shell(s*(gap+la),tb,ha),
                        shell(s*(gap+lb),tb,hb),shell(s*(gap+lb),ta,hb)]
                out=shell(s*gap,(ta+tb)/2,.02)-center
                if j==0:
                    out=Vector((-s,0,0))
                m.face(points,"carving" if j in (2,3,4) else "verdigris" if j in (0,7) else "limestone",
                       outward=out,smooth=j in (2,3,4))
            if t%5==2:
                p=shell(s*(gap+.093),(ta+tb)/2,.014)
                n=(p-center).normalized()
                lathe(m,p,n,[(0,.027),(.009,.029),(.015,.018)],8,"gunmetal")
        for t in (0,pi):
            p=shell(s*gap,t,0)
            m.turned(p.x,p.y,[(p.z-.075,.20),(p.z-.04,.22),(p.z+.08,.20),(p.z+.13,.16)],8,"carving")
    for i in range(12):
        la=-gap+2*gap*i/12
        lb=-gap+2*gap*(i+1)/12
        m.face([shell(la,crown,-.107),shell(lb,crown,-.107),shell(lb,crown,.025),shell(la,crown,.025)],
               "gunmetal",outward=Vector((0,-1,0)))
    for t in (2.12,2.55,2.94):
        roll(-.91,.91,t,t,.036,"gunmetal",-.117,26)


def telescope(m):
    x,y=TOWER
    y-=1.39
    x-=.54
    m.name="观测镜回转台与俯仰架"
    base=SPRING+.092
    band_ring(m,(x,y,base+.039),UP,.516,.050,.050,"gunmetal","degrees",80)
    lathe(m,(x,y,base),UP,[(0,.43),(.058,.43),(.082,.37),(.13,.37),(.155,.29),
                            (.38,.26),(.42,.32),(.49,.32)],48,"verdigris")
    lathe(m,(x,y,base+.12),UP,[(0,.366),(.017,.38),(.051,.38),(.061,.35)],48,"gilt")
    pivot=Vector((x,y,base+.69))
    n=Vector((.74,-.52,.44)).normalized()
    lateral=UP.cross(n).normalized()
    for s in (-1,1):
        low=Vector((x,y,base+.34))+lateral*s*.19
        p=pivot+lateral*s*.22
        m.rod(low,p,.106,"gunmetal",16,.13)
        lathe(m,p,lateral*s,[(0,.179),(.041,.178),(.060,.154),(.073,.139)],32,"brass")
        lathe(m,p+lateral*s*.073,lateral*s,[(0,.069),(.021,.074),(.037,.051)],12,"gilt")
    chord=UP.cross(lateral).normalized()
    center=pivot+lateral*.205
    for j in range(18):
        a,b=-.19+j*.088,-.19+(j+1)*.088
        def bearing(angle,r):
            return center+chord*(r*sin(angle))-UP*(r*cos(angle))
        m.face([bearing(a,.27),bearing(b,.27),bearing(b,.33),bearing(a,.33)],"degrees",
               [(j/18,0),((j+1)/18,0),((j+1)/18,1),(j/18,1)],lateral)
    m.name="伸缩望远镜与物镜"
    lathe(m,pivot,n,[(-.79,.085),(-.77,.101),(-.67,.098),(-.64,.085),(-.53,.085),
                     (-.51,.135),(-.26,.146),(-.23,.158),(.23,.163),(.25,.183),
                     (.73,.191),(.75,.207),(1.12,.221),(1.19,.24),(1.34,.247)],56,"brass")
    for d,r in ((-.76,.108),(-.55,.123),(-.26,.165),(.23,.187),(.71,.209),(1.12,.238)):
        lathe(m,pivot+n*d,n,[(-.018,r-.013),(0,r+.009),(.036,r+.009),(.052,r-.009)],48,"gilt")
    lathe(m,pivot+n*.03,n,[(-.13,.166),(-.1,.18),(.095,.182),(.12,.169)],40,"gunmetal")
    # 物镜座向内回折。玻璃在口沿之后另建轻微凸面。
    lathe(m,pivot,n,[(1.29,.244),(1.31,.264),(1.405,.264),(1.423,.246),
                     (1.423,.212),(1.396,.205),(1.352,.203)],64,"gunmetal",False)
    band_origin=pivot+n*1.382
    lathe(m,band_origin,n,[(0,.263),(.005,.269),(.021,.269),(.026,.262)],64,"gilt",False)
    lens_center=pivot+n*1.356
    second=n.cross(lateral)
    def pane(q,a):
        return lens_center+(lateral*cos(a)+second*sin(a))*(q*.204)+n*(.020*(1-q*q))
    for j in range(9):
        for i in range(64):
            params=[(j/9,tau*i/64),((j+1)/9,tau*i/64),((j+1)/9,tau*(i+1)/64),(j/9,tau*(i+1)/64)]
            uv=[(.5+.49*q*cos(a),.5+.49*q*sin(a)) for q,a in params]
            m.face([pane(q,a) for q,a in params],"lens",uv,n,True)
    guide=pivot+UP*.22-lateral*.08
    for d in (-.22,.28):
        m.rod(guide+n*d-UP*.08,guide+n*d,.034,"gunmetal",8)
    lathe(m,guide,n,[(-.34,.036),(-.32,.046),(.24,.050),(.28,.066),(.36,.067)],20,"verdigris")
    p=pivot-n*.45+lateral*.12
    lathe(m,p,lateral,[(0,.031),(.065,.032),(.071,.074),(.098,.074),(.11,.060)],18,"brass")


def armillary(m):
    x,y=ANNEX
    m.name="浑仪转盘与铸铜托架"
    lathe(m,(x,y,4.34),UP,[(0,.86),(.04,.87),(.075,.83),(.13,.63),(.18,.57),
                            (.22,.57),(.27,.47),(.42,.38)],64,"verdigris")
    band_ring(m,(x,y,4.46),UP,.69,.081,.056,"brass","degrees",80)
    center=Vector((x,y,5.79))
    meridian=Vector((-.60,-.60,.53)).normalized()
    ecliptic=Vector((.48,-.43,.79)).normalized()
    # 浑仪由两只分开的耳座承重，夹持点落在外子午环所在平面。
    lowest=(-UP+meridian*UP.dot(meridian)).normalized()
    sideways=meridian.cross(lowest)
    for s in (-1,1):
        q=center+1.17*(lowest*cos(.36)+sideways*s*sin(.36))
        p=Vector((x,y,4.53))+sideways*s*.37
        m.rod(p,q,.145,"gunmetal",16,.103)
        lathe(m,q-meridian*.15,meridian,[(0,.104),(.036,.142),(.075,.109),(.244,.108),(.285,.128)],24,"brass")
    m.name="交错厚星环与刻度"
    band_ring(m,center,meridian,1.20,.265,.177,"brass","degrees",112)
    band_ring(m,center,ecliptic,1.055,.300,.159,"gilt","ecliptic",112)
    axis=Vector((-.32,.17,.93)).normalized()
    band_ring(m,center,axis,.74,.066,.052,"verdigris","brass",80)
    m.name="黄道星球与极轴"
    orb(m,center,.501,"globe",axis,64,36)
    for s in (-1,1):
        lathe(m,center+axis*s*.44,axis*s,[(0,.096),(.08,.09),(.096,.064),(.18,.063),
                                          (.20,.048),(.59,.044)],28,"brass")
        orb(m,center+axis*s*.58,.082,"gilt",longitudes=20,latitudes=10)
    # 极轴与环的交点以螺钉盖封住。
    pin=meridian.cross(ecliptic).normalized()
    for s in (-1,1):
        p=center+pin*s*1.06
        lathe(m,p,pin*s,[(0,.105),(.117,.102),(.14,.13),(.169,.121),(.183,.072)],24,"gunmetal")


def sundial(m):
    m.name="庭院黄铜日晷"
    x,y=2.71,-2.59
    m.turned(x,y,[(.249,.42),(.29,.45),(.34,.45),(.37,.38),(.55,.36),
                  (.66,.35),(.69,.45),(.75,.46)],40,"limestone")
    lathe(m,(x,y,.744),UP,[(0,.448),(.024,.459),(.046,.443)],48,"brass")
    center=Vector((x,y,.797))
    for i in range(64):
        a,b=tau*i/64,tau*(i+1)/64
        uv=[(.5,.5),(.5+.495*cos(a),.5+.495*sin(a)),(.5+.495*cos(b),.5+.495*sin(b))]
        m.face([center,center+.438*Vector((cos(a),sin(a),0)),center+.438*Vector((cos(b),sin(b),0))],
               "dial",uv,UP)
    outline=[(x-.04,y-.32,.798),(x-.04,y+.24,.798),(x-.04,y+.35,1.18)]
    m.extrude(outline,(.08,0,0),"brass")
