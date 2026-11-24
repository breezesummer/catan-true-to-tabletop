"""头骨、面部软组织、鬓发与八瓣软帽。正侧面共享一张连续的面部网格。"""
from math import sin, cos, pi, tau, exp, sqrt
from surfaces import PORTRAIT_BOX
from workshop import P,V,UP,RIGHT,FRONT,smooth,lerp_profile,orb,trim,tube,sweep


def sculpt_head(g):
    g.part("01 面部与颈项")
    neck=smooth([(237,273,1,57,53),(235,250,1,50,46),(234,230,0,37,39),
                 (232,210,-1,28,31),(231,193,0,28,30),(234,179,2,32,29)],5)
    rows=[]
    for x,y,d,w,h in neck:
        ring=[]
        for i in range(52):
            a=i*tau/52
            cord=1.85*exp(-((abs((a+.02+pi)%tau-pi)-(.45+(235-y)*.009))/.15)**2)
            larynx=2.9*exp(-((y-228)/5.8)**2)*max(0,cos(a))**12
            ring.append(P(x+(w+cord)*sin(a),y+3*cos(a),d-(h+cord+larynx)*cos(a)))
        rows.append(ring)
    def painted_neck(j,k):
        a=(k+.5)*tau/52
        pt=rows[j][k]
        return "portrait" if cos(a)>.25 and pt.z<795-210 else "skin"
    def neck_uv(j,k,key,pt):
        if key=="skin": return k/52,j/(len(rows)-1)
        x0,y0,x1,y1=PORTRAIT_BOX
        return (max(0,min(1,(pt.x+230-x0)/(x1-x0))),max(0,min(1,(pt.z-795+y1)/(y1-y0))))
    g.grid(rows,painted_neck,uv=neck_uv)
    profiles=[(113,228,35,39,2),(124,230,40,45,1),(135,232,40.4,46,-1),
              (148,232.5,40.7,45,-2),(161,232.7,41.6,43,-4),(170,234,40.4,42,-5),
              (181,235,40.5,40,-5),(191,235.8,38,36,-7),(201,237.7,32,31,-10),
              (208,241,23,23,-12),(212,245,13.6,14,-14),(214.7,246,2,4,-17)]
    def head(row,a):
        cx,rx,depth,center=lerp_profile(profiles,row)
        x=cx+rx*sin(a)
        front=max(0,cos(a))
        def bump(xc,yc,w,h):
            return exp(-((x-xc)/w)**2-((row-yc)/h)**2)
        relief=7.4*bump(246,154.5,7.1,15)+12.2*bump(249.7,166.5,8.0,6.9)
        relief+=4.5*bump(244,168.4,5.3,3.5)+3.9*bump(255,167.2,4.7,3.5)
        relief+=5.6*bump(242,179.4,17,5.5)+5.4*bump(249.5,197.5,17,9)
        relief-=1.8*bump(246,183,14,1.8)
        for ex,ey,sign in ((226,153,-1),(257.5,149.6,1)):
            relief-=4.4*bump(ex,ey,9.5,6.4)
            relief+=1.7*bump(ex,ey,5.4,2.8)+4.0*bump(ex,ey-9,10,3.8)
            relief+=3.9*bump(ex+sign*5,ey+11,11.5,7)
            q=(x-ex)/8.0
            if abs(q)<1:
                envelope=1-q*q
                for k,amt in ((-1,1.7),(1,1.15)):
                    lid=ey+k*2.7*sqrt(envelope)-sign*q*.65
                    relief+=amt*envelope*exp(-((row-lid)/1.25)**2)
        # 嘴角向上收，唇下沟与两片咬肌之间保留颏突。
        relief+=2.2*bump(250,183.7,11,2.2)-2*bump(249,188,13,2.4)
        relief+=3.2*bump(237,176.5,8.8,3.1)+2.9*bump(257,175,7.2,3.3)
        beard=min(1,max(0,(row-185+9*abs(sin(a)))/11))
        depth+=beard*(1.35+.48*cos(35*a+(row-190)*.15))
        y=center-depth*cos(a)-relief*front**.65
        # 鼻部在四分之三视图内向人物左侧突出，侧看仍有独立鼻尖与鼻根。
        y+=4.2*((x-cx)/max(20,rx))*front
        return P(x,row,y)
    m,n=64,100
    rows=[[head(113+(214.7-113)*j/(m-1),tau*k/n) for k in range(n)] for j in range(m)]
    g.grid(rows,"skin",chart="头颅")
    # 耳轮、对耳轮和耳甲腔朝外，各自有厚度。
    for side in (-1,1):
        c=P(193 if side==-1 else 271,173,-1)
        normal=V((side*.82,-.57,0)).normalized()
        across=UP.cross(normal).normalized()
        orb(g,c,(8.5,15,4.7),"skin",22,14,(across,UP,normal))
        orb(g,c+normal*4.3+UP*.7,(4.6,8.6,1.0),"nail",18,10,(across,UP,normal))
        outline=[(-1,14),(4.3,11),(7.1,3),(5.1,-7),(1,-12),(-3.9,-9),(-6,0),(-5.4,8),(-1,14)]
        tube(g,[c+across*x+UP*z+normal*4.8 for x,z in outline],
             [1.5,1.8,1.8,1.6,1.7,1.5,1.5,1.5,1.5],"skin",8,3)
        tube(g,[c+normal*5+across*x+UP*z for x,z in ((-2,-6),(.5,-1),(1,3),(-1,7))],
             [1.8,1.45,1.05,.45],"skin",8)


def hair(g):
    g.part("02 波状头发与鬓须")
    center=230
    def hairline(a):
        angle=abs((a+pi)%tau-pi)
        (edge,)=lerp_profile([(0,119),(.78,123),(1,130),(1.18,143),
                             (1.44,157),(1.65,177),(2,194),(2.4,200),(pi,203)],angle)
        return edge+2.6*sin(5*a+.5)
    def point(a,t,offset=0):
        root=146-9.8*sin(a)-21*max(0,cos(a))**1.55+7*max(0,-cos(a))
        row=root+(max(root+2,hairline(a))-root)*t
        wave=1.6*cos(10*a+4*t)+1.4*sin(6*a-4*t)
        bulge=sin(pi*t)*5+(1-t)*3
        turn=4.3*max(0,(t-.72)/.28)**2
        rx=41.6+bulge+wave+offset+6.4*t-turn
        ry=39.7+bulge+wave+offset+4.3*t-turn
        return P(center+rx*sin(a)-7.5*t,row,4-ry*cos(a)+9*t)
    rows=[[point(k*tau/64,j/16) for k in range(64)] for j in range(17)]
    rows.append([point(k*tau/64,1,-5)+UP*3.6 for k in range(64)])
    g.grid(rows,"hair",caps=False)
    # 下层在耳后折回颈窝，上层覆盖发根。锁间留沟，发梢回卷进发团。
    for layer,count in ((0,16),(1,13)):
        for i in range(count):
            a=1.02+i*(tau-2.04)/(count-1)+layer*.075+.035*sin(i*2.32)
            sign=1 if a<pi else -1
            pts=[];r=[];outward=[];sideways=[]
            for j in range(19):
                t=j/18
                phase=a+sign*(.27*(1-t)+(.20+.04*cos(i*1.9))*sin((1.72+.17*cos(i))*pi*t+.46*sin(2*a))+
                              .065*cos(3*pi*t+.32*i))
                h=(.32+.68*t if layer==0 else .08+.72*t)
                radial=V((sin(phase),-cos(phase),0))
                end=max(0,(t-.74)/.26)
                puff=(5+layer*2.5)*sin(pi*t)+3.2*end-2.8*end**2
                p=point(phase,h,2.8+layer*1.9)+radial*puff+UP*(end**2*6.6+2*sin(a*3))
                pts.append(p);outward.append(radial);sideways.append(V((cos(phase),sin(phase),0)))
                belly=sin(pi*t)**.64
                r.append((1.9+6.8*belly-.82*end,1.1+3.9*belly-.5*end))
            sweep(g,pts,r,"hair",12,sideways[0])
            for strand in (-.37,.24):
                crest=[p+outward[j]*(r[j][1]*.90)+sideways[j]*r[j][0]*strand for j,p in enumerate(pts)]
                sweep(g,crest,[.19+.57*sin(pi*j/18) for j in range(19)],"beard",5)


def bonnet(g):
    g.part("03 八瓣呢帽与皮帽箍")
    # 帽腹比帽口宽，帽顶向右上方倾斜；片间缝合令冠顶自然收拢。
    profile=[(0,46,42,153),(.09,59,50,155),(.19,77,60,153),(.32,83,65,141),
             (.46,82,64,127),(.61,72,57,111),(.77,53,46,94),(.89,32,30,84),(1,2.1,2.2,81)]
    def crown(a,t):
        rx,ry,row=lerp_profile(profile,t)
        phase=a+.13*t+.036*sin(2*a+3*t)
        seam=exp(-(sin(4*phase)/.22)**2)
        tuck=(1.1+5.3*sin(pi*t)**.7)*seam
        lobe=2.8*cos(5*a+1.9*t)+1.7*sin(3*a-2*t)-2.9*cos(8*phase)
        x=221+16*t+(rx-tuck+lobe*sin(pi*t))*sin(a)
        d=6+(ry-tuck+lobe*sin(pi*t))*(-cos(a))
        row+=-.248*(x-222)-30*max(0,cos(a))**1.55*(1-t)**.79+3.6*cos(3*a+.8)*sin(pi*t)
        return P(x,row+tuck*.58,d)
    rows=[[crown(tau*k/80,j/26) for k in range(80)] for j in range(27)]
    g.grid(rows,"cap",chart="帽冠")
    for j in range(8):
        a=tau*j/8
        pts=[]
        for i in range(32):
            t=.12+.84*i/31
            angle=a-.13*t-.036*sin(2*a+3*t)
            pts.append(crown(angle,t)+V((sin(angle),-cos(angle),.5))*.45)
        trim(g,pts,.54,"binding",sides=5)
    rows=[]
    for level,rx,ry in ((0,47,46),(.12,50,48),(.58,49,49),(.87,47.6,47),(1,46,44)):
        row=[]
        for k in range(64):
            a=k*tau/64
            x=230+rx*sin(a)
            y=150-.235*(x-229)-14.2*max(0,cos(a))**1.7-level*11.7
            row.append(P(x,y,2-ry*cos(a)))
        rows.append(row)
    g.grid(rows,"hatband")
    trim(g,rows[1],.95,"welt",True)
