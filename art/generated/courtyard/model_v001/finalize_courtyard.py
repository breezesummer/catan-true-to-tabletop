"""通过 Blender MCP 在刚生成的庭院场景内执行：检查、归并、导出和四向渲染。"""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
S=bpy.context.scene
C=next(c for c in S.collection.children if c.name.startswith('庭院组件'))
mesh_objects=[o for o in C.objects if o.type=='MESH']
report={'三角面上限':6000,'非流形边':0,'零面积面':0,'重复面':0,'缺少UV对象':[],'缺失贴图':[],'各组件':{},'说明':'重复面以顶点世界坐标完全一致判定；构件之间的隐藏榫接不等于表面穿插检测。'}
seen=set()
for o in mesh_objects:
 bm=bmesh.new();bm.from_mesh(o.data);report['非流形边']+=sum(not e.is_manifold for e in bm.edges);report['零面积面']+=sum(f.calc_area()<1e-9 for f in bm.faces);bm.free()
 if not o.data.uv_layers:report['缺少UV对象'].append(o.name)
 for p in o.data.polygons:
  key=tuple(sorted(tuple(round(v,6) for v in o.matrix_world@o.data.vertices[i].co) for i in p.vertices))
  if key in seen:report['重复面']+=1
  seen.add(key)
for image in bpy.data.images:
 if image.source=='FILE' and image.users and not image.packed_file and not Path(bpy.path.abspath(image.filepath)).exists():report['缺失贴图'].append(image.filepath)
# 按原始资产 ID 合并，保留43个可选组件，材质与UV不变。
groups={}
for o in mesh_objects:groups.setdefault(o.name.split('_')[0],[]).append(o)
for key,group in sorted(groups.items()):
 bpy.ops.object.select_all(action='DESELECT')
 for o in group:o.select_set(True)
 bpy.context.view_layer.objects.active=group[0];
 if len(group)>1:bpy.ops.object.join()
 o=bpy.context.object;o.name=key
 report['各组件'][key]=sum(len(p.vertices)-2 for p in o.data.polygons)
report['三角面总数']=sum(report['各组件'].values());report['组件数']=len(report['各组件'])
assert report['三角面总数']<=6000,report
assert report['非流形边']==report['零面积面']==report['重复面']==0,report
assert not report['缺少UV对象'] and not report['缺失贴图'],report
# 运行时使用单一静态网格，减少对象与材质提交开销。
runtime=bpy.data.scenes.new('庭院_运行时导出');bpy.context.window.scene=runtime
for source in C.objects:
 o=source.copy();o.data=source.data.copy();runtime.collection.objects.link(o)
bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=next(iter(runtime.objects));bpy.ops.object.join();o=bpy.context.object;o.name='Courtyard_5998tris';o.location=(0,0,0)
# 明确三角化后导出，FBX与GLB拥有相同索引拓扑。
m=o.modifiers.new('游戏三角化','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=m.name)
report['导出三角面']=len(o.data.polygons);report['运行时网格数']=1;report['运行时材质数']=len(o.data.materials)
bpy.ops.export_scene.gltf(filepath=str(ROOT/'courtyard.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_cameras=False,export_lights=False)
bpy.ops.export_scene.fbx(filepath=str(ROOT/'courtyard.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='COPY',embed_textures=True,add_leaf_bones=False,bake_anim=False)
bpy.context.window.scene=S
# 所需图像打包，避免依赖临时目录或绝对路径。
for o in C.objects:
 for m in o.data.materials:
  if not m or not m.use_nodes:continue
  for n in m.node_tree.nodes:
   if n.type=='TEX_IMAGE' and n.image:n.image.pack()
S.camera.location=(8,-17,16);S.camera.rotation_euler=(Vector((0,0,1.8))-S.camera.location).to_track_quat('-Z','Y').to_euler()
bpy.data.libraries.write(str(ROOT/'courtyard.blend'),{S},fake_user=True,compress=True)
(ROOT/'validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
for name,loc in [('front',(8,-17,16)),('rear',(-11,17,13)),('left',(-16,-8,11)),('right',(17,8,12)),('top',(0,0,24))]:
 S.camera.location=loc;S.camera.rotation_euler=(Vector((0,0,1.8))-S.camera.location).to_track_quat('-Z','Y').to_euler();S.render.filepath=str(ROOT/'renders'/f'{name}.png');bpy.ops.render.render(write_still=True)
S.camera.location=(8,-17,16);S.camera.rotation_euler=(Vector((0,0,1.8))-S.camera.location).to_track_quat('-Z','Y').to_euler()
result=report
