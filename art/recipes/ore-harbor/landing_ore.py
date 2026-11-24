"""开敞木桩下的水面贯通。吊臂通过双颊滑轮、吊钩与四角挂索承接同一个货斗。"""
import math
from craft import (BACK,FINGER_END,FINGER_X0,FINGER_X1,FLOOR,QUAY_FRONT,QUAY_X,SHORE,TOWER_FRONT,
                   TOWER_X,TOWER_W,Part,R,V,UP,beam,cord,faced_block,iron_loop,mineral,pin,trunk,tube)

LIFT=(-2.32,-3.18)
PIVOT=(TOWER_X+1.23,TOWER_FRONT-.29,8.78)
STOCK=(8.37,-2.18)
SAMPLE=(2.43,-2.68)
STONE_START=FINGER_X1+.22


def stone_quay(p):
    """重货岸用五皮有厚度的石砌胸墙承压，转角接短回墙，桥趾下面留水。"""
    from warehouse_ore import Wall
    sea=QUAY_FRONT+.064
    right=QUAY_X-.06
    def courses(w,length,seed):
        low=-4.55; cap=FLOOR-.28; count=5
        for k in range(count):
            lo=low+(cap-low)*k/count
            hi=low+(cap-low)*(k+1)/count
            marks=[0]; u=-(.98 if (k+seed)%2 else 0)
            while u<length:
                u+=R.uniform(1.83,2.69)
                if u<length-.52: marks.append(u)
            marks.append(length)
            for start,end in zip(marks,marks[1:]):
                a,b=start+.017,end-.017
                c=R.uniform(.038,.117)
                pts=[(a+c,lo+.015),(b-.049,lo+.016),(b,lo+.075),(b,hi-c),(b-c,hi-.013),
                     (a+.058,hi-.008),(a,hi-.064),(a,lo+c)]
                # 墙趾略向海放脚，局部保留石工凿痕与盐蚀孔洞。
                tile=R.choice(("scour","scour","ashlar1","ashlar3")) if k<3 else R.choice(("ashlar0","ashlar1","ashlar3"))
                shift=.045*(4-k)+R.uniform(-.021,.021)
                w.block(p,pts,-shift,.64,tile,.040)
        w.block(p,[(0,low),(length,low),(length,cap),(0,cap)],.11,.38,"dress3",.032)
    courses(Wall(STONE_START,sea,(1,0)),right-STONE_START,0)
    courses(Wall(right,sea+.42,(0,1)),SHORE-sea-.47,1)
    courses(Wall(STONE_START,1.17,(0,-1)),1.17-sea-.43,1)
    back=QUAY_FRONT+8*(SHORE-QUAY_FRONT)/23
    count=5
    for row in range(count):
        y0=QUAY_FRONT+(back-QUAY_FRONT)*row/count
        y1=QUAY_FRONT+(back-QUAY_FRONT)*(row+1)/count
        x0=STONE_START; cursor=x0-(.96 if row%2 else 0); xs=[x0]
        while cursor<QUAY_X:
            cursor+=R.uniform(1.78,2.52)
            if cursor<QUAY_X-.43: xs.append(cursor)
        xs.append(QUAY_X)
        for a,b in zip(xs,xs[1:]):
            c=R.uniform(.049,.096)
            a+=.014; b-=.014
            l=y0+.012; f=y1-.012
            xy=[(a+c,l),(b-c,l),(b,l+c),(b,f-c),(b-c,f),(a+c,f),(a,f-c),(a,l+c)]
            tile=R.choice(("dress1","dress2","coping","coping")) if row==0 else R.choice(("ashlar0","ashlar2","dress1","coping"))
            faced_block(p,[(x,y,FLOOR+R.uniform(-.001,.001)) for x,y in xy],UP,.298,tile,.037)


def wharf():
    piles,wood,hardware=Part("入水桩与斜撑"),Part("栈道面板与仓下格栅梁"),Part("系缆柱与道钉")
    stone=Part("重货驳岸的胸墙与石铺装")
    xs=(-10.79,-5.55,.0,5.15,10.79)
    # 后排直接承托临岸门槛，前排和桥趾的桩一直伸到真实海面之下。
    for row,y in enumerate((-4.03,-.08,4.40,9.46)):
        for x in xs:
            if x>10 or (row==0 and x>=0): continue
            trunk(piles,(x+.045,y-.027,-5.10-R.uniform(0,.15)),(x,y,-.16),
                  R.uniform(.275,.335),side_tile="oak",cap_tile="agedcut",rings=4,n=14,pile=True)
        beam(wood,(-QUAY_X+.07,y,-.31),(QUAY_X-.07,y,-.31),.48,.48,"tar",.056)
    for x in (FINGER_X0+.43,FINGER_X1-.43):
        trunk(piles,(x,-6.30,-5.23),(x,-6.30,-.09),.34,"oak","agedcut",4,16,True)
        beam(piles,(x,-6.27,-2.13),(x,-4.03,-.37),.20,.25,"oak",.027)
    for a,b in zip(xs,xs[1:]):
        if a>=FINGER_X1: continue
        beam(piles,(a+.15,-4.02,-2.48),(b-.13,-4.02,-.51),.20,.27,"oak",.037)
    for x in (*xs, (FINGER_X0+FINGER_X1)*.5):
        start=FINGER_END+.09 if FINGER_X0<x<FINGER_X1 else QUAY_FRONT+.08
        beam(wood,(x,start,-.038),(x,SHORE-.07,-.038),.36,.42,"oak",.041)
    # 面板从两端交错接头。仓内铺板、仓前横埠、短指桥分别由其下面的纵梁支承。
    def deck_row(a,b,y0,y1,above,fasteners):
        if b<=a: return
        tile=R.choice(("dock","dock","dock","boards"))
        beam(wood,(a,(y0+y1)*.5,above-.134),(b,(y0+y1)*.5,above-.134),y1-y0-.021,.268,
             tile,.033,end="agedcut")
        if fasteners:
            for x in (a+.19,b-.19):
                pin(hardware,(x,(y0+y1)*.5,above-.002),UP,.038,"iron")
    count=23; step=(SHORE-QUAY_FRONT)/count
    for i in range(count):
        y=QUAY_FRONT+i*step; shore=i==count-1
        # 在端排留一列窄板封接岸线；中排就近落在仓脚下，不设任何展示托板。
        splits=(-QUAY_X,-5.55,5.15,QUAY_X) if i%2 else (-QUAY_X,0,QUAY_X)
        for a,b in zip(splits,splits[1:]):
            if i<8: b=min(b,STONE_START)
            deck_row(a+.015,b-.015,y,y+step,FLOOR+R.uniform(-.009,.009),i<8 or shore)
    for j in range(4):
        step=(QUAY_FRONT-FINGER_END)/4
        deck_row(FINGER_X0,FINGER_X1,FINGER_END+j*step,FINGER_END+(j+1)*step,FLOOR,True)
    for a,b in ((-QUAY_X,FINGER_X0),(FINGER_X1,STONE_START)):
        beam(wood,(a,QUAY_FRONT+.052,.02),(b,QUAY_FRONT+.052,.02),.23,.34,"dock",.036)
    beam(wood,(FINGER_X0,FINGER_END+.084,-.016),(FINGER_X1,FINGER_END+.084,-.016),.20,.39,"oak",.039)
    for x,y in ((-10.58,-3.61),(-8.62,-5.85),(.02,-3.82),(10.52,-3.76)):
        heights=(0,.095,.16,.25,.53,.62,.72,.78)
        widths=(.32,.35,.29,.207,.223,.32,.338,.285)
        tube(hardware,[(x,y,FLOOR+v) for v in heights],widths,"iron",18)
        tube(hardware,[(x,y,FLOOR+.107),(x,y,FLOOR+.143)],(.324,.312),"bronze",18)
    stone_quay(stone)
    return wood,piles,hardware,stone


def crate(p,x,y,z,width,depth,height,heavy=False):
    thick=.13 if heavy else .093
    base=height*.15
    for i in range(4):
        yy=y-depth/2+(i+.5)*depth/4
        beam(p,(x-width/2,yy,z+base*.5),(x+width/2,yy,z+base*.5),depth/4-.016,base,"dock",.024)
    rows=3 if heavy else 2
    for n in range(rows):
        h=(height-base)/rows
        zz=z+base+(n+.5)*h
        for s in (-1,1):
            yy=y+s*(depth*.5-thick*.5)
            beam(p,(x-width*.5,yy,zz),(x+width*.5,yy,zz),thick,h-.025,
                 "boards" if n%2 else "dock",.022)
            xx=x+s*(width*.5-thick*.5)
            beam(p,(xx,y-depth*.5+thick,zz),(xx,y+depth*.5-thick,zz),thick,h-.025,"oak",.021)
    for s in (-1,1):
        for k in (-1,1):
            xx=x+s*(width*.5-.15); yy=y+k*(depth*.5-.047)
            beam(p,(xx,yy,z+.03),(xx,yy,z+height+.045),.245,.22,"oak",.037)
            for u in (.17,height-.135):
                pin(p,(xx,yy+k*.126,z+u),(0,k,0),.076 if heavy else .052)
            if heavy:
                yy=y+k*(depth*.5+.04)
                beam(p,(xx,yy,z+.075),(xx,yy,z+height+.026),.213,.049,"iron",.010,up=V((0,k,0)),end="iron")
                for u in (.17,height-.135): pin(p,(xx,yy+k*.033,z+u),(0,k,0),.073,"bronze")
    for off in (-.30,.30):
        xx=x+width*off
        beam(p,(xx,y-depth*.51,z-.035),(xx,y+depth*.51,z-.035),.21,.10,"tar",.018)


def load_bin(p,center,items):
    x,y,z=center
    for dx,dy,dz,a,b,h,tile in items:
        mineral(p,(x+dx,y+dy,z+dz),(a,b,h),tile)


def cargo():
    coop,stone=Part("矿箱与分级浅盘"),Part("带石英脉的矿块")
    x,y=STOCK
    crate(coop,x,y,FLOOR+.075,4.42,2.64,1.04,True)
    load_bin(stone,(x,y,FLOOR+.08),[
        (-1.25,-.44,.89,.81,.74,.70,"ore"),(-.22,-.39,.96,.96,.80,.77,"ochre"),
        (1.08,-.37,1.07,1.13,.85,.82,"ore"),(-1.05,.51,1.16,.97,.83,.92,"ore"),
        (.63,.49,1.42,1.13,.83,1.06,"ore"),(-.13,.14,1.69,.86,.66,.82,"ore")])
    x,y=SAMPLE
    crate(coop,x,y,FLOOR+.07,2.75,1.66,.46)
    load_bin(stone,(x,y,FLOOR+.08),[(-.72,-.18,.48,.58,.55,.49,"ore"),
        (.05,.19,.64,.73,.63,.63,"ore"),(.75,-.15,.43,.53,.43,.38,"ochre")])
    for i in range(8):
        # 细料收在大块之间；几块溢落的矿屑紧挨箱脚。
        x=STOCK[0]-2.09+R.random()*.58; y=STOCK[1]-1.47-R.random()*.25
        s=R.uniform(.059,.125)
        mineral(stone,(x,y,FLOOR+s*.49),(s,s*.87,s*.66),"ochre" if i%3==0 else "ore")
    # 高仓的券洞可见后方矿箱，前口给进出港装卸留空。
    crate(coop,TOWER_X-.43,2.04,FLOOR+.07,1.96,1.40,.69)
    load_bin(stone,(TOWER_X-.43,2.04,FLOOR+.07),[(-.28,0,.59,.66,.57,.45,"ochre"),(.48,.09,.53,.48,.49,.40,"ore")])
    return coop,stone


def hoist():
    frame,rig,bucket=Part("榫接吊臂与滑轮架"),Part("滑轮卷筒与三股吊绳"),Part("四角挂索货斗与矿料")
    x,y=LIFT
    anchor=V(PIVOT)
    reach=V((x-anchor.x,y-anchor.y,0)).normalized()
    end=V((x,y,8.98))-reach*.23; along=(end-anchor).normalized()
    side=UP.cross(along).normalized()
    heel=anchor-UP*2.62
    beam(frame,heel-UP*.35,anchor+UP*.18,.46,.47,"oak",.057)
    beam(frame,anchor-along*.83,end+along*.24,.48,.44,"oak",.051)
    beam(frame,heel+UP*.04,end-along*.79-UP*.15,.34,.40,"oak",.046)
    for k in (0,.17):
        c=anchor+along*k
        beam(frame,c-UP*.27,c+UP*.27,.52,.082,"iron",.017,up=along,end="iron")
    for a in (heel+UP*.18,anchor-UP*.19,anchor-along*.58):
        pin(frame,a+side*.24,side,.10); pin(frame,a-side*.24,-side,.10)
    wheel=end-UP*.32
    for sign in (-1,1):
        c=wheel+side*(sign*.23)
        beam(frame,c-UP*.28,c+UP*.48,.31,.15,"iron",.047,up=side,end="iron")
        pin(frame,c+side*sign*.085,side*sign,.098,"bronze")
    tube(rig,[wheel+side*d for d in (-.22,-.16,-.11,.11,.16,.22)],(.201,.267,.215,.215,.267,.201),"bronze",24)
    spool=anchor+along*.38-UP*.36
    tube(rig,[spool+side*d for d in (-.47,-.39,-.32,.32,.39,.47)],(.15,.29,.20,.20,.29,.15),"iron",14)
    wraps=[]
    for i in range(81):
        t=i/80; a=t*math.tau*5
        wraps.append(spool+side*(-.31+.62*t)+UP*(.218*math.cos(a))+along*(.218*math.sin(a)))
    tube(rig,wraps,.054,"rope",6)
    hook=V((x,y,3.73))
    bend=[wheel+.23*(UP*math.cos(a*math.pi/12)+reach*math.sin(a*math.pi/12)) for a in range(7)]
    cord(rig,[spool+UP*.214,*bend,hook+UP*.24],.077,.45)
    tube(rig,[hook+UP*.19,hook+UP*.12,hook+UP*.014],(.119,.145,.10),"iron",10)
    iron_loop(rig,hook-UP*.086,V((.135,0,0)),UP*.178,.054)
    z=1.13
    crate(bucket,x,y,z,2.68,1.79,.55,True)
    load_bin(bucket,(x,y,z),[(-.62,-.13,.64,.66,.60,.64,"ore"),(.40,.21,.74,.74,.64,.74,"ore"),
                             (.76,-.33,.51,.49,.44,.43,"ochre")])
    for s in (-1,1):
        for k in (-1,1):
            corner=V((x+s*1.16,y+k*.76,z+.58))
            iron_loop(rig,corner,V((.105,0,0)),UP*.112,.044)
            cord(rig,[corner+UP*.08,hook-UP*.23+V((s*.065,k*.046,0))],.064,.39)
    return frame,rig,bucket
