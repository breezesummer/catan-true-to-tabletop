"""六码头货堆的造型，局部原点位于装卸平台。"""
import math
import random
from itertools import combinations
from mathutils import Matrix, Quaternion
from sculpt import BARK,CUT,CLAY,ORE,GRAIN,FLEECE,CORD,METAL,OAK,QUARTZ,WORN_IRON,CLEAVAGE,TAU,UP,frame,vec

def twine(m,path,r=.14):
    if m.proxy:
        return
    path=[vec(p) for p in path]
    bases=[];distance=0;u=None
    for i,p in enumerate(path):
        tangent=(path[min(i+1,len(path)-1)]-path[max(i-1,0)]).normalized()
        u=frame(tangent)[0] if u is None else (u-tangent*u.dot(tangent)).normalized()
        if i: distance+=(p-path[i-1]).length
        bases.append((p,u,tangent.cross(u),distance))
    for strand in range(3):
        curve=[]
        for p,u,v,distance in bases:
            phase=distance/(r*6.5)*TAU+strand*TAU/3
            curve.append(p+r*.41*(u*math.cos(phase)+v*math.sin(phase)))
        m.tube(curve,r*.73,CORD,.97+.015*strand,8,detail=True)

def log(m,x,y,z,r,length,seed):
    n=12 if m.proxy else 60
    levels=4 if m.proxy else 15
    # 交错的斜锯口在正俯视下也露出浅色木芯。收短轴向长度，控制两端占地。
    bevel=.72+.17*math.sin(1.3*seed+.4)
    length-=2*r*(bevel-.46)
    rings=[]
    for j in range(levels):
        f=j/(levels-1)
        layer=[]
        for i in range(n):
            a=TAU*i/n
            rad=r*(1+.037*math.sin(a*5+seed)+.030*math.cos(a*9+seed*.3)+.022*math.sin(15*a+f*2.3))
            h=math.sin(a)*rad
            end=(bevel*h+.045*math.cos(a*7))*(2*f-1)
            layer.append((x+math.cos(a)*rad+.14*f,y+length*(.5-f)+end,z+h))
        rings.append(layer)
    m.shell(rings,BARK,.97,True,cap=False)
    # 树皮围住切面；浅色边材与错心的年轮共面。
    for front,row in enumerate((rings[0],rings[-1])):
        cy=y+length*(.5-front);cx=x+.14*front
        inner=[(cx+(px-cx)*.935,cy+(py-cy)*.935,z+(pz-z)*.935) for px,py,pz in row]
        for i in range(n):
            j=(i+1)%n
            rim=[row[i],row[j],inner[j],inner[i]]
            if not front:rim.reverse()
            m.face([m.vertex(p) for p in rim],((.12,.3),(.24,.30),(.25,.4),(.13,.40)),BARK,.83)
        ids=[m.vertex(p) for p in inner]
        angle=seed*1.73+front*.94
        radius=.46-.024*math.sin(angle)
        uv=[(.50+.024*math.sin(seed)+radius*math.cos(TAU*i/n+angle),
             .50+.027*math.cos(seed)+radius*math.sin(TAU*i/n+angle)) for i in range(n)]
        m.face(ids if front else ids[::-1],uv if front else uv[::-1],CUT,.91+.08*math.cos(seed),
               cut_variant=(round(seed/1.38)+2*front)%4)
    if m.proxy:
        return
    for side in (-1,1):
        def end_point(a,t):
            dx=r*t*math.cos(a); dz=r*t*math.sin(a)
            return vec((x+dx+(.14 if side<0 else 0),y+side*(length*.5-bevel*dz+.055),z+dz))
        for split in range(1+(int(seed)%2)):
            a=.85+seed+side*.41+split*2.21
            reach=.46+.20*math.sin(seed*1.7+side)
            m.tube([end_point(a,.95),end_point(a+.034,.71),end_point(a-.06,reach)],
                   [.035,.023,.006],BARK,.65,5,detail=True)
    # 翘起的树皮和断枝绕着主干布置，端面斜切以露出髓心与年轮。
    for k in range(3):
        a=.58+k*.89+seed*.38
        strip=[]
        for j in range(14):
            f=.10+.065*math.sin(k+seed)+j*(.043+.007*math.cos(k*2.2+seed))
            b=a+.075*math.sin(f*6+k)
            width=.016+(.11+.075*math.sin(k+seed))*max(0,math.sin(math.pi*j/13))**.65
            row=[]
            for side in (-1,1):
                angle=b+side*width
                rad=r*(1+.037*math.sin(angle*5+seed)+.030*math.cos(angle*9+seed*.3)+.022*math.sin(15*angle+f*2.3))+.022
                row.append(m.vertex((x+rad*math.cos(angle)+.14*f,
                                     y+length*(.5-f)+(bevel*math.sin(angle)*rad+.045*math.cos(angle*7))*(2*f-1),
                                     z+rad*math.sin(angle))))
            strip.append(row)
        for j in range(13):
            m.face((strip[j][0],strip[j][1],strip[j+1][1],strip[j+1][0]),
                   ((.19,j/13),(.75,j/13),(.75,(j+1)/13),(.19,(j+1)/13)),OAK,.82+.05*k)
    start=vec((x+.60*r,y+.15*length,z+.65*r))
    d=vec((.63,-.22,.76));u,v=frame(d)
    m.shell([[start+d*t+rad*(u*math.cos(TAU*i/18)+v*math.sin(TAU*i/18)) for i in range(18)]
             for t,rad in [(0,r*.36),(.45,r*.32),(.68,r*.24)]],BARK,.88,True,CUT)

def wood(m):
    # 五根原木错头垛放，末端从三个方向都能看出圆形剖面。
    for i,(x,y,z,r,l) in enumerate([(-3.02,.40,1.42,1.40,8.8),(-.22,-.65,1.49,1.47,8.9),
        (2.45,.39,1.34,1.32,7.95),(-1.55,-.68,3.95,1.44,7.90),(1.30,.52,3.84,1.36,8.45)]):
        log(m,x,y,z,r,l,i*1.38)
    if m.proxy:
        return
    # 绳索服贴于外层原木，双道绑绳沿一条真实截面收紧。
    outline=[(-4.39,1.05),(-4.29,2.15),(-2.80,4.66),(-2.23,5.22),(-1.64,5.42),(-.6,5.43),
             (1.15,5.28),(1.94,5.16),(2.54,4.66),(3.70,2.02),(3.81,1.17)]
    for shift in (-.24,.25):
        p=[]
        for i,(x,z) in enumerate(outline[:-1]):
            x1,z1=outline[i+1]
            for j in range(6):
                t=j/6;p.append((x+(x1-x)*t,1.20+shift+.11*x,z+(z1-z)*t))
        p.append((3.81,1.20+shift+.11*3.81,1.17))
        twine(m,p,.18)

def brick_block(m,x,y,z,w,d,h,yaw,tone,frog):
    # 每一块坯的八个转角有独立崩口，烧成后表面略有翘曲。
    r=m.rng
    outline=[(-.5+.08*r.random(),-.5),(.33+r.random()*.13,-.5),(.5,-.27-r.random()*.17),(.5,.32+r.random()*.14),
             (.42,.5),(-.41,.5),(-.5,.42),(-.5,-.4)]
    co,si=math.cos(yaw),math.sin(yaw)
    def pos(a,b,height):
        return (x+co*a-si*b,y+si*a+co*b,z+height)
    def level(sx,sy,height,jitter):
        return [pos(u*w*sx,v*d*sy,height+r.uniform(-jitter,jitter)) for u,v in outline]
    rings=[level(.93,.94,.015,0),level(1,1,.15,.034),level(1,1,h-.17,.052),level(.92,.95,h,.068)]
    # 顶面崩去一个不规则三角角块，新鲜断面贯穿侧壁与砖顶。
    corner=r.randrange(len(outline))
    old=vec(rings[-1][corner])
    chipped=old.copy()
    chipped.z-=min(.31,h*.24)
    rings[-2][corner]=(chipped.x,chipped.y,min(chipped.z-.035,rings[-2][corner][2]))
    rings[-1][corner]=chipped
    m.shell(rings,CLAY,tone,False,cap=False)
    top=rings[-1]
    p0=old.lerp(vec(top[corner-1]),.34)
    p1=old.lerp(vec(top[(corner+1)%len(top)]),.43)
    lid=top[:corner]+[p0,p1]+top[corner+1:]
    def face_uv(p):
        dx,dy=p[0]-x,p[1]-y
        return (.5+(co*dx+si*dy)/w,.5+(-si*dx+co*dy)/d)
    m.face([m.vertex(p) for p in lid],[face_uv(p) for p in lid],CLAY,tone)
    m.face([m.vertex(p) for p in (p0,chipped,p1)],[(.30,.55),(.47,.63),(.63,.50)],CLAY,min(1,tone+.09))
    for side in ((top[corner-1],chipped,p0),(chipped,top[(corner+1)%len(top)],p1)):
        m.face([m.vertex(p) for p in side],[(.1,.42),(.6,.41),(.25,.56)],CLAY,tone*.93)
    # 仅个别砖的侧面留下旧窑印，顶面保留完整的烧结表皮。
    if frog:
        for u0,u1 in ((-.33,-.10),(.04,.29)):
            m.tube([pos(u0,-d*.50-.008,.82),pos(u1,-d*.50-.008,.91)],
                   .028,CLAY,tone*.64,5,detail=True)
    # 少量暴露的浅色砂粒，随窑砖的颜色和光照变化。
    for _ in range(6):
        a=r.uniform(-.44,.44)*w;b=r.choice((-1,1))*r.uniform(.37,.43)*d
        m.tube([pos(a,b,h+.017),pos(a+.08,b+.025,h+.025)],.035,CLAY,1,5,detail=True)

def brick(m):
    # 三、二、一错缝压叠的大砖坯；每一层都能从上方分辨出来。
    layout=[(-2.93,-.18,0,2.95,7.25,.02),(.02,.10,0,2.91,7.12,-.025),
            (2.90,-.24,0,2.84,7.30,.045),(-.18,-1.76,1.91,3.35,7.24,1.55),
            (.14,1.60,1.91,3.24,7.14,1.61),(-.47,.21,3.81,3.56,5.91,.16)]
    for i,(x,y,z,w,d,yaw) in enumerate(layout):
        brick_block(m,x,y,z,w,d,2.0,yaw,(.91,.98,.83,.95,.89,1)[i],i==3)
    for x,y,s in [(-3.69,-3.72,.43),(3.6,2.5,.50)]:
        brick_block(m,x,y,.015,s,.92,.40,.27,.82,False)

def rock(m,center,radii,seed):
    # 由断裂平面直接切掉块料的棱角。每个大面共用一个法线，转动时整面同时受光。
    rng=random.Random(319+round(seed*231))
    planes=[]
    for axis in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1)):
        n=vec(axis)+vec(tuple(rng.uniform(-.39,.39) for _ in range(3)))
        planes.append((n.normalized(),rng.uniform(.77,.93)))
    for i in range(10):
        a=TAU*i/10+seed
        n=vec((math.cos(a),math.sin(a),rng.uniform(.56,1.24) if i%3 else -.78)).normalized()
        planes.append((n,rng.uniform(.76,1.06)))
    corners=[]
    for a,b,c in combinations(planes,3):
        rows=Matrix((a[0],b[0],c[0]))
        if abs(rows.determinant())<.0001:
            continue
        v=rows.inverted()@vec((a[1],b[1],c[1]))
        if all(n.dot(v)<=d+.0001 for n,d in planes):
            corners.append(v)
    rotation=Quaternion(UP,seed+.34)
    def place(point):
        return vec(center)+rotation@vec(tuple(point[k]*radii[k] for k in range(3)))
    facets=[]
    for j,(n,dist) in enumerate(planes):
        points=[p for p in corners if abs(n.dot(p)-dist)<.0002]
        if len(points)<3:
            continue
        u,v=frame(n);middle=sum(points,vec((0,0,0)))/len(points)
        points.sort(key=lambda pt:math.atan2((pt-middle).dot(v),(pt-middle).dot(u)))
        facets.append((j,n,middle,points,u,v))
    # 大块新劈开的金属面集中在主石和一块前排矿石上，四周保留氧化暗壳。
    target=vec((.40*math.cos(seed),-.39,.88)).normalized()
    hero=max(facets,key=lambda f:f[1].dot(target)+.035*sum((p-f[2]).length_squared for p in f[3]))[0]
    for j,n,middle,points,u,v in facets:
        edge=[place(p) for p in points];face=[place(middle+(p-middle)*.966+n*.014) for p in points]
        uv=[(.5+.25*(p-middle).dot(u),.5+.25*(p-middle).dot(v)) for p in points]
        stripe=(j+round(seed*4))%6
        mat=CLEAVAGE if j==hero or (n.z>.25 and stripe in (1,4)) else ORE
        tone=rng.uniform(.81,1) if mat!=ORE else rng.uniform(.73,.96)
        if m.proxy:
            m.face([m.vertex(p) for p in edge],uv,mat,tone)
            continue
        m.face([m.vertex(p) for p in face],uv,mat,tone)
        for k,p in enumerate(edge):
            l=(k+1)%len(edge)
            m.face([m.vertex(pt) for pt in (p,edge[l],face[l],face[k])],
                   ((.2,.3),(.78,.3),(.78,.39),(.2,.39)),mat,min(1,tone+.07))
        if j==hero:
            large=center[2]>3 or seed==0
            # 鲜银断口延至棱边，另一端咬进风化外壳。
            factors=[(.94 if k<2 else .60+.29*math.sin(2.4*k+seed))*(1 if large else .60) for k in range(len(points))]
            patch=[place(middle+(p-middle)*factor+n*.024) for p,factor in zip(points,factors)]
            m.face([m.vertex(p) for p in patch],uv,QUARTZ,1 if large else .89)

def ore(m):
    for j,(x,y,z,rx,ry,rz) in enumerate([(-2.13,-1.95,1.52,2.35,2.28,1.63),(1.99,-2.02,1.49,2.17,2.02,1.55),
        (-2.08,2.03,1.60,2.24,2.14,1.70),(2.11,2.04,1.45,2.13,2.19,1.61),(-.50,.18,4.43,2.56,2.10,2.04)]):
        rock(m,(x,y,z),(rx,ry,rz),j*.73)

def grain_kernel(m,base,tip,width,tone):
    base,tip=vec(base),vec(tip)
    axis=tip-base;d=axis.normalized()
    u,v=frame(d);sides=7 if m.proxy else 13
    layers=[]
    for f,r in [(0,.15),(.15,.69),(.38,1),(.61,.91),(.83,.53),(1,.07)]:
        c=base+axis*f
        layers.append([c+width*r*(u*math.cos(TAU*i/sides)+v*.74*math.sin(TAU*i/sides)) for i in range(sides)])
    m.shell(layers,GRAIN,tone)
    if not m.proxy:
        # 胚粒中央隆起一条颖脊，细芒从颖尖长出。
        m.tube([base+axis*.18+UP*width*.41,base+axis*.50+UP*width*.70,tip],
               [.034,.048,.018],GRAIN,1,6,detail=True)
        m.tube([tip,tip+d*.44+UP*.06,tip+d*.91+UP*.11],[.054,.032,.009],GRAIN,tone,5,detail=True)

def wheat(m):
    # 两把麦沿同一装卸方向摆放，穗头向两侧散开，根部露出切齐的空心秆。
    for side in (-1,1):
        x=side*2.04;yc=side*.22
        for j in range(10):
            a=j*2.39996
            r=math.sqrt((j+.4)/10)
            dx=math.cos(a)*r; dz=math.sin(a)*r
            sway=math.sin(2.75*j+.84*side)
            stem=vec((x+dx*.74,-2.70+yc,1.30+dz*.63))
            root=vec((x+dx*1.28-side*.25,-4.36+yc+.22*math.cos(a),.95+dz*.73))
            head=vec((x+dx*1.35+side*.12,-1.83+yc+.44*dx+.16*sway,1.79+dz*.74))
            axis=vec((dx*.32+side*.12,.94,.32+dz*.30)).normalized()
            length=4.20+.74*math.sin(a*.9)+.24*sway
            def rachis(t):
                return head+axis*(length*t)+vec((.43*sway,0,-.52-.20*dz))*(t*t)
            tip=rachis(1)
            m.tube([root,stem,head,rachis(.50),tip],[.225,.19,.15,.10,.039],GRAIN,.85+.14*(j%4)/3,9)
            if m.proxy:
                m.tube([head,rachis(.1),rachis(.66),tip+axis*.68],
                       [.35,.88,.68,.055],GRAIN,sides=8)
                continue
            spread=vec((1,0,.18*dx))
            rows=5+(j+int(side>0))%3
            for k in range(rows):
                t=k/rows
                for wing in (-1,1):
                    start=rachis(t)+axis*(.10 if wing>0 else 0)
                    end=start+axis*(1.32-.22*t)+spread*(wing*(.62-.20*t))+UP*.16
                    grain_kernel(m,start,end,(.47-.105*t)*(1+.07*sway),.85+.15*((k+j)%4)/3)
            if not m.proxy:
                # 麦叶保留狭长尖端、中央折脊和清楚的外轮廓。
                if j%3==0:
                    m.tube([stem,head+vec((side*.35,-.55,.25)),head+vec((side*.94,.43,.58))],
                           [.035,.21,.016],GRAIN,.82,7,flat=.20,detail=True)
        for band in range(3):
            path=[]
            for k in range(65):
                a=TAU*k/64
                path.append((x+1.0*math.cos(a),-2.74+yc+.17*band+.08*math.cos(a),1.30+.84*math.sin(a)))
            twine(m,path,.135)

def fleece_ball(m,center,radii,axis,seed):
    c=vec(center);d=vec(axis).normalized();u,v=frame(d)
    n=16 if m.proxy else 112
    lats=12 if m.proxy else 115
    def point(lat,a,h=0):
        # 将卷层直接压进羊毛卷的连续表皮，绒脊和沟底共用同一曲面。
        corr=(lat/math.pi+.5)*6.35-a/TAU+.074*math.sin(a*3+lat*4+seed)+.043*math.cos(a*5-lat*7)
        crest=math.cos(TAU*corr)
        loft=.20*crest-.084*crest**2
        tuft=.019*math.sin(a*13+lat*19+seed)+.012*math.cos(a*17-lat*31)
        fade=max(0,math.cos(lat))**.34
        swell=(loft+tuft)*fade+h
        f=1+.037*math.sin(3*a+seed)*math.cos(lat)**2+.029*math.cos(5*a-lat*3+seed)*fade
        return c+(u*(radii[0]+swell)*math.cos(a)+v*(radii[1]+swell)*math.sin(a))*math.cos(lat)*f+d*(radii[2]+swell-.31*max(0,math.sin(lat))**15)*math.sin(lat)
    layers=[[point(-1.552+3.104*j/lats,TAU*i/n) for i in range(n)] for j in range(lats+1)]
    m.shell(layers,FLEECE,1)
    if m.proxy:
        return
    # 蓬松的短纤维顺着卷层成束生长，软弯的小钩在轮廓缘形成茸毛。
    for _ in range(94):
        a=m.rng.uniform(0,TAU);lat=m.rng.uniform(-1.35,1.48)
        length=m.rng.uniform(.07,.16);rise=m.rng.uniform(.06,.115)
        path=[point(lat+length*t,a+.07*math.sin(t*2.7),rise*math.sin(t*2.6)) for t in (0,.33,.67,1)]
        m.tube(path,[.026,.032,.018,.006],FLEECE,m.rng.uniform(.96,1),5,detail=True)

def wool(m):
    fleece_ball(m,(-2.15,-1.56,2.00),(2.15,2.06,2.39),(-.24,-.45,.86),.40)
    fleece_ball(m,(2.01,-1.08,2.00),(2.05,1.96,2.31),(.25,-.64,.72),1.9)
    fleece_ball(m,(-.36,2.17,3.46),(2.37,2.18,2.67),(-.18,.16,.97),.85)
    if not m.proxy:
        # 松垂的一小段尾纱将顶团和下方两团自然连起来。
        path=[(-.32+2.0*math.sin(i/30*3.83),-.01-i/30*4,2.85*(1-i/30)**2+.31) for i in range(31)]
        m.tube(path,.13,FLEECE,.95,8,detail=True)

def barrel(m,x,y,r,h,angle):
    # 每块弧形桶板均有两条独立板边。桶盖嵌入桶口，有压低的横缝和独立的木塞。
    n=15
    age=.5+.44*math.sin(angle*3.4-.20)
    levels=[(0,.83),(.09,.87),(.28,.96),(.5,1),(.72,.96),(.92,.86),(1,.84)]
    for k in range(n):
        a0=TAU*k/n+angle+.008;a1=TAU*(k+1)/n+angle-.008
        arc=[a0+(a1-a0)*j/4 for j in range(5)]
        rows=[[(x+r*rad*math.cos(a),y+r*rad*math.sin(a),h*f) for a in arc] for f,rad in levels]
        wear=(k*7+round(13*angle))%15/14
        # 桶板在桶口和腰箍处有不同的磨损；旧桶与替换板不取同一色调。
        repaired=(k+round(11*angle))%15==0
        tint=1 if repaired else .68+.25*wear+.035*math.cos(angle*4)
        board_age=.5 if repaired else min(1,max(0,age+.11*math.cos(k*2.4-angle)))
        grain_start=.06+.37*wear
        for j in range(len(rows)-1):
            for i in range(4):
                ids=[m.vertex(p) for p in (rows[j][i],rows[j][i+1],rows[j+1][i+1],rows[j+1][i])]
                m.face(ids,[(grain_start+i*.12,j/6),(grain_start+(i+1)*.12,j/6),(grain_start+(i+1)*.12,(j+1)/6),(grain_start+i*.12,(j+1)/6)],OAK,tint,True,wear=board_age)
        ids=[m.vertex(p) for p in rows[-1]]+[m.vertex((x+r*.70*math.cos(a),y+r*.70*math.sin(a),h-.07)) for a in arc[::-1]]
        m.face(ids,[(.5+.46*math.cos(a),.5+.46*math.sin(a)) for a in arc]+[(.5+.38*math.cos(a),.5+.38*math.sin(a)) for a in arc[::-1]],CUT,.86)
    # 内侧暗木衬住板缝和桶口，近看不会透过板缝看见海面。
    inner=[[(x+r*rr*math.cos(TAU*j/32),y+r*rr*math.sin(TAU*j/32),ht)
             for j in range(32)] for ht,rr in ((.02,.79),(h*.49,.96),(h-.15,.80))]
    m.shell(inner,OAK,.52)
    for hz,rr in [(h*.12,r*.90),(h*.37,r*.994),(h*.84,r*.917)]:
        if m.proxy:
            continue
        def hoop_point(a,s,off):
            dent=max(0,math.cos(a-angle*2.7-hz))**22
            rad=rr*s-.046*dent
            return (x+rad*math.cos(a),y+rad*math.sin(a),hz+off-.075*dent)
        ring=[[hoop_point(TAU*j/48,s,off) for j in range(48)]
              for off,s in [(0,1),(.04,1.035),(.24,1.035),(.28,1)]]
        m.shell(ring[:3],METAL,.83,True,cap=False,wear=age)
        # 经搬运磨亮的上沿与深色氧化箍分层。
        m.shell(ring[2:],WORN_IRON,.82+.14*math.cos(angle+hz),False,cap=False)
        for j in range(8):
            a=TAU*j/8+angle; p=vec(hoop_point(a,1.03,.15))
            m.tube([p,p+vec((.055*math.cos(a),.055*math.sin(a),0))],.083,WORN_IRON,1,10,detail=True)
    # 桶盖拆成实际弦段。板缝在俯视时也可读出桶口方向。
    rad=r*.78
    for i in range(5):
        lo=-1+i*.4+.010;hi=lo+.38
        poly=[]
        for yy in [lo+(hi-lo)*j/6 for j in range(7)]:
            poly.append((rad*math.sqrt(max(0,1-yy*yy)),rad*yy))
        poly += [(-a,b) for a,b in reversed(poly)]
        co,si=math.cos(angle),math.sin(angle)
        ids=[m.vertex((x+a*co-b*si,y+a*si+b*co,h-.11)) for a,b in poly]
        # 横截木纹随板统一取向。
        m.face(ids,[(.5+b/(2*rad),.5+a/(2*rad)) for a,b in poly],OAK,.87+i*.024,wear=age)
    if not m.proxy:
        m.tube([(x+.28*r,y+.23*r,h-.12),(x+.28*r,y+.23*r,h-.01)],.27,CUT,.69,18,detail=True)

def generic(m):
    for args in [(-2.35,-2.28,2.18,4.65,.27),(2.11,-2.05,2.10,4.02,-.67),
                 (-2.18,2.08,2.14,5.45,1.04),(2.01,2.03,1.91,4.87,.72)]:
        barrel(m,*args)
    # 第五只短桶卧在两只底桶之间，正俯视时仍露出两道横箍。
    m.local_rotation=Quaternion(vec((-1,.13,0)).normalized(),math.radians(83))
    m.offset.z+=5.36*m.scale
    m.local_anchor=vec((-1.15,-1.80,0))
    barrel(m,-1.15,-1.80,1.49,3.65,.48)
    m.offset.z-=5.36*m.scale
    m.local_rotation=None

KINDS={'wood':wood,'brick':brick,'ore':ore,'wheat':wheat,'wool':wool,'generic':generic}
