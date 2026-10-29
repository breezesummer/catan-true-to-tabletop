"""通过Blender MCP制作高细节庭院，保留此前场景，不受6000三角面限制。"""
import bpy,math,json,sys,importlib,bmesh
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
if str(ROOT) not in sys.path:sys.path.insert(0,str(ROOT))
for name in ['geometry','architecture','props']:
 if name in sys.modules:del sys.modules[name]
from geometry import *
from architecture import *
from props import *
S=bpy.data.scenes.new('三国庭院_高细节_v002');bpy.context.window.scene=S;S.unit_settings.system='METRIC'
C=bpy.data.collections.new('庭院_高细节组件');S.collection.children.link(C)
M={}
def material(name,color,rough=.5,metal=0,wood=False):
 m=bpy.data.materials.new('v002_'+name);m.use_nodes=True;m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Roughness'].default_value=rough;p.inputs['Metallic'].default_value=metal
 if name.startswith('tile'):p.inputs['Specular IOR Level'].default_value=.30
 if name in ['red','wood','wood1','pot']:p.inputs['Coat Weight'].default_value=.22;p.inputs['Coat Roughness'].default_value=.23
 if wood:
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(ROOT/'textures'/'wood_basecolor.png'));m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color'])
 if name in ['stone','stone1','stone2','sculpt','plaster']:
  t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(ROOT/'textures'/'plaster_normal.png'),check_existing=True);t.image.colorspace_settings.name='Non-Color';n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.14;m.node_tree.links.new(t.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],p.inputs['Normal'])
 if name=='lantern':p.inputs['Emission Color'].default_value=(1,.24,.01,1);p.inputs['Emission Strength'].default_value=.35
 if name=='cloth':p.inputs['Sheen Weight'].default_value=.3;p.inputs['Sheen Roughness'].default_value=.4
 M[name]=m
for name,c,r,met in [('red',(.64,.006,.012),.30,0),('red_dark',(.30,.002,.004),.42,0),('wood',(.30,.055,.012),.4,0),('wood1',(.40,.085,.022),.43,0),('wood_lacquer',(.34,.053,.014),.34,0),('wood_dark',(.12,.025,.009),.6,0),('stone',(.45,.46,.48),.66,0),('stone1',(.51,.52,.54),.66,0),('stone2',(.41,.42,.44),.7,0),('sculpt',(.60,.60,.62),.42,0),('plaster',(.87,.76,.60),.83,0),('mortar',(.29,.29,.30),.9,0),('tile',(.033,.04,.05),.36,0),('tile1',(.043,.052,.063),.37,0),('tile2',(.027,.032,.04),.38,0),('paper',(.12,.22,.25),.82,0),('gold',(.72,.32,.075),.25,.72),('gold_light',(.9,.48,.14),.26,.7),('gold_dark',(.32,.095,.016),.4,.6),('pot',(.29,.055,.008),.22,0),('water',(.035,.23,.29),.14,.15),('lantern',(1,.48,.035),.37,0),('cloth',(.67,.003,.008),.36,0),('hay',(.55,.43,0),.9,0),('hay_light',(.75,.48,.15),.87,0),('hay_dark',(.33,.17,.038),.9,0),('rope',(.57,.34,.12),.8,0),('recess',(.12,.13,.15),.65,0),('eye',(.2,.22,.24),.35,0)]:material(name,c,r,met,wood=name=='wood')
group('A42_朱红圆底座');lathe((0,0,0),[(7.1,0),(7.19,.025),(7.23,.10),(7.23,.79),(7.20,.86),(7.1,.90)],'red',192)
group('T01_石砖地面');lathe((0,0,.90),[(7.04,0),(7.04,.07)],'mortar',192);paving(-7.04,7.04,-7.04,7.04,1,.29,7.04)
building('A01_正殿',.30,2.70,7.15,3.30,1.75,2.35,1.63)
building('A02_厢房',-3.9,-1.00,3.50,1.75,1.28,1.82,.88,True)
stairs('A04_正殿台阶',.3,-1.0,2.6,1.085,.75,5)
stairs('A05_厢房台阶',-2.30,-2.19,1.18,.44,.28,2,math.pi/2)
stairs('A06_门楼台阶',0,-5.60,2.08,1.05,.40,4)
group('A03_前门楼');transform(0,-4.55)
box((0,0,1.25),(2.6,.6,.50),'stone',.035)
for x in [-1.13,1.13]:
 box((x,0,1.40),(.45,.48,.30),'stone',.03);lathe((x,0,1.55),[(.20,0),(.20,.12),(.155,.17),(.155,1.66),(.20,1.70)],'wood_lacquer',40);bracket(x,0,3.15,.8)
door(0,-.035,1.43,1.89,1.91,True);box((0,0,3.42),(2.65,.35,.20),'wood',.025);roof(3.15,1.45,3.51,.66)
wall('A07_前左围墙',(-5,-4.5),(-1.32,-4.5));wall('A08_前右围墙',(1.32,-4.5),(5,-4.5));wall('A09_左前围墙',(-5,-4.5),(-5,-2.60));wall('A10_右围墙',(5,-4.5),(5,4.60));wall('A11_后围墙',(-5,4.60),(5,4.60));wall('A11b_后左补墙',(-5,.75),(-5,4.6))
for id,x,y in [('A12_前左柱',-5,-4.5),('A13_左端柱',-5,-2.6),('A14_前右柱',5,-4.5),('A15_后右柱',5,4.6),('A15b_后左柱',-5,4.6)]:post(id,x,y)
for id,x in [('A16_左石墩',-1.58),('A17_右石墩',1.58)]:
 group(id);transform(x,-5.22,1);box((0,0,.075),(.69,.68,.15),'stone',.025);box((0,0,.41),(.57,.56,.53),'stone',.028);box((0,0,.72),(.67,.67,.12),'stone1',.02)
 for sg in [-1,1]:
  box((0,sg*.29,.42),(.40,.025,.38),'mortar',.018);box((0,sg*.31,.42),(.34,.022,.32),'stone1',.018)
lion('A18_左石狮',-1.9,.53,1.77);lion('A19_右石狮',2.5,.53,1.77)
lantern('A20_左灯笼',-1.49,-4.75,2.92);lantern('A21_右灯笼',1.49,-4.75,2.92)
group('A22_陶罐');transform(-2.60,-.40,1);lathe((0,0,0),[(.23,0),(.26,.04),(.34,.16),(.38,.38),(.35,.57),(.24,.70),(.22,.77),(.25,.79),(.25,.84),(.20,.85),(.18,.81),(.18,.72),(.28,.53),(.29,.25),(.18,.06)],'pot',56)
group('A23_水缸');transform(-4.07,-3.47,1);lathe((0,0,0),[(.27,0),(.30,.05),(.40,.18),(.46,.45),(.44,.61),(.47,.64),(.47,.71),(.37,.73),(.36,.65),(.36,.45),(.30,.10)],'stone',56);torus((0,0,.67),.424,.034,'stone1','Z',56,10);lathe((0,0,.56),[(.363,0),(.363,.008)],'water',56)
for j in range(3):
 for i in range(10):
  a=i*math.pi/5+j*.25;pts=[(.44*math.cos(a),.44*math.sin(a),.24+j*.13),(.448*math.cos(a+.12),.448*math.sin(a+.12),.30+j*.13)];tube(pts,.0035,'mortar',5)
chest(-2.64,-1.25)
group('A25_圆石桌');transform(3.15,-1.40,1);lathe((0,0,0),[(.25,0),(.29,.04),(.29,.12),(.22,.17),(.19,.22),(.19,.55),(.25,.61),(.49,.64),(.51,.69),(.51,.78),(.48,.81)],'stone1',64)
torus((0,0,.815),.36,.003,'stone2','Z',64,6)
for id,x,y in [('A26_石凳后左',2.49,-1.16),('A27_石凳前左',2.70,-1.99),('A28_石凳后右',3.79,-1.18),('A29_石凳前右',3.67,-1.91)]:
 group(id);transform(x,y,1);lathe((0,0,0),[(.19,0),(.22,.04),(.22,.10),(.16,.15),(.16,.35),(.22,.40),(.23,.46),(.20,.49)],'stone',40)
for id,y in [('A30_草捆一',2.1),('A31_草捆二',2.77),('A32_草捆三',3.44)]:hay(id,4.38,y)
well();flag()
ornament('A38_正殿左脊兽',-2.90,2.70,6.24,.75,False);ornament('A39_正殿右脊兽',3.50,2.70,6.24,.75,True)
ornament('A40_门楼左脊兽',-1.40,-4.55,4.61,.48,False);ornament('A41_门楼右脊兽',1.40,-4.55,4.61,.48,True)
objects=finish(C,M)
# 将狮身、腿、头和面部的基础体积融合为连续石雕；眼与浮雕另保留。
for source in list(objects):
 if not source.name.startswith(('A18_','A19_')):continue
 sculpt_indices={i for i,m in enumerate(source.data.materials) if m==M['sculpt']}
 faces=[tuple(p.vertices) for p in source.data.polygons if p.material_index in sculpt_indices]
 mesh=bpy.data.meshes.new(source.name+'_连续石雕');mesh.from_pydata([tuple(v.co) for v in source.data.vertices],[],faces);mesh.update()
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(mesh);bm.free()
 sculpt=bpy.data.objects.new(source.name+'_连续石雕',mesh);C.objects.link(sculpt);mesh.materials.append(M['sculpt'])
 bm=bmesh.new();bm.from_mesh(source.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index in sculpt_indices],context='FACES');bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(source.data);bm.free()
 bpy.ops.object.select_all(action='DESELECT');sculpt.select_set(True);bpy.context.view_layer.objects.active=sculpt
 modifier=sculpt.modifiers.new('融合石雕体积','REMESH');modifier.mode='VOXEL';modifier.voxel_size=.008;modifier.use_smooth_shade=True;bpy.ops.object.modifier_apply(modifier=modifier.name)
 modifier=sculpt.modifiers.new('石雕表面平滑','SMOOTH');modifier.factor=.45;modifier.iterations=3;bpy.ops.object.modifier_apply(modifier=modifier.name)
 uv=sculpt.data.uv_layers.new(name='UVMap')
 for p in sculpt.data.polygons:
  p.use_smooth=True
  for li in p.loop_indices:
   co=sculpt.data.vertices[sculpt.data.loops[li].vertex_index].co;uv.data[li].uv=(co.x/2,co.z/2)
 objects.append(sculpt)
# 加厚底座，保持所有建筑与地面的相对位置。
for ob in objects:
 for v in ob.data.vertices:
  if ob.name.startswith('A42'):v.co.z*=1.25/.90
  else:v.co.z+=.35
for o in objects:
 bm=bmesh.new();bm.from_mesh(o.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()
# 摄影棚地台独立收藏，导出时排除。
studio=bpy.data.collections.new('摄影棚_不导出');S.collection.children.link(studio)
mesh=bpy.data.meshes.new('地台');mesh.from_pydata([(-200,-200,-.025),(200,-200,-.025),(200,200,-.025),(-200,200,-.025)],[],[(0,1,2,3)]);o=bpy.data.objects.new('摄影棚地台',mesh);studio.objects.link(o);material('studio',(.60,.65,.74),.9);mesh.materials.append(M['studio'])
S.world=bpy.data.worlds.new('柔光环境');S.world.use_nodes=True;S.world.node_tree.nodes['Background'].inputs[0].default_value=(.58,.65,.79,1);S.world.node_tree.nodes['Background'].inputs[1].default_value=.30
for name,loc,power,size,color in [('左前主光',(-7,-10,15),2400,8,(1,.88,.75)),('右补光',(8,-3,11),1500,7,(.79,.87,1)),('后轮廓',(2,9,13),2300,6,(1,.91,.82))]:
 d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color;o=bpy.data.objects.new(name,d);studio.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,2.5))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('成品相机');cam=bpy.data.objects.new('成品相机',d);studio.objects.link(cam);S.camera=cam;d.type='ORTHO';d.ortho_scale=15.8;cam.location=(9,-20,22);cam.rotation_euler=(Vector((0,0,2.4))-cam.location).to_track_quat('-Z','Y').to_euler()
S.render.engine='CYCLES';S.cycles.samples=48;S.cycles.use_denoising=True;S.render.resolution_x=1400;S.render.resolution_y=1400;S.render.resolution_percentage=100;S.render.image_settings.file_format='PNG';S.view_settings.view_transform='AgX';S.view_settings.look='AgX - Medium High Contrast'
report={o.name:sum(len(p.vertices)-2 for p in o.data.polygons) for o in objects};(ROOT/'mesh_stats.json').write_text(json.dumps({'三角面总数':sum(report.values()),'组件数':len(report),'各组件':report},ensure_ascii=False,indent=2))
for m in M.values():
 for n in m.node_tree.nodes:
  if n.type=='TEX_IMAGE':n.image.pack()
bpy.data.libraries.write(str(ROOT/'courtyard_high.blend'),{S},fake_user=True,compress=True)
result={'triangles':sum(report.values()),'objects':len(objects),'scene':S.name}
