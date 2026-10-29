"""狮、脊兽、灯笼、井架与院内器物。"""
import math,random
from geometry import *
def smoothpath(points,steps=8):
 p=[Vector(v) for v in points];out=[]
 for j in range(len(p)-1):
  a=p[max(0,j-1)];b=p[j];c=p[j+1];d=p[min(j+2,len(p)-1)]
  for k in range(steps):
   t=k/steps;out.append(tuple(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t)))
 out.append(tuple(p[-1]));return out

def lion(id,x,y,z):
 group(id);transform(x,y,z)
 box((0,.02,.085),(.86,.90,.17),'stone',.027);box((0,.02,.18),(.79,.83,.055),'stone1',.018)
 # 蹲坐体态：臀部宽、胸肩厚，前腿承担身体，头部向前探。
 ball((0,.16,.49),(.285,.335,.30),'sculpt',40,28)
 ball((0,-.055,.59),(.255,.25,.32),'sculpt',40,28)
 for sg in [-1,1]:
  ball((sg*.245,.19,.38),(.165,.215,.18),'sculpt',32,24)
  ball((sg*.245,.08,.255),(.14,.20,.065),'sculpt',28,20)
  tube(smoothpath([(sg*.19,-.10,.58),(sg*.21,-.19,.40),(sg*.235,-.29,.265)],12),.105,'sculpt',24,radii=[.12-.025*i/24 for i in range(25)])
  ball((sg*.235,-.32,.25),(.14,.16,.075),'sculpt',32,20)
  for j in range(4):
   dx=sg*.235+(j-1.5)*.06
   ball((dx,-.428,.25),(.034,.068,.048),'sculpt',20,16)
   ball((dx,-.476,.237),(.024,.022,.027),'stone1',20,12)
 # 宽额头、鼻梁和下颌作为连续雕塑体积，不再使用平板圆脸。
 ball((0,-.13,.87),(.265,.232,.235),'sculpt',40,32)
 ball((0,-.24,.79),(.24,.18,.17),'sculpt',40,28)
 ball((0,-.36,.725),(.17,.10,.042),'sculpt',32,20)
 ball((0,-.40,.805),(.095,.078,.056),'sculpt',32,20)
 for sg in [-1,1]:
  ball((sg*.10,-.371,.774),(.115,.088,.063),'sculpt',32,20)
  # 深灰仅用于石雕口缝与眼窝，眼睛仍然是石材。
  ball((sg*.112,-.339,.873),(.061,.032,.050),'recess',32,20)
  ball((sg*.112,-.36,.870),(.052,.035,.052),'stone1',32,24)
  ball((sg*.112,-.39,.869),(.012,.004,.016),'stone1',24,16)
  brow=smoothpath([(sg*.024,-.338,.925),(sg*.08,-.365,.94),(sg*.16,-.35,.922),(sg*.21,-.30,.95)],12)
  tube(brow,.031,'sculpt',20)
  for cx,cz,rr in [(sg*.070,.949,.028),(sg*.145,.94,.026)]:
   pp=[(cx+rr*(1-t)*math.cos(t*math.pi*3),-.374,cz+rr*(1-t)*math.sin(t*math.pi*3)) for t in [j/36 for j in range(37)]];tube(pp,.011,'stone1',12)
  # 实体三角耳和内耳凹陷。
  ball((sg*.245,-.23,1.005),(.062,.047,.082),'sculpt',32,24)
  ball((sg*.245,-.285,1.015),(.036,.012,.045),'stone2',24,16)
  tube(smoothpath([(sg*.207,-.28,.99),(sg*.23,-.285,1.065),(sg*.274,-.26,1.045),(sg*.283,-.26,.99),(sg*.207,-.28,.99)],10),.016,'stone1',12)
  ball((sg*.047,-.466,.803),(.025,.009,.014),'recess',20,12)
  # 上唇翻卷、犬齿与下唇。
  tube(smoothpath([(0,-.455,.767),(sg*.10,-.449,.752),(sg*.19,-.40,.778),(sg*.21,-.35,.79)],12),.015,'stone1',16)
  ball((sg*.115,-.414,.730),(.018,.025,.032),'stone1',20,16)
  tube(smoothpath([(sg*.10,-.405,.706),(sg*.12,-.404,.675),(sg*.08,-.39,.645)],10),.021,'sculpt',16)
 ball((0,-.40,.738),(.157,.035,.028),'recess',32,16)
 tube(smoothpath([(-.17,-.37,.72),(-.10,-.414,.702),(0,-.425,.704),(.10,-.414,.702),(.17,-.37,.72)],12),.018,'stone1',16)
 # 厚实的鬃毛锁片沿头的侧后方分布；每束卷毛有底部体积与收尖浮雕。
 for row in range(3):
  count=9 if row<2 else 7
  for i in range(count):
   a=-.30+(math.pi+.60)*i/(count-1)
   cx=.267*math.cos(a);cz=.84+.244*math.sin(a)-row*.035;cy=-.06+row*.115
   ball((cx,cy,cz),(.076,.08,.085),'sculpt',24,20)
   pts=[];rads=[]
   for k in range(49):
    t=k/48;ang=a+t*math.pi*3.3;r=.064*(1-t)+.005
    pts.append((cx+r*math.cos(ang),cy-.070-.012*math.sin(math.pi*t),cz+r*math.sin(ang)));rads.append(.023*(1-t)+.008)
   tube(pts,.02,'stone1',12,radii=rads)
 for sg in [-1,1]:
  for k in range(3):
   cx=sg*(.24-.025*k);cy=.015+k*.065;cz=.69-k*.07
   ball((cx,cy,cz),(.08,.08,.11),'sculpt',24,20)
   pts=[(cx+.056*(1-t)*math.cos(t*math.pi*3),cy-.07,cz+.08*(1-t)*math.sin(t*math.pi*3)) for t in [i/40 for i in range(41)]];tube(pts,.021,'stone1',14)
 # 项圈贴合胸部，吊铃与尾巴形成明确轮廓。
 pts=[(.215*math.cos(a),-.11-.15*math.sin(a),.63-.07*math.sin(a)) for a in [i*math.pi/40 for i in range(41)]];tube(pts,.028,'stone1',16)
 torus((0,-.294,.562),.035,.013,'stone1','Y',32,12)
 ball((0,-.30,.502),(.055,.045,.052),'stone1',28,20);torus((0,-.30,.496),.053,.007,'stone2','Z',32,8)
 tail=smoothpath([(0,.39,.30),(-.22,.40,.36),(-.31,.33,.53),(-.30,.28,.67),(-.23,.26,.72),(-.22,.29,.67)],12)
 tube(tail,.055,'sculpt',20,radii=[.059*(1-i/(len(tail)-1))+.018 for i in range(len(tail))])


def ornament(id,x,y,z,scale=1,flip=False):
 group(id);transform(x,y,z,math.pi if flip else 0)
 s=scale
 def B(p,d,ma='gold'):ball(tuple(q*s for q in p),tuple(q*s for q in d),ma,32,24)
 def T(pts,r,ma='gold_light',radii=None):
  pp=smoothpath(pts,12);tube([tuple(q*s for q in p) for p in pp],r*s,ma,16,radii=[s*(r*(1-.65*i/(len(pp)-1))) for i in range(len(pp))] if radii else None)
 box((0,0,.0),(.58*s,.31*s,.10*s),'gold',.032*s)
 B((-.025,0,.29),(.25,.16,.235));B((.17,0,.35),(.18,.17,.25));B((.235,0,.48),(.18,.16,.17))
 B((.36,0,.43),(.15,.12,.07));B((.35,0,.34),(.14,.11,.042))
 for sg in [-1,1]:
  B((-.16,sg*.11,.18),(.13,.10,.145));B((-.14,sg*.15,.075),(.13,.08,.055))
  T([(.13,sg*.11,.29),(.16,sg*.15,.16),(.25,sg*.17,.085)],.07,'gold')
  for j in range(3):B((.26+j*.042,sg*.15,.061),(.04,.055,.031))
  B((.30,sg*.125,.485),(.052,.027,.045),'gold_dark');B((.31,sg*.148,.483),(.032,.017,.033),'gold_light')
  T([(.17,sg*.14,.56),(.23,sg*.16,.57),(.32,sg*.15,.535),(.37,sg*.11,.55)],.024)
  T([(.12,sg*.09,.60),(.04,sg*.12,.66),(.02,sg*.12,.75)],.045,'gold',True)
  T([(.40,sg*.10,.43),(.49,sg*.11,.48),(.46,sg*.10,.52),(.425,sg*.10,.49)],.018)
  B((.40,sg*.075,.38),(.10,.043,.025),'gold_dark')
  T([(.27,sg*.12,.37),(.35,sg*.125,.33),(.45,sg*.08,.36)],.017)
  B((.405,sg*.08,.365),(.018,.02,.028),'gold_light')
  # 耳与颊部卷纹沿侧面隆起。
  T([(.17,sg*.15,.50),(.10,sg*.19,.49),(.08,sg*.18,.55)],.023)
  for cx,cz,rr in [(.17,.35,.075),(.04,.21,.10),(-.14,.23,.06)]:
   pts=[((cx+rr*(1-t)*math.cos(t*math.pi*3))*s,sg*(.153+.015*math.sin(math.pi*t))*s,(cz+rr*(1-t)*math.sin(t*math.pi*3))*s) for t in [i/48 for i in range(49)]];tube(pts,.016*s,'gold_light',12)
 # 宽厚S形火焰尾：在侧视平面宽、横向薄，避免旧版圆管形尾巴。
 path=smoothpath([(-.18,0,.30),(-.32,0,.48),(-.28,0,.70),(-.10,0,.90),(-.12,0,1.12),(-.30,0,1.29),(-.34,0,1.40),(-.25,0,1.43),(-.23,0,1.38)],12)
 v=[];N=28;profiles=[]
 for i,p in enumerate(path):
  t=i/(len(path)-1);tan=(Vector(path[min(i+1,len(path)-1)])-Vector(path[max(0,i-1)])).normalized();u=Vector((tan.z,0,-tan.x));width=.13*(math.sin(math.pi*t)**.6)+.055*(1-t)+.009;depth=.075*(1-t)+.015
  profiles.append((u,width,depth))
  for j in range(N):
   a=j*2*math.pi/N;vv=Vector(p)+u*width*math.cos(a)+Vector((0,depth*math.sin(a),0));v.append(tuple(q*s for q in vv))
 f=[(i*N+j,i*N+(j+1)%N,(i+1)*N+(j+1)%N,(i+1)*N+j) for i in range(len(path)-1) for j in range(N)]+[tuple(range(N-1,-1,-1)),tuple((len(path)-1)*N+j for j in range(N))];add(v,f,'gold',True)
 for sg in [-1,1]:
  for off in [-.60,0,.60]:
   pts=[]
   for i,p in enumerate(path[3:-3],3):
    u,w,d=profiles[i];vv=Vector(p)+u*w*off+Vector((0,sg*(d*math.sqrt(1-off*off)+.004),0));pts.append(tuple(q*s for q in vv))
   tube(pts,.009*s,'gold_light',12)
  # 鳞片紧贴椭球身体包络面，避免悬空珠状突起。
  def surface(xx,zz):
   values=[]
   for cx,cz,rx,rz,ry in [(-.025,.29,.25,.235,.16),(.17,.35,.18,.25,.17)]:
    q=1-((xx-cx)/rx)**2-((zz-cz)/rz)**2
    if q>0:values.append(ry*math.sqrt(q))
   return max(values,default=.025)
  for row in range(5):
   for j in range(4):
    cx=-.205+j*.064+(row%2)*.030;cz=.26+row*.055;rr=.037
    if cx>.055 and cz>.42:continue
    vv=[(cx,sg*(surface(cx,cz)+.006),cz-.007)]
    for k in range(20):
     a=k*2*math.pi/20;xx=cx+rr*math.cos(a);zz=cz+rr*.95*math.sin(a);vv.append((xx,sg*(surface(xx,zz)+.003),zz))
    ff=[(0,1+k,1+(k+1)%20) for k in range(20)];sheet([tuple(q*s for q in p) for p in vv],ff,'gold_light',offset=(0,-sg*.009*s,0),smooth=True)


def lantern(id,x,y,z):
 group(id);transform(x,y,z)
 ball((0,0,0),(.28,.28,.32),'lantern',40,24)
 for i in range(12):
  a=2*math.pi*i/12;pts=[(.282*math.sin(t)*math.cos(a),.282*math.sin(t)*math.sin(a),.322*math.cos(t)) for t in [j*math.pi/32 for j in range(2,31)]];tube(pts,.009,'gold',6)
 for sg in [-1,1]:lathe((0,0,sg*.30),[(.075,-.025),(.09,0),(.09,.045),(.07,.06)],'gold',32)
 torus((0,0,.405),.055,.012,'gold','Y',24,8);beam((0,0,.43),(0,.17,.63),.015,'gold',10)
 ball((0,0,-.39),(.045,.045,.055),'red',16,12)
 for i in range(10):
  a=2*math.pi*i/10;beam((.022*math.cos(a),.022*math.sin(a),-.43),(.045*math.cos(a),.045*math.sin(a),-.61),.008,'red',6)

def chest(x,y):
 group('A24_铜箍木箱');transform(x,y,1)
 box((0,0,.25),(.73,.54,.50),'wood',.025)
 for side in [-1,1]:
  for j in range(3):box((0,side*.277,.10+j*.14),(.67,.035,.13),'wood1',.008)
 box((0,0,.53),(.77,.58,.10),'wood',.024)
 for x in [-.28,.28]:
  box((x,0,.54),(.055,.60,.09),'gold',.008)
  for sy in [-1,1]:
   box((x,sy*.293,.28),(.055,.035,.5),'gold',.006)
   for z in [.10,.45]:ball((x,sy*.316,z),(.017,.012,.017),'gold_light',12,8)
 box((0,-.313,.38),(.09,.04,.16),'gold',.008);torus((0,-.345,.32),.033,.009,'gold','Y',16,6)

def hay(id,x,y):
 group(id);transform(x,y,1)
 box((0,0,.35),(.66,.61,.70),'hay',.045)
 for k in range(500):
  side=k%5;u=random.uniform(-.31,.31);v=random.uniform(-.29,.29);z=random.uniform(.04,.67);length=random.uniform(.055,.18)
  if side==0:a=(u,v,.708+random.uniform(0,.016));b=(max(-.31,min(.31,u+length)),v+random.uniform(-.025,.025),a[2])
  elif side in [1,2]:a=(u,(-1 if side==1 else 1)*.312,z);b=(max(-.32,min(.32,u+length)),a[1]+random.uniform(-.006,.006),min(.69,z+random.uniform(-.06,.06)))
  else:a=((-1 if side==3 else 1)*.338,v,z);b=(a[0],max(-.30,min(.30,v+length)),min(.68,z+random.uniform(-.06,.06)))
  beam(a,b,random.uniform(.004,.008),random.choice(['hay','hay_light','hay_dark']),5)
 for x in [-.19,.19]:
  pts=[(x,-.32,.04),(x,-.32,.70),(x,.32,.70),(x,.32,.04),(x,-.32,.04)];tube(pts,.018,'rope',8)

def well():
 group('A33_石井');transform(3.35,-2.95,1)
 lathe((0,0,0),[(.47,0),(.47,.50),(.34,.50),(.34,.02)],'mortar',64)
 for j in range(3):
  for i in range(14):
   a=(i+.5*(j%2))*2*math.pi/14+.015;b=(i+1+.5*(j%2))*2*math.pi/14-.015;poly=[(r*math.cos(t),r*math.sin(t)) for r,ts in [(.49,[a+(b-a)*k/4 for k in range(5)]),(.335,[b-(b-a)*k/4 for k in range(5)])] for t in ts];extrude(poly,.01+j*.165,.165+j*.165,random.choice(['stone','stone1','stone2']))
 lathe((0,0,.025),[(.333,0),(.333,.018)],'water',48)
 group('A34_辘轳');transform(3.35,-2.95,1)
 for x in [-.55,.55]:
  box((x,0,.47),(.12,.15,.94),'wood',.025);box((x,0,.05),(.24,.62,.1),'wood',.02)
  for sg in [-1,1]:beam((x,sg*.27,.09),(x,0,.62),.045,'wood',8)
 beam((-.65,0,.88),(.72,0,.88),.07,'wood',24)
 for x in [-.57,.57]:torus((x,0,.88),.09,.018,'gold','X',24,8)
 helix=[(-.23+.46*i/240,.102*math.cos(i*math.pi/6),.88+.102*math.sin(i*math.pi/6)) for i in range(241)];tube(helix,.015,'rope',8)
 beam((0,0,.80),(0,0,.15),.014,'rope',8);beam((.72,0,.88),(.72,0,.65),.028,'gold',12);beam((.72,0,.65),(.86,0,.65),.035,'wood',16)
 group('A35_木桶');transform(4.15,-3.0,1)
 lathe((0,0,0),[(.15,0),(.20,.36),(.19,.39),(.16,.39),(.12,.045)],'wood',32)
 for z,R in [(.07,.16),(.30,.19)]:torus((0,0,z),R,.017,'gold','Z',32,8)
 for i in range(12):
  a=2*math.pi*i/12;beam((.151*math.cos(a),.151*math.sin(a),.025),(.198*math.cos(a),.198*math.sin(a),.37),.003,'wood_dark',5)

def flag():
 group('A36_旗杆');transform(5,-4.5,0)
 lathe((0,0,2.5),[(.085,0),(.085,3.80),(.10,3.82),(.10,3.95),(.07,3.99)],'wood',40)
 for z in [2.62,3.25,6.20,6.30]:torus((0,0,z),.089,.018,'gold','Z',40,10)
 ball((0,0,6.49),(.105,.105,.13),'gold',24,16)
 group('A37_红旗');transform(5.08,-4.5,0)
 nx,ny=40,24;v=[]
 for j in range(ny+1):
  for i in range(nx+1):
   u=i/nx;t=j/ny;v.append((1.85*u,.20*math.sin(u*8-t*1.6)*u+.05*math.sin(u*18+t*4)*u,6.25-1.50*t-.20*u+.08*math.sin(u*5+t*2)*u))
 f=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)];sheet(v,f,'cloth',offset=(0,.012,0),smooth=True)
 for edge in [0,ny]:tube([v[edge*(nx+1)+i] for i in range(nx+1)],.012,'red',8)
 tube([v[j*(nx+1)+nx] for j in range(ny+1)],.012,'red',8)
 for z in [4.79,6.21]:torus((-.04,0,z),.095,.012,'gold','Z',24,8)
