"""科学大都会。连续铜穹顶、黄铜浑天仪和有缺口的弧廊，三层圆形从外向内收拢。"""
def observatory():
    cx,cy=-.035,.035
    def polar(r,a,z):return Vector((cx+r*cos(a),cy+r*sin(a),z))
    column(cx,cy,[(0,.79,.79),(.032,.80,.80),(.073,.75,.75)],'stone',64)
    for j in range(3):
        inner=.24+j*.154;outer=inner+.147
        for i in range(24):
            a=tau*i/24+.008;b=tau*(i+1)/24-.008
            q=[polar(inner,a,.073),polar(outer,a,.073),polar(outer,b,.073),polar(inner,b,.073)]
            sheet(q,'trim' if (i+j)%5==0 else 'stone',.012)
    threshold(cx,-.51,.43,.44,.124,4,.28)
    # 仪器基座与开间，以深蓝窗洞衬出浅色支柱。
    column(cx,cy,[(.08,.58,.58),(.14,.60,.60),(.18,.546,.546),(.65,.546,.546),(.69,.58,.58)],'stone',48)
    for i in range(16):
        a=tau*i/16;aa=a+.062;bb=a+.31
        q=[polar(.551,aa,.26),polar(.551,bb,.26),polar(.551,bb,.515),polar(.551,(aa+bb)/2,.581),polar(.551,aa,.515)]
        face(q,'shadow')
        p=polar(.553,a,.17);column_base(p.x,p.y,p.z,.50,.028,'trim')
    # 星历藏书楼、观测层与外挑观星台。塔身本身高过旧城的檐线。
    column(cx,cy,[(.66,.549,.549),(.72,.575,.575),(.81,.521,.521),(1.46,.491,.491),
                  (1.51,.525,.525),(1.59,.534,.534),(1.64,.476,.476),(2.06,.474,.474),
                  (2.12,.557,.557),(2.18,.625,.625)],'stone',64)
    for i in range(16):
        a=tau*i/16;b=a+tau/16
        def bay(t,r,z):return polar(r,a+(b-a)*t,z)
        for bottom,h,r in ((.82,.53,.527),(1.67,.34,.483)):
            g=[bay(.15,r,bottom),bay(.85,r,bottom),bay(.85,r,bottom+.78*h),bay(.50,r,bottom+h),bay(.15,r,bottom+.78*h)]
            face(g,'blue')
            path(g[1:]+g[:1],.012,'trim')
            rod(bay(.5,r+.004,bottom),bay(.5,r+.004,bottom+h),.010,'brass',n=8)
        p=polar(.51,a,.75);column_base(p.x,p.y,.75,.69,.031,'trim')
    for z,r in ((.185,.563),(.645,.58),(.76,.55),(1.45,.533),(1.53,.56),(2.08,.58),(2.16,.62)):
        column(cx,cy,[(z,r,r),(z+.027,r+.008,r+.008)],'brass',64)
    for j in range(16):
        a=tau*j/16;b=tau*(j+1)/16
        q=[polar(.47,a,1.52),polar(.66,a,1.52),polar(.66,b,1.52),polar(.47,b,1.52)]
        sheet(q,'green',.062)
        p=polar(.64,a,1.58)
        rod(p,p+Vector((0,0,.157)),.018,'brass')
        path([polar(.64,a+(b-a)*i/5,1.733) for i in range(6)],.015,'brass')
    # 弧形旋梯绕塔身上行，外缘栏杆跟随真实踏步的高差。
    for j in range(23):
        a=.04+j*.133;b=a+.137;z=.81+j*.031
        q=[polar(.51,a,z),polar(.63,a,z),polar(.63,b,z),polar(.51,b,z)]
        sheet(q,'stone' if j%4 else 'trim',.032)
        if j%3==0:rod(polar(.626,a,z),polar(.626,a,z+.144),.010,'brass',n=8)
    path([polar(.626,.04+j*.133,.967+j*.031) for j in range(23)],.011,'brass')
    # 十二瓣穹顶独立排铜片，色块纵贯到顶，沿经线用暗压缝与鎏金肋收口。
    def dome(a,t):
        angle=t*pi*.487
        return polar(.679*cos(angle),a,2.20+.58*sin(angle))
    for i in range(12):
        a=tau*i/12;b=tau*(i+1)/12
        k='player' if i%3 in (0,1) else 'green'
        for j in range(8):
            t=j/8;u=(j+1)/8
            for n in range(4):
                c=a+(b-a)*n/4;d=a+(b-a)*(n+1)/4
                textile_face([dome(c,t),dome(d,t),dome(d,u),dome(c,u)],k,[(n/4,t),((n+1)/4,t),((n+1)/4,u),(n/4,u)])
        path([dome(a,j/20)+Vector((0,0,.011)) for j in range(21)],.015,'brass',8)
        for t in (.23,.49,.72):
            path([dome(a+(b-a)*n/8,t)+Vector((0,0,.004)) for n in range(9)],.006,'green',6)
    column(cx,cy,[(2.17,.667,.667),(2.20,.69,.69),(2.227,.678,.678)],'green',64)
    # 侧弧廊为真透空，前门避开扩展城墙的入口。
    a0=-.44;a1=3.94;n=32
    for i in range(n):
        a=a0+(a1-a0)*i/n;b=a0+(a1-a0)*(i+1)/n
        for lo,hi,z,k,h in ((.64,.835,.078,'stone',.063),(.64,.96,.76,'green',.065)):
            q=[polar(lo,a,z),polar(hi,a,z),polar(hi,b,z),polar(lo,b,z)]
            sheet(q,k,h)
    for i in range(14):
        a=a0+(a1-a0)*i/13;p=polar(.755,a,.138)
        column_base(p.x,p.y,p.z,.622,.034,'trim')
    for r in (.64,.96):
        path([polar(r,a0+(a1-a0)*i/52,.829) for i in range(53)],.014,'brass')
    # 支架托住浑仪的最低点，环有厚度；黄道作宽带以便全盘俯视仍读得出来。
    column(cx,cy,[(2.74,.142,.142),(2.82,.183,.183),(2.89,.124,.124)],'brass',32)
    center=Vector((cx,cy,3.23));radius=.49
    axes=[((1,.14,0),.54,.030),((.12,1,.04),1.26,.024),((.78,.62,0),1.60,.021)]
    for index,(axis,tilt,gauge) in enumerate(axes):
        q=Quaternion(Vector(axis).normalized(),tilt)
        def globe(t,r=radius):return center+q@Vector((r*cos(t),r*sin(t),0))
        path([globe(tau*i/72) for i in range(72)],gauge,'brass',12,True)
        if index==0:
            normal=q@Vector((0,0,.044))
            for i in range(60):
                a=tau*i/60;b=tau*(i+1)/60
                face([globe(a)+normal,globe(b)+normal,globe(b)-normal,globe(a)-normal],'brass')
                mark=globe(a,radius+.008)
                rod(mark+normal,mark+normal*(.18 if i%5 else -.96),.004 if i%5 else .009,'linen',n=6)
    bulb(center,(.138,.138,.138),'green',24)
    v=Vector((.61,.14,.78)).normalized()
    rod(center-v*.48,center+v*.52,.021,'brass',n=12)
    for s in (-1,1):
        pivot=center+s*Vector((.46,.02,0))
        path([Vector((cx+s*.09,cy,2.82)),Vector((cx+s*.18,cy,2.86)),
              Vector((cx+s*.36,cy,2.98)),pivot],.022,'brass',10)
        bulb(pivot,(.046,.046,.046),'brass',16)
        bulb(center+s*v*.48,(.042,.042,.042),'brass',16)
