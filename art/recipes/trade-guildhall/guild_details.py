"""城钟、铜秤盾徽、吊货滑轮、榫接木箱和布匹。构造坐标的正门朝 −Y。"""
from math import cos, pi, sin, tau

from mathutils import Vector

from stonework import Frame, UP


def timber(m,a,b,width,depth=None):
    p,q = Vector(a),Vector(b)
    length = (q-p).length
    axis = (q-p).normalized()
    side = axis.cross(UP if abs(axis.z)<.94 else Vector((0,1,0))).normalized()
    normal = axis.cross(side)
    w,h = width/2,(depth or width)/2
    bevel = min(w,h)*.16
    corners = [(-w+bevel,-h),(w-bevel,-h),(w,-h+bevel),(w,h-bevel),
               (w-bevel,h),(-w+bevel,h),(-w,h-bevel),(-w,-h+bevel)]
    low = [p+side*x+normal*y for x,y in corners]
    high = [v+q-p for v in low]
    m.face(low[::-1],"oak")
    m.face(high,"oak")
    for i in range(8):
        j=(i+1)%8
        u=.128+.075*i
        v=(low[i]-low[j]).length*.94
        m.face([low[i],low[j],high[j],high[i]],"oak",
               [(u,.08),(u+v,.08),(u+v,.08+length*.68),(u,.08+length*.68)])


def moulding(m,f,x,z,r,profile,swatch,sides=56):
    rings=[[f.at(x+rr*r*cos(tau*i/sides),z+rr*r*sin(tau*i/sides),depth)
            for i in range(sides)] for rr,depth in profile]
    for a,b in zip(rings,rings[1:]):
        for i in range(sides):
            j=(i+1)%sides
            m.face([a[i],a[j],b[j],b[i]],swatch,outward=f.n*(a[i]-b[i]).length+
                   ((a[i]+a[j])/2-f.at(x,z,0)).normalized()*.01)


def clock(m,f,z,r):
    moulding(m,f,0,z,r,[(1.18,.005),(1.18,.046),(1.13,.089),(1.07,.093),(1.045,.074)],
             "carving")
    moulding(m,f,0,z,r,[(1.065,.071),(1.043,.104),(1.004,.117),(.970,.097),(.965,.082)],"bronze")
    face,uv=[],[]
    for i in range(72):
        a=tau*i/72
        face.append(f.at(r*.979*cos(a),z+r*.979*sin(a),.084))
        uv.append((.5+.48*cos(a),.5+.48*sin(a)))
    m.face(face,"dial",uv,f.n)
    # 两针独立铆接，针尖、镂空感的配重和铜轴帽在掠光下有各自的轮廓。
    def hand(angle,length,width,depth):
        a=angle*pi/180
        def at(l,w):
            return f.at(r*(l*sin(a)+w*cos(a)),z+r*(l*cos(a)-w*sin(a)),depth)
        silhouette=[(-.19,0),(-.135,-width*.90),(-.08,-width*.24),(.09,-width*.36),
                    (.47*length,-width*.38),(.64*length,-width),(.72*length,-width*.54),
                    (length,0),(.72*length,width*.54),(.64*length,width),
                    (.47*length,width*.38),(.09,width*.36),(-.08,width*.24),(-.135,width*.90)]
        m.extrude([at(l,w) for l,w in silhouette],-f.n*.012,"lead")
    hand(213.5,.53,.061,.122)
    hand(42,.82,.041,.147)
    m.rod(f.at(0,z,.139),f.at(0,z,.170),r*.076,"bronze",16,r*.055)


def shield(m,f):
    path=[(-.306,2.23),(.306,2.23),(.288,1.91),(.261,1.77),(.145,1.68),
          (0,1.60),(-.145,1.68),(-.261,1.77),(-.288,1.91)]
    def plate(scale,depth,mat):
        outline=[f.at(x*scale,1.96+(z-1.96)*scale,depth) for x,z in path]
        m.extrude(outline,-f.n*.052,mat)
    plate(1.07,.19,"carving")
    plate(.99,.216,"bronze")
    plate(.88,.225,"limestone")
    d=.252
    m.rod(f.at(0,1.80,d),f.at(0,2.16,d),.019,"bronze",10)
    m.rod(f.at(-.075,1.81,d),f.at(.075,1.81,d),.016,"bronze",8)
    m.rod(f.at(-.177,2.071,d),f.at(.177,2.095,d),.015,"bronze",8)
    m.rod(f.at(0,2.165,d-.012),f.at(0,2.165,d+.010),.030,"bronze",12)
    for side in (-1,1):
        x=.163*side
        beam=2.083+.012*side
        for dx in (-.074,.074):
            m.rod(f.at(x,beam,d),f.at(x+dx,beam-.176,d),.0058,"lead",5)
        lip=beam-.172
        bowl=[f.at(x-.081,lip,d+.007),f.at(x+.081,lip,d+.007),
              f.at(x+.053,lip-.037,d+.007),f.at(x,lip-.057,d+.007),f.at(x-.053,lip-.037,d+.007)]
        m.extrude(bowl,-f.n*.026,"bronze")


def crate(m,x,y,bottom,size):
    height=size*.93
    for nx,ny in ((0,-1),(1,0),(0,1),(-1,0)):
        f=Frame(x+nx*size/2,y+ny*size/2,nx,ny)
        for i in range(5):
            u=size*(-.5+(i+.5)/5)
            timber(m,f.at(u,bottom+.018,-.016),f.at(u,bottom+height-.016,-.016),size*.193,.037)
        for sign in (-1,1):
            u=sign*size*.435
            timber(m,f.at(u,bottom,.014),f.at(u,bottom+height,.014),size*.119,.040)
            h=bottom+height*(.067 if sign<0 else .930)
            timber(m,f.at(-size*.5,h,.037),f.at(size*.5,h,.037),.044,size*.115)
        for sign in (-1,1):
            timber(m,f.at(-size*.39,bottom+height*(.15 if sign<0 else .85),.035),
                   f.at(size*.39,bottom+height*(.85 if sign<0 else .15),.035),
                   .035,size*.077)
        for sign in (-1,1):
            for level in (.077,.918):
                p=f.at(sign*size*.435,bottom+height*level,.058)
                m.rod(p,p+f.n*.015,size*.025,"lead",8,size*.018)
    for i in range(5):
        dx=size*(-.5+(i+.5)/5)
        timber(m,(x+dx,y-size*.5,bottom+height-.011),(x+dx,y+size*.5,bottom+height-.011),
               size*.19,.025)


def hoist(m):
    m.name="东侧橡木吊架与滑车"
    y=-2.55
    timber(m,(3.90,y,2.35),(4.94,y,2.35),.15,.16)
    timber(m,(3.92,y,1.55),(4.60,y,2.29),.14,.15)
    timber(m,(3.965,y,1.38),(3.965,y,2.42),.19,.14)
    for x in (4.03,4.53,4.84):
        for sign in (-1,1):
            m.rod((x,y+sign*.077,2.35),(x,y+sign*.090,2.35),.027,"lead",10,.019)
    x=4.82
    for sign in (-1,1):
        m.rod((x-.065,y+.056*sign,2.31),(x-.065,y+.056*sign,2.183),.021,"lead")
        m.rod((x+.035,y+.056*sign,2.31),(x-.004,y+.056*sign,2.183),.021,"lead")
    m.rod((x-.03,y-.082,2.185),(x-.03,y+.082,2.185),.069,"bronze",14)
    for offset in (-.061,.012):
        m.rod((x+offset,y,2.19),(x+offset,y,1.79),.010,"linen",8)
    m.rod((x-.026,y-.036,1.766),(x-.026,y+.036,1.766),.056,"lead",12)
    for sign in (-1,1):
        for far in (-1,1):
            m.rod((x-.026,y,1.746),(x-.026+.172*sign,y+.172*far,1.535),.011,"linen",6)
    crate(m,x-.026,y,1.14,.418)


def bolt_of_linen(m,x,y,z,length,r):
    p,q=x-length/2,x+length/2
    for i in range(36):
        a,b=tau*i/36,tau*(i+1)/36
        def rim(x,t,scale=1):
            return Vector((x,y+r*scale*cos(t),z+r*scale*sin(t)))
        m.face([rim(p,a),rim(p,b),rim(q,b),rim(q,a)],"linen",
               [(.01,i/36),(.01,(i+1)/36),(.99,(i+1)/36),(.99,i/36)],
               Vector((0,cos((a+b)/2),sin((a+b)/2))))
    for end,sign in ((p,-1),(q,1)):
        pts=[rim(end+sign*.001,tau*i/36) for i in range(36)]
        coords=[(.5+.478*cos(tau*i/36),.5+.478*sin(tau*i/36)) for i in range(36)]
        m.face(pts,"selvedge",coords,Vector((sign,0,0)))
    for xx in (p+.16*length,q-.16*length):
        for i in range(24):
            a,b=tau*i/24,tau*(i+1)/24
            m.rod(rim(xx,a,1.012),rim(xx,b,1.012),.0105,"linen",5)


def goods(m):
    m.name="拱廊门前布匹与木箱"
    bolt_of_linen(m,-2.72,-3.37,.159,.88,.159)
    bolt_of_linen(m,-2.58,-3.11,.165,.93,.165)
    bolt_of_linen(m,-2.72,-3.248,.416,.83,.151)
    crate(m,2.84,-3.24,0,.47)
    crate(m,2.40,-3.39,0,.345)
