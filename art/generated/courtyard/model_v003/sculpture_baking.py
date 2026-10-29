"""雕塑形体修整、独立高模源、唯一 UV 与真实高模到成品的 Cycles 烘焙。"""
import bpy,bmesh,json,math
from pathlib import Path
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parent;S=bpy.context.scene
C=next(c for c in S.collection.children if c.name.startswith('庭院_高细节组件'))
H=bpy.data.collections.new('雕塑高模源_不导出');S.collection.children.link(H)
report={}
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
# 在连续主体上刻出真实口腔，而不是仅贴深色扁片。
for o in list(C.objects):
 if not o.name.startswith(('A18_','A19_')) or '连续石雕' not in o.name:continue
 x=-1.9 if o.name.startswith('A18_') else 2.5
 bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=24,location=(x,.53-.394,1.77+.35+.738));cut=bpy.context.object;cut.scale=(.145,.077,.032)
 active(o);mod=o.modifiers.new('石狮口腔雕刻','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut;bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
 for p in o.data.polygons:p.use_smooth=True
# 只烘焙两个原型，其余雕塑按刚体变换共享对应图集。
for prefix,label in [('A18_','lion'),('A38_','ornament')]:
 parts=[o for o in C.objects if o.name.startswith(prefix)];active(parts[0])
 for o in parts:o.select_set(True)
 bpy.ops.object.join();target=bpy.context.object;target.name=prefix+('石狮成品' if label=='lion' else '脊兽成品')
 # 保留完整细节，无面数削减目标；源模型细分承载亚毫米表面雕刻。
 high=target.copy();high.data=target.data.copy();high.name=label+'_高模雕刻源';H.objects.link(high)
 for i,m in enumerate(high.data.materials):high.data.materials[i]=m.copy() if m else next(mm for mm in high.data.materials if mm and ('sculpt' in mm.name or 'gold' in mm.name)).copy()
 mod=high.modifiers.new('微雕采样细分','SUBSURF');mod.subdivision_type='SIMPLE';mod.levels=1;mod.render_levels=1
 tex=bpy.data.textures.new(label+'_细微雕刻','CLOUDS');tex.noise_scale=.014 if label=='lion' else .008;tex.noise_depth=2
 mod=high.modifiers.new('微雕真实位移','DISPLACE');mod.texture=tex;mod.strength=.0009 if label=='lion' else .00035;mod.mid_level=.5;mod.texture_coords='GLOBAL'
 active(target)
 # 合并的体素主体与装饰构件使用唯一图集，避免投影平铺替代真实烘焙。
 target.data.uv_layers.active.name='DetailUV'
 uv=target.data.uv_layers.new(name='UVMap');target.data.uv_layers.active=uv;uv.active_render=True
 bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.004,area_weight=.25,correct_aspect=True,scale_to_bounds=True);bpy.ops.object.mode_set(mode='OBJECT')
 for i,m in enumerate(target.data.materials):target.data.materials[i]=m.copy() if m else next(mm for mm in target.data.materials if mm and ('sculpt' in mm.name or 'gold' in mm.name)).copy()
 for tm in target.data.materials:
  for node in tm.node_tree.nodes:
   if node.type in ['UVMAP','NORMAL_MAP'] and node.uv_map=='UVMap':node.uv_map='DetailUV'
 imgs={};S.render.engine='CYCLES';S.cycles.samples=24;S.render.bake.use_selected_to_active=True;S.render.bake.cage_extrusion=.001;S.render.bake.max_ray_distance=.003;S.render.bake.margin=24;S.render.bake.use_clear=False
 S.render.bake.use_pass_direct=False;S.render.bake.use_pass_indirect=False;S.render.bake.use_pass_color=True
 for suffix,typ in [('basecolor','EMIT'),('roughness','ROUGHNESS'),('normal','NORMAL')]:
  img=bpy.data.images.new(label+'_atlas_'+suffix,width=4096,height=4096,alpha=False);img.colorspace_settings.name='sRGB' if suffix=='basecolor' else 'Non-Color';imgs[suffix]=img
  img.generated_color=(.5,.5,1,1) if suffix=='normal' else (.5,.5,.5,1) if suffix=='roughness' else (.76,.75,.73,1) if label=='lion' else (.83,.61,.32,1)
  for mat in target.data.materials:
   n=mat.node_tree.nodes.new('ShaderNodeTexImage');n.image=img;mat.node_tree.nodes.active=n
  emission_nodes=[]
  if typ=='EMIT':
   for hm in high.data.materials:
    nn=hm.node_tree.nodes;ll=hm.node_tree.links;hp=nn.get('Principled BSDF');em=nn.new('ShaderNodeEmission')
    if hp.inputs['Base Color'].is_linked:ll.new(hp.inputs['Base Color'].links[0].from_socket,em.inputs[0])
    else:em.inputs[0].default_value=hp.inputs['Base Color'].default_value
    ll.new(em.outputs[0],nn.get('Material Output').inputs['Surface']);emission_nodes.append((hm,em))
  S.render.bake.use_selected_to_active=typ=='NORMAL'
  active(target)
  if typ=='NORMAL':high.select_set(True)
  # 颜色及粗糙度使用同一原始材质在目标DetailUV采样，避免交叠处射线污染。
  target_emissions=[]
  if typ=='EMIT':
   for tm in target.data.materials:
    nn=tm.node_tree.nodes;ll=tm.node_tree.links;pp=nn.get('Principled BSDF');em=nn.new('ShaderNodeEmission')
    if pp.inputs['Base Color'].is_linked:ll.new(pp.inputs['Base Color'].links[0].from_socket,em.inputs[0])
    else:em.inputs[0].default_value=pp.inputs['Base Color'].default_value
    ll.new(em.outputs[0],nn.get('Material Output').inputs['Surface']);target_emissions.append((tm,em))
  bpy.ops.object.bake(type=typ)
  for tm,em in target_emissions:tm.node_tree.links.new(tm.node_tree.nodes.get('Principled BSDF').outputs[0],tm.node_tree.nodes.get('Material Output').inputs['Surface']);tm.node_tree.nodes.remove(em)
  for hm,em in emission_nodes:hm.node_tree.links.new(hm.node_tree.nodes.get('Principled BSDF').outputs[0],hm.node_tree.nodes.get('Material Output').inputs['Surface']);hm.node_tree.nodes.remove(em)
  img.filepath_raw=str(ROOT/'textures'/f'{label}_atlas_{suffix}.png');img.file_format='PNG';img.save();img.pack()
 mat=bpy.data.materials.new('v003_'+label+'_烘焙图集');mat.use_nodes=True;p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=0 if label=='lion' else .88
 for suffix,inputname in [('basecolor','Base Color'),('roughness','Roughness'),('normal','Normal')]:
  t=mat.node_tree.nodes.new('ShaderNodeTexImage');t.image=imgs[suffix]
  if suffix=='normal':
   n=mat.node_tree.nodes.new('ShaderNodeNormalMap');n.uv_map='UVMap';mat.node_tree.links.new(t.outputs[0],n.inputs['Color']);mat.node_tree.links.new(n.outputs[0],p.inputs[inputname])
  else:mat.node_tree.links.new(t.outputs[0],p.inputs[inputname])
 target.data.materials.clear();target.data.materials.append(mat)
 for p in target.data.polygons:p.material_index=0
 high.hide_render=True;high.hide_set(True)
 report[label]={'成品三角面':sum(len(p.vertices)-2 for p in target.data.polygons),'图集尺寸':4096,'图集数量':3,'射线距离':.003,'笼外扩':.001,'高模细分级别':1,'高模位移强度':.0009 if label=='lion' else .00035}
 if label=='lion':placements=[('A19_',Vector((4.4,0,0)),1,False)]
 else:placements=[('A39_',Vector((3.50,2.70,6.23+.35)),1,True),('A40_',Vector((-1.40,-4.55,4.61+.35)),.48/.75,False),('A41_',Vector((1.40,-4.55,4.61+.35)),.48/.75,True)]
 for dest,offset,scale,flip in placements:
  for ob in list(C.objects):
   if ob.name.startswith(dest):bpy.data.objects.remove(ob,do_unlink=True)
  ob=target.copy();ob.data=target.data.copy();ob.name=dest+('石狮成品' if label=='lion' else '脊兽成品');C.objects.link(ob)
  for v in ob.data.vertices:
   if label=='lion':v.co+=offset
   else:
    local=(v.co-Vector((-2.90,2.70,6.23+.35)))*scale
    if flip:local.x=-local.x;local.y=-local.y
    v.co=offset+local
S.render.bake.use_selected_to_active=False
H.hide_render=True
(ROOT/'sculpture_bake_report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
bpy.data.libraries.write(str(ROOT/'courtyard_high.blend'),{S},fake_user=True,compress=True)
result=report
