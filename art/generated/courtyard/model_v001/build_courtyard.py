"""庭院低面数资产：通过 Blender MCP 执行，不修改原场景。尺寸为美术估算米制。"""
import bpy, math, json, bmesh
from mathutils import Vector
from pathlib import Path
ROOT=Path(__file__).resolve().parent
S=bpy.data.scenes.new('三国庭院_优化');bpy.context.window.scene=S
S.unit_settings.system='METRIC'
C=bpy.data.collections.new('庭院组件');S.collection.children.link(C)
assets={}; current='A42'; mats={}
def mat(name,color,rough=.6,metal=0,tex=None):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal;p.inputs['Specular IOR Level'].default_value=.25
 if tex:
  for suf in ['basecolor','normal']:
   t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(ROOT/'textures'/f'{tex}_{suf}.png'));t.interpolation='Closest';t.extension='REPEAT'
   if suf=='basecolor':m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
   else:
    t.image.colorspace_settings.name='Non-Color';n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35;m.node_tree.links.new(t.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],p.inputs['Normal'])
 mats[name]=m;return m
mat('朱漆',(.72,.008,.012),.24);mat('木材',(.3,.07,.018),.55,tex='wood');mat('灰瓦',(.05,.065,.075),.4,tex='roof');mat('石材',(.5,.51,.52),.85,tex='stone');mat('素石',(.53,.56,.59),.8);mat('白墙',(.85,.75,.56),.9,tex='plaster');mat('鎏金',(.68,.32,.055),.3,.7);mat('窗纸',(.14,.23,.26),.9);mat('陶土',(.4,.075,.018),.28);mat('水',(.045,.27,.34),.18,.15);mat('灯笼', (1,.42,.035),.4);mat('草捆',(.55,.32,.095),.9,tex='wood');mat('暗色',(.025,.022,.02),.95)
mat('门板',(.3,.07,.02),.55,tex='door');mat('窗格',(.3,.07,.02),.6,tex='window')
def obj(name,v,f,ma,uv=None):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(current+'_'+name,me);C.objects.link(o);me.materials.append(mats[ma]);assets.setdefault(current,[]).append(o)
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 layer=me.uv_layers.new(name='UVMap')
 for p in me.polygons:
  axis=max(range(3),key=lambda k:abs(p.normal[k]));ids=([1,2] if axis==0 else [0,2] if axis==1 else [0,1]);scale=1.6 if ma in ['石材','灰瓦'] else 2
  for li in p.loop_indices:
   co=me.vertices[me.loops[li].vertex_index].co;layer.data[li].uv=(co[ids[0]]/scale,co[ids[1]]/scale)
 return o
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
def bevel(o,w=.025):
 bm=bmesh.new();bm.from_mesh(o.data)
 edges=[e for e in bm.edges if abs((e.verts[1].co-e.verts[0].co).normalized().z)>.9]
 bmesh.ops.bevel(bm,geom=edges,offset=w,segments=1,affect='EDGES');bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free();return o
def box(name,pos,size,ma,b=0):
 x,y,z=pos;a,c,d=[n/2 for n in size];v=[(x+i*a,y+j*c,z+k*d) for i,j,k in [(-1,-1,-1),(-1,-1,1),(-1,1,-1),(-1,1,1),(1,-1,-1),(1,-1,1),(1,1,-1),(1,1,1)]]
 o=obj(name,v,[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)],ma)
 return bevel(o,b) if b else o
def lathe(name,x,y,profile,ma,n=12):
 n=min(n,8) if n<32 else 32
 v=[(x+r*math.cos(i*2*math.pi/n),y+r*math.sin(i*2*math.pi/n),z) for r,z in profile for i in range(n)];f=[]
 for j in range(len(profile)-1):
  for i in range(n):f.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 f.extend([tuple(range(n-1,-1,-1)),tuple((len(profile)-1)*n+i for i in range(n))]);o=obj(name,v,f,ma)
 for p in o.data.polygons:p.use_smooth=len(p.vertices)==4
 return o
def cyl(name,pos,r,h,ma,n=10):return lathe(name,pos[0],pos[1],[(r,pos[2]-h/2),(r,pos[2]+h/2)],ma,n)
def beam(name,a,b,r,ma,n=4):
 a,b=Vector(a),Vector(b);d=b-a;u=d.cross(Vector((0,0,1)))
 if u.length<.001:u=d.cross(Vector((0,1,0)))
 u.normalize();v=d.normalized().cross(u);verts=[tuple(c+r*(u*math.cos(i*2*math.pi/n)+v*math.sin(i*2*math.pi/n))) for c in (a,b) for i in range(n)]
 return obj(name,verts,[tuple(range(n-1,-1,-1)),tuple(n+i for i in range(n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],ma)
def rail(name,points,r,ma):
 points=[Vector(p) for p in points];v=[]
 for j,p in enumerate(points):
  tangent=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized();u=tangent.cross(Vector((0,0,1)))
  if u.length<.001:u=Vector((1,0,0))
  u.normalize();w=tangent.cross(u)
  v.extend([tuple(p+r*(u*math.cos(i*math.pi/2)+w*math.sin(i*math.pi/2))) for i in range(4)])
 f=[(0,3,2,1),tuple((len(points)-1)*4+i for i in range(4))]+[(j*4+i,j*4+(i+1)%4,(j+1)*4+(i+1)%4,(j+1)*4+i) for j in range(len(points)-1) for i in range(4)]
 return obj(name,v,f,ma)
def ring(name,x,y,z,r):
 v=[(x+(r+.014*math.cos(j*2*math.pi/3))*math.cos(i*math.pi/3),y+.014*math.sin(j*2*math.pi/3),z+(r+.014*math.cos(j*2*math.pi/3))*math.sin(i*math.pi/3)) for i in range(6) for j in range(3)]
 return obj(name,v,[(i*3+j,((i+1)%6)*3+j,((i+1)%6)*3+(j+1)%3,i*3+(j+1)%3) for i in range(6) for j in range(3)],'鎏金')
def ell(name,pos,scale,ma,n=8,rings=3):
 n=min(n,6);rings=min(rings,3);x,y,z=pos
 v=[(x,y,z-scale[2])]+[(x+scale[0]*math.sin(math.pi*j/rings)*math.cos(i*2*math.pi/n),y+scale[1]*math.sin(math.pi*j/rings)*math.sin(i*2*math.pi/n),z-scale[2]*math.cos(math.pi*j/rings)) for j in range(1,rings) for i in range(n)]+[(x,y,z+scale[2])]
 f=[(0,1+(i+1)%n,1+i) for i in range(n)]+[(1+i,1+(i+1)%n,1+n+(i+1)%n,1+n+i) for i in range(n)]+[(len(v)-1,1+n+i,1+n+(i+1)%n) for i in range(n)]
 o=obj(name,v,f,ma)
 for p in o.data.polygons:p.use_smooth=True
 return o
def roof(name,x,y,w,d,eave,rise,hip=False):
 h=w/2;dep=d/2;ridge=h-(.8 if hip else .12)
 rings=[(ridge,0,eave+rise),(h-.25,dep*.38,eave+rise*.50),(h-.12,dep*.78,eave+.12),(h,dep,eave+.08)]
 v=[];f=[]
 for side in [-1,1]:
  offset=len(v)
  for a,b,z in rings:
   for t in [-1,0,1]:v.append((x+t*a,y+side*b,z+.18*abs(t)**4))
  f.extend([(offset+j*3+i,offset+j*3+i+1,offset+(j+1)*3+i+1,offset+(j+1)*3+i) for j in range(3) for i in range(2)])
  for edge in [-1,1]:
   pts=[(x+edge*a,y+side*b,z+.20) for a,b,z in rings]
   rail('红色垂脊',pts,.095,'朱漆')
   ell('金色檐帽',pts[-1],(.10,.10,.10),'鎏金',4,3)
  pts=[(x+t*h,y+side*dep,eave+.08+.18*abs(t)**4) for t in [-1,0,1]]
  rail('红色檐边',pts,.065,'朱漆')
 # 两侧采用连续扇面补全；底面封闭，不用相互交叠的坡面。
 for col in [0,2]:
  boundary=[col,col+3,col+6,col+9,col+21,col+18,col+15,col+12]
  center=len(v);v.append((x+(-1 if col==0 else 1)*h,y,eave+.26))
  for i in range(len(boundary)-1):f.append((center,boundary[i],boundary[i+1]))
 # 屋底边界：前檐、右侧中点、后檐、左侧中点。
 perimeter=[9,10,11,25,23,22,21,24]
 bottom=[]
 for i in perimeter:
  bottom.append(len(v));p=v[i];v.append((p[0],p[1],eave-.08))
 for j,i in enumerate(perimeter):f.append((i,perimeter[(j+1)%8],bottom[(j+1)%8],bottom[j]))
 f.append(tuple(reversed(bottom)))
 o=obj(name,v,f,'灰瓦');bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00001);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 for p in o.data.polygons:p.use_smooth=True
 o.data.set_sharp_from_angle(angle=math.radians(38))
 # 所有瓦片沿坡面朝下，避免侧坡出现斜向贴图撕裂。
 for p in o.data.polygons:
  sideface=abs(p.normal.x)>.45 and abs(p.center.x-x)>ridge*.8
  for li in p.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co;o.data.uv_layers.active.data[li].uv=((co.y-y)/1.6 if sideface else (co.x-x)/1.6,-(abs(co.x-x)-ridge)/1.6 if sideface else -abs(co.y-y)*math.sqrt(dep*dep+rise*rise)/dep/1.6)
 rail('正脊',[(x+t*ridge,y,eave+rise+.18*abs(t)) for t in [-1,0,1]],.12,'朱漆')
def panel(x,y,z,w,h,door=False):
 o=box('闭合门窗',(x,y,z),(w,.10,h),'门板' if door else '窗格')
 for p in o.data.polygons:
  for li in p.loop_indices:
   co=o.data.vertices[o.data.loops[li].vertex_index].co;o.data.uv_layers.active.data[li].uv=((co.x-x)/w+.5,(co.z-z)/h+.5)
 for dz in [-h/2,h/2]:box('门窗横框',(x,y-.07,z+dz),(w+.15,.14,.065),'木材')
 if door:
  for dx in [-w*.16,w*.16]:
   ring('门环',x+dx,y-.13,z,.085)
def hall(id,x,y,w,d,base,height,roofrise,side=False):
 global current;current=id;start=len(C.objects)
 box('台基',(x,y,.6+base/2),(w+.35,d+.35,base),'石材',.02)
 z=.6+base;box('封闭墙体',(x,y,z+height/2),(w,d,height),'白墙')
 for sy in [-1,1]:
  for xx in ([-w/2,-w/6,w/6,w/2] if sy==-1 else [-w/2,w/2]):
   cyl('木柱',(x+xx,y+sy*(d/2+.04),z+height/2),.13,height,'木材',6);box('柱础',(x+xx,y+sy*(d/2+.04),z+.1),(.36,.38,.2),'素石')
  box('横梁',(x,y+sy*(d/2+.07),z+height-.08),(w+.2,.2,.2),'木材')
  if sy==-1:
   panel(x-w*.22 if side else x,y-d/2-.065,z+height*.45,w*.27 if side else w*.25,height*.86,True)
   for dx in ([w*.23] if side else [-w*.33,w*.33]):panel(x+dx,y-d/2-.07,z+height*.56,w*.32 if side else w*.22,height*.5)
  else:
   for dx in [-w*.28,w*.28]:panel(x+dx,y+d/2+.12,z+height*.55,w*.19,height*.47)
 roof('屋顶',x,y,w+.9,d+1.0,z+height,roofrise,not side)
 # 山墙三角闭合结构
 for xx in [-w/2,w/2]:
  v=[(x+xx-.03,y-d/2,z+height),(x+xx-.03,y+d/2,z+height),(x+xx-.03,y,z+height+roofrise*(.88 if side else .35)),(x+xx+.03,y-d/2,z+height),(x+xx+.03,y+d/2,z+height),(x+xx+.03,y,z+height+roofrise*(.88 if side else .35))]
  obj('山墙',v,[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],'白墙')
 if side:
  # 厢房朝向院内，围绕建筑中心旋转90度。
  for o in assets[id]:
   for v in o.data.vertices:
    a,b=v.co.x-x,v.co.y-y;v.co.x=x-b;v.co.y=y+a
current='A42';lathe('红色圆底座',0,0,[(7.15,0),(7.25,.06),(7.25,.44),(7.15,.5)],'朱漆',48)
current='T01';lathe('圆形铺砖',0,0,[(7.05,.5),(7.05,.6)],'石材',48)
hall('A01',.3,2.35,7,3.2,.65,2.45,1.55)
hall('A02',-3.85,-.65,3.35,1.65,.28,1.85,.85,True)
def steps(id,x,y,width,depth,height,n):
 global current;current=id
 for i in range(n):box('踏步',(x,y+(i+.5)*depth/n,.6+height*(i+1)/n/2),(width,depth/n,height*(i+1)/n),'石材')
 for dx in [-width/2-.11,width/2+.11]:beam('斜石扶边',(x+dx,y,.68),(x+dx,y+depth,.6+height+.08),.13,'素石',4)
steps('A04',.3,-.6,2.65,1.15,.65,5)
steps('A06',0,-5.5,2.0,1.0,.4,4)
steps('A05',-2.2,-1.387,1.2,.65,.28,2)
for o in assets['A05']:
 for v in o.data.vertices:
  a,b=v.co.x+2.2,v.co.y+1.387;v.co.x=-2.2-b;v.co.y=-1.387+a
current='A03';box('门槛',(0,-4.35,.8),(2.45,.48,.4),'石材',.02)
for x in [-1.1,1.1]:
 cyl('门柱',(x,-4.35,1.85),.14,2.1,'木材');box('门柱础',(x,-4.35,.93),(.4,.45,.26),'素石')
panel(0,-4.35,1.83,1.85,1.85,True);box('门楼横梁',(0,-4.35,2.85),(2.6,.4,.24),'木材');roof('门楼',0,-4.35,3,1.35,2.98,.65)
def wall(id,a,b):
 global current;current=id;a,b=Vector(a),Vector(b);mid=(a+b)/2;L=(b-a).length;ang=math.atan2(b.y-a.y,b.x-a.x)
 for z,h,w,ma in [(.74,.28,.35,'石材'),(1.38,1,.25,'白墙'),(1.96,.18,.5,'灰瓦'),(2.08,.09,.16,'朱漆')]:
  o=box('院墙',(0,0,z),(L,w,h),ma)
  for v in o.data.vertices:xx,yy=v.co.x,v.co.y;v.co.x=mid.x+xx*math.cos(ang)-yy*math.sin(ang);v.co.y=mid.y+xx*math.sin(ang)+yy*math.cos(ang)
wall('A07',(-4.8,-4.35),(-1.25,-4.35));wall('A08',(1.25,-4.35),(4.8,-4.35));wall('A09',(-4.8,-4.35),(-4.8,-2.35));wall('A10',(4.8,-4.35),(4.8,4.3));wall('A11',(-4.8,4.3),(4.8,4.3));wall('A09',(-4.8,.85),(-4.8,4.3))
current='A09';box('后墙转角收口',(-4.8,4.3,2.205),(.59,.59,.16),'素石',.025)
for id,x,y in [('A12',-4.8,-4.35),('A13',-4.8,-2.35),('A14',4.8,-4.35),('A15',4.8,4.3)]:
 current=id;box('墙柱',(x,y,1.4),(.45,.45,1.6),'石材');box('柱顶',(x,y,2.22),(.59,.59,.18),'素石',.025);box('柱脚',(x,y,.72),(.59,.59,.24),'素石',.025)
for id,x in [('A16',-1.55),('A17',1.55)]:
 current=id;box('门外石墩',(x,-5.15,.98),(.55,.6,.72),'石材',.02);box('墩盖',(x,-5.15,1.34),(.64,.68,.12),'素石',.025)
for id,x in [('A18',-1.85),('A19',2.45)]:
 current=id;y=.35;z=1.25;box('狮座',(x,y,z+.08),(.72,.67,.16),'素石',.025)
 ell('狮身',(x,y+.04,z+.43),(.25,.25,.33),'素石',8,4);ell('鬃毛',(x,y-.10,z+.72),(.27,.22,.26),'素石',10,4);ell('狮面',(x,y-.26,z+.73),(.18,.12,.16),'素石',8,3);ell('鼻',(x,y-.36,z+.76),(.065,.045,.045),'暗色',6,3)
 for dx in [-.18,.18]:
  ell('后腿',(x+dx,y+.1,z+.3),(.16,.18,.19),'素石',8,3);beam('前腿',(x+dx,y-.18,z+.19),(x+dx,y-.16,z+.49),.075,'素石');ell('爪',(x+dx,y-.25,z+.19),(.11,.15,.07),'素石',8,3);ell('耳',(x+dx*.85,y-.08,z+.91),(.075,.06,.08),'素石',6,3)
 for dx in [-.085,.085]:ell('眼',(x+dx,y-.365,z+.81),(.027,.02,.022),'暗色',6,3)
 beam('尾',(x,y+.24,z+.28),(x+.13,y+.27,z+.56),.055,'素石')
for id,x in [('A20',-1.42),('A21',1.42)]:
 current=id;beam('挂杆',(x,-4.35,2.8),(x,-4.65,2.65),.035,'鎏金');lathe('灯笼',x,-4.65,[(.06,2.15),(.20,2.25),(.24,2.43),(.17,2.59),(.06,2.65)],'灯笼',10);cyl('顶盖',(x,-4.65,2.65),.09,.08,'鎏金');cyl('流苏',(x,-4.65,2.05),.045,.18,'朱漆',6)
current='A22';lathe('陶罐',-2.52,.2,[(.22,.6),(.33,.72),(.37,1.02),(.23,1.3),(.22,1.37),(.17,1.37),(.16,1.25),(.26,1.02),(.14,.69)],'陶土',12)
current='A23';lathe('水缸',-4,-3.2,[(.28,.6),(.43,.78),(.46,1.13),(.42,1.22),(.34,1.22),(.34,1.1),(.32,.74)],'石材',14);cyl('水面',(-4,-3.2,1.08),.34,.025,'水',14)
current='A24';box('木箱',(-2.5,-.4,.86),(.7,.52,.52),'木材',.03)
for x in [-2.77,-2.23]:box('包边',(x,-.4,.86),(.055,.54,.54),'鎏金')
box('锁扣',(-2.5,-.68,.87),(.08,.035,.15),'鎏金')
current='A25';lathe('石桌',2.65,-1.55,[(.28,.6),(.15,.76),(.15,1.08),(.49,1.12),(.49,1.24)],'素石',16)
for id,x,y in [('A26',2,-1.55),('A27',2.65,-2.25),('A28',3.3,-1.55),('A29',2.65,-.9)]:
 current=id;lathe('石凳',x,y,[(.19,.6),(.15,.72),(.16,.94),(.22,1.02)],'素石',10)
for id,y in [('A30',2.15),('A31',2.85),('A32',3.55)]:
 current=id;box('草捆',(4.1,y,.94),(.66,.62,.68),'草捆',.035)
 for dx in [-.19,.19]:box('绑绳',(4.1+dx,y,.94),(.035,.635,.695),'鎏金')
current='A33';lathe('井圈',3.25,-3.1,[(.48,.6),(.48,1.04),(.34,1.04),(.34,.62)],'石材',16);cyl('井底',(3.25,-3.1,.635),.335,.015,'暗色',16)
current='A34'
for x in [2.71,3.79]:box('井架',(x,-3.1,1.1),(.10,.16,1),'木材');beam('斜撑',(x,-3.45,.63),(x,-3.1,1.15),.045,'木材')
beam('辘轳轴',(2.65,-3.1,1.48),(3.92,-3.1,1.48),.07,'木材',8);beam('绕绳',(3.07,-3.1,1.48),(3.43,-3.1,1.48),.10,'鎏金',10);beam('垂绳',(3.25,-3.1,1.42),(3.25,-3.1,.74),.018,'草捆',5)
current='A35';lathe('木桶',3.98,-3.1,[(.14,.6),(.19,.94),(.15,.94),(.11,.65)],'木材',10)
current='A36';cyl('旗杆',(4.8,-4.35,3.43),.065,2.5,'木材',10)
for z in [2.3,3.1,4.48]:cyl('旗箍',(4.8,-4.35,z),.085,.08,'鎏金',10)
ell('旗顶',(4.8,-4.35,4.73),(.095,.095,.11),'鎏金',8,3)
current='A37';vv=[]
for j in range(4):
 for i in range(7):
  t=i/6;vv.append((4.84+1.55*t,-4.35+.20*math.sin(t*9-j*.6)*t,4.48-j*.36-.15*t+.08*math.sin(t*5+j)))
o=obj('旗面',vv,[(j*7+i,j*7+i+1,(j+1)*7+i+1,(j+1)*7+i) for j in range(3) for i in range(6)],'朱漆');active(o);m=o.modifiers.new('布料厚度','SOLIDIFY');m.thickness=.018;bpy.ops.object.modifier_apply(modifier=m.name)
for p in o.data.polygons:p.use_smooth=True
for id,x,y,z,sc in [('A38',-2.9,2.35,5.45,.65),('A39',3.5,2.35,5.45,.65),('A40',-1.35,-4.35,3.8,.4),('A41',1.35,-4.35,3.8,.4)]:
 current=id;sgn=-1 if x<0 else 1
 ell('脊兽身',(x,y,z),(.20*sc,.15*sc,.22*sc),'鎏金',8,3);ell('兽首',(x+sgn*.15*sc,y,z+.16*sc),(.19*sc,.12*sc,.12*sc),'鎏金',8,3)
 pts=[(x-sgn*.1*sc,y,z),(x-sgn*.25*sc,y,z+.4*sc),(x-sgn*.2*sc,y,z+.75*sc)]
 for a,b in zip(pts,pts[1:]):beam('卷尾',a,b,.085*sc,'鎏金',4)
# 固定渲染设置及相机；灯光不计入游戏网格。
S.world=bpy.data.worlds.new('摄影棚');S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs[0].default_value=(.55,.59,.67,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.45
for name,loc,power,size in [('主光',(-6,-8,12),1800,8),('补光',(8,-1,8),1200,7),('轮廓',(1,8,11),1900,6)]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;o=bpy.data.objects.new(name,d);S.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,2))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('预览相机');cam=bpy.data.objects.new('预览相机',d);S.collection.objects.link(cam);S.camera=cam;d.type='ORTHO';d.ortho_scale=16.5;cam.location=(8,-17,16);cam.rotation_euler=(Vector((0,0,1.8))-cam.location).to_track_quat('-Z','Y').to_euler()
S.render.engine='CYCLES';S.cycles.samples=32;S.cycles.use_denoising=True;S.render.resolution_x=1200;S.render.resolution_y=1200;S.render.resolution_percentage=100;S.render.image_settings.file_format='PNG';S.render.film_transparent=False
S.view_settings.view_transform='AgX'
# 统计实际三角面，保留独立组件。
counts={}
for key,objects in assets.items():
 counts[key]=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects)
(ROOT/'mesh_stats.json').write_text(json.dumps({'三角面总数':sum(counts.values()),'各组件':counts},ensure_ascii=False,indent=2))
bpy.data.libraries.write(str(ROOT/'courtyard.blend'),{S},fake_user=True,compress=True)
result={'tris':sum(counts.values()),'components':counts,'objects':len(C.objects)}
