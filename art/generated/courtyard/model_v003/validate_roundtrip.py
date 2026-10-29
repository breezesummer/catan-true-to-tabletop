"""通过 Blender MCP 后台独立进程重新读取源文件并导入交付模型。"""
import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
C=next(c for c in bpy.context.scene.collection.children if c.name.startswith('庭院_高细节组件'))
def stats(objects):
 lo=[float('inf')]*3;hi=[-float('inf')]*3;tri=0
 for o in objects:
  tri+=sum(len(p.vertices)-2 for p in o.data.polygons)
  for corner in o.bound_box:
   p=o.matrix_world@Vector(corner)
   for i in range(3):lo[i]=min(lo[i],p[i]);hi[i]=max(hi[i],p[i])
 return {'网格':len(objects),'三角面':tri,'UV完整':all(o.data.uv_layers for o in objects),'包围盒':[lo,hi]}
report={'源文件':stats(list(C.objects)),'源场景数':len(bpy.data.scenes)}
for ext in ['glb','fbx']:
 S=bpy.data.scenes.new('独立导入_'+ext);bpy.context.window.scene=S
 if ext=='glb':bpy.ops.import_scene.gltf(filepath=str(ROOT/'courtyard_high.glb'))
 else:bpy.ops.import_scene.fbx(filepath=str(ROOT/'courtyard_high.fbx'))
 bpy.context.view_layer.update();objects=[o for o in S.objects if o.type=='MESH'];report[ext]=stats(objects)
 assert report[ext]['网格']==report['源文件']['网格'] and report[ext]['三角面']==report['源文件']['三角面'] and report[ext]['UV完整']
 assert max(abs(a-b) for aa,bb in zip(report[ext]['包围盒'],report['源文件']['包围盒']) for a,b in zip(aa,bb))<.0001
report['缺失图片']=[i.filepath for i in bpy.data.images if i.source=='FILE' and not i.packed_file and not Path(bpy.path.abspath(i.filepath)).exists()]
assert not report['缺失图片']
(ROOT/'roundtrip_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2));result=report
