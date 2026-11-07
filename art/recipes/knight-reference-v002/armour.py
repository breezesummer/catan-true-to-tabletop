"""参考装备：分层护具、鼻盔和闭面盔、木盾、剑、双手战锤与骑枪。"""
def plate_limb(a,b,r,k='steel'):
    segment(a,b,[(0,.68*r,.70*r),(.07,.9*r,.88*r),(.17,r,1.02*r),(.47,.94*r,.96*r),
                 (.81,.76*r,.79*r),(.94,.71*r,.73*r),(1,.69*r,.70*r)],'greave' if k=='steel' else k,28)
    segment(a,b,[(.075,.932*r,.91*r),(.106,.943*r,.93*r)],'edge',28)
    segment(a,b,[(.88,.759*r,.782*r),(.94,.745*r,.76*r)],'strap',26)

def boot(ankle,heavy):
    x,y,z=ankle
    if z<.3:footprints.append((x,y-.082,.125,.225))
    global transform
    saved=transform
    transform=saved@Matrix.Translation((x,y,z))@Matrix.Rotation(math.radians(22 if x>0 else -22),4,'Z')
    # 鞋楦沿趾至跟横向放样，使足弓、脚背、平底和靴筒各有独立轮廓。
    sections=[(-.255,.032,-.071,-.105),(-.231,.068,-.052,-.108),(-.181,.085,-.03,-.108),
              (-.117,.087,.006,-.108),(-.055,.078,.048,-.105),(.015,.073,.073,-.105),(.075,.057,.04,-.104)]
    rows=[]
    for cy,w,top,bottom in interp(sections,3):
        rows.append([(w*cos(a),cy,bottom+(top-bottom)*(.5+.5*sin(a))**.74) for a in (i*tau/26 for i in range(26))])
    rings(rows,'boots')
    sole=[(w,cy,-.105) for cy,w,t,b in sections]+[(-w,cy,-.105) for cy,w,t,b in reversed(sections)]
    solid(sole,(0,0,-.013),'strap')
    shell([(0,.009,-.054,.075,.067),(0,.014,.011,.078,.072),(0,.013,.055,.065,.062),
           (0,.010,.118,.070,.064)],'boots',26,2)
    for j in range(4):
        cy=-.052-.045*j;w=.084-.003*j;h=.053-.024*j
        grid=[[(w*u,cy-.061*v+.01*abs(u),h-.055*u*u-.026*v)
               for u in (-1,-.78,-.48,0,.48,.78,1)] for v in (0,.18,.75,1)]
        panel(grid,'steel' if heavy else 'boots',.007,(0,-.37,.93))
        line(grid[-1],.005,'edge' if heavy else 'strap')
    belt(0,.01,.041,.073,.067,.023,'strap')
    transform=saved

def greave(hip,knee,ankle,heavy):
    plate_limb(hip,knee,.127 if heavy else .109,'steel' if heavy else 'mail')
    knee=Vector(knee);ankle=Vector(ankle)
    oval(knee+Vector((0,-.031,.012)),(.108,.101,.11),'iron')
    for side in (-1,1):bead(knee+Vector((side*.099,-.059,.011)),.012)
    # 整块膝甲的中央折脊和两片侧翼，共用连续的外壳，不在球体上叠共面三角片。
    rows=[]
    for extent,depth in ((.91,.047),(1,.066),(.96,.079),(.64,.105),(.26,.126),(.018,.133)):
        row=[]
        for j in range(32):
            angle=tau*j/32;u=cos(angle);v=sin(angle)
            row.append(knee+Vector((extent*.108*u,-depth-.012*(1-abs(u)),.014+extent*.105*v*(.94+.08*abs(v)))))
        rows.append(row)
    rings(rows,lambda i,j:'edge' if j==1 else 'steel',True,True)
    plate_limb(knee.lerp(ankle,.13),ankle,.097 if heavy else .084)
    line([knee+Vector((0,-.095,-.045)),knee.lerp(ankle,.53)+Vector((0,-.090,0)),ankle+Vector((0,-.065,.027))],.006,'edge')
    boot(ankle,heavy)

def helmet(heavy,crest):
    # 原点在下颌。先塑面部，再加包住颞侧与后脑的锁甲、成瓣的盔壳。
    shell([(0,-.015,.015,.063,.059),(0,-.022,.056,.090,.086),
           (0,-.024,.122,.107,.099),(0,-.012,.193,.113,.099),(0,.008,.236,.093,.080)],'visage',32,3)
    if not heavy:
        for s in (-1,1):
            # 眉弓从鼻根压向眼尾，眼睑包住眼珠；明暗留在头盔下的三角区。
            eyebrow=[(s*.014,-.134,.166),(s*.040,-.132,.172),(s*.072,-.111,.171),(s*.087,-.093,.164)]
            line(eyebrow,.007,'strap')
            oval((s*.047,-.123,.150),(.018,.006,.006),'eye',18)
            line([(s*.028,-.126,.150),(s*.047,-.130,.155),(s*.068,-.117,.151)],.003,'skin')

        solid([(-.019,-.115,.172),(.017,-.115,.172),(.024,-.147,.107),(0,-.154,.098),(-.022,-.146,.108)],(0,.023,0),'skin')
        line([(-.037,-.108,.078),(-.016,-.121,.081),(0,-.124,.078),(.016,-.121,.081),(.037,-.108,.078)],.003,'strap')
        oval((0,-.105,.067),(.032,.012,.010),'skin',20)
    for side in (-1,1):
        g=[]
        for j in range(6):
            t=j/5;row=[]
            for i in range(12):
                a=-.72+2.31*i/11
                row.append((side*(.114+.032*t)*cos(a),.008+(.11+.031*t)*sin(a),.196-.21*t+.024*t*sin(a)))
            g.append(row)
        panel(g,'mail',.008,(side,0,0))
    levels=[(.177,.129,.130),(.194,.139,.140),(.23,.133,.137),(.28,.112,.120),(.332,.078,.083),(.376,.025,.030),(.391,.005,.006)]
    shell([(0,.003,h,x,y) for h,x,y in levels],'helm',48,3)
    belt(0,.002,.174,.137,.138,.026,'iron')
    belt(0,.002,.194,.138,.139,.009,'edge')
    for i in range(20):
        a=tau*i/20;bead((.139*cos(a),.002+.14*sin(a),.187),.006,'edge')
    for a in (-pi/2,0,pi/2,pi):
        line([(rx*cos(a),.003+ry*sin(a),h+.003) for h,rx,ry in levels[1:]],.006,'edge')
    if not heavy:
        solid([(-.019,-.139,.196),(.019,-.139,.196),(.017,-.153,.119),
               (0,-.158,.092),(-.016,-.154,.118)],(0,.012,0),'steel')
    else:
        # 面甲沿左右脸折向尖脊。眼缝与呼吸槽贯穿外板，透出下层暗甲。
        def visor(u,t):
            a=u*1.34
            return (.133*sin(a),-.010-(.119+.056*(1-abs(u)))*cos(a),.18-.146*t+.034*abs(u)*t)
        for s in (-1,1):
            q=[visor(s*.0,0),visor(s*.96,.0),visor(s*.93,1),visor(0,1)]
            solid([(x,y+.009,z) for x,y,z in q],(0,.012,0),'dark')
        bands=[(0,.11),(.21,.39),(.39,.78),(.78,.95),(.95,1)]
        for a,b in bands:
            for i in range(32):
                if a==.39 and i in (5,8,11,14,17,20,23,26): continue
                u=-1+i/16;v=u+1/16
                solid([visor(u,a),visor(v,a),visor(v,b),visor(u,b)],(0,.007,0),'steel')
        for s in (-1,1):
            line([visor(s*t,.115) for t in (0,.25,.5,.75,1)],.004,'edge')
            bead(visor(s*.90,.33),.014,'gold')
    if crest:
        # 第三级的刷状红缨沿盔顶成扇。
        for j in range(25):
            t=j/24;y=-.075+t*.215
            root=Vector((0,y,.36-.06*abs(t-.34)))
            tip=root+Vector((.010*sin(t*4),.014+.056*t,.145-.053*t))
            tube([root,root.lerp(tip,.52)+Vector((0,-.011,.018)),tip],
                 [(.015,.014),(.032,.014),(.030,.009)],'colour',10)
        line([(0,-.077,.344),(0,-.015,.377),(0,.065,.365),(0,.137,.320)],.017,'gold')

def shoulder(pos,side,heavy):
    x,y,z=pos; w=.17 if heavy else .135
    global transform
    saved=transform
    origin=Vector((x,y,z))
    transform=saved@Matrix.Translation(origin)@Matrix.Rotation(math.radians(side*(20 if heavy else 15)),4,'Y')@Matrix.Translation(-origin)
    for j in range(4 if heavy else 3):
        t=j/(3 if heavy else 2)
        cx=x+side*(.038*(1-t));h=z-.124+.125*t
        shell([(cx,y,h,w*(1-.06*t),w*1.06),
               (cx,y,h+.012,w*(1-.035*t),w*1.08),
               (cx-side*.009,y,h+.053,w*.90,w*.98),
               (cx-side*.028,y,h+(.108 if t==1 else .074),w*(.12 if t==1 else .69),w*(.21 if t==1 else .79))],'pauldron',32,2)
        arc=[(cx+w*cos(a),y+w*1.06*sin(a),h+.008) for a in [pi+i*pi/12 for i in range(13)]]
        line(arc,.005,'edge')
        for a in (-2.42,-.72):bead((cx+w*.98*cos(a),y+w*1.055*sin(a),h+.020),.009,'gold')

    transform=saved


def fist(p,axis,heavy):
    c=Vector(p);axis=Vector(axis).normalized()
    radial=Vector((0,-1,.14));radial=(radial-axis*axis.dot(radial)).normalized();up=axis.cross(radial)
    oval(c-radial*.033,(.064,.057,.058),'hide')
    for i in range(4):
        offset=(i-1.5)*.026
        path=[c+axis*offset+radial*r+up*h for r,h in ((-.032,.052),(.015,.056),(.052,.025),(.052,-.012),(.027,-.029))]
        tube(path,[.015,.017,.016,.014,.010],'glove' if heavy else 'hide',12)
        if heavy:
            for j in (0,1,2):
                a=path[j].lerp(path[j+1],.10)+radial*.003;b=path[j].lerp(path[j+1],.88)+radial*.003
                segment(a,b,[(0,.015,.014),(.18,.018,.019),(.83,.017,.018),(1,.014,.013)],'steel',10)
            bead(path[1]+radial*.006,.010,'edge')
    rod(c-axis*.053-radial*.029,c-axis*.064+radial*.039,.020,'hide',.014)

def blade(hand,d):
    h=Vector(hand);d=Vector(d).normalized()
    across=Vector((d.z,0,-d.x)).normalized()
    normal=d.cross(across).normalized()
    guard=h+d*.106
    rod(h-d*.13,guard,.031,'hide',.024,20)
    for i in range(7):rod(h+d*(-.111+i*.028),h+d*(-.103+i*.028),.031,'strap',n=16)
    oval(h-d*.148,(.047,.041,.047),'gold',20)
    line([guard+across*x+d*z for x,z in ((-.142,-.015),(-.088,.009),(0,.014),(.087,.009),(.142,-.015))],.019,'steel')
    rings([[guard+d*dist+across*width,guard+d*dist+normal*thick,
            guard+d*dist-across*width,guard+d*dist-normal*thick] for dist,width,thick in ((.025,.041,.012),(.13,.045,.014),(.57,.036,.011),(.79,.027,.009),(.92,.001,.001))],'edge',True,False)

def shield(c,active,mounted=False):
    c=Vector(c)
    side=Vector((.78,.05,.04) if mounted else (.91,.32,.08) if active else (.42,.90,.08)).normalized()
    up=Vector((-.09,.15 if active else .025,.98)).normalized()
    normal=side.cross(up).normalized()
    size=.78 if mounted else .89
    def surface(u,t):
        width=.37*(.018+.982*math.sin(min(1,t*.97)*pi/2)**.69)
        return c+size*(side*u*width+up*((t-.45)*.99+.065*t**4*(1-abs(u)))-normal*.077*u*u+normal*.045)
    # 一条连续的斜白纹，与木板共形。
    grid=[]
    for n in range(13):
        u=-1+n/6
        band=.46+.22*u
        grid.append([surface(u,t) for t in (0,.06,band-.022,band,band+.119,band+.14,.94,1)])
    panel(grid,lambda i,j:'linen' if i==3 else 'shieldwood',.021)
    edge=[surface(-1,i/24) for i in range(25)]+[surface(-1+i/15,1) for i in range(1,31)]+[surface(1,1-i/24) for i in range(1,25)]
    tube(edge+[edge[0]],.021,'steel',10)
    for i in range(2,len(edge)-2,4):bead(edge[i]+normal*.016,.011,'edge')
    for u in (-.59,-.18,.27,.68):
        line([surface(u,t)+normal*.001 for t in (.13,.35,.59,.79,.98)],.002,'strap',6)

def warhammer(hand,d):
    hand=Vector(hand);d=Vector(d).normalized()
    end=hand+d*.62
    rod(hand-d*.85,end,.039,'shaft',.047,24)
    for start in (-.84,-.55,.14,.57):rod(hand+d*start,hand+d*(start+.04),.048,'steel',n=24)
    for j in range(14):
        t=-.38+j*.032
        rod(hand+d*t,hand+d*(t+.022),.047,'hide',n=16)
    # 锤面向下，截面为削角的锻铁长方形；背部收成向外弯的钩喙。
    def section(z,w,l,drift):
        c=end+Vector((drift,0,z))
        return [c+Vector((u*w,v*l,0)) for u,v in ((-.80,-1),(.80,-1),(1,-.80),(1,.80),(.80,1),(-.80,1),(-1,.80),(-1,-.80))]
    rows=[section(*v) for v in ((-.265,.103,.105,.018),(-.245,.121,.119,.014),(-.205,.117,.114,.01),
                                 (-.08,.098,.085,0),(.043,.085,.074,0),(.077,.079,.070,-.003))]
    rings(rows,'hammer',True,False)
    rings([section(.039,.091,.080,0),section(.064,.091,.080,0)],'edge',True,False)
    rings([section(*p) for p in ((.071,.073,.065,0),(.154,.060,.057,-.01),(.22,.042,.038,-.038),(.285,.023,.019,-.08),(.34,.003,.002,-.143))],'edge',True,False)
    for s in (-1,1):bead(end+Vector((s*.043,-.079,.002)),.014,'gold')
