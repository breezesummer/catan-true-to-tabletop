"""通过 Blender MCP 执行：网格检查、按材质合并导出、回读与多角度渲染。"""
import bpy,bmesh,json,math,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/Project/catan-true-to-tabletop')
OUT=ROOT/'art/exports/three-kingdoms-pieces/B201-v001'
PRE=ROOT/'art/previews/three-kingdoms-pieces/B201-v001'
REPORT=ROOT/'docs/verification/three-kingdoms-pieces/B201-v001'
scene=bpy.data.scenes['B201_验证样板'];bpy.context.window.scene=scene
for old in list(bpy.data.scenes):
    if old.name.startswith(('B201_导出检查','B201_GLB回读')):
        for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
objs=[o for o in scene.objects if o.type=='MESH']
stats={'三角面':0,'非流形边':0,'退化三角形':0,'精确重复面':0,'UV超界':0,'组件数':len(objs),'逐组件':[]}
faces=set();bounds=[]
for o in objs:
    me=o.data;me.calc_loop_triangles();bm=bmesh.new();bm.from_mesh(me)
    non=sum(not e.is_manifold for e in bm.edges);bm.free()
    deg=sum(t.area<1e-15 for t in me.loop_triangles)
    stats['三角面']+=len(me.loop_triangles);stats['非流形边']+=non;stats['退化三角形']+=deg
    for p in me.polygons:
        coords=tuple(sorted(tuple(round(c,8) for c in o.matrix_world@me.vertices[v].co) for v in p.vertices))
        if coords in faces:stats['精确重复面']+=1
        faces.add(coords)
    for d in me.uv_layers.active.data:
        if any(v<0 or v>1 for v in d.uv):stats['UV超界']+=1
    bounds.extend(o.matrix_world@v.co for v in me.vertices)
    stats['逐组件'].append({'名称':o.name,'三角面':len(me.loop_triangles),'非流形边':non,'退化三角形':deg})
stats['尺寸毫米']=[round((max(v[i] for v in bounds)-min(v[i] for v in bounds))*1000,3) for i in range(3)]
stats['纹理密度每厘米']=round(1254*.105,2)
stats['说明']='精确重复面检测不等同于任意共面重叠或穿插检测；木构榫接及瓦底接合需结合渲染审查。尺寸为样板估算，非实测。'
assert stats['三角面']<=6000,stats
assert stats['非流形边']==0,stats
assert stats['退化三角形']==0,stats
assert stats['精确重复面']==0,stats
assert stats['UV超界']==0,stats
(REPORT/'mesh-check.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')

# 合并导出物只存在于临时场景，制作场景保留独立结构。
ex=bpy.data.scenes.new('B201_导出检查');bpy.context.window.scene=ex
groups={}
for o in objs:
    cp=o.copy();cp.data=o.data.copy();ex.collection.objects.link(cp)
    groups.setdefault(cp.data.materials[0],[]).append(cp)
merged=[]
for mat,group in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in group:o.select_set(True)
    bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();o=bpy.context.object
    o.name='B201_'+mat.name.replace('B201_','').split('.')[0]
    bpy.context.scene.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    mod=o.modifiers.new('导出显式三角化','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=mod.name)
    merged.append(o)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.gltf(filepath=str(OUT/'B201.glb'),export_format='GLB',use_selection=True,export_yup=True,use_active_scene=True)
# 显式写入着色因子，避免 glTF 导出器丢弃 MixRGB 的常量乘色。
blob=(OUT/'B201.glb').read_bytes();ln=struct.unpack_from('<I',blob,12)[0];doc=json.loads(blob[20:20+ln])
for mat in doc['materials']:
    factor=.26 if '灰瓦' in mat['name'] else (.67 if '旧木' in mat['name'] else None)
    if factor:mat['pbrMetallicRoughness']['baseColorFactor']=[factor,factor,factor,1]
    mat['doubleSided']=False
chunk=json.dumps(doc,ensure_ascii=False,separators=(',',':')).encode('utf-8');chunk+=b' '*((-len(chunk))%4)
tail=blob[20+ln:];(OUT/'B201.glb').write_bytes(struct.pack('<4sII',b'glTF',2,20+len(chunk)+len(tail))+struct.pack('<I4s',len(chunk),b'JSON')+chunk+tail)
bpy.ops.export_scene.fbx(filepath=str(OUT/'B201.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_anim=False,path_mode='COPY',embed_textures=True)

# GLB 实际回读，复核三角形与尺寸。
rt=bpy.data.scenes.new('B201_GLB回读');bpy.context.window.scene=rt
bpy.ops.import_scene.gltf(filepath=str(OUT/'B201.glb'))
count=0;bb=[]
for o in rt.objects:
    if o.type=='MESH':
        o.data.calc_loop_triangles();count+=len(o.data.loop_triangles)
        bb.extend(o.matrix_world@v.co for v in o.data.vertices)
stats['GLB回读三角面']=count;stats['GLB网格数']=sum(o.type=='MESH' for o in rt.objects)
stats['GLB回读尺寸毫米']=[round((max(v[i] for v in bb)-min(v[i] for v in bb))*1000,3) for i in range(3)]
assert count==stats['三角面']
assert stats['GLB回读尺寸毫米']==stats['尺寸毫米']
cam=scene.camera.copy();cam.data=scene.camera.data.copy();rt.collection.objects.link(cam);rt.camera=cam
rt.world=scene.world;rt.render.engine='CYCLES';rt.cycles.samples=32;rt.cycles.use_denoising=True
rt.render.resolution_x=1100;rt.render.resolution_y=1100;rt.render.resolution_percentage=100
rt.render.film_transparent=True;rt.render.image_settings.file_format='PNG'
rt.view_settings.view_transform=scene.view_settings.view_transform;rt.view_settings.look=scene.view_settings.look;rt.view_settings.exposure=scene.view_settings.exposure
for o in scene.objects:
    if o.type=='LIGHT':c=o.copy();c.data=o.data.copy();rt.collection.objects.link(c)
rt.render.filepath=str(PRE/'glb-roundtrip.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=scene
for s in [ex,rt]:
    for o in list(s.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.scenes.remove(s)
(REPORT/'mesh-check.json').write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
scene.render.resolution_x=1100;scene.render.resolution_y=1100
cam=scene.camera
views={'hero':(.06,-.085,.065),'front':(0,-.10,.017),'right':(.10,0,.017),'back':(0,.10,.017),'left':(-.10,0,.017),'top':(0,0,.12),'rear-three-quarter':(-.065,.08,.06),'low-angle':(.06,-.08,.024)}
for name,loc in views.items():
    cam.location=loc;cam.rotation_euler=(Vector((0,0,.017))-cam.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
cam.location=views['hero'];cam.rotation_euler=(Vector((0,0,.017))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(PRE/'hero.png')
for im in bpy.data.images:
    if im.source=='FILE' and im.users:im.pack()
bpy.data.libraries.write(str(ROOT/'art/source/three-kingdoms-pieces/B201-v001/B201.blend'),{scene},fake_user=True,compress=True)
print(json.dumps({k:v for k,v in stats.items() if k!='逐组件'},ensure_ascii=False))
