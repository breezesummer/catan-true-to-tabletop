"""三国骑士。面朝 -Y，足点为原点；队色由同一材质槽提供。"""

def plume(points, widths):
    path = [Vector(v) for v in points]
    rings = []
    for i,(center,radius) in enumerate(zip(path,widths)):
        along = path[min(i+1,len(path)-1)]-path[max(0,i-1)]
        side = along.cross(Vector((1,0,0))).normalized()
        rings.append([center + radius*(Vector((cos(j*tau/10),0,0))+.59*sin(j*tau/10)*side)
                      for j in range(10)])
    loft(rings,'player',True)


def mantle(z, span, drift, reach):
    # 连续的双面布料；背面与布边共用缝线，展开后仍看得到整片披风。
    rows = []
    for t in (0,.22,.48,.74,.96,1):
        row = []
        for i in range(11):
            u = (i-5)/5
            fold = cos(u*pi*2.25)
            row.append(Vector((u*(.24+span*t)+drift*t*t,
                        .10+reach*t-.11*u*u+.028*t*fold,
                        z+1.25-.42*t+.09*t*u*u+.024*fold*t)))
        rows.append(row)
    lining=Vector((0,.018,-.016))
    for a,b in zip(rows,rows[1:]):
        for i in range(10):
            q=[a[i],b[i],b[i+1],a[i+1]]
            face(q,'brass' if b is rows[-1] else 'player',True)
            face([v+lining for v in reversed(q)],'leather',True)
    edge=rows[0]+[row[-1] for row in rows[1:]]+list(reversed(rows[-1][:-1]))+[row[0] for row in reversed(rows[1:-1])]
    for i,a in enumerate(edge):
        b=edge[(i+1)%len(edge)]
        face([a,b,b+lining,a+lining],'brass')


def shield(hand, active):
    # 收盾时盾面立于身侧，架盾时向前平举，正面能从棋盘上方读出。
    c = Vector(hand) + Vector((-.055,-.045,.015))
    side = Vector((.92,-.39,0) if active else (.46,.89,0))
    up = Vector((.03,.67,.74) if active else (.03,.14,.99))
    normal = side.cross(up).normalized()
    outline = [(-.31,.27),(-.22,.36),(.22,.36),(.31,.27),
               (.30,-.14),(.18,-.31),(0,-.39),(-.18,-.31),(-.30,-.14)]
    def surface(u,v,depth):
        return c+side*u+up*v+normal*(depth+.055*(1-abs(u)/.32))
    rim = [surface(u,v,0) for u,v in outline]
    loft([rim,[p-normal*.065 for p in rim]], 'leather')
    inner = [surface(u*.88,v*.90,.016) for u,v in outline]
    for i in range(len(rim)):
        j=(i+1)%len(rim)
        face([rim[i],rim[j],inner[j],inner[i]],'brass')
        face([inner[i],inner[j],surface(0,0,.035)],'player')
    for sign in (-1,1):
        face([surface(0,.06,.055),surface(sign*.21,.18,.046),
              surface(sign*.18,.085,.048),surface(0,-.035,.055)],'linen')
    rod(surface(0,.02,.047),surface(0,.02,.095),.060,'brass',.038,8)


def knight(level,active):
    mounted = level == 3
    z = .82 if mounted else 0
    def p(x,y,h):
        return Vector((x,y,z+h))
    if mounted:
        horse()
        # 马背的鞍毯从左右两面翻过后腰，露出完整的队色面。
        for sign in (-1,1):
            q=[p(sign*.035,.21,.33),p(sign*.05,.52,.23),
               p(sign*.25,.53,.08),p(sign*.29,.33,-.13),p(sign*.27,.16,.02)]
            sheet(q,'player',.021)
    spread = .26 if active else .145
    for sign in (-1,1):
        foot_x = sign*(.32 if mounted else spread)
        foot_y = sign*(.13 if active else .043)
        knee=p(foot_x,-.105+foot_y,.44)
        heel=p(foot_x,foot_y,.115)
        plate(p(sign*.13,.015,.72),knee,.115,.085,'leather')
        plate(knee,heel,.089,.063,'iron')
        bulb(heel+Vector((0,-.045,-.044)),(.085,.145,.070),'leather',10)
    # 交领袍、分片甲裙和错叠胸甲。用暗铁与布色划分躯干，避免银盔银肩粘成一片。
    column(0,0,[(z+.46,.32,.235),(z+.63,.277,.185),(z+.82,.205,.15)],'player',12)
    column(0,0,[(z+.73,.22,.164),(z+.90,.222,.171),(z+1.13,.29,.172),
                 (z+1.21,.19,.126)],'iron',12)
    for j in range(3):
        h=z+.78+j*.105
        column(0,0,[(h,.232+j*.014,.173),(h+.056,.222+j*.014,.166)],'iron',12)
    column(0,0,[(z+.721,.242,.18),(z+.789,.230,.17)],'leather',14)
    box(0,-.174,z+.728,.10,.03,.050,'brass')
    for sign in (-1,1):
        # 扇形披膊顺着肩膀向外下覆，肩顶留整块队色。
        cx=sign*(.23 if level==1 else .29)
        for j in range(3):
            h=z+1.015+j*.060
            column(cx-sign*.018*j,0,[(h,.164-.012*j,.18-.01*j),
                   (h+.065,.138-.013*j,.158-.012*j)],'player' if j==2 else 'iron',10)
        q=[p(sign*.08,-.17,.72),p(sign*.28,-.12,.72),
           p(sign*.34,-.18,.47),p(sign*.22,-.231,.42),p(sign*.10,-.222,.49)]
        sheet(q,'iron',.015)
        for j in range(3):
            rod(p(sign*.125,-.208,.51+j*.058),p(sign*.272,-.179,.51+j*.058),.012,'leather',n=6)
    # 左右交叠的护胸皮襟及铜扣，在头盔与腰带之间留出斜向亮边。
    for a,b in ((p(-.21,-.103,1.18),p(.11,-.174,.97)),(p(.19,-.108,1.17),p(.04,-.181,1.04))):
        rod(a,b,.024,'leather',n=8)
    for x in (-.14,0,.14):
        bulb(p(x,-.159 if x else -.180,.91),(.019,.013,.019),'brass',8)
    mantle(z,.14 if level==1 else .22,.16 if active else .025,.70 if active else .46)
    # 露脸的兜鍪、护颊、额箍与随动长缨。
    bulb(p(0,-.015,1.31),(.128,.122,.146),'skin',14)
    for sign in (-1,1):
        q=[p(sign*.135,-.075,1.36),p(sign*.148,.085,1.34),
           p(sign*.163,.096,1.17),p(sign*.11,-.079,1.215)]
        sheet(q,'iron',.018)
    column(0,.01,[(z+1.34,.174,.157),(z+1.394,.16,.15),
                    (z+1.48,.125,.121),(z+1.55,.044,.041)],'iron',16)
    column(0,0,[(z+1.34,.175,.16),(z+1.369,.17,.156)],'brass',16)
    rod(p(0,.01,1.49),p(0,.018,1.595),.046,'brass',.031)
    swept = .39 if active else .16
    plume([p(0,.018,1.565),p(-.012,.075,1.641),p(-.026,.14,1.657),
           p(-.045,swept*.82,1.622),p(-.055,swept,1.574),
           p(-.09,swept+.045,1.47 if active else 1.42)], [.024,.060,.073,.063,.040,.004])
    if level==1:
        left=p(-.36,-.29,1.03) if active else p(-.31,.01,.77)
        right=p(.37,-.21,1.06) if active else p(.24,-.04,.88)
        shield(left,active)
        sword(right,(.80,-.57,.43) if active else (.26,-.29,-.87))
    elif level==2:
        # 同一柄钺：休整拄在身侧，激活时双手架于身前，横刃从俯视下伸出肩线。
        direction=Vector((.88,-.39,.24) if active else (.08,-.09,1)).normalized()
        right=p(.32,-.32,1.09) if active else p(.29,-.12,.96)
        left=right-direction*.42
        joint=right+direction*.46
        rod(left-direction*(.33 if active else .41),joint,.036,'plank',n=10)
        # 斧面与柄构成宽扇形，刃口的浅铁色在山石上也可见。
        across=Vector((-.27,-.87,.39) if active else (.86,-.44,.0)).normalized()
        def blade(u,v):
            return joint+direction*u+across*v
        q=[blade(-.11,0),blade(-.09,.11),blade(-.23,.31),blade(-.12,.39),
           blade(.085,.42),blade(.22,.32),blade(.095,.12),blade(.09,0)]
        normal=direction.cross(across).normalized()
        loft([q,[v+normal*.047 for v in q]],'iron')
        for i in (2,3,4):
            face([q[i]-normal*.004,q[i+1]-normal*.004,
                  q[i+1]-across*.046-normal*.005,q[i]-across*.046-normal*.005],'steel')
        rod(joint-direction*.11,joint+direction*.13,.050,'brass',.035)
    else:
        left=p(-.24,-.23,.91);right=p(.30,-.17,.96)
        axis=Vector((.84,-.69,.23) if active else (.09,.12,1)).normalized()
        tip=right+axis*.89
        rod(right-axis*.42,tip,.032,'wood',.022)
        rod(tip,tip+axis*.27,.061,'steel',.003,6)
        rod(tip-axis*.20,tip-axis*.03,.065,'player',.026)
        for sign in (-1,1):
            rod(left,p(sign*.115,-.715,.43),.011,'leather',n=6)
    for sign,hand in ((-1,left),(1,right)):
        shoulder=p(sign*.245,-.01,1.135)
        elbow=(shoulder+hand)*.5+Vector((sign*.076,0,-.09))
        plate(shoulder,elbow,.089,.07,'player')
        plate(elbow,hand,.073,.046,'leather')
        bulb(hand,(.065,.064,.058),'skin',10)
