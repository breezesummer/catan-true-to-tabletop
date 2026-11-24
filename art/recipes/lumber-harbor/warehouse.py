"""临海吊楼与侧向石仓，两座屋脊互相垂直。屋面牌、原木浮雕与铸字均有实体厚度。"""
import math
from joinery import Part, RNG, V, UP, beam, grain, trunk, tube
from masonry import Face, Opening, ashlar, dress_opening, lancet, lay_wall, skin

TOWER_X=-8.00
TOWER_HALF=3.78
TOWER_FRONT=1.15
TOWER_BACK=9.91
SILL=6.42
TOWER_EAVE=10.67
TOWER_RIDGE=15.38
STORE_LEFT=-4.40
STORE_RIGHT=11.89
STORE_FRONT=1.55
STORE_BACK=9.91
STORE_MID=(STORE_FRONT+STORE_BACK)*.5
STORE_HALF=(STORE_BACK-STORE_FRONT)*.5
STORE_EAVE=5.91
STORE_RIDGE=10.72
FLOOR=.13
BED=-4.62


def pin(p,wall,u,s,z,r=.083,tile="iron"):
    tube(p,[wall.at(u,s-.024,z),wall.at(u,s+.014,z),wall.at(u,s+.043,z)],(r,r,r*.70),tile,9)


def boarded_end(p,wall,left,right,bottom,height):
    """山墙的每根板材一直裁到屋坡，板缝后的暗板防止漏光。"""
    mid=(left+right)/2
    n=max(3,round((right-left)/.59))
    pts=[wall.at(u,-.15,z) for u,z in ((left,bottom),(right,bottom),(right,height(right)),(mid,height(mid)),(left,height(left)))]
    skin(p,pts,-wall.out*.16,"tar",[(.02,.05),(.96,.05),(.96,.70),(.48,.93),(.02,.70)])
    for i in range(n):
        a=left+(right-left)*i/n+.018
        b=left+(right-left)*(i+1)/n-.018
        za,zb=max(bottom+.025,height(a)),max(bottom+.025,height(b))
        if max(za,zb)-bottom<.045:
            continue
        tex=grain(max(za,zb)-bottom,b-a)
        outline=[(a,bottom),(b,bottom),(b,zb),(a,za)]
        skin(p,[wall.at(u,.012,z) for u,z in outline],-wall.out*.21,
             RNG.choice(("wall","dock","oak","wall","dock")),[tex((z-bottom)/(max(za,zb)-bottom),(u-a)/(b-a)) for u,z in outline])


def timber_frame(p,wall,left,right,bottom,top,gable=False):
    mid=(left+right)/2
    for u in (left+.20,mid,right-.20):
        beam(p,wall.at(u,.21,bottom),wall.at(u,.21,top+.06),.41,.43,"oak",.054)
    for z in (bottom+.14,top-.02):
        beam(p,wall.at(left-.11,.23,z),wall.at(right+.11,.23,z),.49,.40,"trim",.058)
    # 交错的斜撑用榫入横梁，端部留钉头。
    for s in (-1,1):
        a=mid+s*(right-left)*.44
        b=mid+s*(right-left)*(.21 if gable else .12)
        beam(p,wall.at(a,.20,bottom+.33),wall.at(b,.20,top-.21),.34,.31,"trim",.044,up=wall.out)
        for u,z in ((a,bottom+.39),(b,top-.29)):
            pin(p,wall,u,.375,z,.065,"agedcut")
    for u in (left+.20,mid,right-.20):
        for z in (bottom+.14,top-.02):
            pin(p,wall,u,.47,z,.076,"agedcut")


def threshold(p,wall,c,w):
    ashlar(p,wall,[(c-w,FLOOR-.12),(c+w,FLOOR-.12),(c+w,FLOOR+.065),(c-w,FLOOR+.065)],
           "dressed",.32,.91,.045)


def quoins(p,wall,x,s,top):
    # 交替使用长短丁石咬住转角，迎水的底层保留潮湿色带。
    courses=round((top-BED)/.76)
    for row in range(courses):
        low=BED+(top-BED)*row/courses+.012
        high=BED+(top-BED)*(row+1)/courses-.017
        a,b=sorted((x-s*.090,x+s*(.72 if row%2 else 1.03)))
        tile=("tideStone" if high<-2.6 else ("buffStone","dressed","ashlar")[row%3])
        ashlar(p,wall,[(a,low),(b,low),(b,high),(a,high)],tile,.087,.98 if row%2 else .66,.061)


def store_doors(wood,hardware,wall,o):
    # 木门按石拱内缘裁切，板缝、中央搭缝与门后暗部相互独立。
    width=o.half*2
    profile=[(o.center-o.half,o.bottom),(o.center+o.half,o.bottom),*reversed(o.arc(20))]
    skin(wood,[wall.at(u,-.25,z) for u,z in profile],-wall.out*.18,"tar",
         [(.5+(u-o.center)/width*.9,.04+.90*(z-o.bottom)/(o.peak-o.bottom)) for u,z in profile])
    for i in range(12):
        a=o.center-o.half+width*i/12+.014
        b=o.center-o.half+width*(i+1)/12-.014
        za,zb=o.top(a)-.055,o.top(b)-.055
        lo=o.bottom+.06
        tex=grain(o.peak-lo,b-a)
        points=[(a,lo),(b,lo),(b,zb),(a,za)]
        skin(wood,[wall.at(u,-.085,z) for u,z in points],-wall.out*.17,
             RNG.choice(("door","door","oak","dock")),[tex((z-lo)/(o.peak-lo),(u-a)/(b-a)) for u,z in points],"tar")
    for s in (-1,1):
        edge=o.center+s*o.half
        for z in (.66,2.74):
            tip=edge-s*.96
            plate=[(edge,z-.072),(tip,z-.056),(tip-s*.15,z),(tip,z+.056),(edge,z+.072)]
            skin(hardware,[wall.at(u,-.048,h) for u,h in plate],-wall.out*.046,"iron",[(.1,.1),(.76,.1),(.89,.5),(.76,.82),(.1,.82)])
            for d in (.13,.78):
                pin(hardware,wall,edge-s*d,-.035,z,.047)
        u=o.center+s*.47
        plate=[(u,1.43),(u+.17,1.68),(u,1.94),(u-.17,1.68)]
        skin(hardware,[wall.at(x,-.032,z) for x,z in plate],-wall.out*.057,"iron",[(.5,0),(1,.5),(.5,1),(0,.5)])
        pin(hardware,wall,u,.015,1.72,.086,"brass")
        ring=[wall.at(u+.14*math.cos(i*math.tau/18),.048,1.54+.18*math.sin(i*math.tau/18)) for i in range(18)]
        tube(hardware,ring,.042,"iron",7,closed=True)
    threshold(wood,wall,o.center,o.half+.27)


def transit_arch(p,wood,wall,o):
    # 装卸通道具有拱腹和侧壁，门内能看到地板、木箱和后墙。
    ring=[(o.center-o.half,o.bottom),*o.arc(20),(o.center+o.half,o.bottom)]
    for (a,z),(b,h) in zip(ring,ring[1:]):
        pts=[wall.at(a,-.20,z),wall.at(b,-.20,h),wall.at(b,-3.65,h),wall.at(a,-3.65,z)]
        into_stone=(wall.u*(z-h)+UP*(b-a)).normalized()*.09
        skin(p,pts,into_stone,"greyStone",[(0,.1),(.22,.1),(.22,.93),(0,.93)])
    for j in range(7):
        x=o.center+(j-3)*.47
        beam(wood,wall.at(x,-.10,FLOOR-.02),wall.at(x,-3.90,FLOOR-.02),.45,.14,"dock",.023)
    beam(wood,wall.at(o.center-o.half-.12,-3.90,2.13),wall.at(o.center+o.half+.12,-3.90,2.13),.28,4.38,"tar",.025)
    # 两只收在阴影里的榫接板箱。
    for x,d,z,w in ((o.center+.69,-2.49,.69,1.15),(o.center-.58,-3.06,.51,.90)):
        for j in range(4):
            b=z+(j-1.5)*.25
            beam(wood,wall.at(x-w/2,d,b),wall.at(x+w/2,d,b),.85,.24,"wall",.025)
        for u in (x-w*.38,x+w*.38):
            beam(wood,wall.at(u,d+.45,z-.49),wall.at(u,d+.45,z+.50),.12,.10,"oak",.018)
        beam(wood,wall.at(x-w*.40,d+.46,z-.37),wall.at(x+w*.40,d+.46,z+.37),.11,.10,"trim",.018,up=wall.out)
    threshold(wood,wall,o.center,o.half+.25)


def roof(tiles,timbers,center,along,across,first,last,half,ridge,eave,over=.42):
    axis=V((*along,0)); span=V((*across,0)); center=V((*center,0))
    slope=(ridge-eave)/half
    run=half+over
    def pos(t,d,lift=0):
        return center+axis*t+span*d+UP*(ridge-abs(d)*slope+lift)
    for s in (-1,1):
        points=[pos(first,0),pos(last,0),pos(last,s*run),pos(first,s*run)]
        skin(timbers,points,-UP*.19,"tar",[(0,0),(1,0),(1,1),(0,1)])
        nrow=7
        spacing=run/nrow
        for row in range(nrow):
            toe=run-row*spacing
            head=max(.005,toe-1.47*spacing)
            count=max(5,round((last-first)/.77))
            marks=[first]
            for i in range(1,count+(row%2)):
                u=first+(i-(row%2)*.50+RNG.uniform(-.09,.09))*(last-first)/count
                if first+.19<u<last-.19:
                    marks.append(u)
            marks.append(last)
            for a,b in zip(marks,marks[1:]):
                a+=.012; b-=.013
                foot=toe+RNG.uniform(-.049,.052)
                chip=RNG.uniform(.045,.105)
                outline=[(a+.024,head),(b-.026,head),(b,foot-chip),(b-chip*.72,foot),
                         (a+chip*1.1,foot+RNG.uniform(-.025,.024)),(a,foot-chip*.84)]
                tilt=RNG.uniform(-.018,.014)
                pts=[pos(u,s*d,.074+.158*(d-head)/(foot-head)+tilt+.013*math.sin(math.pi*(u-a)/(b-a))) for u,d in outline]
                tex=grain((foot-head)*math.hypot(1,slope),b-a)
                uv=[tex((d-head)/(foot-head),(u-a)/(b-a)) for u,d in outline]
                tile=RNG.choice(("slate","slate","slate","blueSlate","blueSlate","heather"))
                skin(tiles,pts,-UP*.098,tile,uv)
        beam(timbers,pos(first,s*(run-.12),-.12),pos(last,s*(run-.12),-.12),.27,.33,"oak",.046)
        for t in (first-.032,last+.032):
            beam(timbers,pos(t,0,.048),pos(t,s*(run+.07),.042),.32,.28,"trim",.043)
    # 脊瓦有搭口、弧面和外翻的瓦唇；山头另装封脊盖。
    count=round((last-first)/1.10)
    for i in range(count):
        a=first+i*(last-first)/count-.035
        b=first+(i+1)*(last-first)/count+.030
        rings=[]
        for t,r in ((a,.255),(a+.09,.273),(b,.237)):
            pts=[center+axis*t+span*(math.cos(j*math.pi/8)*r)+UP*(ridge+.147+math.sin(j*math.pi/8)*r) for j in range(9)]
            rings.append(pts)
        for arow,brow in zip(rings,rings[1:]):
            for j in range(8):
                pts=[arow[j],brow[j],brow[j+1],arow[j+1]]
                skin(tiles,pts,-UP*.048,"ridge",[(.07,j/9),(.94,j/9),(.94,(j+1)/9),(.07,(j+1)/9)])
    for t in (first,last):
        beam(tiles,pos(t-.17,0,.23),pos(t+.17,0,.23),.56,.52,"ridge",.090,end="ridge")


def warehouse():
    stone,frame,tiles,metal=map(Part,("错缝石墙、门券与窗套","吊楼梁架、山墙与仓门","两组板岩屋面与分节脊瓦","铅窗、门环与锻铁件"))
    front=Face(0,TOWER_FRONT,(1,0))
    left,right=TOWER_X-TOWER_HALF,TOWER_X+TOWER_HALF
    enter=Opening(TOWER_X,1.48,FLOOR,2.62,1.94,True)
    lay_wall(stone,front,left,right,BED,SILL,[enter])
    dress_opening(stone,front,enter,.44,12,.145)
    transit_arch(stone,frame,front,enter)
    back=Face(0,TOWER_BACK,(-1,0))
    sideL=Face(left,0,(0,-1)); sideR=Face(right,0,(0,1))
    sideLight=Opening(-(TOWER_FRONT+TOWER_BACK)*.5,.47,1.53,3.21,.76,True)
    lay_wall(stone,sideL,-TOWER_BACK,-TOWER_FRONT,BED,SILL,[sideLight])
    lancet(stone,metal,sideL,sideLight)
    lay_wall(stone,back,-right,-left,BED,SILL)
    # 紧邻低仓的一面只需露出墙头。
    lay_wall(stone,sideR,TOWER_FRONT,STORE_FRONT,BED,SILL,block=.9)
    for wall,a,b in ((front,left,right),(back,-right,-left)):
        quoins(stone,wall,a,1,SILL)
        quoins(stone,wall,b,-1,SILL)
    for wall,a,b,gable in ((front,left-.11,right+.11,True),(back,-right-.11,-left+.11,True),
                           (sideL,-TOWER_BACK-.08,-TOWER_FRONT+.08,False),
                           (sideR,TOWER_FRONT-.08,TOWER_BACK+.08,False)):
        mid=(a+b)/2
        def ceiling(u):
            return TOWER_EAVE+(TOWER_RIDGE-TOWER_EAVE)*max(0,1-abs(u-mid)/((b-a)*.5)) if gable else TOWER_EAVE
        boarded_end(frame,wall,a,b,SILL+.16,ceiling)
        timber_frame(frame,wall,a,b,SILL+.12,TOWER_EAVE,gable)
        if gable:
            beam(frame,wall.at(mid,.20,TOWER_EAVE),wall.at(mid,.20,TOWER_RIDGE-.12),.32,.32,"trim",.040)
        for u in (a+.33,mid,b-.33):
            beam(frame,wall.at(u,-.13,SILL-.62),wall.at(u,.38,SILL+.15),.34,.39,"oak",.041)
    roof(tiles,frame,(TOWER_X,0),(0,1),(1,0),TOWER_FRONT-.52,TOWER_BACK+.40,
         TOWER_HALF,TOWER_RIDGE,TOWER_EAVE,.39)

    front=Face(0,STORE_FRONT,(1,0)); east=Face(STORE_RIGHT,0,(0,1))
    door=Opening(4.02,2.12,FLOOR,3.48,.78)
    windows=[Opening(u,.47,2.10,3.50,.77,True) for u in (-.80,8.98)]
    lay_wall(stone,front,STORE_LEFT,STORE_RIGHT,BED,STORE_EAVE,[door,*windows])
    dress_opening(stone,front,door,.39,11,.134)
    store_doors(frame,metal,front,door)
    for o in windows:
        lancet(stone,metal,front,o)
    eastLight=Opening(STORE_MID,.58,1.87,3.27,.94,True)
    lay_wall(stone,east,STORE_FRONT,STORE_BACK,BED,STORE_EAVE,[eastLight])
    lancet(stone,metal,east,eastLight)
    rear=Face(0,STORE_BACK,(-1,0))
    rearLights=[Opening(u,.47,2.14,3.52,.74,True) for u in (-6.92,-.15)]
    lay_wall(stone,rear,-STORE_RIGHT,-STORE_LEFT,BED,STORE_EAVE,rearLights)
    for o in rearLights:
        lancet(stone,metal,rear,o)
    quoins(stone,front,STORE_RIGHT,-1,STORE_EAVE)
    quoins(stone,rear,-STORE_RIGHT,1,STORE_EAVE)
    # 山墙角料及出挑的梁头独立投影。
    ceiling=lambda y: STORE_RIDGE-(STORE_RIDGE-STORE_EAVE)*abs(y-STORE_MID)/STORE_HALF
    boarded_end(frame,east,STORE_FRONT,STORE_BACK,STORE_EAVE-.08,ceiling)
    beam(frame,east.at(STORE_FRONT-.09,.18,STORE_EAVE),east.at(STORE_BACK+.09,.18,STORE_EAVE),.40,.39,"trim")
    beam(frame,east.at(STORE_MID,.16,STORE_EAVE+.05),east.at(STORE_MID,.16,STORE_RIDGE-.14),.32,.34,"oak")
    for s in (-1,1):
        beam(frame,east.at(STORE_MID+s*2.59,.14,STORE_EAVE+.11),east.at(STORE_MID+s*.75,.14,STORE_EAVE+2.26),
             .26,.27,"oak",.038,up=east.out)
    for y in (STORE_FRONT,STORE_BACK):
        for i in range(9):
            x=STORE_LEFT+.53+i*(STORE_RIGHT-STORE_LEFT-1.06)/8
            sign=-1 if y==STORE_FRONT else 1
            beam(frame,(x,y-sign*.06,STORE_EAVE-.33),(x,y+sign*.42,STORE_EAVE-.84),.29,.26,"oak",.04)
    roof(tiles,frame,(0,STORE_MID),(1,0),(0,1),STORE_LEFT+.03,STORE_RIGHT+.42,
         STORE_HALF,STORE_RIDGE,STORE_EAVE,.43)
    return stone,frame,tiles,metal


# 牌面平行于石仓临海的屋坡。左边是铸字，右边留出一整捆原木的浮雕位置。
SLOPE=(STORE_RIDGE-STORE_EAVE)/STORE_HALF
UP_SLOPE=V((0,1,SLOPE)).normalized()
NORMAL=V((0,-SLOPE,1)).normalized()
SIGN_CENTER=V((4.12,STORE_MID-2.26,STORE_RIDGE-SLOPE*2.26)) + NORMAL*.26
SIGN_HALF=6.25
SIGN_RISE=4.29/2
def sign_point(x,v,relief):
    return SIGN_CENTER+V((x,0,0))+UP_SLOPE*v+NORMAL*relief


def emblem():
    p=Part("捆木浮雕")
    # 木雕采用斜投影浅浮雕：锯口朝牌外，带皮的树身向右上回落到牌芯。
    first=len(p.vertices)
    r=.59
    centers=((2.06,-.72),(3.27,-.72),(2.66,.31))
    for x,v in centers:
        trunk(p,(x,0,v),(x,1,v),r,"bark","cut",4,32)
        for j in range(1,4):
            path=[]
            for k in range(35):
                f=k*math.tau/35
                size=(.11+j*.104)*(1+.027*math.sin(f*3+x))
                path.append((x+.026+size*math.cos(f),-.015,v+.015+size*.96*math.sin(f)))
            tube(p,path,.020 if j==3 else .016,"oak",5,closed=True)
    for y in (.53,.65):
        path=[]
        for i,a,b in ((1,-90,30),(2,30,150),(0,150,270)):
            x,v=centers[i]
            for j in range(13):
                t=math.radians(a+(b-a)*j/12)
                path.append((x+1.03*r*math.cos(t),y,v+1.03*r*math.sin(t)))
        tube(p,path,.056,"rope",7,closed=True)
    for i in range(first,len(p.vertices)):
        x,t,v=(val*1000 for val in p.vertices[i])
        p.vertices[i]=tuple(sign_point(x+1.73*t,v+.75*t,.65-.49*t)*.001)
    return p
