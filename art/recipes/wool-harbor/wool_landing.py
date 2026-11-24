"""石驳岸、桩承短栈桥、悬臂滑车与各自带压痕的实心生羊毛捆。"""
import math
from wool_craft import Face,Part,R,V,beam,cord,extrude,masonry,ovoid,pin,rect,stone,trunk,tube
from wool_buildings import BACK,FLOOR,TOWER_X
from mathutils.bvhtree import BVHTree

PIER_LEFT,PIER_RIGHT,PIER_END = -11.22,-5.85,-5.68
QUAY_LEFT,QUAY_RIGHT,QUAY_FRONT = -5.84,12.02,-1.94
LOAD_X,LOAD_Y,LOAD_Z = -2.11,-1.96,1.28
POST_X,POST_Y = -6.16,.78


def quay():
    quaywall=Part("浪蚀驳岸与系缆石")
    planks=Part("开敞桩桥与木护舷")
    front=Face(0,QUAY_FRONT,1,0)
    # 短石岸仅承托长仓前的装卸通道；拱门前的栈桥和吊索下均可看到游戏海水。
    masonry(quaywall,front,QUAY_LEFT,QUAY_RIGHT,-4.94,-.23,(),.78,1.84,True)
    for x,angle in ((QUAY_LEFT,-1),(QUAY_RIGHT,1)):
        masonry(quaywall,Face(x,-.08,0,angle),-1.86,1.86,-4.94,-.23,(),.83,1.51,True)
    for row,(y0,y1) in enumerate(((QUAY_FRONT-.07,-.86),(-.85,.39),(.40,1.65))):
        cuts=[QUAY_LEFT-.03]
        x=cuts[0]+(1.18 if row%2 else 2.21)
        while x<QUAY_RIGHT-.60:
            cuts.append(x); x+=R.uniform(1.84,2.56)
        cuts.append(QUAY_RIGHT+.02)
        for a,b in zip(cuts,cuts[1:]):
            l,r=a+.011,b-.011
            c=R.uniform(.052,.14)
            pts=[(l+c,y0,FLOOR),(r-.08,y0,FLOOR),(r,y0+.09,FLOOR),(r,y1-.07,FLOOR),
                 (r-.09,y1,FLOOR),(l+.08,y1,FLOOR),(l,y1-.07,FLOOR),(l,y0+c,FLOOR)]
            extrude(quaywall,pts,(0,0,-.42),R.choice(("dressed","dressed","stone","honey")))
    # 背墙接岸处的门槛石搭入陆地倒角，建筑围护本身下探至水线以下。
    for i in range(13):
        x=-11.95+i*1.846
        pts=[(x,9.35,.048),(x+1.84,9.35,.048),(x+1.84,10.42,.025),(x,10.42,.025)]
        extrude(quaywall,pts,(0,0,-.51),"stone")
    for x in (PIER_LEFT+.37,PIER_RIGHT-.37):
        beam(planks,(x,PIER_END+.01,-.29),(x,1.52,-.29),.43,.54,"tar")
        for y in (PIER_END+.49,-2.07,.91):
            trunk(planks,(x,y,-5.12),(x,y,-.21),.34,"wet","agedcut",4,16,True)
            tube(planks,[(x,y,-.99),(x,y,-.81)],.351,"iron",16)
    for y in (PIER_END+.47,-2.07,.91):
        beam(planks,(PIER_LEFT+.10,y,-.53),(PIER_RIGHT-.10,y,-.53),.37,.37,"tar",.047)
    for i in range(10):
        x=PIER_LEFT+(i+.5)*(PIER_RIGHT-PIER_LEFT)/10
        beam(planks,(x,PIER_END+R.uniform(0,.026),.033),(x,1.50,.033),
             (PIER_RIGHT-PIER_LEFT)/10-.014,.24,R.choice(("dock","dock","wall")),.028)
        for y in (PIER_END+.44,-2.07,.89):
            pin(planks,(x,y,.147),(x,y,.165),.037)
    # 低矮蘑菇形系缆桩与带箍橡木护舷。下半段长出深色潮痕。
    for x,y in ((-6.43,-5.10),(.34,-1.48),(11.61,-1.47)):
        tube(quaywall,[(x,y,FLOOR-.04),(x,y,FLOOR+.11),(x,y,FLOOR+.23),
                       (x,y,FLOOR+.65),(x,y,FLOOR+.76),(x,y,FLOOR+.86),(x,y,FLOOR+.92)],
             [.48,.48,.32,.30,.43,.43,.36],"dressed",16)
    for x in (-4.94,5.88,11.27):
        trunk(planks,(x,QUAY_FRONT-.20,-4.90),(x,QUAY_FRONT-.20,-.41),.22,"wet","agedcut",4,12,True)
        tube(planks,[(x,QUAY_FRONT-.20,-.99),(x,QUAY_FRONT-.20,-.86)],.228,"iron",12)
    return quaywall,planks


def pallet(p,x,y,z,w,d,rotation=0):
    c,s=math.cos(rotation),math.sin(rotation)
    def at(u,v,h):
        return (x+c*u-s*v,y+s*u+c*v,z+h)
    for u in (-w*.30,w*.30):
        beam(p,at(u,-d/2,.10),at(u,d/2,.10),.24,.20,"tar",.026)
    for j in range(4):
        v=(j-1.5)*d/4
        beam(p,at(-w/2,v,.245),at(w/2,v,.245),d/4-.031,.16,"dock",.03)


def bale(p,x,y,bottom,w,d,h,rot=0):
    c,s=math.cos(rot),math.sin(rot)
    rx,ry,rz=w/2,d/2,h/2
    phase=R.uniform(0,math.tau)
    ties=(-.48,.46) if w>2.1 else (.15,)
    def tied(nx):
        return sum(.095*math.exp(-((nx-b)/.103)**2) for b in ties)
    def at(u,v,z):
        return V((x+c*u-s*v,y+s*u+c*v,bottom+rz+z))
    def power(a,e):
        return math.copysign(abs(a)**e,a)
    seg=32
    angles=[math.tau*i/seg for i in range(seg)]
    first_vertex,first_face=len(p.vertices),len(p.faces)
    def point(a,depth,scale=1):
        nx=scale*power(math.cos(a),.65)
        nz=scale*power(math.sin(a),.63)
        dent=tied(nx)
        fuzz=.022*math.sin(7*a+2.3*depth+phase)+.014*math.sin(11*a-2.4*depth-phase)
        return at(rx*nx*(1+fuzz),ry*depth*(1-dent*.72),rz*nz*(1-dent+fuzz))
    levels=((-1,.83),(-.91,.95),(-.60,1),(-.22,1.018),(.19,1.01),(.61,1),(.92,.94),(1,.82))
    rings=[p.verts([point(a,v,sc) for a in angles]) for v,sc in levels]
    roll=R.choice(("roll","roll2"))
    for j,(lo,hi) in enumerate(zip(rings,rings[1:])):
        for i in range(seg):
            k=(i+1)%seg
            # 羊毛锁沿着卷毛方向展开，每半圈重新采样。
            u=(i%(seg//2))/(seg//2)
            v=u+2/seg
            p.face((lo[i],hi[i],hi[k],lo[k]),"fleece",
                   [(u,j/7),(u,(j+1)/7),(v,(j+1)/7),(v,j/7)],True)
    for sign,edge in ((-1,rings[0]),(1,rings[-1])):
        prior=edge
        last=.83 if sign<0 else .82
        def capuv(a,sc):
            return (.50+.47*power(math.cos(a),.65)*sc,.5+.47*power(math.sin(a),.63)*sc)
        for scale in (.62,.37,.15):
            cur=p.verts([point(a,sign*(1.045+.035*math.sin(scale*6)),scale) for a in angles])
            for i in range(seg):
                k=(i+1)%seg
                ids=[prior[i],prior[k],cur[k],cur[i]]
                tex=[capuv(angles[i],last),capuv(angles[k],last),capuv(angles[k],scale),capuv(angles[i],scale)]
                p.face(ids[:: -sign],roll,tex[:: -sign],True)
            prior,last=cur,scale
        p.face(prior[:: -sign],roll,[capuv(a,.15) for a in angles][:: -sign],True)
    skin=BVHTree.FromPolygons([V(v)*1000 for v in p.vertices[first_vertex:]],
        [tuple(i-first_vertex for i in face) for face in p.faces[first_face:]])
    def on_skin(origin,direction,lift):
        hit,normal,_,_=skin.ray_cast(origin,direction.normalized(),max(w,d,h)*1.35)
        if hit is None: raise ValueError("未找到毛包表面")
        return hit+normal*lift
    # 每条绒毛的受束方向不同。两条带边分别投到实际包面上，压痕处也保有带厚。
    for tie in ties:
        rails=[]
        count=52
        for edge in (-.089,.089):
            strip=[]
            origin=at(rx*tie+edge,0,0)
            for i in range(count):
                a=math.tau*i/count
                ray=at(rx*tie+edge,ry*math.cos(a),rz*math.sin(a))-origin
                strip.append(on_skin(origin,ray,.025))
            rails.append(strip)
        ids=[p.verts(strip) for strip in rails]
        for i in range(count):
            k=(i+1)%count
            p.face((ids[0][i],ids[0][k],ids[1][k],ids[1][i]),"web",
                   [(0,i/count),(0,(i+1)/count),(1,(i+1)/count),(1,i/count)],True)
        for strip in rails: tube(p,strip,.020,"rope",5,True)
    # 从卷端梳出的短毛锁，沿外圈自然起伏；避开已经扎紧的部位。
    forward=V((s,-c,0))
    for j in range(12):
        a=math.tau*(j+R.random()*.68)/12
        sc=R.uniform(.56,.80)
        xx=rx*sc*power(math.cos(a),.65)
        zz=rz*sc*power(math.sin(a),.63)
        if any(abs(xx-rx*tie)<.18 for tie in ties): continue
        curl=[]
        for k in range(9):
            phi=a+k*.47
            at_root=at(xx+.051*math.cos(phi)+k*.006,0,zz+.047*math.sin(phi))
            curl.append(on_skin(at_root,forward,.003+.008*math.sin(math.pi*k/8)))
        tube(p,curl,[.009+.010*math.sin(math.pi*k/8) for k in range(9)],"fleece",5)


def goods():
    stock=Part("码头双层原毛与内仓备货")
    pallets=Part("压绳货托")
    pallet(pallets,2.71,-.55,FLOOR,3.14,2.12,-.046)
    bale(stock,2.71,-.55,FLOOR+.323,2.64,1.80,1.44,-.046)
    pallet(pallets,9.09,-.59,FLOOR,4.34,2.23,.021)
    for j in range(2):
        for i in range(2):
            bale(stock,9.09+(i-.5)*1.91+.06*j,-.60+.035*(i+j),FLOOR+.321+j*1.30,
                 1.91-.07*j,1.86-.07*j,1.36-.05*j,.025*(1+i-2*j))
    bale(stock,TOWER_X+.42,3.91,FLOOR,1.71,1.72,1.32,.24)
    return stock,pallets


def crane():
    frame,gear,load=map(Part,("橡木悬臂与斜撑","绞盘、滑轮和吊索","吊运毛包与木托"))
    px,py=POST_X,POST_Y
    beam(frame,(px,py,3.02),(px,py,9.27),.51,.60,"oak")
    start=V((px,py+.25,8.91))
    tip=V((LOAD_X,LOAD_Y,8.91))
    beam(frame,start,tip,.53,.60,"dock",.062)
    beam(frame,(px,py,5.52),start.lerp(tip,.82)-V((0,0,.25)),.43,.44,"oak",.047)
    for a in (.06,.85,.96):
        loc=start.lerp(tip,a)
        beam(gear,loc-.10*(tip-start).normalized(),loc+.10*(tip-start).normalized(),.558,.624,"iron",.02)
    # 木滑轮的轮缘与夹板分件，主绳绕轮后回到有棘轮的卧式绞盘。
    for offset in (-.22,.22):
        beam(frame,(LOAD_X+offset,LOAD_Y,8.50),(LOAD_X+offset,LOAD_Y,9.17),.18,.39,"oak",.035)
    pin(gear,(LOAD_X-.32,LOAD_Y,8.80),(LOAD_X+.32,LOAD_Y,8.80),.093,"brass")
    tube(gear,[(LOAD_X-.13,LOAD_Y,8.78),(LOAD_X-.08,LOAD_Y,8.78),
               (LOAD_X+.08,LOAD_Y,8.78),(LOAD_X+.13,LOAD_Y,8.78)],[.25,.198,.198,.25],"iron",16)
    cord(gear,[(LOAD_X,LOAD_Y-.21,8.76),(LOAD_X,LOAD_Y-.20,3.78)],.079,.55)
    tube(gear,[(LOAD_X,LOAD_Y-.20,3.82),(LOAD_X,LOAD_Y-.20,3.64),
              (LOAD_X+.11,LOAD_Y-.22,3.48),(LOAD_X+.22,LOAD_Y-.22,3.54),
              (LOAD_X+.23,LOAD_Y-.21,3.64)],.063,"iron",8)
    cord(gear,[(LOAD_X,LOAD_Y+.21,8.77),(px+.26,.24,5.76),(px+.18,.32,4.99)],.067,.60)
    tube(gear,[(px-.51,.27,4.90),(px+.51,.27,4.90)],.27,"tar",14)
    for x in (px-.41,px+.41):
        tube(gear,[(x-.06,.27,4.9),(x+.06,.27,4.9)],.36,"iron",14)
    for x in (px-.27,px-.13,px,px+.13,px+.27):
        tube(gear,[(x-.061,.27,4.9),(x+.061,.27,4.9)],.285,"rope",14)
    for z in (3.53,5.54,8.88):
        pin(gear,(px,.44,z),(px,.38,z),.117,"iron")
    pallet(load,LOAD_X,LOAD_Y,LOAD_Z,2.85,2.01)
    bale(load,LOAD_X,LOAD_Y,LOAD_Z+.322,2.46,1.79,1.42,-.024)
    for s in (-1,1):
        for t in (-1,1):
            path=[(LOAD_X+s*1.34,LOAD_Y+t*.78,LOAD_Z+.14),
                  (LOAD_X+s*1.25,LOAD_Y+t*.75,LOAD_Z+.91),
                  (LOAD_X+s*.96,LOAD_Y+t*.52,LOAD_Z+1.65),
                  (LOAD_X+s*.078,LOAD_Y-.17+t*.055,3.48)]
            cord(gear,path,.067,.61)
        tube(gear,[(LOAD_X+s*1.34,LOAD_Y-.80,LOAD_Z+.14),(LOAD_X+s*1.34,LOAD_Y+.80,LOAD_Z+.14)],.07,"rope",7)
    return frame,gear,load
