"""政治大都会。两道平行的大屋檐共享正殿轴线，正脊、阔阶与列柱都横向展开。"""
def palace_roof(cx,cy,z,w,d,rise,tile):
    def roofpoint(side,u,t):
        # 下垂的檐腹、四角抬起的飞檐、内收的庑殿顶。
        halfw=w*.50;halfd=d*.50
        width=halfw*(1-t)+(halfw-halfd*.80)*t
        p=[(-width,-halfd*(1-t)-.022*t),(width,-halfd*(1-t)-.022*t),
           (width,halfd*(1-t)+.022*t),(-width,halfd*(1-t)+.022*t)]
        a,b=p[side],p[(side+1)%4]
        corner=abs(2*u-1)**3.5
        h=z+rise*(.35*t+.65*t*t)+.118*corner*(1-t)**2+.045*t*corner
        return Vector((cx+a[0]*(1-u)+b[0]*u,cy+a[1]*(1-u)+b[1]*u,h))
    for side in range(4):
        n=24 if side%2==0 else 12
        for i in range(n):
            for j in range(7):
                a=i/n;b=(i+1)/n;t=j/7;v=(j+1)/7
                q=[roofpoint(side,a,t),roofpoint(side,b,t),roofpoint(side,b,v),roofpoint(side,a,v)]
                textile_face(q,tile,[(a,t),(b,t),(b,v),(a,v)])
                if j==0:
                    face([q[1],q[0],q[0]-Vector((0,0,.041)),q[1]-Vector((0,0,.041))],'wood')
            path([roofpoint(side,i/n,j/10)+Vector((0,0,.009)) for j in range(11)],.010 if side%2==0 else .008,tile,6)
        for t in (.19,.43,.66,.87):
            path([roofpoint(side,i/n,t)+Vector((0,0,.005)) for i in range(n+1)],.005,tile,6)
        path([roofpoint(side,u/n,0)-Vector((0,0,.015)) for u in range(n+1)],.021,'brass')
        path([roofpoint(side,0,j/14)+Vector((0,0,.014)) for j in range(15)],.028,'brass')
    left=roofpoint(0,0,1);right=roofpoint(0,1,1)
    ridge=[]
    for i in range(25):
        u=i/24;v=left.lerp(right,u);v.y=cy;v.z=z+rise+.042+.045*abs(2*u-1)**4
        ridge.append(v)
    path(ridge,.043,'brass',12)
    for end,sign in ((left,-1),(right,1)):
        start=Vector((end.x,cy,z+rise+.08))
        path([start,start+Vector((sign*.078,0,.018)),start+Vector((sign*.122,0,.093)),start+Vector((sign*.101,0,.171))],.029,'brass',10)
        # 脊端鸱吻的颈、背鳍、口吻均为实体，从灰瓦背景上挑起。
        base=start+Vector((sign*.083,0,.052))
        bulb(base,(.058,.039,.048),'brass',16)
        head=base+Vector((sign*.027,0,.070))
        bulb(head,(.045,.033,.047),'brass',16)
        rod(head+Vector((0,0,.012)),head+Vector((-sign*.069,0,.023)),.027,'brass',.014,n=10)
        for k in range(3):
            a=base+Vector((sign*(.026+.008*k),0,.026*k))
            rod(a,a+Vector((sign*(.051-.008*k),0,.043)),.023,'brass',.002,n=8)
        for face_y in (-1,1):
            bead=head+Vector((-sign*.012,face_y*.028,.016))
            bulb(bead,(.009,.006,.009),'iron',8)
    # 飞椽在两面檐下收成一排莲纹瓦当。
    for side in (0,2):
        for i in range(23):
            p=roofpoint(side,(i+.5)/23,0)
            ysign=-1 if side==0 else 1
            a=p+Vector((0,ysign*.008,-.028))
            rod(a,a+Vector((0,ysign*.028,0)),.019,tile,n=12)
            bulb(a+Vector((0,ysign*.03,0)),(.007,.005,.007),'brass',8)
    # 从低角度也可见的檐板；中央压在深色梁架里。
    box(cx,cy,z-.033,w*.92,d*.86,.041,'wood',.030)

def palace():
    podium(0,.10,1.67,1.18,.246)
    threshold(0,-.41,.715,.50,.246,7,.30)
    for s in (-1,1):
        for j in range(6):
            y=-.72+j*.052;z=.10+j*.027
            box(s*.378,y,0,.068,.093,z+.052,'stone')
            if j%2==0:bulb((s*.378,y,z+.076),(.037,.037,.035),'trim',12)
    # 首层议政厅与次层朝堂都有完整柱网、隔扇、阑额、斗拱和梁枋。上层占足正殿宽度。
    def hall(y,z,width,depth,height,upper):
        wall_y=y-depth*.30
        box(0,y+.055,z,width*.82,depth*.66,height*.92,'wood')
        for i in range(5):
            x=(i-2)*width*.15
            box(x,wall_y-.008,z+.10,width*.126,.035,height*.71,'shadow')
            for j in range(5):
                box(x+(j-2)*width*.021,wall_y-.031,z+.15,.013,.019,height*.58,'plank',.003)
            for h in (.23,.45,.69):box(x,wall_y-.034,z+height*h,width*.124,.018,.014,'brass',.002)
        for back in (-1,1):
            for i in range(6):
                x=(i-2.5)*width*.17;yy=y+back*depth*.50
                column_base(x,yy,z,height,.046 if upper else .058,'leather')
                for j in range(3):
                    cap=z+height-.108+j*.045
                    box(x,yy,cap,.08+j*.049,.09+j*.040,.049,'wood')
                    box(x,yy,cap+.033,.112+j*.046,.105+j*.040,.012,'brass')
        band(0,y,z+height,width*1.02,depth*1.06,.056,'wood')
        band(0,y,z+height+.045,width,depth*1.04,.025,'brass')
        # 阑额有独立的彩画板和菱花，内外两面都压在斗拱上。
        for sy in (-1,1):
            face_y=y+sy*(depth*.53+.009)
            for i in range(5):
                x=(i-2)*width*.169
                box(x,face_y,z+height-.064,width*.126,.023,.075,'blue',.006)
                for q in (-1,0,1):
                    xx=x+q*width*.038
                    path([(xx-.027,face_y+sy*.014,z+height-.027),(xx,face_y+sy*.015,z+height-.001),
                          (xx+.027,face_y+sy*.014,z+height-.027),(xx,face_y+sy*.015,z+height-.052)],.005,'brass',6,True)
    hall(.12,.25,1.62,.94,1.01,False)
    for x in (-.535,.535):
        lantern(x,-.37,1.18);banner(x,-.391,.94,'blue')
    palace_roof(0,.12,1.325,2.19,1.35,.405,'blue')
    # 退一步的上殿从下檐中心穿出，留出宽阔的外露横向廊台。
    box(0,.12,1.55,1.39,.68,.23,'wood')
    band(0,.12,1.73,1.68,.84,.066,'trim')
    hall(.12,1.79,1.54,.70,.76,True)
    for side in (-1,1):
        rail((side*.75,-.24),(side*.75,.48),1.796,.170)
        rail((side*.74,-.245),(side*.31,-.245),1.796,.17)
        banner(side*.54,-.258,2.43,'blue')
    box(0,-.261,2.263,.365,.035,.142,'brass')
    box(0,-.284,2.276,.329,.014,.112,'blue')
    for i in (-1,0,1):
        for side in (-1,1):
            rod((i*.086,-.295,2.294),(i*.086+side*.028,-.295,2.335),.007,'brass',n=6)
            rod((i*.086+side*.028,-.295,2.335),(i*.086,-.295,2.373),.007,'brass',n=6)
    palace_roof(0,.12,2.62,2.13,1.01,.57,'player')
    for side in (-1,1):
        x=side*.62;y=-.49
        column(x,y,[(.0,.115,.115),(.04,.113,.113),(.07,.087,.087)],'stone',12)
        for a in (0,2.1,4.2):
            rod((x+.064*cos(a),y+.064*sin(a),.068),(x+.045*cos(a),y+.045*sin(a),.20),.015,'brass')
        column(x,y,[(.14,.046,.046),(.17,.088,.088),(.24,.11,.11),(.27,.104,.104)],'brass',24)
        for s in (-1,1):
            path([(x+s*.076,y,.25),(x+s*.095,y,.325),(x+s*.070,y,.34),(x+s*.057,y,.269)],.010,'brass')
