"""通过Blender MCP检查高细节资产、保存源文件并导出可交换模型。"""
import bpy,bmesh,json,hashlib,struct
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
S=bpy.context.scene
C=next(c for c in S.collection.children if c.name.startswith('庭院_高细节组件'))
objects=list(C.objects)
for o in objects:
 if o.name.startswith(('A18_','A19_','A38_','A39_','A40_','A41_')) and 'DetailUV' in o.data.uv_layers:o.data.uv_layers.remove(o.data.uv_layers['DetailUV'])
report={'三角面上限':None,'非流形边':0,'零面积面':0,'完全重合面':0,'缺少UV':[],'缺失贴图':[],'各组件':{},'说明':'按本次用户要求不限制面数；完全重合面以六位小数世界坐标判定，不等同于全局体积无相交。'}
seen=set()
for o in objects:
 bm=bmesh.new();bm.from_mesh(o.data);report['非流形边']+=sum(not e.is_manifold for e in bm.edges);report['零面积面']+=sum(f.calc_area()<1e-10 for f in bm.faces);bm.free()
 if not o.data.uv_layers:report['缺少UV'].append(o.name)
 keys=[tuple(round(x,6) for x in o.matrix_world@v.co) for v in o.data.vertices]
 for p in o.data.polygons:
  # 保存紧凑摘要，避免百万面检查额外占用大量内存。
  key=hashlib.blake2b(repr(tuple(sorted(keys[i] for i in p.vertices))).encode(),digest_size=16).digest()
  if key in seen:report['完全重合面']+=1
  seen.add(key)
 report['各组件'][o.name]=sum(len(p.vertices)-2 for p in o.data.polygons)
for o in objects:
 for mat in o.data.materials:
  for n in mat.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:
    if not n.image.packed_file and not Path(bpy.path.abspath(n.image.filepath)).exists():report['缺失贴图'].append(n.image.filepath)
    n.image.pack()
report['三角面总数']=sum(report['各组件'].values());report['组件数']=len(objects)
assert report['非流形边']==report['零面积面']==report['完全重合面']==0,report
assert not report['缺少UV'] and not report['缺失贴图'],report
# 固定交付源文件的相机与画质，摄影棚不纳入导出。
S.camera.location=(10,-22,20);S.camera.rotation_euler=(Vector((0,0,1.9))-S.camera.location).to_track_quat('-Z','Y').to_euler();S.camera.data.ortho_scale=15.2
S.render.resolution_x=2048;S.render.resolution_y=2048;S.cycles.samples=64;S.render.filepath=str(ROOT/'renders'/'front.png')
bpy.data.libraries.write(str(ROOT/'courtyard_high.blend'),{S},fake_user=True,compress=True)
for o in S.objects:o.select_set(False)
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.gltf(filepath=str(ROOT/'courtyard_high.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_cameras=False,export_lights=False,export_texcoords=True,export_normals=True,export_materials='EXPORT')
bpy.ops.export_scene.fbx(filepath=str(ROOT/'courtyard_high.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True,add_leaf_bones=False,bake_anim=False)
b=(ROOT/'courtyard_high.glb').read_bytes();n,t=struct.unpack_from('<II',b,12);glb=json.loads(b[20:20+n]);report['GLB场景数']=len(glb['scenes']);report['GLB网格数']=len(glb['meshes']);report['GLB三角面']=sum(glb['accessors'][p['indices']]['count']//3 for m in glb['meshes'] for p in m['primitives']);report['GLB图像内嵌']=all('bufferView' in i for i in glb.get('images',[]));report['GLB材质数']=len(glb['materials'])
assert report['GLB三角面']==report['三角面总数'] and report['GLB场景数']==1
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
(ROOT/'mesh_stats.json').write_text(json.dumps({'三角面总数':report['三角面总数'],'组件数':report['组件数'],'各组件':report['各组件']},ensure_ascii=False,indent=2))
result=report
