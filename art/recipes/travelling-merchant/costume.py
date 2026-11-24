"""萨克外袍、马甲、衬衣、灯笼裤与翻口长靴。裁片在腰、肘与膝处收放。"""
from math import sin, cos, pi, tau, exp, asin, sqrt
from workshop import P,V,UP,RIGHT,FRONT,smooth,lerp_profile,orb,trim,tube,sweep,stitches,buckle


def boots_and_breeches(g):
    for sign in (-1,1):
        g.part("04 "+("前靴与左裤腿" if sign<0 else "后靴与右裤腿"))
        x=157 if sign<0 else 276
        depth=-13 if sign<0 else 20
        yaw=-.105 if sign<0 else .84
        forward=V((sin(yaw),-cos(yaw),0))
        side=V((cos(yaw),sin(yaw),0))
        ankle=P(x,795,depth)
        # 鞋楦前宽后窄，楦头上翘；后跟与整圈双层沿条分开。
        def shoe(a,w,front,heel,h):
            ca=cos(a)
            length=front*(1.12 if sign>0 else 1) if ca>=0 else heel
            spread=(1+.16*max(0,ca)-.14*max(0,-ca))
            return ankle+side*(w*spread*sin(a))+forward*(length*ca)+UP*(h+2.2*max(0,ca)**4)
        rows=[]
        for h,w,fore,heel in ((0,31,83,30),(1.6,35,88,32),(5,35.6,89,32),(8,34.8,88,31),(10,33,86,30)):
            rows.append([shoe(k*tau/44,w,fore,heel,h) for k in range(44)])
        g.grid(rows,"sole")
        trim(g,rows[3],1.55,"welt",True)
        stitches(g,[p+UP*.8 for p in rows[4]+[rows[4][0]]],7,3.7,.65,"thread")
        rows=[]
        for h,w,fore,heel in ((8,33,86,30),(16,34,85,31),(23,33,79,31),
                             (29,31,65,30),(35,29,49,28),(42,26,35,27),(52,24,24,25)):
            rows.append([shoe(k*tau/44,w,fore,heel,h) for k in range(44)])
        g.grid(rows,"boots",chart="左鞋面" if sign<0 else "右鞋面")
        # 脚背横向搭片与后帮接缝。
        for level in (0,1):
            arc=[ankle+side*(q*30)+forward*(49-6*q*q-level*8)+UP*(32-13*q*q+level*5)
                 for q in [i/9 for i in range(-9,10)]]
            trim(g,arc,1.15,"welt")
            if level: stitches(g,[p+forward*2+UP*.4 for p in arc],6,3.2,.58)
        rows=[]
        shaft=smooth([(757,25,27,2),(748,26.5,26,1),(739,30,27,-1),(731,27,25,1),
                      (722,29.7,28,0),(714,26,26,0),(705,29,27,-1),(695,28.7,27.5,0),
                      (680,31.7,29,0),(659,32,28,1)],3)
        for row,w,d,shift in shaft:
            ring=[]
            for k in range(44):
                a=k*tau/44
                fold=(3.5*sin((row-700)*.18+1.8*sin(a+.5))+
                      1.5*cos(.29*row-3*a))*exp(-((row-725)/28)**2)
                ring.append(ankle+side*((w+fold)*sin(a))+forward*((d+fold)*cos(a)+shift)+
                            UP*(795-row+1.65*sin(2*a+row*.07)))
            rows.append(ring)
        g.grid(rows,"boots",chart="左靴筒" if sign<0 else "右靴筒")
        heel_seam=[r[22]+forward*(-1) for r in rows]
        trim(g,heel_seam,1,"welt")
        cuff=[]
        for row,w,d in ((650,34,32),(648,37,35),(652,39,36),(668,39,35),(682,43,37),
                        (685,43.5,38),(687,41,36),(683,37,33)):
            cuff.append([ankle+side*((w+1.6*cos(4*a))*sin(a))+forward*((d+1.9*cos(3*a))*cos(a))+
                         UP*(795-row+2.5*cos(a+.75)) for a in [k*tau/44 for k in range(44)]])
        g.grid(cuff,"boots",chart="左靴口" if sign<0 else "右靴口")
        trim(g,cuff[-3],1.15,"binding",True)
        # 裤脚在靴口内聚拢，髋部留有体积供外袍开衩处观察。
        pants=smooth([(506,sign*54+227,44,44,1),(548,sign*55+227,52,49,1.2),
                      (590,sign*57+227,56,53,1.4),(620,sign*58+225,55,49,1.6),
                      (639,x,45,43,1.8),(651,x,33,34,1.1),(663,x,30,30,.6)],4)
        rows=[]
        for row,cx,w,d,f in pants:
            ring=[]
            for k in range(48):
                a=k*tau/48
                ridge=f*(2.15*cos(7*a+row*.026)+1.4*cos(4*a-row*.055)+1.3*sin(3*a+row*.09))
                ring.append(P(cx+(w+ridge)*sin(a),row+3.2*sin(3*a+row*.04),
                              depth*.62-(d+ridge)*cos(a)))
            rows.append(ring)
        g.grid(rows,"breeches",chart="左裤" if sign<0 else "右裤")


COAT=[(215,231,8,53,46,1.2,.84),(225,229,7,84,49,2.2,.69),
      (243,228,7,106,57,3,.60),(268,229,6,110,61,3.4,.53),
      (319,231,4,103,64,3.7,.48),(369,232,4,94,59,3.2,.44),
      (408,234,3,85,53,2.6,.47),(434,233,6,90,57,3.4,.46),
      (483,232,9,108,66,5,.39),(535,229,11,128,80,6.2,.34),
      (579,228,12,143,90,7.6,.32),(600,229,12,146,94,8,.32)]
VEST=[(215,234,4,43,37,.96),(229,232,4,73,48,.84),(253,232,3,85,57,.62),
      (281,234,1,91,61,.21),(298,235,0,91,62,0),(344,236,0,88,60,0),
      (380,236,0,84,56,0),(405,236,1,79,53,0),(424,236,2,79,54,0),
      (456,236,4,80,56,0),(492,237,5,83,61,0),(536,236,5,86,65,0)]
YAW=.38


def coat_point(row,a,push=0):
    x,d,w,h,f,_=lerp_profile(COAT,row)
    angle=a+YAW
    waist=exp(-((row-415)/44)**2)
    shift=(row-421)/189
    ridge=f*(.71*cos(8*a+.55*shift)+.38*cos(13*a-1.47*shift)+.21*sin(5*a+1.5))
    ridge+=waist*(2.5*cos(5*a+(row-414)*.048)+1.3*sin(8*a-row*.065))
    drape=max(0,(row-422)/178)
    y=row+drape**1.8*(8.5*cos(3*a+.4)+6*cos(5*a-1.1)+4.5*cos(a+.7))
    return P(x+(w+ridge+push)*sin(angle),y,d-(h+ridge+push)*cos(angle))


def vest_point(row,a,push=0):
    x,d,w,h,_=lerp_profile(VEST,row)
    angle=a+YAW+.10
    r=1.5*cos(5*a+row*.015)+1.2*sin(8*a+row*.025)
    r+=exp(-((row-398)/22)**2)*3.0*sin(9*a+row*.09)
    return P(x+(w+r+push)*sin(angle),row+max(0,(row-494)/42)*2.5*cos(3*a),d-(h+r+push)*cos(angle))


def layers(g):
    g.part("05 亚麻衬衣与翻领")
    shirt=[];inner=[]
    for row,cx,w,h,d in smooth([(210,232,37,34,2),(225,231,64,45,3),
                                (246,231,79,53,3),(272,233,85,55,4),
                                (315,233,79,53,5)],5):
        gap=max(0,1.12*(267-row)/57)
        angles=[YAW+gap+(tau-2*gap)*k/52 for k in range(53)]
        sleeve=[P(cx+(w+1.3*cos(7*a))*sin(a),row,d-(h+1.3*cos(7*a))*cos(a)) for a in angles]
        shirt.append(sleeve)
        inner.append([p+V((-sin(a),cos(a),0))*1.5 for p,a in zip(sleeve,angles)])
    g.sheet(shirt,inner,"linen","linen","felt")
    g.part("06 赭金马甲与黄铜衣扣")
    rows=[];inside=[]
    for j in range(48):
        row=215+321*j/47
        gap=lerp_profile(VEST,row)[-1]
        angles=[gap+(tau-2*gap)*k/60 for k in range(61)]
        rows.append([vest_point(row,a) for a in angles])
        inside.append([vest_point(row,a,-2.6) for a in angles])
    g.sheet(rows,inside,"damask","lining","binding",chart="马甲")
    for index in (0,-1):
        trim(g,[r[index] for r in rows],1.45,"binding")
    trim(g,rows[-1],1.35,"binding")
    # 翻折的马甲领沿开襟向第一粒纽扣收尖。
    for s in (-1,1):
        outer=[];inner=[]
        for j in range(18):
            t=j/17; row=222+75*t
            a=lerp_profile(VEST,row)[-1]
            width=(.27*sin(pi*t)**.7+.02)
            outer.append([vest_point(row,s*(a+width*k/3),2.1+1.7*sin(pi*k/3)) for k in range(4)])
            inner.append([q+FRONT*(-2) for q in outer[-1]])
        g.sheet(outer,inner,"damask","lining","binding")
        trim(g,[r[-1] for r in outer],.9,"binding")
    for row,button_x in ((310,283),(338,287),(365,292),(391,295),(467,288),(497,290)):
        cx,_,w,_,_=lerp_profile(VEST,row)
        angle=asin((button_x-cx)/w)-YAW-.10
        c=vest_point(row,angle,2)
        front=V((.35,-.935,0))
        rail=UP.cross(front).normalized()
        orb(g,c+front*1.0,(5.3,2.3,5.8),"welt",16,8,(rail,front,UP))
        orb(g,c+front*3.0,(5.0,3,5.65),"brass",20,10,(rail,front,UP))
    g.part("07 奶油色折领")
    leaves=[
        [((225,211,27),(212,216,42)),((205,211,11),(184,221,18)),
         ((202,213,-8),(184,227,-9)),((211,220,-30),(194,237,-40)),
         ((230,231,-43),(220,245,-59)),((254,249,-52),(248,251,-65)),
         ((269,267,-60),(272,277,-63))],
        [((225,211,27),(229,215,44)),((251,211,20),(273,218,28)),
         ((260,214,0),(287,223,1)),((266,220,-24),(296,239,-31)),
         ((273,232,-39),(288,241,-54)),((275,249,-51),(280,253,-63)),
         ((272,266,-59),(272,276,-63))]]
    for leaf in leaves:
        outer=[];inner=[]
        for station in smooth([(*P(*left),*P(*right)) for left,right in leaf],4):
            inner_c,out=V(station[:3]),V(station[3:])
            row=[]
            for k in range(6):
                f=k/5
                row.append(inner_c.lerp(out,f)+(UP*3.4+FRONT*2.6)*sin(pi*f))
            outer.append(row);inner.append([p-UP*2 for p in row])
        g.sheet(outer,inner,"linen","linen","felt")
        trim(g,[r[-1] for r in outer],.9,"linen")


def overcoat(g):
    g.part("08 酒红长外袍、里布与贴边")
    outer=[];inner=[];sections=52;sides=72
    def cut(row,k,push=0):
        a=lerp_profile(COAT,row)[-1]
        return coat_point(row,a+(tau-2*a)*k,push)
    for j in range(sections):
        row=215+385*j/(sections-1)
        outer.append([cut(row,k/sides) for k in range(sides+1)])
        inner.append([cut(row,k/sides,-3.7) for k in range(sides+1)])
    g.sheet(outer,inner,"coat","lining","binding",chart="袍身")
    for sign in (-1,1):
        band=[];back=[]
        for j in range(sections):
            row=215+385*j/(sections-1)
            a=lerp_profile(COAT,row)[-1]
            w=.08+.026*(1-j/(sections-1))
            line=[coat_point(row,sign*(a+w*k/5),1.25+1.25*sin(pi*k/5)) for k in range(6)]
            band.append(line);back.append([p+UP*.1+V((0,1,0))*.8 for p in line])
        g.sheet(band,back,"lining","lining","binding")
        trim(g,[r[0] for r in band],1.38,"binding")
        trim(g,[r[-1] for r in band],1.0,"binding")
        stitches(g,[r[2]+FRONT*.5 for r in band],7.3,3.4,.55,"binding")
    trim(g,[cut(598,k/sides,1.0) for k in range(sides+1)],1.7,"binding")
    trim(g,[cut(588,k/sides,1.1) for k in range(sides+1)],1.28,"binding")
    for a in (pi,.72*pi,1.28*pi):
        seam=[coat_point(225+369*j/40,a+.018*sin(j*.11),.9) for j in range(41)]
        trim(g,seam,.83,"hatband")
        if a==pi: stitches(g,seam,7.5,3.4,.52,"binding")


def sleeves(g,offer,wrist):
    g.part("09 迎客的垂袖" if offer else "10 夹布的垂袖")
    if offer:
        controls=[(174,241,6,20,21),(159,249,7,36,38),(148,268,6,42,43),(125,301,1,39,43),
                  (109,328,-12,43,47),(107,351,-29,47,50),(114,373,-53,50,54),
                  (125,383,-73,50,61),(137,385,-99,47,62)]
    else:
        controls=[(295,240,7,17,18),(306,245,8,28,29),(314,252,9,33,35),(327,276,10,35,37),(341,306,6,35,37),
                  (350,345,-3,36,42),(355,379,-15,40,47),(361,402,-28,41,51),
                  (368,413,-47,39,53),(373,414,-61,37,50)]
    if offer:
        controls=[(x,y,d-42*(j/(len(controls)-1))**1.65,w,h)
                  for j,(x,y,d,w,h) in enumerate(controls)]
    vals=smooth(controls,4);rows=[]
    for j,(x,y,d,w,h) in enumerate(vals):
        c=P(x,y,d)
        tangent=(P(*vals[min(j+1,len(vals)-1)][:3])-P(*vals[max(0,j-1)][:3])).normalized()
        u=(RIGHT-tangent*tangent.dot(RIGHT)).normalized()
        v=tangent.cross(u).normalized()
        ring=[]
        t=j/(len(vals)-1)
        for k in range(44):
            a=tau*k/44
            ridge=3.6*cos(5*a+4.2*t)+2.3*sin(3*a-3*t)
            ridge+=7.6*cos(24*t+3.3*sin(a-.5))*exp(-((t-.57)/.24)**2)
            ridge+=3.8*cos(17*t-2.6*cos(a))*exp(-((t-.32)/.19)**2)
            ring.append(c+u*(w+ridge)*cos(a)+v*(h+ridge)*sin(a))
        rows.append(ring)
    c=P(*controls[-1][:3])
    tangent=(c-P(*vals[-2][:3])).normalized()
    inside=[[c+(p-c)*amount+tangent*lip for p in rows[-1]] for amount,lip in ((.995,.75),(.979,1.15),(.95,.7),(.925,0))]
    # 里布沿原袖管反向返回肩窝，弯肘内不留穿出袖面的平截口。
    for j in range(len(rows)-2,-1,-1):
        mid=P(*vals[j][:3])
        inside.append([mid+(p-mid)*.92 for p in rows[j]])
    stop=len(rows)
    g.grid(rows+inside,lambda j,k: "coat" if j<stop else "lining",
           chart="迎客袖" if offer else "持布袖",chart_rows=stop)
    trim(g,rows[-1],1.8,"lining",True)
    trim(g,rows[3],1.75,"binding",True)
    trim(g,rows[4],1.05,"lining",True)
    cuff_c=P(153,361,-156) if offer else P(376,399,-78)
    cuff=[]
    stations=((15,10.0),(10,11.6),(3,10.4),(-3,8.6),(-6,6.4),(-5,3.9),(0,2.4),(8,3.3))
    for (depth,ease),section in zip(stations,wrist):
        ring=[]
        for k,(surface,ray) in enumerate(section):
            angle=tau*k/len(section)
            radius=ease*(1+.14*cos(5*angle+.4)+.06*sin(8*angle))
            p=surface+ray*radius
            p.y=cuff_c.y+depth+1.2*sin(2*angle+.6)
            ring.append(p)
        cuff.append(ring)
    g.grid(cuff+[cuff[0]],"linen",caps=False)
    # 里布沿腕口翻回袖内。管口围住前臂，指根后方可以看见袖内的阴影。
    forearm=[cuff_c+V((0,16,0)),cuff_c+V((-1,22,-1))]
    if offer: forearm.extend([P(145,366,-128),P(137,374,-104),P(127,373,-84)])
    else: forearm.extend([P(373,400,-59),P(366,403,-44),P(359,396,-29)])
    sweep(g,forearm,[(18,20),(22,24),(26,27),(29,28),(27,26)],"linen",32,RIGHT,
          lambda t,a: (1.4+.9*t)*cos(6*a+2*t))


def belt(g):
    g.part("11 根鞣腰带与扣舌")
    rows=[]
    for row,bump in ((402,0),(403,1.4),(406,2.0),(424,2.3),(427,1.6),(428,0)):
        ring=[]
        for k in range(68):
            a=k*tau/68
            f=max(0,min(1,(abs((a+pi)%tau-pi)-.83)/.54))
            ring.append(vest_point(row,a,3.8+bump).lerp(coat_point(row,a+.10,2.2+bump),f*f*(3-2*f)))
        rows.append(ring)
    g.grid(rows,"harness")
    for r in (rows[1],rows[-2]): trim(g,r,.87,"welt",True)
    n=V((.39,-.92,0));u=UP.cross(n).normalized()
    c=vest_point(415,.30,7)
    buckle(g,c,u,UP,15.6,16.5,3.4,"pewter")
    for i in range(3):
        p=vest_point(415,-.11-i*.115,7.0)
        orb(g,p,(1.8,.6,1.5),"shade",12,6)
