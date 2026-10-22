"""森林重制：通过 Blender MCP 执行；STAGE=gray/detail，渲染由 render.py 分步执行。"""
import bpy, math, random, json, bmesh
import numpy as np
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[5]
SRC=ROOT/'art/source/ultra/forest-camp/v002'
OUT=ROOT/'art/exports/ultra/forest-camp/v002'
PRE=ROOT/'art/previews/ultra/forest-camp/v002'
STAGE=globals().get('STAGE','gray')
for p in (SRC,OUT,PRE,SRC/'textures'):p.mkdir(parents=True,exist_ok=True)
random.seed(223602)
# 新建场景；不清空当前文件中的其他场景。
scene=bpy.data.scenes.new('森林重制_'+STAGE)
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
parts=[];group='地块';M={}
palette={'树皮':(.24,.125,.055),'浅木':(.64,.39,.14),'红布':(.48,.055,.045),'林地':(.12,.23,.035),'泥土':(.28,.17,.065),'苔石':(.30,.34,.24),'绳':(.66,.51,.28),'铁':(.15,.19,.18),'衣':(.58,.55,.42),'裤':(.12,.16,.115),'肤':(.52,.31,.17),'亮叶':(.33,.55,.035),'叶片':(.11,.34,.035),'深叶':(.035,.20,.10),'嫩叶':(.24,.46,.035),'灰模':(.45,.45,.45)}
# 数学定义的本色和微高度，非照片投影，不含灯光或阴影。
size=1024;tile=size//4;pixels=np.ones((size,size,4),dtype=np.float32);heights=np.zeros((size,size),dtype=np.float32)
yy,xx=np.mgrid[0:tile,0:tile]/tile
for i,(name,color) in enumerate(palette.items()):
 ox=i%4*tile;oy=i//4*tile
 noise=(np.sin(xx*151+np.sin(yy*43))*np.sin(yy*137+xx*17)+np.sin(xx*381+yy*231))*.025
 h=noise.copy()
 if name=='树皮':h+=.16*np.sin(xx*85+np.sin(yy*16)*1.8)+.06*np.sin(xx*220+yy*3)
 elif name=='浅木':h+=.08*np.sin(np.sqrt((xx-.5)**2+(yy-.5)**2)*170+np.sin(xx*25)*.7)
 elif name in ('红布','衣','绳'):h+=.025*np.sin(xx*400)*np.sin(yy*400)
 elif name in ('叶片','亮叶','深叶','嫩叶'):h+=.07*np.exp(-abs(xx-.5)*80)+.015*np.sin((yy+abs(xx-.5)*.7)*180)
 elif name=='苔石':h+=.06*np.sin(xx*20+np.sin(yy*17))*np.sin(yy*23)
 pixels[oy:oy+tile,ox:ox+tile,:3]=np.clip(np.array(color)[None,None,:]*(1+h[...,None]),0,1)
 if name=='林地':
  angle=np.arctan2(yy-.5,xx-.5);edge=np.sin(xx*37+yy*19)*.11+np.sin(yy*65)*.035
  mask=np.clip(np.minimum(angle+math.pi*.60+edge,math.pi*.067-angle+edge)*8,0,1)
  flecks=np.random.default_rng(3206).uniform(-1,1,(tile,tile))
  for repeat in range(3):flecks=(flecks+np.roll(flecks,1,0)+np.roll(flecks,-1,0)+np.roll(flecks,1,1)+np.roll(flecks,-1,1))/5
  flecks*=3
  forest=np.array((.17,.26,.045))[None,None,:]*(1+flecks[...,None]*.4)
  dirt=np.array((.34,.23,.105))[None,None,:]*(1+flecks[...,None]*.22)
  pixels[oy:oy+tile,ox:ox+tile,:3]=forest*(1-mask[...,None])+dirt*mask[...,None]
 heights[oy:oy+tile,ox:ox+tile]=h
image=bpy.data.images.new('森林本色图集',width=size,height=size)
image.pixels.foreach_set(pixels.ravel());image.filepath_raw=str(SRC/'textures/forest-color.png');image.file_format='PNG';image.save()
normal=np.ones_like(pixels);dy,dx=np.gradient(heights);normal[:,:,:3]=np.stack((-dx*1.5,-dy*1.5,np.ones_like(dx)),axis=-1)
normal[:,:,:3]/=np.linalg.norm(normal[:,:,:3],axis=-1)[...,None];normal[:,:,:3]=normal[:,:,:3]*.5+.5
nim=bpy.data.images.new('几何微高度法线',width=size,height=size);nim.colorspace_settings.name='Non-Color';nim.pixels.foreach_set(normal.ravel());nim.filepath_raw=str(SRC/'textures/forest-normal.png');nim.file_format='PNG';nim.save()
for name,color in palette.items():
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
 nt=m.node_tree;p=nt.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=.72
 if name!='灰模':
  t=nt.nodes.new('ShaderNodeTexImage');t.image=image;nt.links.new(t.outputs['Color'],p.inputs['Base Color'])
  t=nt.nodes.new('ShaderNodeTexImage');t.image=nim;n=nt.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.35;nt.links.new(t.outputs['Color'],n.inputs['Color']);nt.links.new(n.outputs['Normal'],p.inputs['Normal'])
 M[name]=m
def uv_tile(o,name):
 idx=list(palette).index(name);uv=o.data.uv_layers.active or o.data.uv_layers.new(name='UVMap')
 for poly in o.data.polygons:
  axes=sorted(range(3),key=lambda k:abs(poly.normal[k]))[:2]
  for li in poly.loop_indices:
   c=o.data.vertices[o.data.loops[li].vertex_index].co
   uv.data[li].uv=((idx%4+.04+(c[axes[0]]*2%1)*.92)/4,(idx//4+.04+(c[axes[1]]*2%1)*.92)/4)
def finish(o,name,material,quad=None,smooth=False):
 o.name=name;o['模块']=group;o['参考']=name.split('_')[0];o.data.materials.append(M['灰模' if STAGE=='gray' else material]);parts.append(o)
 for p in o.data.polygons:p.use_smooth=smooth
 uv_tile(o,material)
 return o
def mesh(name,v,f,material,quad=None,smooth=False):
 me=bpy.data.meshes.new(name);me.from_pydata(v,[],f);me.update();o=bpy.data.objects.new(name,me);scene.collection.objects.link(o);return finish(o,name,material,quad,smooth)
def rod(name,a,b,r,material='树皮',r2=None,n=10,cap='浅木'):
 a,b=Vector(a),Vector(b);d=b-a;n=max(n,10 if STAGE=='detail' else 6)
 bpy.ops.mesh.primitive_cone_add(vertices=n,radius1=r,radius2=r if r2 is None else r2,depth=d.length,location=(a+b)/2)
 o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y');finish(o,name,material,smooth=True)
 if material=='树皮' and STAGE=='detail':
  o.data.materials.append(M[cap]);idx=list(palette).index(cap)
  for p in o.data.polygons:
   if abs(p.normal.z)>.99:
    p.material_index=1;p.use_smooth=False
    for li in p.loop_indices:
     c=o.data.vertices[o.data.loops[li].vertex_index].co;o.data.uv_layers.active.data[li].uv=((idx%4+.5+c.x/max(r,r2 or r)*.45)/4,(idx//4+.5+c.y/max(r,r2 or r)*.45)/4)
 return o
def box(name,loc,size,material='浅木',bevel=.015):
 bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.scale=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);finish(o,name,material)
 if bevel and STAGE=='detail':
  m=o.modifiers.new('硬边倒角','BEVEL');m.width=bevel;m.segments=2;bpy.ops.object.modifier_apply(modifier=m.name)
 return o
leaf_cache={}
def ico(name,loc,scale,material='叶片',quad=None):
 if material in ('叶片','深叶','嫩叶') and STAGE=='detail':
  variant=random.randrange(4);key=(material,variant)
  if key not in leaf_cache:
   rng=random.Random(variant+731);vv=[];ff=[]
   # 锁定椭球冠幅，枝梢按螺旋叶序覆盖冠壳；不是长条蕨叶串。
   for branch in range(16):
    z=1-2*(branch+.5)/16;a=branch*2.39996+variant;rr=math.sqrt(1-z*z)
    axis=Vector((rr*math.cos(a),rr*math.sin(a),z));side=axis.cross(Vector((0,0,1))).normalized()
    for step in range(1,3):
     for sign in (-1,1):
      center=axis*(.64+step*.075)+side*sign*.095
      direction=(side*sign*.75+axis*.35+Vector((0,0,.25))).normalized();width=direction.cross(axis).normalized()
      length=.42+rng.random()*.09;base=len(vv)
      for e in range(8):
       theta=e*math.tau/8;vv.append(tuple(center+direction*math.cos(theta)*length*.55+width*math.sin(theta)*length*.32))
      vv.extend([tuple(center+axis*length*.055),tuple(center-axis*.006)])
      for e in range(8):ff.extend([(base+e,base+(e+1)%8,base+8),(base+(e+1)%8,base+e,base+9)])
   o=mesh(name,vv,ff,material,smooth=True);leaf_cache[key]=o.data
  else:
   o=bpy.data.objects.new(name,leaf_cache[key]);scene.collection.objects.link(o);o['模块']=group;parts.append(o)
  o.location=loc;o.scale=scale;o['根部固定']=True;o['风动相位']=variant/4
  return o
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2 if STAGE=='detail' else 1,radius=1,location=loc);o=bpy.context.object;o.scale=scale;return finish(o,name,material,smooth=True)
# 连续地表和封闭土层。内圈高差小于0.25mm，边界严格共面。
N=96;vv=[];ff=[]
for r,z in [(4.92,-.48),(5,-.40),(5,-.08),(4.98,0)]:
 for i in range(N):
  a=i*math.tau/N;rr=r*math.cos(math.pi/6)/math.cos((a%(math.pi/3))-math.pi/6);vv.append((rr*math.cos(a),rr*math.sin(a),z))
ff=[tuple(reversed(range(N)))]
for j in range(3):
 for i in range(N):ff.append((j*N+i,j*N+(i+1)%N,(j+1)*N+(i+1)%N,(j+1)*N+i))
vv.append((0,0,0))
for i in range(N):ff.append((4*N,3*N+i,3*N+(i+1)%N))
base=mesh('D01_土层及连续地表',vv,ff,'泥土');base.data.materials.append(M['灰模' if STAGE=='gray' else '林地'])
for p in base.data.polygons[-N:]:
 p.material_index=1
 if p.material_index==1:
  idx=list(palette).index('林地')
  for li in p.loop_indices:
   c=base.data.vertices[base.data.loops[li].vertex_index].co;base.data.uv_layers.active.data[li].uv=((idx%4+.05+(c.x/10+.5)*.9)/4,(idx//4+.05+(c.y/10+.5)*.9)/4)
group='树林'
positions=[(-2.3,2.6,2.7),(-.9,3.15,2.5),(.5,3.,2.15),(1.9,2.6,2.4),(3.,1.5,2.0),(-3.2,1.5,1.9),(-1.8,1.3,2),(-.3,1.8,2.1),(1,1.5,1.9),(2.2,.8,2.0),(-3.5,0,1.9),(-2.5,-.1,2.2),(-1.1,.1,1.7),(.2,.3,2),(-3,-1.4,2.2),(-1.8,-1.3,2.25),(-.3,-1.2,1.65),(-2.3,-2.6,2.2),(-.9,-2.7,1.6),(-1.5,-3.5,1.5),(.5,-3.4,1.5),(3.5,-.1,1.6),(.8,2.45,1.8),(0,3.6,1.55),(-3.4,.8,1.5),(-.9,2.6,1.55),(.5,.9,1.65),(-3.5,-.8,1.5),(-2.5,3.0,1.5),(0,-2.2,1.35)]
for k,(x,y,h) in enumerate(positions):
 pine=k%4==1;mat='深叶' if pine else ('嫩叶' if k%3==0 else '叶片')
 rod(f'T{k+1:02}_树干',(x,y,0),(x+.06,y,h*.88),.11,r2=.025)
 for a in range(4):rod(f'T{k+1:02}_树根',(x,y,.18),(x+math.cos(a*1.57)*.27,y+math.sin(a*1.57)*.27,.015),.07,r2=.015,n=7)
 if pine:
  for j in range(5):
   radius=h*(.27-j*.045);z=h*(.25+j*.15)
   for a in range(4):
    ang=a*math.pi/2+j*.75;dx,dy=math.cos(ang)*radius*.55,math.sin(ang)*radius*.55
    ico(f'T{k+1:02}_针叶枝层{j}',(x+dx,y+dy,z),(radius*.65,radius*.65,h*.13),mat)
 else:
  for j in range(5):
   a=j*2.399+k*.7;rr=h*.24 if j<4 else .04;dx,dy=math.cos(a)*rr,math.sin(a)*rr;z=h*(.55+j*.08)
   rod(f'T{k+1:02}_一级枝{j}',(x,y,h*.30),(x+dx,y+dy,z),.06,r2=.018)
   ico(f'T{k+1:02}_叶冠{j}',(x+dx,y+dy,z),(h*.34,h*.34,h*.29),mat)
group='地被'
for k in range(70):
 a=k*2.39996;r=1.0+3.25*math.sqrt((k+.5)/70);x,y=r*math.cos(a),r*math.sin(a)
 if x>.35 and y<.5:continue
 ico(f'S_林下灌木{k}',(x,y,.23),(.46,.46,.32),'嫩叶' if k%3==0 else '深叶')
for k,(x,y,s) in enumerate([(-2.7,1.2,.5),(-1.6,1.9,.55),(-3.5,-.8,.3),(-2.2,-2.8,.5),(.55,.16,.3),(3.5,.25,.30),(.1,-3.3,.25),(-.6,-3.8,.3),(2.95,-3.05,.20)]):ico(f'R{k+1:02}_苔石',(x,y,s*.4),(s,s*.75,s*.75),'苔石')
# 地被只用于柔化林缘，不用随机小几何代替树冠和棚架。
for k in range(180 if STAGE=='detail' else 40):
 a=k*2.39996;r=4.12*math.sqrt((k+.5)/180);x,y=r*math.cos(a),r*math.sin(a)
 if x>.45 and -3<y<.4:continue
 for j in range(4):
  ang=j*2.399;d=Vector((math.cos(ang),math.sin(ang),0));p=Vector((x,y,0));s=Vector((-d.y,d.x,0))*.035
  mesh('G01_折脊草叶',[tuple(p-s),tuple(p+s),tuple(p+d*.15+Vector((0,0,.10))),tuple(p+d*.22+Vector((0,0,.19)))],[(0,1,2),(0,2,3)],'亮叶')

if STAGE=='detail':
 # 中性边带以低矮草叶和真实土层细石过渡，不占用高树和设施净空。
 for k in range(240):
  a=(k+random.uniform(-.4,.4))*math.tau/240;rr=random.uniform(4.45,4.85)*math.cos(math.pi/6)/math.cos((a%(math.pi/3))-math.pi/6)
  x,y=rr*math.cos(a),rr*math.sin(a)
  for j in range(3):
   aa=a+j*2.1;p=Vector((x,y,0));d=Vector((math.cos(aa),math.sin(aa),0));s=Vector((-d.y,d.x,0))*.025
   mesh('G02_边带低草',[tuple(p-s),tuple(p+s),tuple(p+d*.08+Vector((0,0,.05)))],[(0,1,2)],'亮叶')
 group='地块'
 for layer in range(2):
  for k in range(90):
   a=(k+layer*.5)*math.tau/90;rr=4.91*math.cos(math.pi/6)/math.cos((a%(math.pi/3))-math.pi/6)
   ico('D01_土层嵌石',(rr*math.cos(a),rr*math.sin(a),-.14-layer*.17+random.uniform(-.04,.04)),(random.uniform(.07,.16),random.uniform(.08,.14),random.uniform(.045,.10)),'泥土' if k%3 else '苔石')
# 营地的明确结构位置继承已记录样板，几何细节在本版重建。
group='营地'
old=(ROOT/'art/recipes/ultra/forest-camp/v001/build.py').read_text(encoding='utf8')
camp=old[old.index('def shelter('):old.index('# 应用变换、法线统一')]
camp=camp.replace('nx=4;ny=2','nx=16;ny=8').replace('n=8;vv=[];ff=[]','n=16;vv=[];ff=[]')
# 新图集的UV统一处理，不再使用旧版四格坐标。
camp=camp.replace("uv=roof.data.uv_layers.new(name='UVMap')","uv=roof.data.uv_layers.active")
camp=camp.replace("(.015+idx%(nx+1)/nx*.47,.015+(idx//(nx+1))/ny*.47)","((2+.04+idx%(nx+1)/nx*.92)/4,(.04+(idx//(nx+1))/ny*.92)/4)")
camp=camp.replace(".025*math.sin(t*math.pi*4)",".022*math.sin(t*math.pi*8)*math.sin(math.pi*u)")
camp=camp.replace("hand=(x+s*.095,y-.39,.64 if not axe else .73)","hand=(x+s*.018,y-.46+s*.02,.73+s*.04) if axe else (x+s*.095,y-.39,.64)")
exec(compile(camp,'v001_营地布局','exec'))
if STAGE=='detail':
 group='营地'
 # 棚边缝线和补片具有厚度；保留玩家配色部件。
 for x,y,w,d,h in [(2.15,.05,1.25,.85,1.05),(3.20,-1.10,1.05,.65,.77)]:
  for j in range(13):
   u=(j+.5)/13;z=h-.18*math.sin(math.pi*u)
   rod('B_布边缝线',(x+(u-.5)*w,y-d/2-.085,z+.01),(x+(u-.5)*w,y-d/2-.035,z+.013),.006,'绳',n=6)
  for sx in (-1,1):
   for sy in (-1,1):
    px=x+sx*w/2;py=y+sy*d/2
    for l in range(3):
     bpy.ops.mesh.primitive_torus_add(major_segments=12,minor_segments=4,location=(px,py,h-.045+l*.025),major_radius=.064,minor_radius=.012);finish(bpy.context.object,'B_绳结','绳',smooth=True)
 # 锯齿；工具仍为独立物体，枢轴在握持端。
 for i in range(13):
  x=1.28-.46+i*.04;y=-1.89;z=.59
  mesh('P06_锯齿',[(x,y-.013,z),(x+.03,y-.013,z),(x+.015,y-.013,z-.025),(x,y+.013,z),(x+.03,y+.013,z),(x+.015,y+.013,z-.025)],[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],'铁')
 # 背心前襟、鼻与耳，避免仅以球体作为完整人物。
 group='人物'
 for x,y in [(1.28,-1.5),(2.65,-1.85)]:
  rod('C_衣襟',(x-.11,y-.157,.82),(x+.05,y-.172,.55),.018,'绳')
  ico('C_鼻',(x,y-.222,.95),(.026,.04,.032),'肤')
  for s in (-1,1):ico('C_耳',(x+s*.104,y-.13,.95),(.018,.025,.035),'肤')

# 所有对象的局部坐标转到米制，非地块内缩留出道路带；共享叶网格保持共享。
factor=.0395/5
for o in parts:
 o.location*=factor;o.scale*=factor
 if o.get('模块')!='地块':o.location.x*=.82;o.location.y*=.82;o.scale.x*=.82;o.scale.y*=.82
 if o.name.startswith(('P06','P07')):o['枢轴约定']='握持端；静态工具，尚未绑定人物动画'
 if any(m.name.startswith('红布') for m in o.data.materials):o['玩家配色']=True
bpy.context.view_layer.update()
for o in parts:
 if o.name.startswith('D01_土层嵌石'):
  inverse=o.matrix_world.inverted()
  for vertex in o.data.vertices:
   p=o.matrix_world@vertex.co;limit=.0395*math.cos(math.pi/6)-.00004
   extent=max(p.x*math.cos(math.pi/6+k*math.pi/3)+p.y*math.sin(math.pi/6+k*math.pi/3) for k in range(6))
   if extent>limit:p.x*=limit/extent;p.y*=limit/extent;vertex.co=inverse@p
for o in parts:
 if o.name.startswith(('P06','P07')):
  grip=(1.28,-1.89,.64) if o.name.startswith('P06') else (2.65,-2.31,.73)
  scene.cursor.location=(grip[0]*factor*.82,grip[1]*factor*.82,grip[2]*factor)
  bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
scene.cursor.location=(0,0,0)
# 底面中心锚点与分模块组织，装饰不带碰撞。
for category in sorted(set(o['模块'] for o in parts)):
 coll=bpy.data.collections.new(category);scene.collection.children.link(coll)
 for o in parts:
  if o['模块']==category:
   for c in list(o.users_collection):c.objects.unlink(o)
   coll.objects.link(o)
scene['阶段']=STAGE;scene['资产半径米']=.0395;scene['设计推定']='背面枝条、底部和人物细节；统一模型解决辅助图矛盾'
if STAGE=='detail':
 p=ROOT/'art/recipes/ultra/forest-camp/v002/details.py'
 exec(compile(p.read_text(encoding='utf8'),str(p),'exec'),{'__file__':str(p)})
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.world=bpy.data.worlds.new('中性暖灰');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.60,.56,.48,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
for name,loc,power,size in [('主光',(-.06,-.07,.13),.28,.08),('补光',(.08,-.01,.07),.10,.07),('轮廓光',(.01,.08,.12),.17,.06)]:
 data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size;o=bpy.data.objects.new(name,data);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(-o.location).to_track_quat('-Z','Y').to_euler()
data=bpy.data.cameras.new('同模型正交相机');cam=bpy.data.objects.new('同模型正交相机',data);scene.collection.objects.link(cam);scene.camera=cam;data.type='ORTHO';data.clip_start=.0001;data.clip_end=10
cam.location=(0,-.12,.145);cam.rotation_euler=(Vector((0,0,.004))-cam.location).to_track_quat('-Z','Y').to_euler();data.ortho_scale=.091
scene.render.resolution_x=1200;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.clip_start=.0001;area.spaces.active.region_3d.view_distance=.13
bpy.ops.file.pack_all()
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/('gray.blend' if STAGE=='gray' else 'forest-camp.blend')))
print('森林阶段生成完成',STAGE,len(parts))
