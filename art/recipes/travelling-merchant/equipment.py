"""行商随身的布卷、旅行行囊与皮具。"""
from math import sin,cos,pi,tau,exp
from mathutils.bvhtree import BVHTree
from workshop import P,V,UP,RIGHT,FRONT,smooth,orb,trim,tube,sweep,stitches,buckle,rounded_loop


def rug(g):
    g.part("14 茜红条纹羊毛布卷")
    axis=V((.77,-.61,-.13)).normalized()
    e1=(UP-UP.dot(axis)*axis).normalized()
    e2=axis.cross(e1).normalized()
    near=P(398,321,-91)
    span=96
    turns=2.8
    steps=155
    def sample(t,v,offset=0):
        a=pi*.83+tau*turns*(t-1)
        rad=6.3+33.6*t
        rad+=.78*sin(3*a+.7*v)*(.5+.5*t)+.48*cos(5*a+v)+offset
        radial=e1*cos(a)+e2*sin(a)
        return near-axis*(span*(1-v))+radial*rad+axis*(.82*sin(a*2+.6)*v**4)
    # 一条真正卷起的毛料。端面的圆边与内、外布面共享顶点。
    section=[(i/20,5.15) for i in range(21)]
    section += [(1.017,3.9),(1.027,.15),(1.017,-3.65),(1,-5.05)]
    section += [(i/20,-5.05) for i in range(19,-1,-1)]
    section += [(-.017,-3.65),(-.027,0),(-.017,3.65)]
    rows=[[sample(j/steps,v,offset) for v,offset in section] for j in range(steps+1)]
    def material(j,k):
        return "carpet" if k<20 or 24<=k<44 else "felt"
    def uv(j,k,key,pt):
        v,depth=section[k%len(section)]
        return j/steps,max(0,min(1,v)) if key=="carpet" else (depth+5.2)/10.4
    g.grid(rows,material,caps=False,uv=uv)
    sweep(g,[near-axis*span,near-axis*3.5,near-axis*1.8],[2.1,2.5,2.2],"felt",18,UP)
    # 布头沿最后一圈的切向松垂，较长的一角从手肘下露出。
    outer=[];inner=[]
    for j in range(24):
        t=j/23
        row=[]
        for k in range(25):
            v=k/24
            edge=sample(1,v,5.15)
            end=P(313+52*v+4*sin(pi*v),445-35*v**1.5+3*sin(v*9),-88-34*v+4*cos(v*8))
            p=edge.lerp(end,t)+FRONT*(12*sin(pi*t)+3.5*sin(v*10+2*t)*t)
            p-=e2*(4.5*sin(pi*t)**2)
            row.append(p)
        outer.append(row)
        radial=e1*cos(.83*pi)+e2*sin(.83*pi)
        inner.append([p-(radial*(1-t)+e2*t)*(10.2*(1-t)+3.7*t) for p in row])
    # 条纹沿卷轴连续至布头；裁片横、纵轴在通用网格内对调。
    g.sheet(list(map(list,zip(*outer))),list(map(list,zip(*inner))),"carpet","carpet","felt")
    trim(g,outer[-1],1.3,"felt")


def strap(g,pts,width,key="harness",axis=RIGHT,thick=3,thread=True):
    rows=[]
    for j,p in enumerate(pts):
        tangent=(pts[min(j+1,len(pts)-1)]-pts[max(0,j-1)]).normalized()
        u=(axis-tangent*axis.dot(tangent)).normalized()
        n=tangent.cross(u).normalized()
        row=[]
        for x,z in ((-1,0),(-.96,.72),(-.82,1),(0,1.08),(.82,1),(.96,.72),(1,0),(.93,-.45),(-.93,-.45)):
            row.append(p+u*(width*x)+n*(thick*z))
        rows.append(row)
    g.grid(rows,key)
    for k in (1,5):
        trim(g,[r[k] for r in rows],.74,"welt",sides=6)
        if thread: stitches(g,[r[2 if k==1 else 4] for r in rows],7,3.25,.49,"thread")
    return rows


def bag_and_harness(g):
    g.part("15 牛皮行囊、翻盖与侧褶")
    yaw=.38
    side=V((cos(yaw),sin(yaw),0))
    back=V((-sin(yaw),cos(yaw),0))
    center=P(182.5,318.5,85)
    def bp(x,z,d):
        return center+side*x+UP*z+back*d
    rows=[]
    for d,w,h,r in ((-30,72,70,13),(-27,79,74,10),(-18,81,76,11),
                    (-9,79,73,10),(7,81,75,12),(17,81,74,11),(29,78,72,9),(32,75,69,9)):
        rows.append([bp(x,z,d) for x,z in rounded_loop(w,h,r,6)])
    guide=rows[2]+[rows[2][0]]
    perimeter=[0.]
    for a,b in zip(guide,guide[1:]): perimeter.append(perimeter[-1]+(b-a).length)
    def gusset_uv(j,k,key,p): return perimeter[k]/perimeter[-1],j/(len(rows)-1)
    def panel_uv(p): return .5+(p-center).dot(side)/165,.5+(p-center).z/158
    g.grid(rows,"satchel",uv=gusset_uv,cap_uv=panel_uv)
    for row in (rows[1],rows[-2]):
        trim(g,row,1.8,"welt",True)
        stitches(g,row+[row[0]],8.2,3.7,.55)
    # 翻盖自包顶绕向背面；盖角圆钝，边缘包浆与线迹独立。
    flap_rows=[]
    for d,z,w in ((-21,70,76),(-17,78,76),(2,80,77),(20,78,77),(32,73,77),
                  (36,62,77),(37,42,77),(38,16,76),(37,4,73)):
        flap_rows.append([bp(w*q,z-3*q*q+1.2*sin(pi*q),d+1.75*cos(pi*q*.7))
                          for q in [i/10 for i in range(-10,11)]])
    g.sheet(flap_rows,[[p-back*2.7-UP*1 for p in r] for r in flap_rows],"satchel","harness","welt")
    trim(g,flap_rows[-1],1.55,"welt")
    stitches(g,[p+UP*3 for p in flap_rows[-1]],7,3.2,.53)
    g.part("16 行囊双束带与背扣")
    for x in (-41,41):
        path=[bp(x,z,d) for z,d in ((-63,-28),(-75,-23),(-77,-6),(-75,18),(-69,33),(-57,35),
                                     (-29,36),(1,40),(29,40),(59,39),(72,35),(79,22),(82,0),(78,-20),(70,-28))]
        vals=[V(p) for p in smooth(path,3)]
        strap(g,vals,7.1,"harness",side,2.25)
        buckle(g,bp(x,4,43),side,UP,9.8,9.4,2.1,"pewter")
        for z in (-15,-25,-35):
            orb(g,bp(x,z,37.9),(1.1,.62,1.25),"shade",9,6,(side,back,UP))
    path=[bp(x,-1,d) for x,d in ((-80,-20),(-83,3),(-81,26),(-73,35),(-47,37),
                                    (0,39),(47,37),(73,35),(81,26),(83,3),(80,-20))]
    strap(g,[V(p) for p in smooth(path,3)],5.4,"harness",UP,2.0,False)
    for s in (-1,1):
        bow=[bp(s*x,z,d) for x,z,d in ((0,80,3),(9,92,3),(21,102,5),(29,99,6),
                                         (28,91,9),(18,86,11),(0,83,9))]
        tube(g,bow,[3,3,3,3.2,3,3,2.8],"cord",9,4)
        tube(g,[bp(0,82,8),bp(s*10,79,17),bp(s*18,74,25),bp(s*23,70,25)],[3,2.7,2.3,1.3],"cord",8)
    orb(g,bp(0,83,7),(5.1,4.1,4.6),"harness")
    g.part("17 双肩鞣皮肩带")
    paths=[[(137,253,63),(144,236,43),(151,225,21),(159,223,0),(170,228,-26),
            (183,243,-43),(194,266,-54),(198,287,-61),(193,314,-66),
            (178,353,-60),(161,382,-42),(155,391,-15),(149,378,40)],
           [(286,251,62),(291,232,44),(295,222,24),(299,218,2),(307,224,-23),
            (316,245,-41),(322,268,-46),(326,287,-49),(334,311,-39),(338,347,-27),(324,368,35)]]
    def clothing(labels):
        vertices=[];polys=[]
        for label,points,faces,_,_ in g.parts:
            if label.startswith(labels):
                offset=len(vertices)
                vertices.extend(points)
                polys.extend([offset+i for i in face] for face in faces)
        return BVHTree.FromPolygons(vertices,polys)
    shoulders=clothing(("06 ","08 ","09 ","10 "))
    breast=clothing(("06 ","08 "))
    for sign,path in zip((-1,1),paths):
        anchors=[bp(sign*58,62,-26),bp(sign*62,74,-22)]+[P(*s) for s in path[1:]]
        anchors+=[bp(sign*71,-51,-31),bp(sign*61,-56,-24)]
        pts=[V(p) for p in smooth(anchors,4)]
        for j,p in enumerate(pts[4:-5],4):
            if p.z>795-253:
                hit=shoulders.ray_cast(p+UP*80,-UP,150)[0]
                if hit is not None and abs(hit.z-p.z)<24: pts[j].z=hit.z+1.4
            elif p.y<-23:
                hit=breast.ray_cast(p+FRONT*115,-FRONT,195)[0]
                if hit is not None and abs(hit.y-p.y)<19: pts[j].y=hit.y-1.3
        strap(g,pts,10,"harness",RIGHT,2.8)
        x,y,d=path[7]
        buckle_center=min(pts,key=lambda p:(p-P(x,y,d)).length_squared)+FRONT*4
        buckle(g,buckle_center,RIGHT,UP,10.9,11.4,2.4,"brass")


def purse(g):
    g.part("18 束口钱袋与系绳")
    c=P(173,470,-64)
    rows=[]
    shape=smooth([(-47,6,5),(-45,19,13),(-37,30,20),(-23,34,23),(-4,30,20),
                 (11,17,13),(18,13,10),(24,19,13),(35,22,14)],3)
    for z,w,d in shape:
        ring=[]
        for k in range(40):
            a=k*tau/40
            r=(1.15+2.15*exp(-((z-22)/11)**2))*cos(6*a+z*.033)
            ring.append(c+V(((w+r)*sin(a)+.12*z,-(d+r)*cos(a),z+max(0,(z-17)/18)*4*cos(5*a))))
        rows.append(ring)
    g.grid(rows,"satchel")
    for a in (-1.7,0,1.5):
        seam=[r[round((a%tau)/tau*40)%40]+FRONT*.5 for r in rows[3:-8]]
        trim(g,seam,.77,"welt",sides=6)
    for z in (16.5,19.5):
        pts=[c+V((15*sin(a)+2,-12.8*cos(a),z+1.2*sin(2*a))) for a in [k*tau/32 for k in range(32)]]
        trim(g,pts,1.6,"cord",True)
    tube(g,[c+V((3,-11,18)),c+V((14,-16,14)),c+V((18,-20,-2)),c+V((20,-20,-9))],
         [2.1,2,1.6,.9],"cord",8)
    tube(g,[c+V((5,3,20)),P(182,436,-54),P(185,418,-47),P(188,413,-41)],[3,3.4,3.5,2.8],"harness")
