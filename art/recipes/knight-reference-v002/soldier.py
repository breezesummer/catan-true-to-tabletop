"""前脚、髋、双肩、肘腕和持械点分别按两种姿态摆放。"""
def cuirass(heavy):
    chest=.253 if heavy else .218
    # 内衬、锁甲裙与腰侧开衩。
    shell([(0,.005,-.26,.232,.17),(0,0,-.20,.222,.155),(0,0,-.055,.187,.132),
           (0,0,.13,.16,.12),(0,0,.32,chest,.14),(0,0,.41,.15,.11)],'mail',36,2,.010)
    mail_band(0,0,-.251,.235,.173,3,41)
    # 腰细、胸骨向前，小腹略凸；亮起的锻造脊沿中心折向两边。
    spec=[(.015,.161,.125),(.045,.173,.135),(.13,.181,.134),(.235,chest*.95,.151),
          (.305,chest,.161),(.369,chest*.93,.143),(.422,.160,.114),(.442,.099,.093)]
    plate=[]
    for z,rx,ry in interp(spec,3):
        row=[]
        for i in range(48):
            a=tau*i/48;front=max(0,-sin(a))
            row.append((rx*cos(a),ry*sin(a)-.038*front**10,z+.010*front))
        plate.append(row)
    rings(plate,'cuirass')
    for side in (-1,1):
        line([(side*.143,-.094,.392),(side*.136,-.129,.32),(side*.120,-.141,.17),(side*.038,-.159,.059)],.005,'edge')
        for j in range(4):bead((side*(.156-.005*j),-.087-.014*j,.35-j*.072),.008,'gold')
    # 胸肩皮吊带与铜耳扣，让胸甲和披膊在亮面之间由暗革分开。
    for s in (-1,1):
        strap=[[(s*(.128+.026*u+.035*t),-.094-.059*t,.426-.128*t) for u in (-1,-.4,.4,1)] for t in (0,.35,.72,1)]
        panel(strap,'hide',.009)
        for i in (0,2):bead(strap[i][1],.010,'gold')
    for j in range(3):
        z=-.08+j*.04
        shell([(0,0,z,.193-.007*j,.145-.003*j),(0,0,z+.012,.194-.007*j,.146-.003*j),
               (0,0,z+.048,.180-.005*j,.134)],'steel',36)
    belt(0,0,-.027,.193,.153,.060,'hide')
    # 腰带方扣与垂带。
    line([(-.047,-.166,.034),(.047,-.166,.034),(.047,-.166,-.023),(-.047,-.166,-.023),(-.047,-.166,.034)],.009,'gold')
    rod((0,-.175,-.012),(.032,-.175,.013),.004,'gold')
    skirt=[]
    for j in range(17):
        u=-1+j/8;tstripe=.48+.24*u
        skirt.append([(u*(.087+.063*t),-.187-.066*t-.017*cos(u*pi*2)*t,
                      -.028-.33*t+.058*t*u*u) for t in (0,.10,tstripe,tstripe+.14,.95,1)])
    panel(skirt,lambda i,j:'linen' if i==2 else 'gold' if i==4 else 'colour')
    for side in (-1,1):
        for j in range(3):
            z=-.117+j*.050;cx=side*(.161-j*.009)
            g=[[(cx+side*(.063*u+.031*t),-.10+.051*u-.020*t,z+.064-.089*t+.012*u)
                for u in (-1,-.55,0,.55,1)] for t in (0,.32,.83,1)]
            panel(g,'steel')
    shell([(0,0,.39,.161,.12),(0,.008,.43,.13,.119),
           (0,0,.48,.097,.097),(0,0,.52,.081,.082)],'mail',36,2)
    mail_band(0,0,.423,.134,.121,2,29)

def soldier(level,active):
    global transform
    mounted=level==3;heavy=level!=1
    if mounted:horse_body()
    hip_z=1.54 if mounted else (.844 if level==2 and active else .931)
    lean=math.radians(12 if level==2 and active else 9 if mounted and active else 3)
    twist=math.radians(-16 if level==2 and active else 9 if level==1 and active else -5)
    center=Vector((-.044 if level==2 and active else 0,-.045 if active else 0,hip_z))
    torso=Matrix.Translation(center) @ Matrix.Rotation(twist,4,'Z') @ Matrix.Rotation(lean,4,'X')
    for side in (-1,1):
        if mounted:
            hip=(side*.135,.060,hip_z);knee=(side*.404,-.265,hip_z-.28);ankle=(side*.426,-.142,hip_z-.58)
        else:
            x=(.51 if level==2 else .34) if active else .197
            knee=(side*x*.78,-.127+side*(.14 if active else .035),.472 if active and level==2 else .515)
            ankle=(side*x,side*(.143 if active else .057),.126)
            hip=(center.x+.12*side,center.y+.03,hip_z)
        greave(hip,knee,ankle,heavy)
        if mounted:
            x,y,z=ankle
            loop((x,y-.027,z-.049),(.082,0,0),(0,0,.088),.011,'steel',24)
            line([(side*.192,.047,1.50),(side*.33,.014,1.255),(x,y,z+.032)],.018,'hide')
    transform=torso
    cuirass(heavy)
    head=torso @ Matrix.Translation((0,-.031,.475)) @ Matrix.Rotation(-lean*.55,4,'X') @ Matrix.Rotation(-twist*.6,4,'Z')
    transform=head;helmet(heavy,mounted);transform=Matrix.Identity(4)
    shoulder_pts={}
    for s in (-1,1):
        v=Vector((s*(.30 if heavy else .239),-.012,.375))
        if level==2 and active:v.z-=s*.067
        shoulder_pts[s]=torso@v
        shoulder(shoulder_pts[s],s,heavy)
    if level==1:
        right=Vector((-.51,-.27,1.20) if active else (-.31,-.06,.95))
        left=Vector((.36,-.31,1.03) if active else (.29,.04,.91))
        direction=Vector((.44,-.23,.92) if active else (-.29,-.31,-.90))
        blade(right,direction);shield(left+Vector((.035,-.055,-.085)),active)
    elif level==2:
        direction=Vector((.96,-.14,-.22) if active else (.12,-.11,1)).normalized()
        right=Vector((-.48,-.29,1.16) if active else (.22,-.22,.87))
        left=right+direction*(.69 if active else .52)
        warhammer(left,direction)
    else:
        right=Vector((-.41,-.33,1.90) if active else (-.31,-.14,1.83))
        left=Vector((.33,-.29,1.82))
        direction=Vector((-.47,-.85,.55) if active else (-.12,-.18,.98)).normalized()
        base=right-direction*.39;point=right+direction*1.62
        rod(base,point,.039,'wood',.021,n=24)
        rod(point-direction*.05,point+direction*.05,.035,'gold',.028,n=20)
        segment(point,point+direction*.35,[(0,.029,.020),(.18,.078,.026),(.42,.040,.016),(1,.001,.001)],'edge',4)
        shield(left+Vector((.136,.17,-.25)),False,True)
    for s,hand in ((-1,right),(1,left)):
        top=shoulder_pts[s]
        if level==2 and active:
            elbow=top+Vector((s*.12,-.08,-.18 if s==1 else -.14))
        else:
            elbow=top.lerp(hand,.48)+Vector((s*.08,.045,-.09))
        plate_limb(top,elbow,.09 if heavy else .077,'steel' if heavy else 'mail')
        oval(elbow,(.087,.079,.078),'steel',22)
        plate_limb(elbow,hand,.076 if heavy else .065)
        fist(hand,direction if s==-1 or level==2 else Vector((0,0,1)),heavy)
    if level==1:
        # 剑鞘垂在腰侧，鞘口与金属鞘尖分件。
        a=torso@Vector((-.18,0,0));b=a+Vector((-.13,.18,-.47))
        segment(a,b,[(0,.041,.022),(.12,.045,.024),(.85,.032,.019),(1,.026,.018)],'hide',16)
        rod(a,a.lerp(b,.06),.043,'gold',n=14);rod(a.lerp(b,.92),b,.032,'gold',.022,n=14)
