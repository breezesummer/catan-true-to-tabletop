"""复用已确认形体和UV，单独重烘焙图集，避免调整材质时重建模型。"""
import bpy,json
from pathlib import Path
ROOT=Path(__file__).resolve().parent;S=bpy.context.scene
C=next(c for c in S.collection.children if c.name.startswith('庭院_高细节组件'));H=next(c for c in S.collection.children if c.name.startswith('雕塑高模源'))
def active(o):
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
source=(ROOT/'sculpture_baking.py').read_text();chunk=source[source.index(' imgs={};'):source.index(' report[label]=')];chunk='\n'.join(line[1:] for line in chunk.splitlines())
for prefix,label in [('A18_','lion'),('A38_','ornament')]:
 target=next(o for o in C.objects if o.name.startswith(prefix));high=next(o for o in H.objects if o.name.startswith(label));H.hide_render=False;high.hide_render=False;high.hide_set(False)
 if 'DetailUV' not in target.data.uv_layers:
  old=target.data.uv_layers.new(name='DetailUV');old.data.foreach_set('uv',[v for d in high.data.uv_layers[0].data for v in d.uv])
 target.data.uv_layers.active=target.data.uv_layers['UVMap'];target.data.uv_layers['UVMap'].active_render=True
 target.data.materials.clear()
 for hm in high.data.materials:
  tm=hm.copy();target.data.materials.append(tm)
  for node in tm.node_tree.nodes:
   if node.type in ['UVMAP','NORMAL_MAP'] and node.uv_map=='UVMap':node.uv_map='DetailUV'
 for p,hp in zip(target.data.polygons,high.data.polygons):p.material_index=hp.material_index
 exec(compile(chunk,'雕塑图集重烘焙','exec'))
 for o in C.objects:
  if o.name.startswith(('A18_','A19_') if label=='lion' else ('A38_','A39_','A40_','A41_')):
   o.data.materials.clear();o.data.materials.append(mat)
H.hide_render=True;S.render.bake.use_selected_to_active=False
bpy.data.libraries.write(str(ROOT/'courtyard_high.blend'),{S},fake_user=True,compress=True)
result={'重烘焙':True,'颜色通道':'EMIT，不含灯光','笼外扩':.001,'射线距离':.003}
