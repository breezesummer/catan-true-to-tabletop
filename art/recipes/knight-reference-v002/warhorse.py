"""战马由肩胸、肋腹、臀、斜颈和颅骨连续放样，腿部按前膝和后跗关节分别塑形。"""
def horse_body():
    # 体轴沿 Y，截面上缘为背线，下缘在肋部收腹。
    stations=[(-.77,1.05,.07,.16),(-.68,1.12,.234,.339),(-.50,1.14,.301,.403),
              (-.25,1.16,.302,.394),(.0,1.15,.326,.371),(.27,1.16,.310,.337),
              (.46,1.155,.315,.36),(.63,1.13,.302,.35),(.77,1.13,.229,.27),(.84,1.16,.026,.08)]
    rows=[]
    for y,z,w,h in interp(stations,4):
        rows.append([(w*cos(a)*(1+.07*sin(a)),y,z+h*sin(a)) for a in (i*tau/44 for i in range(44))])
    rings(rows,'horse')
    neck=[(0,-.47,1.28,.269,.323),(0,-.56,1.47,.236,.31),(.003,-.653,1.65,.180,.240),
          (-.018,-.76,1.80,.138,.188),(-.045,-.853,1.91,.121,.144)]
    shell(neck,'horse',40,4)
    # 头骨从枕骨沿面脊向前下探，口鼻有独立的颊肉、下唇、鼻翼和鼻孔。
    path=[(-.039,-.804,1.885),(-.055,-.902,1.883),(-.087,-1.010,1.805),
          (-.103,-1.127,1.710),(-.113,-1.217,1.645),(-.113,-1.264,1.633)]
    longitudinal=interp([(*p,rx,ry) for p,rx,ry in zip(path,(.065,.139,.134,.091,.091,.066),(.070,.152,.149,.119,.079,.054))],3)
    tube([v[:3] for v in longitudinal],[(v[3],v[4]) for v in longitudinal],'horse',36)
    oval((-.1,-1.14,1.636),(.081,.099,.067),'horse')
    for s in (-1,1):
        oval((-.067+.115*s,-.986,1.827),(.041,.057,.035),'strap',22)
        oval((-.067+.14*s,-.994,1.83),(.018,.031,.021),'eye',22)
        bead((-.069+.154*s,-1.004,1.839),.005,'edge')
        oval((-.111+s*.073,-1.224,1.666),(.025,.045,.027),'hoof')
        oval((-.111+s*.087,-1.24,1.67),(.014,.026,.017),'dark')
        line([(-.108+s*.057,-1.255,1.606),(-.11+s*.082,-1.203,1.61),(-.10+s*.09,-1.124,1.63)],.005,'dark')
        ear=Vector((-.045+s*.08,-.854,1.969))
        tube([ear,ear+Vector((s*.015,.015,.067)),ear+Vector((s*.036,.039,.143)),ear+Vector((s*.05,.047,.19))],
             [(.044,.043),(.050,.036),(.027,.017),(.003,.004)],'horse',22)
        solid([ear+Vector((-.029,-.035,.029)),ear+Vector((.03,-.033,.035)),ear+Vector((s*.036,.014,.157))],(0,.008,0),'strap')
    # 四肢关节与蹄底各自落地。
    for s in (-1,1):
        for fore in (True,False):
            stride=s*.076
            y=-.48 if fore else .57
            if fore:
                stations=[(s*.205,y,1.23,.14,.154),(s*.211,y+.034,1.01,.121,.137),
                    (s*.213,y+.047,.80,.083,.104),(s*.216,y+stride,.59,.061,.077),
                    (s*.222,y+stride-.012,.53,.057,.065),(s*.223,y+stride+.013,.28,.039,.046),
                    (s*.231,y+stride-.002,.188,.052,.061),(s*.236,y+stride-.045,.10,.045,.047)]
            else:
                stations=[(s*.201,y,1.26,.179,.170),(s*.21,y-.03,1.08,.149,.166),
                    (s*.215,y-.165,.837,.11,.126),(s*.223,y-.025,.651,.083,.096),
                    (s*.231,y+.108+stride,.509,.055,.073),(s*.233,y+.118+stride,.455,.047,.055),
                    (s*.235,y+.046+stride,.232,.038,.046),(s*.237,y+.033+stride,.166,.052,.058),
                    (s*.24,y-.01+stride,.099,.046,.046)]
            stations=[(x,y,z,rx*(1.16 if z<.78 else 1.04),ry*(1.12 if z<.78 else 1.03)) for x,y,z,rx,ry in stations]
            shell(stations,'horse',32,4)
            hx,hy,hz,_,_=stations[-1]
            footprints.append((hx,hy-.026,.098,.125))
            for j in range(22):
                a=tau*j/22
                tube([(hx+.045*cos(a),hy+.044*sin(a),.194),(hx+.051*cos(a),hy+.056*sin(a),.144),
                      (hx+.053*cos(a),hy+.065*sin(a),.092+.015*sin(3*a))],
                     [.008,.009,.002],'dark' if j%4==0 else 'horse',7)
            shell([(hx,hy-.022,.012,.068,.092),(hx,hy-.022,.023,.071,.093),(hx,hy-.012,.071,.063,.079),
                   (hx,hy+.001,.116,.048,.057)],'hoof',26,2)
    # 马衣从背脊铺过肋部，金色包边、织物折纹和斜白纹共用连续曲面。
    from mathutils.bvhtree import BVHTree
    hide_surface=BVHTree.FromPolygons(verts,faces)
    for s in (-1,1):
        def cloth(u,t):
            y=-.55+u*1.30
            angle=min(t,.67)*2.20
            z=1.15+(.432-.083*u**5)*cos(angle)-max(0,t-.67)*1.04+.018*cos(tau*u)+.028*t*sin(6*pi*u)
            x=(.376-.061*u**5)*sin(angle)+.020*t*cos(7*pi*u+.4*t)
            point=Vector((s*x,y+.046*t*cos(u*pi),z))
            # 在肌肉和折线之间留出布厚；后腿、臀部和胸侧采用实际表面约束。
            hit=hide_surface.ray_cast(Vector((s*.95,point.y,point.z)),Vector((-s,0,0)),.95)[0]
            if hit is not None:point.x=s*max(abs(x),s*hit.x+.021)
            return point
        grid=[]
        for j in range(35):
            u=j/34;stripe=.68-.41*u
            grid.append([cloth(u,t) for t in (0,.07,.18,stripe,stripe+.115,.72,.88,.945,.975,1)])
        panel(grid,lambda i,j:'gold' if i==8 else 'linen' if i==3 else 'colour',.012,(s,0,.36))
        # 鞍带越过马衣，缝线与铜扣沿着实表面贴合。
        for u0 in (.29,.72,.92):
            strap_grid=[]
            for i in range(13):
                t=.10+i*.067
                strap_grid.append([cloth(u0+du+.06*t,t)+Vector((s*.009,0,.003)) for du in (-.017,0,.017)])
            panel(strap_grid,'hide',.007,(s,0,.14))
            line([row[0] for row in strap_grid],.003,'gold',6)
            pt=strap_grid[7][1]+Vector((s*.006,0,0))
            line([pt+Vector((0,dy,dz)) for dy,dz in ((-.032,-.026),(.032,-.026),(.032,.026),(-.032,.026),(-.032,-.026))],.006,'gold')
        for u in (.14,.44,.63,.84):
            a=cloth(u,.87);b=cloth(u,.99)
            ornament=[a,cloth(u+.017,.925),b,cloth(u-.017,.925)]
            solid([v+Vector((s*.005,0,0)) for v in ornament],(s*.004,0,0),'gold')
        for u in (.05,.20,.39,.58,.77,.94):
            t=.945;p=cloth(u,t);bead(p+Vector((s*.008,0,0)),.012,'gold')
    # 肩胸前挂叠甲，马颈上的活动节片露出皮革底衬。
    for j in range(4):
        t=j/3;z=.91+.12*j
        row=[]
        for a in [pi+pi*i/24 for i in range(25)]:
            row.append(Vector((.336*cos(a),-.48+(.358+.07*t)*sin(a),z+.075*abs(cos(a)))))
        panel([row,[p+Vector((-.025*p.x,-.008,.127)) for p in row]],'peytral')
        line(row,.008,'edge')
        for p in row[2::5]:bead(p+Vector((0,-.01,.026)),.009)
    for j in range(6):
        t=j/5
        cy=-.55-.281*t;z=1.46+.445*t;rx=.236-.116*t;ry=.273-.112*t
        shell([(0,cy,z,rx,ry),(0,cy-.003,z+.012,rx+.003,ry+.003),
               (0,cy-.035,z+.085,rx-.014,ry-.022)],'steel',32)
        for s in (-1,1):bead((s*rx,cy,z+.025),.011,'gold')
    # 鼻梁上的额甲窄入鼻带，眼与鼻翼保留马皮。
    ridge=[(-.060,-.966,2.044,.119),(-.07,-1.055,1.978,.115),
           (-.085,-1.148,1.892,.102),(-.101,-1.214,1.816,.085),(-.11,-1.268,1.734,.065)]
    grid=[[(x+w*u,y-.030*(1-abs(u)),z+.025*(1-abs(u))-.036*abs(u)) for u in (-1,-.55,0,.55,1)] for x,y,z,w in ridge]
    panel(grid,'chamfron',.014,(0,-.55,.84));line([r[2] for r in grid],.007,'edge')
    for r in grid[::2]:
        for i in (0,4):bead(r[i],.008,'gold')
    for s in (-1,1):
        bit=(-.104+s*.096,-1.186,1.638)
        loop(bit,(0,.036,0),(0,0,.033),.007,'gold')
        line([bit,(-.072+s*.145,-1.035,1.773),(-.055+s*.13,-.90,1.943)],.015,'hide')
        line([bit,(s*.221,-.862,1.55),(s*.22,-.62,1.59),(s*.135,-.386,1.74)],.009,'hide')
    # 鼻翼前突，眼睑包住眼球。额甲边缘绕过眼眶，皮缰用铜扣固定。
    for s in (-1,1):
        eye=Vector((-.072+s*.143,-.991,1.839))
        oval(eye,(.024,.034,.026),'eye')
        line([eye+Vector((s*.012,-.034,-.005)),eye+Vector((s*.019,-.020,.018)),
              eye+Vector((s*.021,.006,.023)),eye+Vector((s*.014,.031,.005))],.007,'horse')
        bead(eye+Vector((s*.024,-.012,.009)),.005,'edge')
        nose=Vector((-.111+s*.066,-1.267,1.652))
        oval(nose,(.033,.034,.028),'hoof')
        oval(nose+Vector((s*.009,-.026,.005)),(.016,.009,.012),'dark')
        loop((-.057+s*.150,-.923,1.893),(0,.027,0),(0,0,.025),.007,'gold',18)
        line([(x+s*w*.91,y-.010,z-.023) for x,y,z,w in ridge],.006,'edge')
    # 鞍与尾帚。尾毛束向下渐细，不依赖透明面片。
    oval((0,.10,1.54),(.25,.294,.064),'strap',32)
    oval((0,.33,1.58),(.205,.073,.065),'hide',26)
    for j in range(12):
        a=tau*j/12
        linepts=[(.073*cos(a),.70+.06*sin(a),1.27),(.095*cos(a),.86+.044*sin(a),1.19),
                 (.074*cos(a)+.04,.896+.045*sin(a),.95),(.055*cos(a)+.075,.895+.04*sin(a),.71),(.021*cos(a)+.121,.865+.028*sin(a),.56+.038*cos(a))]
        tube(linepts,[.033,.037,.031,.022,.005],'dark' if j%3 else 'horse',9)
