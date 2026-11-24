"""高低相交的两座仓房、屋面交易牌、前门和进货拱廊。"""
import math

from grain_mesh import (Face, Opening, Part, R, UP, V, beam, extrude, leaf, masonry,
                        peg, rect, rim, roof, slab, stone, surround, tube, window)

TX, TW, TF, TB = -7.64, 3.43, .94, 10.14
TE, TR = 10.47, 14.96
HL, HR, HF, HB = TX+TW-.07, 10.82, 1.58, 10.12
HE, HH = 6.23, 10.60
HY, RUN = (HF+HB)/2, (HB-HF)/2+.59
SILL, BOTTOM = .51, -4.78
PITCH = (HH-HE)/RUN
INCLINE = V((0,1,PITCH)).normalized()
SIGN_NORMAL = V((0,-PITCH,1)).normalized()
SIGN_CENTER = V((3.79, HY-RUN*.50, HE+(HH-HE)*.50))
SIGN_W, SIGN_H = 12.50, 4.29


def sp(u,v,d):
    return SIGN_CENTER+V((u,0,0))+INCLINE*v+SIGN_NORMAL*d


def plank_door(p,f,opening):
    x,r=opening.x,opening.r
    f.plate(p,opening.outline,-.24,.14,"tar")
    for i in range(10):
        a,b=x-r+2*r*i/10+.016,x-r+2*r*(i+1)/10-.016
        if i in (4,5):
            if i==4: b-=.027
            else: a+=.027
        outline=[(a,opening.bottom+.04),(b,opening.bottom+.04),(b,opening.head(b)-.025),(a,opening.head(a)-.025)]
        f.plate(p,outline,-.112,.135,"door" if i%4 else "oak",
                [(.07,.18),(.07,.46),(.93,.46),(.93,.18)])
    for s in (-1,1):
        for z in (opening.bottom+.57,opening.spring-.18):
            start=x+s*(r-.065)
            end=x+s*(r*.54)
            f.member(p,(start,z),(end,z),.11,.097,"iron",-.02,.014)
            peg(p,f.at(end,z,.030),f.n,.055)
        pos=f.at(x+s*.34,opening.bottom+1.47,-.018)
        peg(p,pos,f.n,.091)
        rim(p,[pos+f.t*(.102*math.cos(j*math.tau/14))+UP*(-.105+.121*math.sin(j*math.tau/14))
               +f.n*.075 for j in range(14)],.026,"iron",6)


def upper_frame(p,f,span,bottom,eave,ridge):
    half=span/2
    def top(x): return eave+(ridge-eave)*(1-abs(x)/half)
    n=round(span/.49)
    for i in range(n):
        a=-half+span*i/n+.010
        b=-half+span*(i+1)/n-.010
        if min(top(a),top(b))<=bottom+.028: continue
        f.plate(p,[(a,bottom),(b,bottom),(b,top(b)),(a,top(a))],-.055,.22,
                R.choice(("wall","wall","wall","door")),
                [(0,.09),(0,.30),(.94,.30),(.94,.09)])
    for z in sorted({bottom+.16,eave-.11}):
        f.member(p,(-half-.11,z),(half+.11,z),.35,.39,"oak",.095,.046)
    for s in (-1,1):
        f.member(p,(s*(half-.10),eave-.07),(0,ridge-.13),.30,.35,"oak",.13,.04)
    for x in (-half+.12,-span*.16,0,span*.16,half-.12):
        z=top(x)-.20
        if z>bottom+.42:
            f.member(p,(x,bottom+.12),(x,z),.27,.33,"oak",.11,.036)
    if eave>bottom+1.6:
        for s in (-1,1):
            f.member(p,(s*(half-.36),bottom+.37),(s*span*.17,eave-.25),.30,.25,"oak",.08,.032)
        for x in (-half+.18,-span*.16,span*.16,half-.18):
            for z in (bottom+.18,eave-.11):
                peg(p,f.at(x,z,.305),f.n,.072,"iron")


def side_boards(p,f,length,bottom,top):
    n=round(length/.52)
    for i in range(n):
        a,b=length*i/n+.012,length*(i+1)/n-.012
        f.plate(p,rect(a,b,bottom,top),-.04,.22,R.choice(("wall","wall","door")),
                [(.03,.13),(.03,.37),(.94,.37),(.94,.13)])
    for z in (bottom+.14,top-.15):
        f.member(p,(-.11,z),(length+.11,z),.31,.39,"oak",.102)
    for i in range(4):
        a=i*length/3
        f.member(p,(a,bottom),(a,top),.27,.40,"oak",.092,.032)
        if i<3:
            f.member(p,(a+.18,bottom+.31),(a+length/3-.18,top-.29),.25,.22,"oak",.07,.028)


def quoins(p,x,y,sx,sy,top):
    # 转角用丁顺交替的整块包石扣住相邻两面墙，底部轻微放脚。
    count=round((top-BOTTOM)/.79)
    for i in range(count):
        z=BOTTOM+(top-BOTTOM)*(i+1)/count-.014
        leg,short=(.88,.46) if i%2 else (.48,.92)
        toe=.12+.16*max(0,(2.9-z)/7.7)
        x0,x1=sorted((x-sx*toe,x+sx*leg))
        y0,y1=sorted((y-sy*toe,y+sy*short))
        c=.056
        ring=[(x0+c,y0),(x1-c,y0),(x1,y0+c),(x1,y1-c),(x1-c,y1),(x0+c,y1),(x0,y1-c),(x0,y0+c)]
        tile="tide" if z< -2.65 else "salt" if z< -1.88 else R.choice(("dressed","stone","honey"))
        low=z-(top-BOTTOM)/count+.027
        center=V(((x0+x1)*.5,(y0+y1)*.5))
        inset=[V(q)+(center-V(q)).normalized()*.034 for q in ring]
        loops=[p.verts([(a,b,h) for a,b in outline])
               for outline,h in ((inset,low),(ring,low+.031),(ring,z-.036),(inset,z))]
        caps=[(.12+(a-x0)*.41,.15+(b-y0)*.41) for a,b in inset]
        p.face(loops[0][::-1],tile,caps[::-1])
        p.face(loops[-1],tile,caps)
        for j in range(8):
            k=(j+1)%8
            width=(V(ring[j])-V(ring[k])).length
            u,v=R.uniform(.07,.38),R.uniform(.07,.38)
            for index,(h0,h1) in enumerate(((low,low+.031),(low+.031,z-.036),(z-.036,z))):
                a,b=loops[index:index+2]
                u1=u+width*.44
                v0,v1=v+(h0-low)*.45,v+(h1-low)*.45
                p.face([a[j],a[k],b[k],b[j]],tile,[(u,v0),(u1,v0),(u1,v1),(u,v1)])


def building():
    stonework, timber, covers = Part("逐皮砌石与拱券"),Part("仓房木架与板门"),Part("交叉双坡鱼鳞板岩屋面")
    interior=Part("拱廊内部与进货木箱")
    tower=Face(TX,TF,1,0)
    back=Face(TX,TB,-1,0)
    entry=Opening(0,1.68,SILL,3.17,1.66,True,6)
    rear=Opening(0,.93,SILL,2.87,.49,False,5)
    for f,h in ((tower,entry),(back,rear)):
        masonry(stonework,f,-TW,TW,BOTTOM,6.43,(h,),height=.75,length=1.53,marine=True)
        surround(stonework,f,h,.41,"dressed",.155)
        upper_frame(timber,f,2*TW,6.48,TE,TR-.15)
        for x in (-2.90,-1.88,-.76,.76,1.88,2.90):
            beam(timber,f.at(x,6.39,.13),f.at(x,6.69,.40),.27,.37,"oak",.045,up=f.n)
    plank_door(timber,back,rear)
    # 洞内有侧壁、顶拱内腹和夯石地坪，海侧视线可进入塔内。
    for a,b in zip(entry.outline,entry.outline[1:]+entry.outline[:1]):
        vs=[tower.at(*a,-.40),tower.at(*b,-.40),tower.at(*b,-1.26),tower.at(*a,-1.26)]
        extrude(interior,vs,tower.n*.06,"shadow")
    deep=Face(TX,TF+3.92,1,0)
    deep.plate(interior,rect(-TW,TW,.2,6.43),0,.17,"shadow")
    slab(interior,TX-TW+.13,TX+TW-.13,TF-.40,TF+4.05,SILL-.018,.25,"grey")
    for i in range(3):
        x=TX-1.67+i*1.113
        slab(interior,x,x+1.100,TF-.49,TF+.42,SILL+.025,.29,"dressed")
    for cx,cy,w,h in ((TX+.57,TF+2.84,1.5,1.30),(TX-.77,TF+3.12,1.26,1.10),(TX+.69,TF+2.92,1.05, .87)):
        z0=SILL+(1.30 if h==.87 else 0)
        for i in range(4):
            y=cy-w*.45+i*w*.30
            beam(interior,(cx-w/2,y,z0+h*.5),(cx+w/2,y,z0+h*.5),
                 w*.30-.021,h,"wall",.036)
        f=Face(cx,cy-w*.60,1,0)
        for z in (z0+.09,z0+h-.09):
            f.member(interior,(-w*.5,z),(w*.5,z),.13,.14,"oak",.07,.017)
        f.member(interior,(-w*.39,z0+.13),(w*.39,z0+h-.13),.15,.11,"oak",.076,.02)
    for x,front,dy in ((TX-TW,TB,-1),(TX+TW,TF,1)):
        f=Face(x,front,0,dy)
        masonry(stonework,f,0,TB-TF,BOTTOM,6.44,marine=True,height=.78,length=1.65)
        side_boards(timber,f,TB-TF,6.46,TE)
    for yy,sy in ((TF,1),(TB,-1)):
        quoins(stonework,TX-TW,yy,1,sy,6.43)
    quoins(stonework,TX+TW,TF,-1,1,6.43)
    roof(timber,covers,(TX,(TB+TF)/2),(0,1,0),(1,0,0),TB-TF+1.24,TW+.49,TE,TR)
    face=Face(0,HF,1,0)
    low_back=Face(0,HB,-1,0)
    door=Opening(3.48,1.78,SILL,3.91,.68,False,7)
    windows=[Opening(x,.51,2.08,3.33,.65,True,4) for x in (-.55,8.24)]
    masonry(stonework,face,HL,HR,BOTTOM,HE-.08,[door,*windows],height=.69,length=1.40,marine=True)
    surround(stonework,face,door,.34,"dressed",.16)
    plank_door(timber,face,door)
    for h in windows: window(stonework,face,h)
    windows=[Opening(x,.53,2.06,3.29,.67,True,4) for x in (-7.77,-.32)]
    hatch=Opening(-3.52,1.18,SILL,3.07,.53,False,5)
    masonry(stonework,low_back,-HR,-HL,BOTTOM,HE-.08,[hatch,*windows],height=.73,length=1.48,marine=True)
    surround(stonework,low_back,hatch,.31)
    plank_door(timber,low_back,hatch)
    for h in windows: window(stonework,low_back,h)
    # 窄门槛搭岸，后墙直接收在岸线，潮水可进入前廊木桩之间。
    slab(stonework,2.18,4.87,HB-.04,HB+.48,SILL,.51,"dressed")
    end=Face(HR,HY,0,1)
    win=Opening(0,.66,1.92,3.33,.91,True,5)
    masonry(stonework,end,-(HB-HF)/2,(HB-HF)/2,BOTTOM,HE,(win,),height=.70,length=1.49,marine=True)
    window(stonework,end,win)
    for yy,sy in ((HF,1),(HB,-1)):
        quoins(stonework,HR,yy,-1,sy,HE)
    upper_frame(timber,end,HB-HF,HE-.03,HE+.16,HH-.14)
    for f,l,r in ((face,HL,HR),(low_back,-HR,-HL)):
        f.member(timber,(l,HE-.16),(r,HE-.16),.32,.42,"oak",.15,.037)
        for i in range(9):
            x=l+.45+i*(r-l-.90)/8
            beam(timber,f.at(x,HE-.57,.05),f.at(x,HE-.19,.35),.24,.29,"oak",.029,up=f.n)
    roof(timber,covers,((HL+HR)/2,HY),(1,0,0),(0,1,0),HR-HL+1.01,RUN,HE,HH)
    return stonework,timber,covers,interior


def emblem():
    p=Part("三穗铸铜麦徽")
    # 三支麦穗在下端束拢。颖壳沿穗轴交错着生，芒尖伸出穗头。
    center=3.19
    for k,(angle,length) in enumerate(((-.62,3.17),(.025,3.62),(.63,3.13))):
        along=V((math.sin(angle),math.cos(angle)))
        cross=V((math.cos(angle),-math.sin(angle)))
        start=V((center+(k-1)*.105,-1.79))
        def point(t,s=0,d=.579):
            u,v=start+along*t+cross*s
            return sp(u,v,d)
        tube(p,[point(0),point(length*.34,.024),point(length*.72),point(length-.06)],
             [.093,.085,.068,.026],"gilt",9)
        for row in range(4):
            t=length*(.32+.123*row)
            for side in (-1,1):
                a=t+(.048 if side==1 else 0)
                spread=(.46 if k==1 else .405)*(1-row*.055)
                leaf(p,point(a,side*.035,.592),point(a+.64,side*spread,.625),
                     .205 if k==1 else .185,.125,SIGN_NORMAL,"gilt")
                if row>=2:
                    tube(p,[point(a+.49,side*spread*.88,.62),point(a+.77,side*(spread+.075),.60)],
                         [.039,.009],"gilt",5)
        leaf(p,point(length-.80,0,.60),point(length+.02,0,.62),.19,.14,SIGN_NORMAL,"gilt")
    return p
