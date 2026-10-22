"""独立读取保存后的源文件，检查可移植性、总面数、几何尺寸与边界。"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[5]
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
total=0;missing=[];invalid_uv=0;points=[];clearance=[]
for im in bpy.data.images:
 if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).exists():missing.append(im.name)
for o in objects:
 o.data.calc_loop_triangles();total+=len(o.data.loop_triangles)
 for uv in o.data.uv_layers.active.data:
  if any(not math.isfinite(t) or t<-.0001 or t>1.0001 for t in uv.uv):invalid_uv+=1
 for v in o.data.vertices:
  p=o.matrix_world@v.co;points.append(p)
  if o.get('模块')!='地块':
   clearance.append(min(.0395*math.cos(math.pi/6)-(p.x*math.cos(math.pi/6+k*math.pi/3)+p.y*math.sin(math.pi/6+k*math.pi/3)) for k in range(6)))
report={'源文件总三角形':total,'缺失外部文件':missing,'非法UV坐标':invalid_uv,'包围盒米':[[min(p[k] for p in points) for k in range(3)],[max(p[k] for p in points) for k in range(3)]],'装饰到六边形边界最小投影距离米':min(clearance),'贴图':[{ '名称':im.name,'尺寸':list(im.size),'已打包':bool(im.packed_file)} for im in bpy.data.images if im.source=='FILE']}
(root/'art/exports/ultra/forest-camp/v001/independent-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
assert total<=6000 and not missing and invalid_uv==0 and min(clearance)>=.003,report
print('FOREST_INDEPENDENT_AUDIT_OK',report)
