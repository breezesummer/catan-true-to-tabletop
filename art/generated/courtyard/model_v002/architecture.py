"""建筑、铺装与屋顶；全部轮廓和砖瓦均为真实几何。"""
import math,random
from geometry import *
import geometry as geo
G=1.0

def paving(x0,x1,y0,y1,z,step=.30,circle=None):
 for j in range(math.floor(y0/step),math.ceil(y1/step)):
  for i in range(math.floor(x0/step),math.ceil(x1/step)):
   x=i*step+step/2+(step*.5 if j%2 else 0);y=j*step+step/2
   xa,xb=max(x-step/2,x0),min(x+step/2,x1);ya,yb=max(y-step/2,y0),min(y+step/2,y1)
   if xb-xa<.015 or yb-ya<.015:continue
   poly=[(xa+.006,ya+.006),(xb-.006,ya+.006),(xb-.006,yb-.006),(xa+.006,yb-.006)]
   if circle:
    if all(px*px+py*py>circle*circle for px,py in poly) and x*x+y*y>(circle+step)**2:continue
    if any(px*px+py*py>circle*circle for px,py in poly):
     for k in range(160):
      nx,ny=math.cos(k*math.pi/80),math.sin(k*math.pi/80);out=[]
      for a,b in zip(poly,poly[1:]+poly[:1]):
       da=a[0]*nx+a[1]*ny-circle;db=b[0]*nx+b[1]*ny-circle
       if da<=0:out.append(a)
       if (da<0)!=(db<0):t=da/(da-db);out.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
      poly=out
      if not poly:break
     if len(poly)>=3:extrude(poly,z-.055,z,random.choice(['stone','stone1','stone2']))
     continue
   box(((xa+xb)/2,(ya+yb)/2,z-.025),(xb-xa-.012,yb-ya-.012,.05),random.choice(['stone','stone1','stone2']),.012)

def foundation(x,y,w,d,top):
 h=top-G;box((x,y,G+h/2),(w,d,h),'mortar',.03)
 rows=max(2,round(h/.22));rh=h/rows
 for side in [-1,1]:
  N=round(w/.55)
  for j in range(rows):
   for i in range(N+1):
    a=max(-w/2,-w/2+(i-.5*(j%2))*w/N);b=min(w/2,-w/2+(i+1-.5*(j%2))*w/N)
    if b-a>.05:box((x+(a+b)/2,y+side*(d/2-.045),G+(j+.5)*rh),(b-a-.012,.11,rh-.012),random.choice(['stone','stone1','stone2']),.015)
  N=round(d/.55)
  for j in range(rows):
   for i in range(N):box((x+side*(w/2-.045),y-d/2+(i+.5)*d/N,G+(j+.5)*rh),(.11,d/N-.012,rh-.012),random.choice(['stone','stone1','stone2']),.015)
 paving(x-w/2+.035,x+w/2-.035,y-d/2+.035,y+d/2-.035,top+.018,.34)

def roof(w,d,eave,rise,hip=False):
 half=w/2;dep=d/2;ridge=half-(.90 if hip else .10)
 def limit(t):
  u=max(0,(t-.48)/.52);return ridge+(half-ridge)*(u*u*(3-2*u) if hip else t)
 def roofz(x,t):return eave+rise*(1-t)+.35*(t*t-t)+.13*t**8+.22*(abs(x)/half)**8*t**2+.12*(abs(x)/ridge)**4*(1-t)**2
 def at(s,t,side):
  x=s*limit(t);return (x,side*(t*dep+.002),roofz(x,t))
 def tile(pfunc,s0,s1,t0,t1,ma):
  # 筒瓦弧截面与内壳，前后端有实际厚度。
  v=[];segments=8
  for inner in [False,True]:
   for t in [t0,t1]:
    for k in range(segments+1):
     a=k*math.pi/segments;s=s0+(s1-s0)*k/segments;p=pfunc(s,t);v.append((p[0],p[1],p[2]+(.025 if inner else .043)+.064*math.sin(a)+(.012 if t==t1 else 0)))
  N=segments+1;f=[]
  for j in [0,2]:
   for k in range(segments):f.append((j*N+k,j*N+k+1,(j+1)*N+k+1,(j+1)*N+k))
  for j in [0,1]:
   for k in range(segments):f.append((j*N+k,j*N+k+1,(j+2)*N+k+1,(j+2)*N+k))
  for k in [0,segments]:f.append((k,N+k,3*N+k,2*N+k))
  add(v,f,ma,[True]*(segments*2)+[False]*(segments*2+2))
 # 连续屋底与铺设筒瓦。
 for side in [-1,1]:
  nx,ny=28,18;v=[at(-1+2*i/nx,j/ny,side) for j in range(ny+1) for i in range(nx+1)];f=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)];sheet(v,f,'tile',smooth=True)
  cols=round(w/.19);rows=round(math.sqrt(dep*dep+rise*rise)/.23)
  for i in range(cols):
   for j in range(rows):tile(lambda a,b:at(a,b,side),-1+(i+.06)*2/cols,-1+(i+.94)*2/cols,j/rows,min(1,(j+1.06)/rows),random.choice(['tile','tile','tile1','tile2']))
  for edge in [-1,1]:
   pts=[(at(edge,j/24,side)[0],at(edge,j/24,side)[1],at(edge,j/24,side)[2]+.15) for j in range(25)]
   ribbon(pts,.23,.14,'red')
   for j in range(2,24,2):
    p=Vector(pts[j]);t=(Vector(pts[j+1])-Vector(pts[j-1])).normalized();u=Vector((-t.y,t.x,0)).normalized();beam(tuple(p-u*.112+Vector((0,0,.072))),tuple(p+u*.112+Vector((0,0,.072))),.003,'red_dark',6)
   tip=pts[-1];ball(tip,(.145,.12,.13),'gold',20,12)
  pts=[(at(-1+2*i/32,1,side)[0],side*dep,at(-1+2*i/32,1,side)[2]+.01) for i in range(33)];ribbon(pts,.14,.14,'red')
  for i in range(cols):
   s=-1+(i+.5)*2/cols;p=at(s,1,side)
   # 灰瓦瓦当与红色滴水，端面在檐口轮廓上可见。
   beam((p[0],p[1]-side*.02,p[2]+.048),(p[0],p[1]+side*.045,p[2]+.048),.067,'tile',16)
   ball((p[0],p[1]+side*.048,p[2]+.048),(.027,.012,.027),'tile1',12,8)
   ball((p[0],p[1]-.04*side,p[2]-.13),(.045,.06,.075),'red',12,8)
 if hip:
  for side in [-1,1]:
   def hp(s,t):
    u=.48+.52*t;x=side*(ridge+(half-ridge)*t*t*(3-2*t));return (x,s*dep*u,eave+rise*(1-u)+.35*(u*u-u)+.13*u**8+.22*abs(s)**6*t)
   nx,ny=20,12;v=[hp(-1+2*i/nx,j/ny) for j in range(ny+1) for i in range(nx+1)];f=[(j*(nx+1)+i,j*(nx+1)+i+1,(j+1)*(nx+1)+i+1,(j+1)*(nx+1)+i) for j in range(ny) for i in range(nx)];sheet(v,f,'tile',smooth=True)
   cols=round(d/.19);rows=6
   for i in range(cols):
    for j in range(rows):tile(hp,-1+(i+.06)*2/cols,-1+(i+.94)*2/cols,j/rows,min(1,(j+1.04)/rows),random.choice(['tile','tile1','tile2']))
   pts=[hp(s,1) for s in [-1+i/16 for i in range(33)]];ribbon([(x,y,z+.10) for x,y,z in pts],.19,.13,'red')
   # 上部山花与木结构，山花下沿连接侧坡顶边。
   lower=eave+rise*.52+.35*(.48*.48-.48)+.015;xx=side*(ridge-.04)
   sheet([(xx,-dep*.48,lower),(xx,dep*.48,lower),(xx,0,eave+rise+.08)],[(0,1,2)],'wood',offset=(-side*.05,0,0))
   beam((xx,-dep*.43,lower+.04),(xx,dep*.43,lower+.04),.055,'wood')
   beam((xx,0,lower),(xx,0,eave+rise+.08),.055,'wood')
   for sg in [-1,1]:beam((xx,0,eave+rise-.05),(xx,sg*dep*.43,lower+.04),.055,'wood')
 else:
  for side in [-1,1]:
   x=side*ridge
   sheet([(x,-dep,eave),(x,dep,eave),(x,0,eave+rise)],[(0,1,2)],'plaster',offset=(-side*.05,0,0))
   beam((x,0,eave),(x,0,eave+rise),.06,'wood')
   beam((x,-dep*.85,eave+.10),(x,dep*.85,eave+.10),.065,'wood')
 pts=[(ridge*(-1+2*i/48),0,eave+rise+.12*abs(-1+2*i/48)**4+.14) for i in range(49)];ribbon(pts,.26,.22,'red')
 for i in range(round(2*ridge/.26)):
  a=-ridge+i*2*ridge/round(2*ridge/.26);b=-ridge+(i+1)*2*ridge/round(2*ridge/.26);x=(a+b)/2;z=eave+rise+.12*abs(x/ridge)**4+.28
  box((x,0,z),(b-a-.008,.27,.07),'tile',.015)
 # 檐下木梁与椽子，避免屋顶只有薄片轮廓。
 for side in [-1,1]:
  box((0,side*(dep-.22),eave-.14),(w-.35,.16,.18),'wood',.025)
  for i in range(round(w/.30)):
   x=-half+.2+i*.30;beam((x,side*(dep-.65),eave-.14),(x,side*(dep-.06),eave-.09),.045,'wood',8)
 return ridge,eave+rise+.4

def window(x,y,z,w,h):
 box((x,y+.012,z),(w,.07,h),'paper',.008)
 for dx in [-w/2,w/2]:box((x+dx,y-.045,z),(.09,.14,h+.16),'wood',.016)
 for dz in [-h/2,h/2]:box((x,y-.045,z+dz),(w+.18,.16,.09),'wood',.016)
 for i in range(1,7):box((x-w/2+i*w/7,y-.08,z),(.033,.055,h),'wood',.006)
 for j in range(1,7):box((x,y-.085,z-h/2+j*h/7),(w,.055,.033),'wood',.006)
 box((x,y-.08,z-h/2-.10),(w+.26,.25,.12),'wood',.018)

def door(x,y,bottom,w,h,gate=False):
 for sg in [-1,1]:
  cx=x+sg*(w/4+.007);leaf=w/2-.014
  box((cx,y,bottom+h/2),(leaf,.16,h),'wood',.014)
  for i in range(5):box((cx-leaf/2+(i+.5)*leaf/5,y-.09,bottom+h/2),(leaf/5-.008,.035,h-.08),'wood1',.005)
  for dz in [.08,h-.08]:box((cx,y-.105,bottom+dz),(leaf-.035,.07,.10),'wood',.014)
  if not gate:
   window(cx,y-.11,bottom+h*.78,leaf*.67,h*.29)
   for dz in [h*.23,h*.46]:
    box((cx,y-.105,bottom+dz),(leaf*.78,.07,h*.18),'wood',.015)
    for dx in [-leaf*.33,leaf*.33]:box((cx+dx,y-.16,bottom+dz),(.026,.04,h*.13),'wood1',.006)
    for zz in [-h*.065,h*.065]:box((cx,y-.16,bottom+dz+zz),(leaf*.68,.04,.026),'wood1',.006)
  else:
   for dz in [.22,h-.24]:
    for i in range(5):ball((cx-leaf*.37+i*leaf*.185,y-.14,bottom+dz),(.025,.023,.025),'gold',12,8)
  hx=x+sg*.19;hz=bottom+h*.51;ball((hx,y-.14,hz+.07),(.055,.035,.07),'gold',16,10);torus((hx,y-.20,hz-.035),.105,.018,'gold','Y')
 for dx in [-w/2-.07,w/2+.07]:box((x+dx,y,bottom+h/2),(.12,.23,h+.08),'wood',.02)
 box((x,y,bottom+h+.05),(w+.3,.25,.13),'wood',.025)

def bracket(x,y,z,scale=1):
 box((x,y,z),(.34*scale,.38*scale,.17*scale),'wood',.025)
 for k in range(2):
  box((x,y-.07*k,z+.12+k*.12),((.42+k*.23)*scale,.18*scale,.12*scale),'wood1',.016)
  for sg in [-1,1]:beam((x+sg*.08,y,z+.06),(x+sg*(.28+k*.05),y,z+.25+k*.1),.055*scale,'wood',8)

def building(id,x,y,w,d,base,height,rise,side=False):
 group(id);transform(x,y,0,math.pi/2 if side else 0)
 foundation(0,-.13 if side else -.34,w+.65,d+(.85 if side else 1.25),base)
 box((0,0,base+height/2),(w,d,height),'plaster',.025)
 for sy in [-1,1]:
  for dx in [-w/2,-w/6,w/6,w/2] if not side else [-w/2,w/2]:
   py=sy*(d/2+(.20 if sy==-1 else -.16));lathe((dx,py,base),[(.17,.32),(.17,height-.20),(.20,height-.15)],'wood_lacquer',40)
   box((dx,py,base+.075),(.48,.47,.15),'stone',.035);lathe((dx,py,base+.15),[(.23,0),(.23,.07),(.19,.10),(.18,.20)],'stone',32)
   bracket(dx,py,base+height-.30,.75 if side else 1)
  box((0,sy*(d/2+.19),base+height-.06),(w+.3,.23,.24),'wood',.025)
  if sy==-1:
   if side:door(-w*.34,-d/2-.035,base+.04,w*.29,height*.84);window(w*.24,-d/2-.055,base+height*.55,w*.30,height*.53)
   else:
    door(0,-d/2-.035,base+.03,w*.25,height*.87)
    for dx in [-w*.335,w*.335]:window(dx,-d/2-.05,base+height*.52,w*.205,height*.53)
  else:
   for dx in [-w*.28,w*.28]:
    # 背窗本地旋转180度，窗框朝外。
    before={key:len(val[0]) for key,val in DATA.items()};window(dx,-d/2-.05,base+height*.54,w*.21,height*.5)
    a=DATA[geo.GROUP]
    for i in range(before[geo.GROUP],len(a[0])):
     # 数据已应用组变换；绕建筑中心翻转窗构件。
     p=Vector(a[0][i]);local=geo.TRANS.inverted()@p;local.x=2*dx-local.x;local.y=-local.y;a[0][i]=tuple(geo.TRANS@local)
 # 山墙外侧木立柱与窗；沿屋体两端补充完整细节。
 for sg in [-1,1]:
  xx=sg*w/2;box((xx,0,base+height-.22),(.17,d,.16),'wood',.018)
  for yy in [-d*.32,d*.32]:box((xx,yy,base+height/2),(.12,.11,height),'wood',.015)
 r,z=roof(w+1.05,d+1.05,base+height+.10,rise,not side)
 return r,z

def stairs(id,x,y,w,depth,h,n,angle=0):
 group(id);transform(x,y,0,angle)
 for j in range(n):
  z=G+(j+1)*h/n;cols=round(w/.46)
  for i in range(cols):box((-w/2+(i+.5)*w/cols,(j+.5)*depth/n,(G+z)/2),(w/cols-.008,depth/n-.006,z-G),'stone',.018)
 for sg in [-1,1]:
  x=sg*(w/2+.13);poly=[(x-.13,0),(x+.13,0),(x+.13,depth),(x-.13,depth)]
  v=[(px,py,G+.025) for px,py in poly]+[(px,py,G+.16+h*py/depth) for px,py in poly];f=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)];add(v,f,'stone')
  for j in range(3):
   a=depth*j/3;b=depth*(j+1)/3;beam((x-.13,a,G+.16+h*a/depth),(x+.13,a,G+.16+h*a/depth),.006,'mortar',6)

def wall(id,a,b):
 group(id);x,y=(a[0]+b[0])/2,(a[1]+b[1])/2;L=math.dist(a,b);transform(x,y,0,math.atan2(b[1]-a[1],b[0]-a[0]))
 box((0,0,G+.65),(L,.24,1.3),'plaster',.018)
 for j in range(2):
  n=round(L/.48)
  for i in range(n):box((-L/2+(i+.5)*L/n,0,G+.09+j*.17),(L/n-.008,.32,.165),'stone',.012)
 box((0,0,G+1.38),(L,.17,.13),'red',.025)
 n=round(L/.17)
 for i in range(n):
  x=-L/2+(i+.5)*L/n
  for sg in [-1,1]:
   # 墙帽半圆瓦沿两侧下斜。
   beam((x,0,G+1.31),(x,sg*.25,G+1.15),.074,random.choice(['tile','tile1']),12)

def post(id,x,y):
 group(id);transform(x,y)
 box((0,0,G+.11),(.63,.63,.22),'stone',.025)
 for j in range(6):
  for sg in [-1,1]:box((sg*.125,0,G+.22+(j+.5)*.20),(.244,.45,.194),'stone',.014)
 box((0,0,G+1.47),(.57,.57,.18),'stone',.03);box((0,0,G+1.35),(.49,.49,.10),'stone1',.015)
