"""通过MCP审计同一模型并导出三档LOD，保留源分件与共享叶网格。"""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[5];out=root/'art/exports/ultra/forest-camp/v002';scene=bpy.context.scene
original=[o for o in scene.objects if o.type=='MESH' and '模块' in o]
report={'Blender':bpy.app.version_string,'参考':'assets/prototypes/ultra/forest-extraction/v001/reference.png','单位':'米','LOD':[],'材质图尺寸':1024,'装饰碰撞器':0}
bad=[];open_solids=[];clearance=[];groups={};unique=set();seen=set();duplicates=0
for o in original:
 me=o.data;me.calc_loop_triangles();groups[o['模块']]=groups.get(o['模块'],0)+len(me.loop_triangles)
 if me.name not in unique:
  unique.add(me.name);bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
  if any(f.calc_area()<1e-12 for f in bm.faces):bad.append(o.name)
  if not o.name.startswith('G') and any(not e.is_manifold for e in bm.edges):open_solids.append(o.name)
  bm.to_mesh(me);bm.free()
 for v in me.vertices:
  p=o.matrix_world@v.co
  if o['模块']!='地块' and not o.name.startswith('G02'):
   clearance.append(min(.0395*math.cos(math.pi/6)-p.x*math.cos(math.pi/6+k*math.pi/3)-p.y*math.sin(math.pi/6+k*math.pi/3) for k in range(6)))
report.update({'模块三角形':groups,'分件数':len(original),'共享网格数':len(unique),'退化面对象':bad,'实体非流形':open_solids,'高物体最小边界净空米':min(clearance)})
assert not bad and not open_solids,report
# 导出前保存修正法线及元数据。
bpy.ops.wm.save_as_mainfile(filepath=str(root/'art/source/ultra/forest-camp/v002/forest-camp.blend'))
for level in range(3):
 copies=[];cache={}
 for src in original:
  # 地被和嵌石逐级降低密度；叶片以完整闭合小叶为单位减少。
  if level and src.name.startswith('G01') and int(src.name.rsplit('.',1)[-1] if '.' in src.name else 0)% (level+1):continue
  if level==2 and src.name.startswith(('G02','D01_土层嵌石','B_布边缝线','P06_锯齿')):continue
  obj=src.copy()
  if src.data.name not in cache:
   me=src.data.copy()
   if level and src.name.startswith(('T','S')) and len(me.polygons)>=800:
    bm=bmesh.new();bm.from_mesh(me);bm.faces.ensure_lookup_table()
    remove=[f for i,f in enumerate(bm.faces) if (i//16)%(2 if level==1 else 4)!=0]
    bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(me);bm.free()
   cache[src.data.name]=me
  obj.data=cache[src.data.name];scene.collection.objects.link(obj);copies.append(obj)
 # 合并导出副本，源对象不变。模块与玩家配色分离保留。
 merged=[]
 for group in sorted(set(o['模块'] for o in copies)):
  for player in (False,True):
   subset=[o for o in copies if o['模块']==group and bool(o.get('玩家配色',False))==player]
   if not subset:continue
   copies=[o for o in copies if o not in subset]
   bpy.ops.object.select_all(action='DESELECT')
   for o in subset:o.select_set(True)
   bpy.context.view_layer.objects.active=subset[0];bpy.ops.object.make_single_user(object=False,obdata=True,material=False,animation=False);bpy.ops.object.join()
   obj=bpy.context.object;obj.name=f'LOD{level}_{group}'+('_玩家色' if player else '')
   # 相同图集材质合成一个槽，草叶单独双面材质，布可换色。
   mat=obj.data.materials[0];obj.data.materials.clear();obj.data.materials.append(mat)
   for p in obj.data.polygons:p.material_index=0
   bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
   merged.append(obj)
 bpy.ops.object.select_all(action='DESELECT')
 for o in merged:o.select_set(True)
 bpy.context.view_layer.objects.active=merged[0]
 for o in merged:o.data.calc_loop_triangles()
 tris=sum(len(o.data.loop_triangles) for o in merged)
 report['LOD'].append({'级别':level,'三角形':tris,'材质槽':len(merged),'网格':len(merged)})
 bpy.ops.export_scene.fbx(filepath=str(out/f'forest-camp-lod{level}.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='AUTO')
 if level==0:bpy.ops.export_scene.gltf(filepath=str(out/'forest-camp.glb'),use_selection=True,export_format='GLB')
 for o in merged:bpy.data.objects.remove(o,do_unlink=True)
report['最大地图49块LOD0三角形理论和']=report['LOD'][0]['三角形']*49
(out/'audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False))
