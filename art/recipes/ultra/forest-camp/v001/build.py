"""森林营地：确定性真实网格、UV、导出和验收渲染。通过 Blender --background --python 执行。"""
import bpy, math, random, json, sys, bmesh
import numpy as np
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[5]
SRC=ROOT/'art/source/ultra/forest-camp/v001'
OUT=ROOT/'art/exports/ultra/forest-camp/v001'
PRE=ROOT/'art/previews/ultra/forest-camp/v001'
for p in (SRC,OUT,PRE):p.mkdir(parents=True,exist_ok=True)
random.seed(2236)
# 独立后台文件，不影响用户正在编辑的场景。
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
atlas=bpy.data.images.load(str(SRC/'textures/forest-atlas.png'))
M={}
def mat(name,color,quad=None):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.84
 if quad is not None:
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=atlas;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
 M[name]=m
 return m
mat('叶片',(.3,.6,.08),0);mat('树皮',(.3,.14,.05),1);mat('红布',(.45,.045,.035),2);mat('林地',(.2,.3,.04),3)
for n,c in [('浅木',(.55,.31,.10)),('泥土',(.24,.14,.065)),('苔石',(.29,.33,.20)),('绳',(.59,.45,.24)),('铁',(.18,.22,.22)),('衣',(.60,.55,.40)),('裤',(.17,.20,.15)),('肤',(.60,.34,.17)),('亮叶',(.33,.52,.045))]:mat(n,c)
details=bpy.data.images.load(str(SRC/'textures/detail-atlas.png'))
for name in ['浅木','泥土','苔石','衣']:
 nt=M[name].node_tree;t=nt.nodes.new('ShaderNodeTexImage');t.image=details;t.interpolation='Closest';nt.links.new(t.outputs['Color'],nt.nodes.get('Principled BSDF').inputs['Base Color'])
leaf=mat('叶簇',(.2,.5,.06));leaf.use_backface_culling=False
nt=leaf.node_tree;t=nt.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(SRC/'textures/leaf-cluster.png'));t.interpolation='Closest'
p=nt.nodes.get('Principled BSDF');nt.links.new(t.outputs['Color'],p.inputs['Base Color'])
clip=nt.nodes.new('ShaderNodeMath');clip.operation='GREATER_THAN';clip.inputs[1].default_value=.5;nt.links.new(t.outputs['Alpha'],clip.inputs[0]);nt.links.new(clip.outputs[0],p.inputs['Alpha'])
parts=[];group='地块'
def finish(o,name,material,quad=None,smooth=False):
 if quad is None:quad={'浅木':0,'泥土':1,'苔石':2,'衣':3}.get(material)
 o.name=name;o['模块']=group;o.data.materials.append(M[material]);parts.append(o)
 if smooth:
  for p in o.data.polygons:p.use_smooth=True
 if quad is not None:
  if not o.data.uv_layers:
   uv=o.data.uv_layers.new(name='UVMap')
   # 平面结构按最长轴投影，树冠由原语提供球形UV。
   for poly in o.data.polygons:
    axes=sorted(range(3),key=lambda a:abs(poly.normal[a]))[:2]
    for li in poly.loop_indices:
     co=o.data.vertices[o.data.loops[li].vertex_index].co
     uv.data[li].uv=(co[axes[0]]%1,co[axes[1]]%1)
  uv=o.data.uv_layers.active
  ox=.5 if quad in (1,3) else 0;oy=.5 if quad in (0,1) else 0
  for l in uv.data:l.uv=(ox+.015+l.uv.x*.47,oy+.015+l.uv.y*.47)
 return o
def mesh(name,v,f,material,quad=None,smooth=False):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o)
 return finish(o,name,material,quad,smooth)
def ico(name,loc,scale,material='叶片',quad=0):
 if material=='叶片':
  # 十个不同朝向的叶簇组成真实体积，合计20面，与原封闭低模同预算。
  vv=[];ff=[];phase=random.random()*math.tau
  for i in range(10):
   z=1-2*(i+.5)/10;a=i*2.39996+phase;rr=math.sqrt(1-z*z)
   normal=Vector((rr*math.cos(a),rr*math.sin(a),z));u=normal.cross(Vector((0,0,1))).normalized();w=normal.cross(u).normalized()
   center=Vector(loc)+Vector((normal.x*scale[0],normal.y*scale[1],normal.z*scale[2]))*.62
   ids=[]
   for sx,sy in [(-1,-1),(1,-1),(1,1),(-1,1)]:
    pt=center+(u*sx+w*sy)*max(scale)*.76;ids.append(len(vv));vv.append(tuple(pt))
   ff.append(tuple(ids))
  o=mesh(name,vv,ff,'叶簇');uv=o.data.uv_layers.new(name='UVMap')
  for f in o.data.polygons:
   for li,co in zip(f.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):uv.data[li].uv=co
  return o
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=1,location=loc)
 o=bpy.context.object;o.scale=scale
 return finish(o,name,material,quad,True)
def rod(name,a,b,r,material='树皮',r2=None,n=6,cap='浅木'):
 a,b=Vector(a),Vector(b);d=b-a
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if r2 is None else r2,depth=d.length,end_fill_type='NGON',location=(a+b)/2)
 o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y')
 finish(o,name,material,1 if material=='树皮' else None,True)
 if material=='树皮':
  o.data.materials.append(M[cap])
  for p in o.data.polygons:
   if abs(p.normal.z)>.99:
    p.material_index=1;p.use_smooth=False
    for li in p.loop_indices:
     co=o.data.vertices[o.data.loops[li].vertex_index].co
     o.data.uv_layers.active.data[li].uv=(.25+co.x/max(r,r2 or r)*.23,.75+co.y/max(r,r2 or r)*.23)
 return o
def box(name,loc,size,material='浅木',bevel=0):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.scale=size
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 finish(o,name,material,{'树皮':1,'红布':2}.get(material))
 if bevel:
  mod=o.modifiers.new('边缘倒角','BEVEL');mod.width=bevel;mod.segments=1
  bpy.ops.object.modifier_apply(modifier=mod.name)
 return o

# 六边形封闭土层：上下收边形成真实倒角；顶面为独立共享顶点面，无重合贴片。
N=18;v=[]
for radius,z in [(4.90,-.38),(5,-.30),(5,-.06),(4.94,0)]:
 for i in range(N):
  a=i*2*math.pi/N;r=radius*math.cos(math.pi/6)/math.cos((a%(math.pi/3))-math.pi/6)
  v.append((r*math.cos(a),r*math.sin(a),z))
f=[tuple(reversed(range(N)))]
for j in range(3):
 for i in range(N):f.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
v.append((0,0,0))
for i in range(N):f.append((4*N,3*N+i,3*N+(i+1)%N))
base=mesh('D01_倒角土层',v,f,'泥土');base.data.materials.append(M['林地'])
for p in base.data.polygons[-N:]:
 center=sum((base.data.vertices[j].co for j in p.vertices),Vector())/len(p.vertices)
 p.material_index=0 if center.x>.05 and center.y<.25 else 1
uv=base.data.uv_layers.active
for p in base.data.polygons:
 for li in p.loop_indices:
  c=base.data.vertices[base.data.loops[li].vertex_index].co;uv.data[li].uv=(.515+(c.x/10+.5)*.47,(.515 if p.material_index==0 else .015)+(c.y/10+.5)*.47)
  if abs(p.normal.z)<.95:
   tangent=Vector((-p.normal.y,p.normal.x,0)).normalized()
   u=(c.dot(tangent)+5)*.025+.535
   uv.data[li].uv=(u,.63+(c.z+.38)*.10)
# 作业地表嵌入连续表面，不叠加重合多边形；局部颜色使用小尺度土色砾石和落木。

group='树林'
positions=[(-2.3,2.6,2.6),(-.9,3.25,2.4),(.5,3.0,2.15),(1.9,2.6,2.4),(3.1,1.5,2.0),(-3.2,1.5,1.9),(-1.8,1.3,2),(-.3,1.8,2.1),(1,1.5,1.9),(2.2,.8,2.0),(-3.8,.0,1.9),(-2.5,-.1,2.2),(-1.1,.1,1.7),(.2,.3,2),(-3,-1.4,2.2),(-1.8,-1.3,2.25),(-.3,-1.2,1.65),(-2.3,-2.6,2.2),(-.9,-2.7,1.6),(-1.5,-3.5,1.5),(.5,-3.4,1.5),(3.6,-.1,1.6),(.8,2.45,1.8),(0,3.8,1.55),(-3.4,.8,1.5),(-.9,2.6,1.55),(.5,.9,1.65),(-3.5,-.8,1.5),(-2.5,3.2,1.5),(.0,-2.2,1.35)]
for k,(x,y,h) in enumerate(positions):
 pine=k%4==1
 rod(f'T{k+1:02}_树干',(x,y,0),(x+.05,y,h*.8),.10,h and '树皮',r2=.045,n=5)
 if pine:
  for j in range(3):
   z=h*(.27+j*.19);r=h*(.27-j*.043)
   bpy.ops.mesh.primitive_cone_add(vertices=6,radius1=r,radius2=.025,depth=h*.45,location=(x,y,z+h*.2))
   finish(bpy.context.object,f'T{k+1:02}_针叶层{j}','叶片',0,True)
 else:
  for j in range(3):
   a=j*2.399+k*.7;rr=h*.22 if j<2 else .05
   dx,dy=math.cos(a)*rr,math.sin(a)*rr;z=h*(.66 if j<2 else .95)
   if j<2:rod(f'T{k+1:02}_枝{j}',(x,y,h*.35),(x+dx,y+dy,z),.055,r2=.025,n=3)
   ico(f'T{k+1:02}_冠{j}',(x+dx,y+dy,z),(h*.30,h*.27,h*.30))

group='地被'
for k in range(24):
 a=k*2.399;r=random.uniform(1.3,4.3);x,y=r*math.cos(a),r*math.sin(a)
 if x>.2 and y<.5:continue
 ico(f'S_灌木{k}',(x,y,.26),(.48,.42,.38))
for k,(x,y,s) in enumerate([(-2.7,1.2,.5),(-1.6,1.9,.55),(-3.5,-.8,.3),(-2.2,-2.8,.5),(.9,-.25,.4),(3.5,-.1,.35),(.3,-3.3,.25),(-.6,-3.8,.3),(2.7,-2.7,.25)]):
 ico(f'R{k+1:02}_苔石',(x,y,s*.4),(s,s*.75,s*.75),'苔石',None)
for k in range(26):
 a=k*2.399;r=random.uniform(3.6,4.25);x,y=r*math.cos(a),r*math.sin(a)
 for j in range(2):
  b=a+j*1.5;dx,dy=math.cos(b),math.sin(b)
  mesh(f'G_叶{k}_{j}',[(x-dy*.06,y+dx*.06,0),(x+dy*.06,y-dx*.06,0),(x+dx*.25,y+dy*.25,.20),(x+dx*.11,y+dy*.11,.05)],[(0,1,2),(0,2,3)],'亮叶')

group='营地'
def shelter(name,x,y,w,d,h):
 for sx in (-1,1):
  for sy in (-1,1):
   px=x+sx*w/2;py=y+sy*d/2
   rod(name+'_柱',(px,py,0),(px,py,h+.12),.055,n=6)
   rod(name+'_绑绳',(px,py,h-.07),(px,py,h+.02),.067,'绳',n=6)
 for sy in (-1,1):rod(name+'_横梁',(x-w*.55,y+sy*d/2,h-.06),(x+w*.55,y+sy*d/2,h-.06),.055,n=6)
 for sx in (-1,1):
  rod(name+'_侧梁',(x+sx*w/2,y-d/2,h-.06),(x+sx*w/2,y+d/2,h-.06),.045,n=5)
  rod(name+'_斜撑',(x+sx*w/2,y+d/2,h*.55),(x+sx*w*.19,y+d/2,h-.09),.035,n=5)
 vv=[];ff=[];nx=4;ny=2
 for j in range(ny+1):
  for i in range(nx+1):
   u=i/nx;t=j/ny;zz=h-.18*math.sin(math.pi*u)+.025*math.sin(t*math.pi*4)
   vv.append((x+(u-.5)*(w+.16),y+(t-.5)*(d+.18),zz))
 for j in range(ny):
  for i in range(nx):
   q=j*(nx+1)+i;ff.append((q,q+1,q+nx+2,q+nx+1))
 roof=mesh(name+'_下垂棚布',vv,ff,'红布')
 uv=roof.data.uv_layers.new(name='UVMap')
 for p in roof.data.polygons:
  for li in p.loop_indices:
   idx=roof.data.loops[li].vertex_index;uv.data[li].uv=(.015+idx%(nx+1)/nx*.47,.015+(idx//(nx+1))/ny*.47)
 bpy.context.view_layer.objects.active=roof;roof.select_set(True)
 mod=roof.modifiers.new('布料厚度','SOLIDIFY');mod.thickness=.018;bpy.ops.object.modifier_apply(modifier=mod.name);roof.select_set(False)
 for i in range(3):
  box(name+'_木箱',(x-w*.28+i*w*.27,y+.05,.19),(.30,.32,.36),'树皮',0)
 for i in range(2):rod(name+'_储木',(x-w*.3,y-.24,.43+i*.13),(x+w*.30,y-.24,.43+i*.13),.075,n=6)
shelter('B01',2.15,.05,1.25,.85,1.05)
shelter('B02',3.20,-1.10,1.05,.65,.77)
rod('P01_旗杆',(1.6,.36,0),(1.6,.36,1.98),.03,n=6)
flag=mesh('P01_红旗',[(1.63,.36,1.90),(1.95,.40,1.87),(2.16,.31,1.82),(2.10,.31,1.53),(1.92,.40,1.59),(1.63,.36,1.61)],[(0,1,4,5),(1,2,3,4)],'红布',2)
bpy.context.view_layer.objects.active=flag;mod=flag.modifiers.new('旗帜厚度','SOLIDIFY');mod.thickness=.012;bpy.ops.object.modifier_apply(modifier=mod.name)
for i in range(7):
 x=.45;y=-2.5+i*.45;rod('P02_围栏柱',(x,y,0),(x,y,.43),.035,n=5)
 if i<6:
  for z in (.18,.34):rod('P02_围栏杆',(x,y,z),(x,y+.45,z),.024,n=5)
def barrel(x,y):
 n=8;vv=[];ff=[]
 for z,r in [(0,.15),(.05,.17),(.22,.195),(.40,.17),(.44,.15)]:
  for i in range(n):vv.append((x+r*math.cos(i*math.tau/n),y+r*math.sin(i*math.tau/n),z))
 ff=[tuple(reversed(range(n)))]
 for j in range(4):
  for i in range(n):ff.append((j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i))
 ff.append(tuple(range(4*n,5*n)));o=mesh('P04_封闭木桶',vv,ff,'树皮',1)
 for z in (.07,.35):rod('桶箍',(x,y,z),(x,y,z+.035),.183,'铁',n=8)
barrel(1.03,-.27);barrel(3.35,-2.0)
for j,count in enumerate([4,3,2]):
 for i in range(count):
  x=1.0+i*.25+j*.125;z=.13+j*.22
  rod('P12_原木堆',(x,-3.05,z),(x+.1,-2.0,z),.123,n=6)
for x,y in [(.5,-3.25),(2.75,-2.65),(.85,-1.4)]:
 rod('ST_树桩',(x,y,0),(x,y,.32),.18,r2=.145,n=7)
 for a in range(3):rod('ST_外张根',(x,y,.10),(x+math.cos(a*2.1)*.29,y+math.sin(a*2.1)*.29,.02),.075,r2=.035,n=5)
for y in [-1.35,-2.05]:
 for sx in [-1,1]:rod('P10_锯马腿',(.85+sx*.24,y,0),(.85-sx*.16,y,.55),.043,n=5)
 rod('P10_顶梁',(.56,y,.47),(1.12,y,.47),.045,n=5)
rod('P09_锯切原木',(.85,-2.4,.63),(.85,-1,.63),.12,n=8)

group='人物'
def worker(name,x,y,axe=False):
 # 静态作业姿态，肩肘膝与握持点显式设置；本版本不宣称动画拓扑。
 for s in (-1,1):
  foot=(x+s*.13,y+(.10 if s==1 else -.13),.08);knee=(x+s*.14,y-.02,.31);hip=(x+s*.09,y,.50)
  box(name+'_靴',foot,(.13,.24,.14),'裤',0)
  rod(name+'_小腿',foot,knee,.065,'裤',n=6);rod(name+'_大腿',knee,hip,.085,'裤',n=6)
 ico(name+'_衣',(x,y-.04,.67),(.19,.13,.25),'衣',None)
 rod(name+'_腰带',(x,y,.49),(x,y,.55),.15,'红布',n=7)
 ico(name+'_头',(x,y-.13,.94),(.105,.10,.13),'肤',None)
 ico(name+'_头巾',(x,y-.12,1.02),(.115,.105,.065),'红布',2)
 for s in (-1,1):
  shoulder=(x+s*.15,y-.05,.78);elbow=(x+s*.20,y-.23,.66);hand=(x+s*.095,y-.39,.64 if not axe else .73)
  rod(name+'_袖',shoulder,elbow,.072,'衣',n=6);rod(name+'_臂',elbow,hand,.042,'肤',n=5)
  ico(name+'_手',hand,(.05,.05,.05),'肤',None)
 if axe:
  rod('P07_斧柄',(x,y-.42,.80),(x,y-.66,.35),.024,n=5)
  box('P07_斧头',(x+.03,y-.66,.38),(.20,.055,.13),'铁',.01)
 else:box('P06_锯片',(x-.19,y-.39,.63),(.55,.025,.08),'铁')
worker('C01',1.28,-1.5);worker('C02',2.65,-1.85,True)

# 应用变换、法线统一与工程审计。
for o in parts:
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
 if not o.data.uv_layers:o.data.uv_layers.new(name='UVMap')
def counts(objects):
 total=0;groups={};bad=[];open_solids=[];duplicates=0;seen=set()
 for o in objects:
  o.data.calc_loop_triangles();n=len(o.data.loop_triangles);total+=n;groups[o['模块']]=groups.get(o['模块'],0)+n
  if any(p.area<1e-10 for p in o.data.polygons):bad.append(o.name)
  bm=bmesh.new();bm.from_mesh(o.data)
  if o.data.materials[0].name not in ['叶簇','亮叶'] and any(not e.is_manifold for e in bm.edges):open_solids.append(o.name)
  bm.free()
  for tri in o.data.loop_triangles:
   key=tuple(sorted(tuple(round(c,6) for c in o.matrix_world@o.data.vertices[i].co) for i in tri.vertices))
   if key in seen:duplicates+=1
   seen.add(key)
 return {'三角形':total,'模块三角形':groups,'退化面对象':bad,'实体非流形对象':open_solids,'完全重复三角形':duplicates,'对象数':len(objects)}
report=counts(parts)
(OUT/'audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('AUDIT',report,flush=True)
assert report['三角形']<=6000,report
assert not report['退化面对象'] and not report['实体非流形对象'] and not report['完全重复三角形'],report

# 导出米制，半径39.5mm，与既有地块一致。
factor=.0395/5
for o in parts:
 o.location*=factor
 for v in o.data.vertices:v.co*=factor
 if o.get('模块')!='地块':
  o.location.x*=.82;o.location.y*=.82
  for v in o.data.vertices:v.co.x*=.82;v.co.y*=.82
bpy.context.view_layer.update()
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=base
bpy.ops.export_scene.fbx(filepath=str(OUT/'forest-camp.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'forest-camp.glb'),use_selection=True,export_format='GLB')
# 低细节导出保留闭合结构；叶簇按空间分布减少一半。
lod=[]
for original in parts:
 o=original.copy();o.data=original.data.copy();scene.collection.objects.link(o);lod.append(o)
 if o.data.materials[0].name=='叶簇':
  bm=bmesh.new();bm.from_mesh(o.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[f for i,f in enumerate(bm.faces) if i%2==1],context='FACES');bm.to_mesh(o.data);bm.free()
 elif len(o.data.polygons)>8:
  bpy.context.view_layer.objects.active=o;m=o.modifiers.new('远景减面','DECIMATE');m.ratio=.65;bpy.ops.object.modifier_apply(modifier=m.name)
bpy.ops.object.select_all(action='DESELECT')
for o in lod:o.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'forest-camp-lod1.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='COPY',embed_textures=True)
for o in lod:o.data.calc_loop_triangles()
report['LOD1三角形']=sum(len(o.data.loop_triangles) for o in lod)
(OUT/'audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
for o in lod:bpy.data.objects.remove(o,do_unlink=True)

# 同一份几何用于棚拍，灯光不计入游戏面数。
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True;scene.cycles.transparent_max_bounces=48
scene.world=bpy.data.worlds.new('暖灰环境');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.48,.52,.58,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
for name,loc,power,size in [('主光',(-.06,-.07,.13),.24,.08),('补光',(.08,-.01,.07),.07,.07),('轮廓光',(.01,.08,.12),.15,.06)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('验收相机');cam=bpy.data.objects.new('验收相机',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.clip_start=.0001;camdata.clip_end=10
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=False
scene.view_settings.view_transform='AgX'
def view(name,loc,target,scale):
 cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=scale;scene.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
cam.location=(.066,-.105,.105);cam.rotation_euler=(Vector((0,0,.005))-cam.location).to_track_quat('-Z','Y').to_euler();camdata.ortho_scale=.101
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'forest-camp.blend'))
views=[('hero',(.055,-.10,.105),(0,0,.004),.101),('reverse',(-.07,.10,.08),(0,0,.005),.101),('front',(0,-.14,.013),(0,0,.013),.089),('right',(.14,0,.013),(0,0,.013),.089),('back',(0,.14,.013),(0,0,.013),.089),('top',(0,0,.15),(0,0,0),.089),('camp',(.047,-.067,.052),(.015,-.012,.004),.046)]
for args in views:view(*args)
