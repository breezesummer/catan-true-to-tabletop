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
 box((0,0,.09),(.83,.83,.18),'stone',.045);box((0,0,.185),(.73,.74,.06),'stone1',.02)
 ball((0,.10,.44),(.25,.28,.31),'sculpt',28,20)
 ball((0,-.11,.63),(.23,.23,.26),'sculpt',28,20)
 for sg in [-1,1]:
  ball((sg*.22,.15,.35),(.16,.20,.19),'sculpt',24,16)
  pts=smoothpath([(sg*.17,-.11,.62),(sg*.19,-.18,.40),(sg*.20,-.23,.25)]);tube(pts,.074,'sculpt',16)
  ball((sg*.20,-.28,.25),(.105,.15,.07),'sculpt',20,12)
  for i in range(3):ball((sg*.20+(i-1)*.053,-.385,.245),(.026,.055,.033),'stone1',12,8)
  ball((sg*.20,-.04,.86),(.095,.065,.09),'sculpt',20,12)
  torus((sg*.20,-.105,.87),.044,.014,'stone1','Y',20,8)
 # 宽额头、额脊、凸眼、鼻翼与嘴，避免普通动物的圆脸。
 ball((0,-.15,.79),(.245,.21,.205),'sculpt',32,20)
 ball((0,-.315,.72),(.185,.11,.105),'sculpt',24,16)
 ball((0,-.34,.68),(.16,.07,.042),'recess',20,10)
 ball((0,-.35,.645),(.17,.075,.05),'sculpt',20,12)
 for sg in [-1,1]:
  ball((sg*.11,-.32,.82),(.075,.055,.058),'stone1',24,12)
  ball((sg*.11,-.366,.82),(.018,.011,.018),'eye',16,10)
  tube(smoothpath([(sg*.03,-.34,.865),(sg*.09,-.37,.89),(sg*.17,-.34,.865)]),.026,'sculpt',12)
  ball((sg*.05,-.41,.755),(.047,.028,.027),'sculpt',16,10)
  ball((sg*.054,-.434,.756),(.012,.005,.008),'recess',12,8)
  for dx in [.035,.075]:ball((sg*dx,-.395,.683),(.012,.016,.022),'stone1',12,8)
  # 卷须从鼻侧伸出，再回卷。
  tube(smoothpath([(sg*.09,-.40,.74),(sg*.20,-.38,.72),(sg*.235,-.33,.77),(sg*.20,-.32,.79)]),.015,'stone1',8)
 # 额头浅浮雕卷纹。
 for sg in [-1,1]:
  pts=[(sg*(.065+.035*(1-k/24)*math.cos(k/24*math.pi*3)),-.328,.915+.035*(1-k/24)*math.sin(k/24*math.pi*3)) for k in range(25)];tube(pts,.010,'stone1',8)
 # 螺旋鬃毛环绕面部，大小有规律变化。
 for row,rr,count in [(0,.255,14),(1,.22,11)]:
  for i in range(count):
   a=2*math.pi*i/count;cx=rr*math.cos(a);cz=.79+rr*.78*math.sin(a);cy=-.15 if row==0 else .035
   ball((cx,cy+.025,cz),(.052,.040,.052),'sculpt',16,10)
   pts=[]
   for k in range(25):
    t=k/24;ang=t*math.pi*3.2;r=.048*(1-t)+.009;pts.append((cx+r*math.cos(ang),cy-.035*math.sin(math.pi*t),cz+r*math.sin(ang)))
   tube(pts,.012,'stone1',8)
 tube(smoothpath([(0,.32,.32),(.19,.36,.36),(.23,.31,.50),(.12,.29,.57),(.07,.32,.49)]),.042,'sculpt',12)
 # 胸前饰圈与圆形吊饰。
 torus((0,-.15,.54),.145,.021,'stone1','Y',28,8);ball((0,-.30,.51),(.045,.025,.045),'stone1',16,10)

def ornament(id,x,y,z,scale=1,flip=False):
 group(id);transform(x,y,z,math.pi if flip else 0)
 s=scale
 def B(p,d,ma='gold'):ball(tuple(q*s for q in p),tuple(q*s for q in d),ma,20,12)
 def T(pts,r):tube([tuple(q*s for q in p) for p in smoothpath(pts)],r*s,'gold',10)
 box((0,0,0),(.35*s,.24*s,.09*s),'gold',.025*s)
 B((0,0,.20),(.22,.13,.19));B((.20,0,.30),(.16,.14,.15));B((.32,-.005,.28),(.12,.10,.055));B((.32,0,.23),(.11,.085,.028),'gold_dark')
 for sg in [-1,1]:
  T([(-.11,sg*.09,.24),(-.16,sg*.14,.12),(-.12,sg*.15,.065)],.055)
  T([(.13,sg*.08,.20),(.16,sg*.15,.12),(.23,sg*.15,.07)],.044)
  for xx in [-.12,.23]:B((xx,sg*.14,.06),(.09,.055,.035))
  B((.245,sg*.12,.35),(.043,.021,.035),'gold_dark');B((.25,sg*.137,.356),(.018,.009,.019),'gold')
  T([(.13,sg*.08,.40),(.09,sg*.14,.49),(.18,sg*.16,.46)],.021)
  T([(.30,sg*.07,.29),(.39,sg*.13,.34),(.34,sg*.17,.39)],.013)
 # 向上反卷的鱼尾，粗根、逐渐收细的尾梢。
 pts=smoothpath([(-.13,0,.22),(-.28,0,.39),(-.22,0,.60),(-.04,0,.78),(-.07,0,.94),(-.18,0,1.0),(-.22,0,.94)],10)
 tube([tuple(q*s for q in p) for p in pts],.1*s,'gold',14,radii=[s*(.115*(1-i/(len(pts)-1))+.018) for i in range(len(pts))])
 for sg in [-1,1]:
  groove=[(p[0],sg*(.115*(1-i/(len(pts)-1))+.018)*.96,p[2]) for i,p in enumerate(pts[3:-4],start=3)];tube([tuple(q*s for q in p) for p in groove],.006*s,'gold_dark',6)
  pts2=[(p[0]-.025,p[1]+sg*.065*(1-i/(len(pts)-1)),p[2]) for i,p in enumerate(pts[:-5])];tube([tuple(q*s for q in p) for p in pts2],.012*s,'gold_light',6)
  # 身体侧面的鳞片浅浮雕。
  for row in range(4):
   for j in range(5-row):
    cx=-.13+j*.065+row*.022;cz=.15+row*.06
    pts=[(cx+.035*math.cos(a),sg*.125,cz+.033*math.sin(a)) for a in [math.pi+i*math.pi/8 for i in range(9)]]
    tube([tuple(q*s for q in p) for p in pts],.009*s,'gold_light',6)

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
