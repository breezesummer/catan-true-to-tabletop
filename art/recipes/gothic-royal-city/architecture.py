"""按四视图定位中殿、翼廊和六座主次塔。前方是 -Y，X 增大为正视图右侧。"""
from math import ceil, cos, pi, sin, sqrt, tau

from mathutils import Vector

from stonework import Frame, UP
from openings import panel, rosette, wall


def buttress(m,f,t,height,width=.29,projection=.32,base=0,pinnacle=True):
    def ring(z,w,out):
        return [f.at(t+s*w*.5,base+z,d) for s,d in
                ((-1,-.065),(1,-.065),(1,out),(-1,out))]
    profile=[(0,1.34,1.25),(.11,1.34,1.25),(.15,1.15,1.08),
             (.44,1.15,1.08),(.49,1,.90),(.76,1,.90),(.81,.77,.62),
             (.96,.77,.62),(1,.56,.27)]
    rings=[ring(k*height,width*w,projection*d) for k,w,d in profile]
    for n,(a,b) in enumerate(zip(rings,rings[1:])):
        for i in range(4):
            j=(i+1)%4
            m.face([a[j],a[i],b[i],b[j]],"foot" if n==0 and base<.1 else
                   "carving" if n in (1,3,5,7) else "limestone")
    m.face(rings[-1],"carving",outward=UP)
    if pinnacle:
        center=f.at(t,base+height,projection*.15)
        pinnacle_at(m,center.x,center.y,center.z,width*.49,width*2.5)


def pinnacle_at(m,x,y,z,r,h):
    m.turned(x,y,[(z,r),(z+h*.07,r*1.14),(z+h*.13,r*.77),(z+h*.29,r*.77),
                  (z+h*.33,r),(z+h*.37,r*.86),(z+h*.86,r*.23),(z+h*.91,r*.30),
                  (z+h,r*.08)],4,"carving",pi/4)


def roof_plane(m,a,b,c,d):
    """从下檐 ab 向上脊 dc 铺瓦。每排瓦的下唇侧面参与轮廓与投影。"""
    a,b,c,d=map(Vector,(a,b,c,d))
    normal=(b-a).cross(d-a).normalized()
    if normal.z<0:
        normal=-normal
    run=((a+b)*.5-(c+d)*.5).length
    count=max(2,round(run/(4.08/18)))
    widths=((b-a).length,(c-d).length)
    for i in range(count):
        low,high=i/count,(i+1)/count
        p,q=a.lerp(d,low),b.lerp(c,low)
        s,r=a.lerp(d,high),b.lerp(c,high)
        # 一排瓦的檐口比上缘高一层岩片，细缝在同位置的法线图中。
        lift=.013*(1-low) if widths[1]<.035 else .013
        w0=widths[0]*(1-low)+widths[1]*low
        w1=widths[0]*(1-high)+widths[1]*high
        uv=[(.37-w0/8.16,i/18),(.37+w0/8.16,i/18),
            (.37+w1/8.16,(i+1)/18),(.37-w1/8.16,(i+1)/18)]
        m.face([p+normal*lift,q+normal*lift,r,s],"slate",uv,normal)
        m.face([q+normal*lift,p+normal*lift,p,q],"lead")


def roof_gable(m,front,back,half,base,top,ornate):
    eave,ridge=base+.06,top+.05
    a,b=front.at(-half-.10,eave,.09),front.at(half+.10,eave,.09)
    c,d=back.at(-half-.10,eave,.09),back.at(half+.10,eave,.09)
    peak,far=front.at(0,ridge,.09),back.at(0,ridge,.09)
    roof_plane(m,a,d,far,peak)
    roof_plane(m,c,b,peak,far)
    m.rod(peak,far,.044,"lead",8)
    for corner,other in ((a,d),(b,c)):
        m.rod(corner,other,.040,"lead",6)
    for f in (front,back):
        for sign in (-1,1):
            points=[(sign*(half+.12),base+.02),(0,top+.08),(0,top+.22),
                    (sign*(half+.14),base+.15)]
            m.extrude([f.at(x,z,.13) for x,z in points],-f.n*.20,"carving")
            if ornate:
                for frac in (.27,.52,.77):
                    u=sign*half*(1-frac)
                    z=base+(top-base)*frac+.20
                    m.rod(f.at(u,z-.04,.08),f.at(u+sign*.025,z+.077,.08),.034,"limestone",5,.007)
        if ornate:
            fin=f.at(0,top+.16,.055)
            pinnacle_at(m,fin.x,fin.y,fin.z,.08,.32)


def cornice(m,f,half,z):
    for a,b,d,s in ((-.15,-.10,.07,"limestone"),(-.10,-.05,.043,"shadow"),
                    (-.05,.026,.094,"carving")):
        m.band(f,-half-.02,half+.02,z+a,z+b,-.03,d,s)


def foot(m,f,half):
    m.band(f,-half,half,.012,.31,-.022,.053,"foot")
    m.band(f,-half-.012,half+.012,.30,.366,-.017,.080,"limestone")


def hall(m,name,x,y,w,length,eave,ridge,axis=(0,1),style="chapel"):
    m.name=name
    along=Vector((*axis,0))
    across=along.cross(UP)
    origin=Vector((x,y,0))
    def frame(n,dist):
        o=origin+n*dist
        return Frame(o.x,o.y,n.x,n.y)
    front,back=frame(-along,length*.5),frame(along,length*.5)
    half=w*.5
    for end,f in enumerate((front,back)):
        top=lambda u:eave+(ridge-eave)*(1-abs(u)/half)
        rows=[]
        if style=="nave" and end==0:
            rows=[(2.45,[(0,.445,.55,1.55,.70,"gate")]),
                  (None,[(-.59,.158,2.59,4.46,.44,"tracery"),
                         (0,.209,2.62,4.80,.60,"tracery"),
                         (.59,.158,2.59,4.46,.44,"tracery")])]
        elif style!="nave" or end==1:
            n=3 if w>=2.4 else 2
            rw=min(.225,w/(n*4.1))
            windows=[((i-(n-1)*.5)*(w*.72/n),rw,eave*.29,eave*.74,
                      min(.57,eave*.16),"tracery" if n==3 and i==1 else "lancet") for i in range(n)]
            rows=[(None,windows)]
        wall(m,f,-half,half,0,top,rows,breaks=(0,))
        foot(m,f,half)
        if style=="nave" and end==0:
            # 三角门罩独立于楼梯和尖拱入口。
            for sign in (-1,1):
                points=[(sign*.73,1.64),(0,2.46),(0,2.61),(sign*.76,1.79)]
                m.extrude([f.at(u,z,.235) for u,z in points],-f.n*.22,"carving")
                for offset,depth in ((.51,.108),(.58,.067)):
                    axis=sign*offset
                    m.rod(f.at(axis,.65,depth),f.at(axis,1.60,depth),.042,"carving",8)
                    for z,w in ((.57,.072),(1.57,.071)):
                        m.rod(f.at(axis,z,depth),f.at(axis,z+.085,depth),w,"limestone",8,w*.91)
            rosette(m,f,0,5.97,.162,.038)
        for k in (-1,1):
            buttress(m,f,k*(half-.065),eave+.24,.32 if style=="nave" else .245,
                     .32 if style=="nave" else .23,pinnacle=True)
            if style=="nave" and end==0:
                t=k*(half-.065)
                for z in (.48,1.45,2.69,3.78):
                    m.band(f,t-.16,t+.16,z,z+.083,.14,.325,"carving")
    for s in (-1,1):
        f=frame(across*s,half)
        long=length*.5
        n=max(2,round(length/1.06))
        centers=[(i+.5)*length/n-long for i in range(n)]
        base=eave*.64 if style=="nave" else eave*.32
        values=[(u,min(.19,length/n*.22),base,eave-.65,.37,"lancet") for u in centers]
        wall(m,f,-long,long,0,lambda u:eave,[(None,values)])
        foot(m,f,long)
        cornice(m,f,long,eave)
        for i in range(1,n):
            buttress(m,f,i*length/n-long,eave+.10,.225,.22,pinnacle=(i%2==1))
    roof_gable(m,front,back,half,eave,ridge,style=="nave")
    return front


def aisle(m,name,sign,front_y,back_y,outer,outer_top,inner_top):
    m.name=name
    inner=1.03
    span=outer-inner
    center=sign*(inner+outer)*.5
    mid=(back_y+front_y)*.5
    half=(back_y-front_y)*.5
    face=Frame(sign*outer,mid,sign,0)
    count=max(2,round(2*half/.98))
    windows=[((i+.5)*2*half/count-half,.165,.70,outer_top-.65,.38,"lancet") for i in range(count)]
    wall(m,face,-half,half,0,lambda u:outer_top,[(None,windows)])
    foot(m,face,half)
    cornice(m,face,half,outer_top)
    for i in range(count+1):
        t=max(-half+.08,min(half-.08,-half+i*2*half/count))
        buttress(m,face,t,outer_top+(.43 if front_y<0 and 0<i<count else .11),.255,.25,pinnacle=True)
    ends=[]
    for y,ny in ((front_y,-1),(back_y,1)):
        f=Frame(center,y,0,ny)
        def top(u):
            amount=(abs(f.at(u,0).x)-inner)/span
            return inner_top+(outer_top-inner_top)*amount
        specs=[(u,.17,.71,2.28,.42,"lancet") for u in (-span*.25,span*.25)]
        wall(m,f,-span*.5,span*.5,0,top,[(None,specs)])
        foot(m,f,span*.5)
        if y==front_y:
            buttress(m,f,0,top(0)+.04,.20,.20,pinnacle=False)
        panel(m,f,[(-span*.5,top(-span*.5)-.10),(span*.5,top(span*.5)-.10),
                   (span*.5,top(span*.5)+.03),(-span*.5,top(-span*.5)+.03)],"carving",.035)
        ends.append(f)
    a=(sign*(outer+.075),front_y-.075,outer_top+.045)
    b=(a[0],back_y+.075,a[2])
    c=(sign*(inner-.02),b[1],inner_top+.03)
    d=(c[0],a[1],c[2])
    roof_plane(m,a,b,c,d)
    m.rod(a,b,.038,"lead")


def flying_arch(m,side,y):
    # 各段拱石由下弦起拱，瓦面之上的空隙透光，外端搭在翼廊的墩帽上。
    f=Frame(0,y,0,-1)
    def at(t,lower=False):
        x=1.17+2.23*t
        z=4.68-1.47*t if not lower else 4.23-.31*t-1.00*t*t
        return (side*x,z)
    for i in range(10):
        a,b=i/10,(i+1)/10
        profile=[at(a),at(b),at(b,True),at(a,True)]
        m.extrude([f.at(u,z,.072) for u,z in profile],-f.n*.144,"shadow")
        a+=.002
        b-=.002
        front=[at(a),at(b),at(b,True),at(a,True)]
        m.extrude([f.at(u,z,.081) for u,z in front],-f.n*.162,
                  "carving" if i%4==0 else "limestone")
    m.rod(f.at(*at(0)),f.at(*at(1)),.048,"carving",4)


def upper_chapel(m):
    m.name="西北高祈祷堂与角尖"
    x,y,w,length=-1.10,3.16,1.39,1.81
    base,eave,ridge=5.04,6.68,7.53
    front,back=Frame(x,y-length/2,0,-1),Frame(x,y+length/2,0,1)
    for f in (front,back):
        top=lambda u:eave+(ridge-eave)*(1-2*abs(u)/w)
        opening=[(u,.105,5.62,6.32,.26,"lancet") for u in (-.26,.26)]
        wall(m,f,-w/2,w/2,base,top,[(None,opening)],breaks=(0,))
        for side in (-1,1):
            p=f.at(side*(w/2-.045),0,.01)
            m.turned(p.x,p.y,[(base,.099),(eave-.08,.09),(eave+.05,.149),
                              (eave+.16,.11)],4,"limestone",pi/4)
            pinnacle_at(m,p.x,p.y,eave+.13,.12,.84)
    for s in (-1,1):
        f=Frame(x+s*w/2,y,s,0)
        wall(m,f,-length/2,length/2,base,lambda u:eave,
             [(None,[(u,.105,5.72,6.25,.26,"lancet") for u in (-.41,.17,.62)])])
        cornice(m,f,length/2,eave)
    roof_gable(m,front,back,w/2,eave,ridge,False)


def steps(m,f):
    m.name="正门石阶与压顶"
    for i in range(8):
        top=.55*(i+1)/8
        depth=1.15-i*.14
        m.band(f,-.82,.82,0,top,depth-.145,depth,"limestone" if i%3 else "carving")
        # 级鼻有石匠倒钝的前缘，伸出踢面；踏步由错缝的两块长石组成。
        split=.19 if i%2 else -.24
        for left,right in ((-.82,split-.006),(split+.006,.82)):
            nose=[(depth-.12,top+.012),(depth+.017,top+.012),
                  (depth+.026,top),(depth+.019,top-.021),(depth-.012,top-.026)]
            m.extrude([f.at(left,z,d) for d,z in nose],f.t*(right-left),"carving")
    m.band(f,-.83,.83,0,.55,-.025,.19,"limestone")
    for sign in (-1,1):
        u=sign*.875
        shape=[f.at(u-.065,0,.025),f.at(u-.065,0,1.19),
               f.at(u-.065,.175,1.19),f.at(u-.065,.70,.12),f.at(u-.065,.70,.025)]
        m.extrude(shape,f.t*.13,"ashlar")
        m.rod(f.at(u,.17,1.19),f.at(u,.71,.09),.079,"carving",4)


def tower(m,name,x,y,r,top,tip,shape,role,base=0):
    m.name=name
    if shape=="square":
        c=r*.35
        outline=[(-r+c,-r),(r-c,-r),(r,-r+c),(r,r-c),(r-c,r),(-r+c,r),(-r,r-c),(-r,-r+c)]
    else:
        outline=[(r*cos(pi/8+i*tau/8),r*sin(pi/8+i*tau/8)) for i in range(8)]
    def contour(z,scale):
        return [Vector((x+u*scale,y+v*scale,z)) for u,v in outline]
    def collar(z,profiles,swatch):
        rings=[contour(z+h,sc) for h,sc in profiles]
        for a,b in zip(rings,rings[1:]):
            for i in range(8):
                j=(i+1)%8
                m.face([a[i],a[j],b[j],b[i]],swatch)
    for i,(a,b) in enumerate(zip(contour(0,1),contour(0,1)[1:]+contour(0,1)[:1])):
        mid=(a+b)*.5
        axis=(b-a).normalized()
        n=axis.cross(UP)
        f=Frame(mid.x,mid.y,n.x,n.y)
        half=(b-a).length*.5
        window_width=half*(.43 if shape=="square" else .49)
        specs=[]
        if role=="great" and i%2==0:
            for low,spring,rise,kind in ((3.0,4.78,.39,"lancet"),(5.59,6.80,.38,"lancet"),
                                         (7.76,8.90,.37,"belfry")):
                # 成对的细长钟楼窗分开两条竖向轴线。
                centers=(-half*.42,half*.42) if low>7 else (0,)
                specs.append((spring+rise+.13,[(u,window_width*(.52 if len(centers)==2 else .80),
                                               low,spring,rise,kind) for u in centers]))
        elif role=="needle":
            specs=[(top-2.20,[(0,window_width*.75,top-4.05,top-2.64,.27,"slit")]),
                   (None,[(0,window_width,top-1.85,top-.76,.31,"belfry")])]
        elif role=="lantern":
            specs=[(None,[(0,window_width*.88,top-1.00,top-.52,.26,"belfry")])]
        elif role=="watch":
            specs=[(top-1.74,[(0,window_width*.70,.93,2.53,.36,"slit")]),
                   (None,[(0,window_width,top-1.43,top-.65,.33,"lancet")])]
        elif role=="turret":
            specs=[(None,[(0,window_width*.85,top-1.16,top-.56,.24,"slit")])]
        wall(m,f,-half,half,base,lambda u:top,specs)
    if base==0:
        collar(0,[(0,1.09),(.27,1.09),(.36,1.02),(.40,1)],"foot")
    belts=([5.30,7.42] if role=="great" else [top-2.12] if role in ("watch","needle") else [])
    for z in belts:
        collar(z,[(0,1),(.05,1.07),(.12,1.07),(.17,1)],"carving")
    collar(top-.32,[(0,1),(.055,1.075),(.11,1.10),(.15,1.055),(.235,1.055),
                     (.26,1.14),(.34,1.14),(.38,1.05)],"carving")
    if role=="great":
        for sx in (-1,1):
            for sy in (-1,1):
                cx,cy=x+sx*r*.87,y+sy*r*.87
                m.turned(cx,cy,[(.1,.117),(7.43,.113),(7.50,.142),(7.63,.101),
                                (top+.07,.101)],4,"limestone",pi/4)
                pinnacle_at(m,cx,cy,top-.04,.134,.97)
    eave=top+.055
    low=contour(eave,1.08)
    crown=Vector((x,y,tip-.16))
    for i in range(8):
        a,b=low[i],low[(i+1)%8]
        roof_plane(m,a,b,crown,crown)
        m.rod(a,crown,.022 if role in ("great","watch","lantern") else .016,"lead",5,.005)
    # 铅皮收尖，顶部小段红铜避雷针。尖塔各棱线不被合并磨圆。
    m.turned(x,y,[(tip-.22,.035),(tip-.12,.047),(tip-.082,.026),(tip,.007)],8,"bronze")


def build_city(m):
    front=hall(m,"中殿、三联窗及正门",0,-.40,2.38,7.30,4.78,6.69,style="nave")
    hall(m,"横厅与山墙",0,.72,2.08,8.92,3.87,5.26,axis=(1,0),style="transept")
    hall(m,"北侧高厅与钟廊",.04,2.89,2.12,6.24,5.02,6.43,axis=(1,0))
    hall(m,"北端后殿",.05,3.85,2.12,1.68,4.18,5.46)
    upper_chapel(m)
    for s,label in ((-1,"西"),(1,"东")):
        aisle(m,label+"前翼廊",s,-3.58,-.12,3.36,2.86,3.96)
        aisle(m,label+"后翼廊",s,1.53,3.70,3.51,3.24,4.08)
        m.name=label+"侧飞扶壁"
        for y in (-3.58+3.46/3,-3.58+2*3.46/3):
            flying_arch(m,s,y)
    steps(m,front)
    tower(m,"西北主钟塔",-2.49,2.71,.667,9.51,13.15,"square","great")
    tower(m,"东侧次高尖塔",2.93,-.13,.55,8.38,11.28,"octagon","needle")
    tower(m,"交叉厅八角采光塔",.31,1.03,.79,7.28,9.11,"octagon","lantern",base=5.10)
    tower(m,"东南瞭望塔",3.94,-1.79,.64,5.13,7.47,"octagon","watch")
    tower(m,"西南阶前角楼",-3.45,-3.46,.56,2.20,3.68,"octagon","turret")
    tower(m,"东南阶前小角楼",3.78,-3.50,.48,1.88,2.97,"octagon","turret")
