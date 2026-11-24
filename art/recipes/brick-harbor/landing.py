"""挡浪砌岸、向海外挑的木栈桥、系船柱与砖材装卸机具。坐标以毫米计。"""
import math
from fabrication import ASHLAR, Part, Plane, R, STOCK, UP, V, beam, brick, cord, facing, rectangle, trunk, tube, wall_plane
from ashlar import clip, courses
from storehouse import FLOOR, TOWER_X

LEFT,RIGHT=-12.12,12.10
FRONT,BACK=-1.95,10.66
SEA_END=-5.34
JETTY_HALF=2.24
PIER_TOP=FLOOR+.035
WATER=-3.50
CARGO=((8.02,-.03,6,3,4),(3.38,-.42,4,2,2))
HOOK_X,HOOK_Y=-2.07,-.80
HANG_BASE=1.00


# 后侧两道斜向回墙收进滩线，给海岸端点的城市屋檐留出空间。
BANK=tuple(map(V,((LEFT,FRONT),(RIGHT,FRONT),(RIGHT,6.15),(RIGHT-1.60,BACK),
                  (LEFT+2.05,BACK),(LEFT,4.92))))


def offset_bank(distance):
    result=[]
    for i,p in enumerate(BANK):
        a=(p-BANK[i-1]).normalized()
        b=(BANK[(i+1)%len(BANK)]-p).normalized()
        corner=V((-a.y-b.y,a.x+b.x))/(1+a.dot(b))
        result.append(p+corner*distance)
    return result


def quay():
    stones,paving,iron=map(Part,("错缝挡浪墙与潮痕","卸货石坪及压顶","系船柱与嵌壁铁环"))
    floor=Plane((0,0,FLOOR),(1,0,0),(0,1,0))
    inner,outer=offset_bank(.50),offset_bank(-.14)
    for i,p in enumerate(BANK):
        j=(i+1)%len(BANK)
        edge=BANK[j]-p
        axis=edge.normalized()
        courses(stones,wall_plane(p.x,p.y,axis.x,axis.y),0,edge.length,-4.90,FLOOR-.36,7,
                stone_width=1.57,tideline=True)
        count=max(2,round(edge.length/1.42))
        for k in range(count):
            lo,hi=(k+.009)/count,(k+.991)/count
            cap=[outer[i].lerp(outer[j],lo),outer[i].lerp(outer[j],hi),
                 inner[i].lerp(inner[j],hi),inner[i].lerp(inner[j],lo)]
            facing(paving,floor,cap,0,.40,R.choice(("coping","coping","ashlar")),.040)
    facing(paving,floor,list(BANK),.16,.61,"lime")
    # 码头面层的每块铺石在真实回墙边界收口，水面和海床由棋盘提供。
    rows=10
    for j in range(rows):
        lo=FRONT+.50+(BACK-FRONT-1)*j/rows
        hi=FRONT+.50+(BACK-FRONT-1)*(j+1)/rows
        x=LEFT+.50
        step=.83 if j%2 else 1.71
        while x<RIGHT-.53:
            end=min(RIGHT-.50,x+step)
            if RIGHT-.50-end<.52: end=RIGHT-.50
            if not (-10.4<x and end<10.4 and lo>2.11 and hi<8.88):
                slab=rectangle(x+.013,end-.013,lo+.014,hi-.014)
                for a,b in zip(inner,inner[1:]+inner[:1]):
                    slab=clip(slab,lambda u,v:(b.x-a.x)*(v-a.y)-(b.y-a.y)*(u-a.x))
                if len(slab)>2:
                    facing(paving,floor,slab,R.uniform(.002,.020),.22,R.choice(ASHLAR),.020)
            x=end
            step=R.uniform(1.37,2.08)
    for x,y in ((-.07,FRONT+.40),(10.80,FRONT+.45)):
        mooring(iron,x,y,FLOOR)
    for x in (-.12,10.70):
        tube(iron,[(x,FRONT-.07,-.81),(x,FRONT-.17,-.81)],(.13,.12),"iron",10)
        tube(iron,[(x+.18*math.cos(i*math.tau/16),FRONT-.18,-.99+.211*math.sin(i*math.tau/16))
                   for i in range(16)],.046,"iron",7,True)
    return stones,paving,iron


def mooring(p,x,y,level):
    shape=((.00,.34),(.07,.36),(.14,.27),(.24,.215),(.64,.19),(.75,.26),(.80,.32),(.91,.34),(.97,.28))
    tube(p,[(x,y,level+z) for z,r in shape],[r for z,r in shape],"iron",20)
    for i in range(4):
        a=math.pi/4+i*math.pi/2
        cx=x+.263*math.cos(a)
        cy=y+.263*math.sin(a)
        tube(p,[(cx,cy,level+.035),(cx,cy,level+.094)],.049,"bronze",6,False,False)


def jetty():
    beams,piles=Part("挑栈铺板与护边"),Part("入水湿桩及斜撑")
    cx=TOWER_X
    y1=FRONT+.49
    for x in (cx-JETTY_HALF+.30,cx+JETTY_HALF-.30):
        for y in (SEA_END+.40,FRONT-.49):
            trunk(piles,(x,y,-4.99),(x,y,PIER_TOP-.32),.31,"oak","agedcut",rings=5,n=15,pile=True)
            for z in (PIER_TOP-.69,-2.84):
                tube(piles,[(x,y,z-.053),(x,y,z+.053)],.311,"iron",14)
        beam(beams,(x,SEA_END+.06,PIER_TOP-.43),(x,y1,PIER_TOP-.43),.34,.44,"tar",.06)
    for y in (SEA_END+.34,FRONT-.48):
        beam(piles,(cx-JETTY_HALF+.12,y,PIER_TOP-.67),(cx+JETTY_HALF-.12,y,PIER_TOP-.67),
             .26,.31,"oak",.042)
    for x in (cx-JETTY_HALF+.27,cx+JETTY_HALF-.27):
        beam(piles,(x,SEA_END+.43,-2.58),(x,FRONT-.48,PIER_TOP-.60),.21,.22,"tar",.032)
    # 并列纵板对准高仓拱洞，栈头横枋到达玩家商船泊位线。
    for i in range(9):
        w=(JETTY_HALF*2-.18)/9
        x=cx-JETTY_HALF+.09+(i+.5)*w
        beam(beams,(x,SEA_END+.16,PIER_TOP-.115),(x,y1,PIER_TOP-.115),w-.016,.23,
             R.choice(("dock","dock","oak")),.032)
        for y in (SEA_END+.41,FRONT-.47):
            for dx in (-.105,.105):
                tube(beams,[(x+dx,y,PIER_TOP-.012),(x+dx,y,PIER_TOP+.011)],.039,"iron",6)
    beam(beams,(cx-JETTY_HALF,SEA_END+.09,PIER_TOP-.17),(cx+JETTY_HALF,SEA_END+.09,PIER_TOP-.17),
         .18,.34,"trim",.036)
    for x in (cx-JETTY_HALF+.063,cx+JETTY_HALF-.063):
        beam(beams,(x,SEA_END+.13,PIER_TOP-.14),(x,y1,PIER_TOP-.14),.13,.28,"trim",.023)
    mooring(beams,cx-.93,SEA_END+.40,PIER_TOP)
    return beams,piles


def loaded_pallet(pallet,load,x,y,base,nx,ny,layers,unit=.75):
    w,d=nx*unit,ny*unit
    for dx in (-w*.37,0,w*.37):
        beam(pallet,(x+dx,y-d/2-.056,base+.103),(x+dx,y+d/2+.056,base+.103),
             .24,.204,"tar",.034)
    n=max(3,round(d/.40))
    for i in range(n):
        cy=y+((i+.5)/n-.5)*(d+.14)
        beam(pallet,(x-w/2-.075,cy,base+.252),(x+w/2+.075,cy,base+.252),
             (d+.14)/n-.031,.125,"dock",.026)
    for row in range(layers):
        # 邻皮的长短向交替。奇数进深末行用真实半砖补齐。
        sx,sy=(2,1) if row%2==0 else (1,2)
        for i in range(0,nx,sx):
            for j in range(0,ny,sy):
                ncol,nrow=min(nx-i,sx),min(ny-j,sy)
                cx=x+(i+(ncol-nx)/2)*unit+R.uniform(-.018,.018)
                cy=y+(j+(nrow-ny)/2)*unit+R.uniform(-.018,.018)
                brick(load,(cx,cy,base+.319+(row+.5)*unit*.55),
                      (ncol*unit-.036,nrow*unit-.033,unit*.55-.013),
                      R.choice(STOCK),R.uniform(-.017,.017),unit*.068)


def goods():
    wood,load=Part("平码枕木托盘"),Part("两垛逐皮交错的窑砖")
    for x,y,nx,ny,layers in CARGO:
        loaded_pallet(wood,load,x,y,FLOOR,nx,ny,layers)
    return wood,load


def crane():
    frame,rig,hook,goods=map(Part,("悬臂与铁箍","滑轮吊钩与绞绳","四角吊索与托盘","吊装中的窑砖"))
    mast=V((-4.68,.83,0))
    high=V((0,0,7.50))
    end=V((HOOK_X,HOOK_Y,7.78))
    start=V((-7.26,1.00,7.44))
    beam(frame,mast+UP*4.22,mast+high,.52,.55,"trim",.071)
    beam(frame,start,end+V((.19,-.15,.02)),.46,.48,"oak",.075)
    beam(frame,end-UP*.32,end+UP*.23,.49,.52,"trim",.057)
    beam(frame,mast+UP*4.76,end+V((-.18,.19,-.22)),.31,.35,"trim",.049)
    beam(frame,(-6.23,1.17,6.52),mast+UP*7.26,.28,.29,"oak",.046)
    axis=(end-start).normalized()
    for pt in (start+axis*.23,mast+UP*7.58,end-axis*.17):
        beam(frame,pt-axis*.073,pt+axis*.073,.50,.51,"iron",.02,end="iron")
    for z in (4.65,5.63,7.03):
        tube(frame,[(mast.x,mast.y-.285,z),(mast.x,mast.y-.34,z)],(.106,.094),"iron",9)
    # 块滑轮的两片木颊夹住铜轴，承重绳穿过槽轮后回到墙侧绞盘。
    for dx in (-.18,.18):
        pt=end+V((dx,0,-.36))
        beam(rig,pt+UP*.25,pt-UP*.23,.23,.106,"tar",.04,up=V((1,0,0)))
    wheel=end-UP*.33
    tube(rig,[wheel+V((x,0,0)) for x in (-.24,-.185,-.11,.11,.185,.24)],
         (.078,.21,.168,.168,.21,.078),"bronze",16)
    winch=mast+V((-.48,-.38,5.57))
    tube(rig,[winch+V((x,0,0)) for x in (-.42,-.36,-.27,.27,.36,.42)],
         (.13,.27,.19,.19,.27,.13),"iron",14)
    for i in range(5):
        tube(rig,[(winch.x-.245+.102*i,winch.y+.209*math.cos(t*math.tau/12),
                   winch.z+.209*math.sin(t*math.tau/12)) for t in range(12)],.052,"rope",6,True)
    top=V((HOOK_X,HOOK_Y,3.18))
    cord(rig,[wheel+V((0,-.132,0)),top+UP*.18],.092,.52)
    cord(rig,[wheel+V((0,.13,0)),winch+V((0,-.09,.17))],.071,.59)
    tube(rig,[(HOOK_X+.124*math.cos(i*math.tau/15),HOOK_Y,3.24+.173*math.sin(i*math.tau/15))
              for i in range(15)],.048,"iron",8,True)
    z=HANG_BASE
    loaded_pallet(hook,goods,HOOK_X,HOOK_Y,z,3,2,2,.61)
    for dx in (-.97,.97):
        for dy in (-.65,.65):
            cord(hook,[(HOOK_X+dx,HOOK_Y+dy,z+.19),(HOOK_X+dx*1.02,HOOK_Y+dy*1.02,z+.68),
                       (HOOK_X+dx*.56,HOOK_Y+dy*.56,2.48),(HOOK_X+dx*.058,HOOK_Y+dy*.058,3.15)],.065,.52)
    return frame,rig,hook,goods
