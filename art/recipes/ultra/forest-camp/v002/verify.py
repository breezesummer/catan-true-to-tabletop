"""独立读取落盘源文件，检查真实几何、UV和引用。通过Blender MCP运行。"""
import bpy,json,math,hashlib
from pathlib import Path
root=Path(__file__).resolve().parents[5];objects=[o for o in bpy.context.scene.objects if o.type=='MESH' and '模块' in o]
missing=[];bad_uv=[];points=[];triangles=0;outside=[]
for im in bpy.data.images:
 if im.source=='FILE' and not im.packed_file and not Path(bpy.path.abspath(im.filepath)).is_file():missing.append(im.name)
for o in objects:
 o.data.calc_loop_triangles();triangles+=len(o.data.loop_triangles)
 if not o.data.uv_layers:bad_uv.append(o.name)
 else:
  for uv in o.data.uv_layers.active.data:
   if any(not math.isfinite(v) or v<0 or v>1 for v in uv.uv):bad_uv.append(o.name);break
 points.extend(o.matrix_world@v.co for v in o.data.vertices)
 for v in o.data.vertices:
  p=o.matrix_world@v.co
  if max(p.x*math.cos(math.pi/6+k*math.pi/3)+p.y*math.sin(math.pi/6+k*math.pi/3) for k in range(6))>.0395*math.cos(math.pi/6)+1e-7:outside.append(o.name);break
report={'源三角形':triangles,'缺失纹理':missing,'UV异常':bad_uv,'包围盒米':[[min(p[k] for p in points) for k in range(3)],[max(p[k] for p in points) for k in range(3)]],'参考SHA256':hashlib.sha256((root/'assets/prototypes/ultra/forest-extraction/v001/reference.png').read_bytes()).hexdigest(),'工具枢轴米':{o.name:list(o.location) for o in objects if o.name.startswith(('P06','P07'))}}
report['越界对象']=outside
assert not missing and not bad_uv and not outside,report
(root/'art/exports/ultra/forest-camp/v002/independent-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('独立审计通过',triangles,report['包围盒米'])
