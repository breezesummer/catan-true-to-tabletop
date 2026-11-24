"""商路上的三名强盗。原型左右指画面左右，角色以错步和遮挡组成三角群像。"""
from math import pi,tau,sin,cos,exp
from sculpt import Figure,V,P,C,S,RIGHT,FRONT,UP,curve,loft,sweep,tube,stroke,orb,trim,stitches,ribbon
from mathutils.bvhtree import BVHTree
from portraits import Head
from hand_mesh import fit_hand
from gestures import NAMES
from tailoring import legs,garment,sleeve,arm,patch,fitted_patch,belt,toggles,mantles


def head_and_neck(g,role):
    g.part=role+" · 头颈与肖像"
    head=Head(role)
    head.face(g)
    g.part=role+" · 发髭"
    if role!="leader":
        head.hair(g)
    head.beard(g)


def sack(g):
    def folds(t,a):
        peak=.85+4.3*exp(-((t-.18)/.17)**2)+1.8*exp(-((t-.45)/.19)**2)
        # 掌根压进袋身。斜褶止于指缘，粮粒的重量往掌下和袋腹两侧走。
        grip=exp(-((a-.30)/.60)**4)
        depression=-3.4*exp(-((t-.735-.032*a)/.066)**2)
        gather=(2.7*exp(-((t-.79+.095*a)/.035)**2)-1.8*exp(-((t-.82+.08*a)/.025)**2))
        return (peak*sin(8*a+1.4*t)+.48*peak*cos(13*a-2*t)+grip*(depression+gather),
                1.4*sin(3*a+.6)*t+1.5*cos(7*a)*exp(-(t/.15)**2))
    body=loft(g,[(276,159,18,8,7),(274,161,18,20,13),(273,167,18,26,18),
            (272,177,17,17,12),(271,183,16,9.2,9),(270,190,16,12,11),
            (263,204,16,22,17),(260,221,16,36,23),(260,244,16,44,26.5),
            (260,267,17,46,28),(258,283,19,38,23),(260,292,21,20,15)],
         "sack",sides=55,steps=3,warp=folds,density=1.46,paint="sack")
    for i in range(3):
        path=[]
        for j in range(41):
            a=tau*j/40
            path.append(P(271+(10+.3*i)*sin(a),180.7+i*1.7+cos(a+1.1)*1.3,
                          16-(9.7+.2*i)*cos(a)))
        trim(g,path,.86,"twine",sides=8)
    for s in (-1,1):
        stroke(g,[(267+s*1.5,185,7.6),(265+s*2,190,5.3),(261+s*3,195,4.5),
                  (259+s*3.2,198,3.5)],[1.0,.9,.7,.38],"twine")
    fitted_patch(g,body,.64,.79,-.83,.23,"sackpatch",phase=.77)


def staff(g):
    path=[V((182.8,58,0)),P(182,448,57),P(180.1,419,56),P(176.5,390,53),
          P(175.3,368,49),P(174,346,46),P(173.2,325,42),P(172.1,306,41)]
    stations=curve([(*p,r) for p,r in zip(path,(3.0,5.0,5.1,5.4,5.3,6.2,7.3,7.1))],4)
    pts=[V(n[:3]) for n in stations]
    rows=sweep(g,pts,[(v[3]*.93,v[3]) for v in stations],"wood",sides=20,ref=RIGHT,
               warp=lambda t,a:.37*sin(5*a+3*t)+.2*cos(8*a-t),density=1.12,paint="wood")
    for i in (2,7,14):
        trim(g,[r[i] for r in rows[3+i%3:-4-i%2]],.22,"bark",sides=5)
    for row,x,d in ((377,174.5,48),(409,179,54)):
        n=V((-.66,-.75,0));u=UP.cross(n)
        c=P(x,row,d)+n*4.3
        rings=[[c+(u*(2.05*r*sin(a))+UP*(3.6*r*cos(a)))+n*(.45+.3*r)
                for a in [tau*k/15 for k in range(15)]] for r in (.3,.63,.91,1.)]
        g.grid(rings,"wood")


def armhole_binding(g,sleeves,vest):
    """沿背心和卷袖的实际交线包边，使滚边随肩面起伏。"""
    def tree(grid,wrap):
        n=len(grid[0])
        faces=[(j*n+k,j*n+(k+1)%n,(j+1)*n+(k+1)%n,(j+1)*n+k)
               for j in range(len(grid)-1) for k in range(n if wrap else n-1)]
        return BVHTree.FromPolygons([p for row in grid for p in row],faces)
    jacket=tree(vest,False)
    axis=V((0,C,-S))
    for sleeve,side,guess in zip(sleeves,(1,-1),(218,330)):
        linen=tree(sleeve,True)
        binding=[]
        for step in range(40):
            t=step/39
            row=150+43*t
            target=guess+side*(1.7-4.6*t)
            def depths(x):
                origin=P(x,row,-140)
                return [b.ray_cast(origin,axis,330)[0] for b in (linen,jacket)]
            crossings=[];previous=None
            for k in range(-22,23):
                x=target+k*.73
                s,v=depths(x)
                front=None if s is None and v is None else (v is not None and (s is None or v.y<s.y))
                if front is not None and previous is not None and front!=previous:
                    crossings.append(x-.365)
                previous=front
            if not crossings:continue
            x=min(crossings,key=lambda xc:abs(xc-target))
            strip=[]
            for j in range(9):
                q=2*j/8-1
                width=.5+2.05*sin(pi*t)**.61
                hits=[p for p in depths(x+q*width) if p is not None]
                if not hits:break
                strip.append(min(hits,key=lambda p:p.y)-axis*(.14+.63*(1-q*q)*sin(pi*t)))
            if len(strip)==9:binding.append(strip)
        if len(binding)>2:
            g.grid(binding,"russet",wrap=False,density=1.7)
            stitches(g,[r[5]-axis*.18 for r in binding],"thread",spacing=3.85,length=1.24,radius=.19)


def carrier(g):
    legs(g,"carrier")
    g.part="carrier · 亚麻衬衣与卷袖"
    garment(g,[(278,131,60,16,16),(276,145,59,34,21),(274,162,60,47,24),
             (272,191,60,45,23),(272,233,63,44,24),(270,277,67,44,24),
             (269,308,68,53,24)],"linen",opening=lambda t:.007+.83*(1-t)**3,
             rag=1.65,crease=.8,paint="linen",neckline=11)
    # 内衫领围在喉结下包住颈根，低头一侧贴近胡须，落到锁骨处再张开。
    collar=loft(g,[(278,131,56,14.1,12.8),(278,134.5,56,16.1,15.4),
                   (278,143,56,19.3,18.1),(276,155,58,31,21.0),(275,162,60,37,22.5)],
                "underlinen",sides=56,steps=4,density=1.5,
                warp=lambda t,a:(.55*sin(7*a+2*t),10.3*max(0,cos(a))**3*(1-t)))
    g.charts[-1].caps=False
    trim(g,collar[0],.55,"underlinen",True)
    left=sleeve(g,[(222,157,63),(206,172,63),(196,195,55),(185,214,45),(181,235,42)],
           [(16,18),(18,18),(16,19),(14,14),(15,13)],"linen","linen",rolled=True)
    right=sleeve(g,[(326,153,59),(332,164,56),(327,178,48),(320,194,34),(315,201,27)],
           [(16,17),(18,19),(16,17),(16,15),(14,13)],"linen","linen",rolled=True)
    g.part="carrier · 粗呢开襟背心"
    vest=garment(g,[(277,133,60,23,17),(275,142,60,47,23),(273,150,59,67,31),
            (272,168,58,57,33),(270,201,61,53,34),(270,237,64,56,34),
             (269,272,66,60,36),(266,305,69,68,37)],"russet",rag=2.35,crease=1.3,
            opening=lambda t:.055+.44*(1-t)**2+.018*sin(t*9),paint="russet",neckline=10)
    for edge in ([r[0] for r in vest],[r[-1] for r in vest]):
        stitches(g,edge,"hemp",spacing=3.45,length=1.5,radius=.32)
    armhole_binding(g,(left,right),vest)
    patch(g,P(276,265,103),15,15.7,"vestpatch",n=-FRONT,phase=.3)
    head_and_neck(g,"carrier")
    g.part="carrier · 麻袋、扎绳与木杖"
    sack(g);staff(g)
    g.part="carrier · 裸臂与手"
    arm(g,[(181,237,42),(180.5,252,39),(175,270,39),(171.3,280,40),(169.7,287.3,40.3)],
        [(12.5,12.7),(15,13.7),(12.5,11.8),(10,9),(9,8)],"staff")
    arm(g,[(315,201,27),(311,212,21),(304,222,14),(296,228,7),(290,234.2,-1)],
        [(13,12.2),(14.5,13),(13.5,12.5),(11.0,10.1),(8.5,7.4)],"embrace")


def hood(g):
    outline=[(395,140,-23),(406,144,-23),(417,157,-23),(423,174,-21),(423,185,-17),
             (419,193,-14),(410,198,-12),(398,204,-13),(382,201,-13),(370,195,-14),
             (361,186,-17),(363,172,-22),(371,155,-25),(382,145,-25)]
    # 帽檐先向前反折，再向上、向后隆起。帽顶偏向左后，后脑收成软尖。
    envelope=[(386,122,3),(399,126,5),(414,135,6),(430,157,3),(438,181,1),
              (436,193,0),(420,204,1),(399,215,3),(378,210,4),(359,204,2),
              (348,190,1),(350,176,2),(361,151,3),(371,133,4)]
    rim=curve(outline,4,True); shell=curve(envelope,4,True);outer=[]
    for j in range(23):
        t=j/22
        section=[]
        for k,(p,q) in enumerate(zip(rim,shell)):
            a=tau*k/len(rim)
            if t<.50:
                u=sin(t*pi)
                x=p[0]+(q[0]-p[0])*u; row=p[1]+(q[1]-p[1])*u;d=p[2]+(q[2]-p[2])*t*2
            else:
                u=2*t-1
                rr=cos(u*pi*.48)
                x=385+(q[0]-385)*rr-4*u;row=176+(q[1]-176)*rr+9*u*u
                d=q[2]+39*sin(u*pi*.51)
            ridge=sin(pi*t)*(.75*cos(7*a+3*t)+.62*sin(4*a-2*t))
            section.append(P(x+ridge*sin(a),row+.66*ridge*cos(a),d+ridge*.44))
        outer.append(section)
    g.grid(outer,"moss",density=1.67,paint="moss",caps=False)
    centre=P(394,175,4)
    lining=[]
    for i,section in enumerate(outer):
        t=i/(len(outer)-1)
        lining.append([centre+(p-centre)*.964+V((0,.9*(1-t),0)) for p in section])
    g.grid(lining,"lining",density=1.03,caps=False)
    g.grid([outer[0],lining[0]],"binding",caps=False)
    # 后尖和衬里底封口，帽檐留作真正的面部开口。
    g.grid([outer[-1],[p.lerp(P(381,185,42),.94) for p in outer[-1]]],"moss")
    g.grid([lining[-1],[p.lerp(P(381.5,185,40),.94) for p in lining[-1]]],"lining")
    trim(g,outer[0],.98,"binding",True,8)
    seam=[r[0]+UP*.40 for r in outer]
    trim(g,seam,.60,"binding");stitches(g,[p+RIGHT*.91 for p in seam],"hemp",spacing=3,radius=.24)
    stitches(g,outer[1]+[outer[1][0]],"hemp",spacing=3.4,length=1.5,radius=.28)


def knife(g):
    # 短刀作为原型棋子的静态配饰，刃面、刃脊和旧皮柄分开着色。
    a=P(467,336,-44);b=P(457.7,355,-49)
    tube(g,[a,a.lerp(b,.22),a.lerp(b,.8),b],[4.0,5.6,5.45,4.65],"boot",sides=16)
    dir=(b-a).normalized()
    cross=V((.91,0,-.41)).normalized()
    n=dir.cross(cross).normalized()
    if n.dot(FRONT)<0:n=-n
    guard=b+dir*.7
    tube(g,[guard-cross*9.8-UP*1.2,guard-cross*5+n*.55,guard,guard+cross*6,guard+cross*9.4-UP*1.8],
         [1.35,2.0,2.15,1.9,1.2],"brass",sides=10)
    stations=[(458,356,-49,4.7),(455.8,364,-50,5.25),(450.1,383,-51,5.7),
              (445.4,398,-52,5.15),(441.8,408,-52,3.8),(437.8,419,-51.3,.20)]
    rows=[]
    for x,row,depth,w in stations:
        c=P(x,row,depth)
        rows.append([c+cross*k*w+n*f for k,f in ((-1,0),(-.64,.74),(.06,1.15),(.91,.16),
                                                   (1,0),(.07,-.83),(-.68,-.5))])
    g.grid(rows,"steel",density=1.55,paint="steel",smooth=False)
    g.grid([[r[0],r[1]] for r in rows],"edge",wrap=False,smooth=False)


def leader(g):
    legs(g,"leader")
    g.part="leader · 衬衣与分片皮袄"
    garment(g,[(394,207,0,15,10),(393,220,0,33,17),(393,250,0,36,19),(391,284,0,29,16),
            (390,309,0,33,18),(388,331,2,40,19),(388,362,3,54,27)],"hem",
            opening=lambda t:.003+.05*t**5,rag=2.3,crease=.9,paint="hem")
    coat=garment(g,[(394,209,0,19,15),(393,222,0,45,23),(393,241,-1,49,27),
            (394,265,0,46.5,27.8),(394,289,0,38.5,25.5),(393,300,0,40,27),
            (392,320,0,48,27),(391,340,1,58,30)],"jerkin",rag=.72,crease=1.0,
            opening=lambda t:.008+.285*max(0,(t-.61)/.39)**1.35,paint="jerkin")
    for i in (1,-2):
        stitches(g,[r[i]+FRONT*.35 for r in coat],"hemp",spacing=3.3,radius=.28)
    belt(g,395,296,-1,41.8,28,5)
    toggles(g,[(401,246,-30),(401,274,-29)])
    for index in (-1,1):
        coords=([(345,230,-1),(332,241,-7),(326,255,-20),(332,267,-38),(344,254,-53)]
                if index<0 else [(444,240,1),(453,254,0),(458,276,-6),(462,294,-17),(463,312,-29)])
        rads=([(13,14),(15.4,15),(13,13.5),(12,13),(10.1,9)] if index<0 else
              [(15,14),(16,15),(13.4,13),(13,13.5),(11,10)])
        sleeve(g,coords,rads,"linen","linen",rolled=True)
    head_and_neck(g,"leader")
    g.part="leader · 披风与兜帽";mantles(g,True);hood(g)
    g.part="leader · 伸掌、握手与垂刀"
    arm(g,[(343,255,-52),(347,252,-57),(349,250,-60),(351,248.3,-63.3)],
        [(9.1,8),(9.3,8.8),(8.7,8.1),(8.4,7.1)],"palm")
    arm(g,[(462.6,311.5,-27),(463.2,315,-30.8),(464,318.2,-33.2),(464.4,320.9,-34.8)],
        [(9.2,8.2),(9.1,8.2),(8.6,7.7),(8.2,7.3)],"knifehand")
    knife(g)


def cap(g):
    head=Head("scout")
    def points(row,rx,ry,lean,tilt,wave):
        ring=[]
        for k in range(56):
            a=-pi+tau*k/56
            tuck=wave*(cos(5*a+.3)+.53*sin(8*a+.7))
            raise_edge=4.25*max(0,min(1,(row-107)/30))
            ring.append(head.point((rx+tuck)*sin(a),lean+(ry+tuck)*cos(a),
                                   row-tilt*cos(a)+wave*.7*sin(3*a-.3)-raise_edge))
        return ring
    stops=[(102.3,1.5,2.3,-11,.5,.16),(104,9,9,-11,1.8,.6),(109,16,17,-9,3,.92),
           (117,21,22,-5.7,5.8,1.05),(127,23.4,26,-3.2,10.3,.68),(133,23.8,26,-2,12.6,.52),
           (138,23,25,-1.7,14.2,.27)]
    crown=[points(row,rx*1.07,ry*1.08,lean,tilt,wave) for row,rx,ry,lean,tilt,wave in curve(stops,3)]
    g.grid(crown,"cap",density=1.58,paint="cap",caps=False)
    peak=sum(crown[0],V((0,0,0)))/len(crown[0])
    g.grid([[peak+(v-peak)*.08+UP*.19 for v in crown[0]],crown[0]],"cap")
    rings=[points(*p) for p in curve([(127.6,25.1,27.9,-2.2,12.1,.48),(128.8,26.6,29.6,-2.2,12.3,.48),
           (130.8,27,29.8,-2.2,12.8,.47),(135.2,26.85,29.7,-1.7,13.7,.38),
           (138.0,26.45,29.1,-1.5,14,.35),(139.7,25.7,28.3,-1.5,14.0,.28),(140.1,24.25,27.0,-1.5,14,.19)],3)]
    g.grid(rings,"cap",density=1.34,paint="cap",caps=False)
    for r in (rings[1],rings[-2]):trim(g,r,.52,"hatwelt",True)


def satchel(g):
    # 软底袋有箱褶、侧围和折回的袋盖。
    cx,cy=530,25
    def shape(t,a):
        return .65*cos(4*a)+.35*sin(7*a+2*t),1.8*sin(a)
    rows=loft(g,[(527,282,29,13,6.5),(527,287,28,18.5,9),(530,295,26,20,11),
             (532,310,25,20,12),(533,323,26,17,10),(532,330,28,11,7)],
         "satchel",warp=shape,sides=38,density=1.4,paint="satchel")
    flap=[]
    for j in range(11):
        t=j/10;line=[]
        for k in range(21):
            u=k/10-1
            line.append(P(cx+u*(18.3-1.8*t)+2.5*t,282+16*t+2.5*u+3*t**4*(1-u*u),
                          cy+6-21*sin(t*pi*.52)+3.8*u*u))
        flap.append(line)
    g.grid(flap,"satchel",wrap=False,density=1.3,paint="satchel")
    trim(g,flap[-1],.60,"welt");stitches(g,flap[-2],"hemp",spacing=3.1,radius=.26)
    c=P(533,298,10);ribbon(g,[c+UP*4,c,c-UP*4],[1.35,1.5,1.15],"belt",thickness=.6)
    orb(g,c+FRONT*.95,(1.15,.65,1.21),"brass",sides=10,bands=5)
    for k in (6,31):
        trim(g,[r[k] for r in rows],.55,"welt")


def scout(g):
    legs(g,"scout")
    g.part="scout · 短袍、卷袖与斜挎带"
    garment(g,[(493,189,67,11,10),(493,195,68,19,12),(495,207,68,29,16),
            (497,238,69,30,19),(496,267,70,28.7,21.2),(496,288,70,32,23),
            (496,313,72,40,30),(492,338,73,45,33)],"smock",
            opening=lambda t:.06+.08*t**3,crease=1.22,rag=1.5,paint="smock")
    sleeve(g,[(474,203,68),(460,217,62),(450,234,54),(456,250,48),(474,274,40)],
           [(13.5,14),(13,13),(12,12.5),(10.8,11.3),(9.1,9.0)],"smock","smock",rolled=True)
    sleeve(g,[(519,194,69),(538,191,68),(558,174,65),(565,164,62),(558,152,58)],
           [(14,14.5),(14,15.5),(14,13.8),(15.7,15),(10.25,10)],"smock","smock",rolled=True)
    belt(g,496,272,70,30.6,23.5,1,True)
    ribbon(g,[P(516,189,54),P(513,208,46),P(507,227,43),P(500,244,43),
              P(492,265,44),P(493,277,43),P(489,289,44),P(486,298,48)],
           [4.3,4.7,4.7,4.7,4.7,4.2,4.0,3.8],"strap",detail=True)
    ribbon(g,[P(516,188,56),P(520,183,69),P(517,192,83),P(510,213,93),
              P(493,247,94),P(477,275,81),P(481,290,62)],[4.2]*7,"strap",normal=-FRONT,detail=True)
    satchel(g)
    for x,start in ((516,514),(533,522)):
        ribbon(g,[P(start,272,54 if x==516 else 61),P((start+x)*.5,277,43),P(x,283,31),P(x,287,24)],
               [2.0,2.0,1.9,1.6],"strap",thickness=.65,detail=True)
    head_and_neck(g,"scout")
    g.part="scout · 翻边帽与蓝灰披肩";cap(g);mantles(g,False)
    g.part="scout · 遮眉与扶带的手"
    arm(g,[(558,152,58),(555,146,55.5),(551.9,139.9,53.5),(549.5,134.8,52.6)],
        [(8.8,9),(10,10.3),(8.4,7.8),(7,6.5)],"visor")
    arm(g,[(471,271,41),(473.6,273,40.2),(475.4,274.6,39.6),(477.2,276.1,39.2)],
        [(8.6,8.1),(8.5,7.8),(7.8,7.1),(7.2,6.3)],"straphand")


def assemble():
    g=Figure()
    carrier(g);scout(g);leader(g)
    for ch in g.charts:
        if ch.skin_region == "forearm":
            fit_hand(ch,g.charts)
            ch.part = ch.part.split(" · ")[0]+" · "+NAMES[ch.paint]
    return g
