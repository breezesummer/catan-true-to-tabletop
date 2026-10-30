"""在 Blender MCP 中执行；独立场景、米制估算、底面中心枢轴。先 sample 再 all。"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector
ROOT = Path('/Users/breezesummer/开发栈/Project/catan-true-to-tabletop')
OUT = ROOT / 'Unity/Catan/Assets/Resources/HarborFishing/v001'
SOURCE = ROOT / 'art/generated/harbor-fishing-v001'
OUT.mkdir(parents=True, exist_ok=True); SOURCE.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('海港捕鱼组件_' + STAGE)
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.render.engine = 'CYCLES'; scene.cycles.samples = 24
scene.render.resolution_x = 256; scene.render.resolution_y = 256; scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new('组件柔光'); scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.65,.73,.8,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.6
materials={}
for name,color,metal in [('铜',(.55,.30,.09,1),.65),('水',(.035,.27,.32,1),.3),('鱼',(.87,.77,.45,1),.35),('靴',(.14,.065,.028,1),0),('沙',(.65,.53,.32,1),0),('草',(.19,.29,.15,1),0),('深色',(.025,.05,.055,1),0)]:
    m=bpy.data.materials.new('F6_'+name);m.diffuse_color=color;m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=color;p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.36
    materials[name]=m
parts=[]
def finish(obj,name,mat):
    obj.name=name;obj.data.materials.append(materials[mat]);parts.append(obj);return obj
def disk(name,radius,depth,z,mat,vertices=48,scale=(1,1,1)):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=(0,0,z))
    o=finish(bpy.context.object,name,mat);o.scale=scale
    bevel=o.modifiers.new('柔和边缘','BEVEL');bevel.width=.0003;bevel.segments=2
    o.modifiers.new('法线','WEIGHTED_NORMAL');return o
def ellipsoid(name,loc,scale,mat):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,location=loc)
    o=finish(bpy.context.object,name,mat);o.scale=scale;return o
def cube(name,loc,scale,mat):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc)
    o=finish(bpy.context.object,name,mat);o.scale=scale
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    b=o.modifiers.new('圆角','BEVEL');b.width=.0006;b.segments=3;o.modifiers.new('法线','WEIGHTED_NORMAL');return o
def fish(x,y,z,size=1):
    ellipsoid('鱼身',(x,y,z),(.0037*size,.00155*size,.00055*size),'鱼')
    # 尾鳍为封闭三角棱柱。
    verts=[(x-.003*size,y,z),(x-.0055*size,y+.0018*size,z),(x-.0055*size,y-.0018*size,z)]
    verts += [(a,b,c+.00065) for a,b,c in verts]
    mesh=bpy.data.meshes.new('鱼尾');mesh.from_pydata(verts,[],[(0,2,1),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)])
    o=bpy.data.objects.new('尾鳍',mesh);scene.collection.objects.link(o);finish(o,'尾鳍','鱼')
    ellipsoid('鱼眼',(x+.002*size,y+.0004*size,z+.0005*size),(.00035*size,)*3,'深色')
def text(value,loc,size,mat='鱼'):
    bpy.ops.object.text_add(location=loc);o=bpy.context.object;o.data.body=value;o.data.size=size;o.data.align_x='CENTER';o.data.align_y='CENTER';o.data.extrude=.00008
    bpy.ops.object.convert(target='MESH');finish(bpy.context.object,'数字_'+value,mat)
def create(name):
    global parts
    parts=[]
    if name.startswith('fish-'):
        disk('铜边',.012,.002,.001,'铜');disk('珐琅',.0112,.0005,.0022,'水')
        n=0 if name=='fish-back' else int(name[-1])
        if n:
            for i in range(n): fish(.0005,(i-(n-1)/2)*.005,.0027)
        else:
            for y in [-.005,0,.005]:
                cube('背面波纹',(0,y,.0027),(.014,.00065,.0003),'鱼')
    elif name.startswith('ground-'):
        disk('渔场铜边',.014,.0015,.00075,'铜',scale=(1,.66,1));disk('渔场水面',.0133,.0004,.0016,'水',scale=(1,.66,1))
        fish(-.005,-.001,.0022,.8);text(name.split('-')[1],(.006,0,.0021),.008)
    elif name=='old-boot':
        cube('靴底',(0,0,.001),(.021,.010,.002),'深色')
        ellipsoid('圆润鞋头',(.005,0,.0035),(.007,.0046,.0035),'靴')
        cube('靴筒',(-.005,0,.008),(.009,.009,.013),'靴')
        cuff=disk('磨损鞋口',.005,.0015,.0145,'靴',32,scale=(.9,1,1));cuff.location.x=-.005
        opening=disk('鞋口内腔',.0038,.0002,.0153,'深色',32,scale=(.9,1,1));opening.location.x=-.005
        for z in [.006,.009,.012]:cube('鞋带',(-.0004,0,z),(.0008,.007,.0006),'鱼')
    elif name=='harbormaster':
        cube('铜质奖励牌',(0,0,.0008),(.036,.052,.0016),'铜');cube('蓝绿牌面',(0,0,.0018),(.033,.049,.0007),'水')
        cube('锚杆',(0,.005,.003),(.0016,.023,.001),'鱼');cube('锚横梁',(0,.009,.003),(.014,.0016,.001),'鱼')
        for sign in [-1,1]:
            o=cube('锚臂',(sign*.006,-.004,.003),(.014,.0016,.001),'鱼');o.rotation_euler.z=sign*.55
        text('+2',(0,-.016,.0024),.009)
    else:
        disk('湖泊厚板',.0395,.007,.0035,'沙',6)
        disk('湖岸',.0388,.001,.0075,'草',6)
        disk('湖水',.029,.0005,.0081,'水',48,scale=(1,.80,1))
        for x,y,s in [(-.021,.014,.004),(.024,-.012,.003),(-.024,-.014,.0025)]:ellipsoid('岸石',(x,y,.008),(s,s*.7,.0018),'沙')
        fish(-.006,.007,.0088,1.3);fish(.007,.002,.0088,1)
        text('2  3  11  12',(0,-.013,.0087),.005)
    return list(parts)
# 相机和光源独立于资产导出。
bpy.ops.object.camera_add(location=(0,-.06,.15));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.clip_start=.001;camera.data.clip_end=10;scene.camera=camera
bpy.ops.object.light_add(type='AREA',location=(-.04,-.04,.12));bpy.context.object.data.energy=.035;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=.1
names=['fish-1','ground-4'] if STAGE=='sample' else ['fish-1','fish-2','fish-3','fish-back','old-boot','harbormaster','ground-4','ground-5','ground-6','ground-8','ground-9','ground-10','lake']
if STAGE=='boot':names=['old-boot']
report=[]
for name in names:
    objects=create(name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    objects=[obj];bpy.context.view_layer.update()
    coords=[o.matrix_world@Vector(c) for o in objects for c in o.bound_box]
    lo=[min(c[i] for c in coords) for i in range(3)];hi=[max(c[i] for c in coords) for i in range(3)]
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',bake_anim=False)
    camera.location=(0,-.06,.042) if name=='old-boot' else (0,-.06,.15)
    target=Vector((0,0,.006 if name=='old-boot' else 0));camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=max(hi[0]-lo[0],hi[1]-lo[1])*1.2
    scene.render.filepath=str(OUT/(name+'.png'));bpy.ops.render.render(write_still=True)
    report.append({'名称':name,'最小坐标':lo,'最大坐标':hi,'三角面数':sum(len(p.vertices)-2 for o in objects for p in o.data.polygons),'尺寸依据':'米制估算，非实体测量'})
    for o in objects:o.hide_render=True;o.hide_set(True)
# 保存仅含新增场景的源文件，不覆盖输入文件。
bpy.data.libraries.write(str(SOURCE/('components-'+STAGE+'.blend')),{scene})
(SOURCE/('检查-'+STAGE+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2))
result={'阶段':STAGE,'资产':report,'输出':str(OUT)}
