"""贸易金帐。主帐的放射分瓣和两个低摊棚从高处围出梯形的露天市庭。"""
def canopy(cx,cy,rx,ry,hem,rise,count,large):
    # 轮廓在撑杆处张起，在两根撑杆之间下垂，篷面沿经向内凹。
    def pos(sector,u,t):
        a=(sector+u)*tau/count+pi/count
        scallop=sin(pi*u)
        radius=.014+.986*t
        contraction=1-.12*scallop*t*t
        edge=hem-.095*scallop*(1 if large else .55)
        height=edge+(hem+rise-edge)*(1-t)**1.50+.027*scallop*sin(pi*t)
        return Vector((cx+rx*radius*contraction*cos(a),cy+ry*radius*contraction*sin(a),height))
    for i in range(count):
        k=('player','yellow','player','linen')[i%4] if large else ('player','linen')[i%2]
        grid=[[pos(i,j/6,t) for j in range(7)] for t in (0,.10,.24,.42,.59,.75,.89,1)]
        for n,(row,nxt) in enumerate(zip(grid,grid[1:])):
            for j in range(6):
                q=[row[j],nxt[j],nxt[j+1],row[j+1]]
                textile_face(q,k,[(j/6,n/7),(j/6,(n+1)/7),((j+1)/6,(n+1)/7),((j+1)/6,n/7)])
                face([p-Vector((0,0,.013)) for p in reversed(q)],k,True)
        path([grid[j][0]+Vector((0,0,.008)) for j in range(len(grid))],.012 if large else .008,'linen')
        rim=grid[-1]
        # 双层翻边和垂花；下面独立内衬，逆向旋转时也能看到布面。
        for j in range(6):
            a,b=rim[j:j+2];dropa=.075+.043*sin(j*pi/6);dropb=.075+.043*sin((j+1)*pi/6)
            face([a,b,b-Vector((0,0,dropb)),a-Vector((0,0,dropa))],k)
        path(rim,.014 if large else .009,'brass')
        path([p-Vector((0,0,.08+.04*sin(j*pi/6))) for j,p in enumerate(rim)],.009,'linen')
        # 帐下边缘与帐心的轮辐作明暗较深的木肋。
        if i%2==0:
            support=pos(i,0,.70)-Vector((0,0,.077))
            column_base(support.x,support.y,.057,support.z-.057,.030)
            # 梁随布面的垂度下弯，所有撑杆留在篷布内侧。
            path([pos(i,0,t)-Vector((0,0,.058)) for t in (.20,.36,.52,.70,.84,.98)],.016,'wood')
            rod(support-Vector((0,0,.11)),pos(i,0,.83)-Vector((0,0,.064)),.018,'wood',.013)
            lantern(support.x,support.y,support.z-.14)
    column_base(cx,cy,.05,hem+rise-.05,.050 if large else .030)
    column(cx,cy,[(hem+rise-.018,.080,.080),(hem+rise+.024,.068,.068),(hem+rise+.07,.030,.030)],'brass',20)
    bulb((cx,cy,hem+rise+.085),(.044,.044,.052),'brass',16)

def stall(x,y,side):
    # 前低后高的卷棚朝市庭开门。四束染布和果筐露在檐口外。
    for a in (-.15,.15):
        for b in (-.21,.21):
            column_base(x+a,y+b,.047,.527 if a*side>0 else .360,.018)
    for i in range(7):
        ya=y-.245+i*.07;yb=ya+.07
        def strip(t,v):
            return Vector((x+side*(.19-.39*t),v,.61-.23*t-.06*sin(pi*t)))
        for j in range(5):
            t=j/5;u=(j+1)/5;q=[strip(t,ya),strip(t,yb),strip(u,yb),strip(u,ya)]
            k='player' if i in (0,3,6) else 'linen' if i%2 else 'yellow'
            sheet(q,k,.014)
        low=[strip(1,ya),strip(1,yb)]
        valance=[low[0],low[1],low[1]-Vector((0,0,.052)),low[0]-Vector((0,0,.052))]
        loft([valance,[p+Vector((side*.008,0,0)) for p in valance]],k)
    rod((x+side*.19,y-.25,.618),(x+side*.19,y+.25,.618),.025,'wood')
    box(x,y,.044,.31,.43,.175,'wood')
    box(x-side*.025,y,.22,.36,.455,.026,'plank')
    for j in range(4):
        yy=y-.16+j*.103
        if side<0:rod((x-.04,yy,.284),(x+.13,yy,.284),.039,('blue','linen','green','player')[j],n=12)
        else:
            box(x-.02,yy,.246,.215,.095,.041,'plank')
            for k in range(3):bulb((x-.086+k*.062,yy,.292),(.036,.038,.034),'yellow' if j%2 else 'green',10)
    crate(x,y-.28,.047,.132);barrel(x-side*.18,y-.19,.046,.052)

def market():
    pavement(0,0,1.52,1.53)
    threshold(0,-.72,.38,.25,.046,3,.30)
    # 大秤的两只挂盘从高帐下探出来，后排铺面退到阴影里。
    podium(0,.42,1.33,.50,.11)
    masonry(0,.62,.11,1.14,.11,.30)
    for x in (-.48,0,.48):
        box(x,.48,.115,.36,.20,.175,'wood')
        box(x,.48,.29,.389,.233,.030,'plank')
        for u in (-.11,-.05,.01,.07,.13):
            rod((x+u,.42,.35),(x+u,.52,.35),.040,'linen' if u<0 else 'green',n=10)
    canopy(0,.335,1.01,.60,.91,.64,12,True)
    exchange_tower(0,.335)
    for side in (-1,1):
        stall(side*.555,-.39,side)
    # 中央留空，只用小秤台和嵌铜边石定出通向前门的轴线。
    column(0,-.39,[(.045,.136,.136),(.074,.142,.142),(.095,.117,.117)],'trim',12)
    rod((0,-.39,.094),(0,-.39,.51),.024,'brass',.017)
    rod((-.144,-.39,.46),(.144,-.39,.49),.017,'brass')
    for side in (-1,1):
        x=side*.134
        for off in (-.045,.045):rod((x,-.39,.46+side*.015),(x+off,-.39,.31),.005,'brass',n=6)
        column(x,-.39,[(.291,.017,.017),(.308,.055,.055),(.32,.058,.058)],'brass',16)
def exchange_tower(cx,cy):
    # 帐布张在交易楼的腰部，下层是秤房，上层可穿透的八面钟楼从市庭就能看见。
    def ring(z,r):return [(cx+r*cos(tau*i/8+pi/8),cy+r*sin(tau*i/8+pi/8),z) for i in range(8)]
    def cornice(z,r):
        loft([ring(z,r-.05),ring(z+.035,r),ring(z+.068,r)],'trim')
        loft([ring(z+.068,r+.008),ring(z+.093,r+.006)],'brass')
    loft([ring(.05,.36),ring(.17,.38),ring(.24,.334),ring(1.88,.318)],'stone')
    cornice(.45,.38);cornice(1.49,.393)
    for j in range(8):
        a=tau*j/8+pi/8;b=a+tau/8
        c=(a+b)/2
        def side(t,r,z):
            return Vector((cx+r*(cos(a)*(1-t)+cos(b)*t),cy+r*(sin(a)*(1-t)+sin(b)*t),z))
        # 每面券窗由真实拱石和内退的暗窗形成，四面转换视角仍有层次。
        outline=[side(.19,.326,1.59),side(.81,.326,1.59),side(.81,.326,1.79),side(.50,.326,1.86),side(.19,.326,1.79)]
        face(outline,'shadow')
        for k in range(7):
            t=k/7;v=(k+1)/7
            def arc(u,h):return side(.5+(.31+h)*cos(pi*u),.333,1.78+(.082+h*.24)*sin(pi*u))
            sheet([arc(t,0),arc(v,0),arc(v,.056),arc(t,.056)],'trim',.011)
        for u in (.20,.50,.80):
            rod(side(u,.335,1.592),side(u,.335,1.793),.010,'brass',n=8)
        # 价秤房八面各嵌一枚方孔铜钱。币面留透孔，与楼下的秤盘呼应。
        center=side(.5,.342,1.39);u=Vector((-sin(c),cos(c),0));v=Vector((0,0,1))
        normal=Vector((cos(c),sin(c),0))
        for k in range(24):
            t=tau*k/24;e=tau*(k+1)/24
            def coin(theta,outer):
                f=.078 if outer else .026/max(abs(cos(theta)),abs(sin(theta)))
                return center+f*(u*cos(theta)+v*sin(theta))+.012*normal
            q=[coin(t,False),coin(t,True),coin(e,True),coin(e,False)]
            loft([q,[point-.011*normal for point in q]],'brass')
        p=side(.0,.336,.31)
        column_base(p.x,p.y,.29,1.58,.032,'wood')
    cornice(1.88,.414)
    # 钟架、栏板、斗拱和挂钟，替代单薄的高旗杆。
    for j in range(8):
        a=tau*j/8+pi/8
        x=cx+.336*cos(a);y=cy+.336*sin(a)
        column_base(x,y,1.977,.694,.037)
        box(x,y,2.575,.104,.104,.052,'wood');box(x,y,2.625,.142,.142,.047,'brass')
        b=a+tau/8
        for z in (2.02,2.18):
            rod((x,y,z),(cx+.336*cos(b),cy+.336*sin(b),z),.017,'wood')
        for t in (.25,.5,.75):
            p=Vector((x,y,0)).lerp(Vector((cx+.336*cos(b),cy+.336*sin(b),0)),t)
            rod((p.x,p.y,2.02),(p.x,p.y,2.18),.010,'brass',n=8)
    rod((cx,cy,2.63),(cx,cy,2.51),.032,'iron')
    column(cx,cy,[(2.14,.176,.176),(2.169,.189,.189),(2.205,.161,.161),(2.43,.131,.131),(2.49,.094,.094),(2.515,.046,.046)],'brass',36,True)
    for h,rad in ((2.183,.176),(2.35,.148)):column(cx,cy,[(h,rad,rad),(h+.018,rad,rad)],'brass',32)
    bulb((cx,cy,2.12),(.039,.039,.051),'iron',14)
    for j in range(8):
        a=tau*j/8+pi/8
        p=Vector((cx+.371*cos(a),cy+.371*sin(a),2.681))
        path([p,p+Vector((.053*cos(a),.053*sin(a),-.014)),p+Vector((.090*cos(a),.090*sin(a),.039))],.016,'brass')
    # 八角楼冠有厚实檐口，径向肋与下方集市金帐呼应。
    loft([ring(2.66,.374),ring(2.697,.48),ring(2.74,.481)],'wood')
    for i in range(8):
        a=tau*i/8+pi/8;b=a+tau/8
        def roofpt(t,u):
            rad=.505*(1-t)+.055*t
            return Vector((cx+rad*((1-u)*cos(a)+u*cos(b)),cy+rad*((1-u)*sin(a)+u*sin(b)),2.748+.418*t**1.45+.039*abs(2*u-1)*(1-t)))
        for j in range(8):
            t=j/8;u=(j+1)/8
            textile_face([roofpt(t,0),roofpt(t,1),roofpt(u,1),roofpt(u,0)],'player' if i%2==0 else 'yellow',[(0,t),(1,t),(1,u),(0,u)])
        path([roofpt(j/12,0) for j in range(13)],.021,'brass')
    column(cx,cy,[(3.14,.068,.068),(3.19,.086,.086),(3.231,.040,.040)],'brass',20)
    pennant(cx,cy,3.26,.28)
