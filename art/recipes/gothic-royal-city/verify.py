"""从正式 FBX 回读坐标、UV、材质与尺度，并检查分件源文件和六座塔的方位。"""
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

folder=str(Path(__file__).resolve().parent)
if folder in sys.path:
    sys.path.remove(folder)
sys.path.insert(0,folder)
from surfaces import ART, BLEND, PREVIEWS, REGIONS

bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
source=bpy.context.scene
materials={m for o in source.objects for m in o.data.materials}
maps={n.image for m in materials for n in m.node_tree.nodes if n.type=="TEX_IMAGE"}
assert len(materials)==2 and len(maps)==4 and all(img.packed_file for img in maps)
assert all(o.type=="MESH" for o in source.objects)
def highest(prefix):
    obj=next(o for o in source.objects if o.name.startswith(prefix))
    return max((obj.matrix_world@v.co for v in obj.data.vertices),key=lambda p:p.z)
great,needle,lantern,watch,west,east=map(highest,["西北主钟塔","东侧次高尖塔","交叉厅八角","东南瞭望塔","西南阶前","东南阶前"])
assert great.x<lantern.x<needle.x<watch.x and great.y>lantern.y>needle.y>watch.y>east.y
assert great.z>needle.z>lantern.z>watch.z>west.z>east.z

scene=bpy.data.scenes.new("王城 FBX 回读")
bpy.context.window.scene=scene
bpy.ops.import_scene.fbx(filepath=str(ART/"city.fbx"),use_anim=False)
assert len(scene.objects)==1
obj=scene.objects[0]
mesh=obj.data
assert obj.type=="MESH" and len(mesh.uv_layers)==2 and len(mesh.materials)==2
mesh.calc_loop_triangles()
points=[obj.matrix_world@v.co for v in mesh.vertices]
low=Vector(tuple(min(p[k] for p in points) for k in range(3)))
high=Vector(tuple(max(p[k] for p in points) for k in range(3)))
span=high-low
assert abs(max(span.x,span.y)-.0205)<1e-6 and .025<span.z<.030
assert abs(low.z)<1e-7 and abs(low.x+high.x)<1e-6 and abs(low.y+high.y)<1e-6
layer=mesh.uv_layers[0].data
exposure=[uv.uv.x for uv in mesh.uv_layers[1].data]
assert .0<=min(exposure)<.33 and .97<max(exposure)<=1.00001
minimum=float("inf")
occupied=set()
for tri in mesh.loop_triangles:
    p,q,r=[points[i] for i in tri.vertices]
    area=(q-p).cross(r-p).length*.5
    assert math.isfinite(area) and area>1e-16,"存在退化的导出三角面"
    minimum=min(area,minimum)
    roof="Natural_Player" in mesh.materials[tri.material_index].name
    for loop in tri.loops:
        u,v=layer[loop].uv
        assert math.isfinite(u) and math.isfinite(v) and 0<u<1 and 0<v<1
        assert roof==(u>.5 and v>.5),"玩家色跨出板岩分区"
        name=next(k for k,(x,y,s) in REGIONS.items() if x<=u*2048<x+s and y<=(1-v)*2048<y+s)
        occupied.add(name)
assert set(REGIONS)==occupied
result={"Blender":bpy.app.version_string,"源场景分件":len(source.objects),"内嵌贴图":len(maps),
        "FBX网格":len(scene.objects),"三角面":len(mesh.loop_triangles),"材质槽":len(mesh.materials),
        "宽深高毫米":[round(x*1000,3) for x in span],"底面高度毫米":round(low.z*1000,5),
        "最小三角面平方毫米":round(minimum*1e6,9),"UV分区":len(occupied),
        "天光可见度范围":[round(min(exposure),3),round(max(exposure),3)]}
PREVIEWS.mkdir(parents=True,exist_ok=True)
(PREVIEWS/"模型回读.json").write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding="utf-8")
