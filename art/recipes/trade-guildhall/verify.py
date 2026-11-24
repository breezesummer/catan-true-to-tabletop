"""通过 Blender MCP 回读正式 FBX，并把该件的实测尺寸写回 Unity 导入清单。"""
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

recipe=Path(__file__).resolve().parent
sys.path.insert(0,str(recipe))
from guild_maps import ART, BLEND, PREVIEWS, REGIONS, SPAN


def extent(points):
    return tuple(Vector(tuple(fn(p[k] for p in points) for k in range(3))) for fn in (min,max))


def check():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    source=bpy.context.scene
    parts=list(source.objects)
    assert all(obj.type=="MESH" for obj in parts)
    materials={m for obj in parts for m in obj.data.materials}
    maps={n.image for mat in materials for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"}
    assert len(materials)==2 and len(maps)==4 and all(im.packed_file for im in maps)
    def part(prefix):
        return next(o for o in parts if o.name.startswith(prefix))
    h=[extent([v.co for v in part(name).data.vertices])[1] for name in
       ("西北方钟楼","东北八角","西商馆","中央商馆","东商馆","东侧橡木吊架")]
    assert h[0].z>h[1].z>h[3].z>h[2].z and abs(h[2].z-h[4].z)<1e-6
    assert h[0].x<h[1].x<h[5].x and h[0].y>h[2].y and h[1].y>h[4].y
    galleries=[]
    for label in ("西商馆","中央商馆","东商馆"):
        mesh=part(label).data
        pts=[v.co for v in mesh.vertices]
        lo,hi=extent(pts)
        mesh.calc_loop_triangles()
        tree=BVHTree.FromPolygons(pts,[tri.vertices for tri in mesh.loop_triangles],all_triangles=True)
        # 沿街从同一深度照向门柱和拱心；验证两者之间有可进入的廊下空间。
        center=(lo.x+hi.x)*.5
        def hit(x):
            dist=tree.ray_cast(Vector((x,lo.y-.0004,.00155)),Vector((0,1,0)),.008)[3]
            assert dist is not None
            return dist
        clear=hit(center)-hit(center+(hi.x-lo.x)*.448)
        assert clear>.0024
        galleries.append(round(clear*1000,2))
    scene=bpy.data.scenes.new("贸易大都会 FBX 回读")
    bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(ART/"metro-trade.fbx"),use_anim=False)
    assert len(scene.objects)==1
    obj=scene.objects[0]
    mesh=obj.data
    assert obj.type=="MESH" and len(mesh.materials)==2 and len(mesh.uv_layers)==2
    points=[obj.matrix_world@v.co for v in mesh.vertices]
    lo,hi=extent(points)
    dims=hi-lo
    assert abs(lo.x+hi.x)<1e-6 and abs(lo.y+hi.y)<1e-6 and abs(lo.z)<1e-7
    assert abs(max(dims.x,dims.y)-SPAN)<1e-6 and 1.01<dims.z/dims.x<1.08 and .78<dims.y/dims.x<.86
    mesh.calc_loop_triangles()
    occupied=set()
    uv=mesh.uv_layers[0].data
    for tri in mesh.loop_triangles:
        a,b,c=(points[i] for i in tri.vertices)
        area=(b-a).cross(c-a).length*.5
        assert math.isfinite(area) and area>1e-16
        roof="Expansion_Seat" in mesh.materials[tri.material_index].name
        for loop in tri.loops:
            u,v=uv[loop].uv
            assert 0<u<1 and 0<v<1 and roof==(u>.5 and v>.5)
            occupied.add(next(name for name,(x,y,s) in REGIONS.items()
                              if x<=u*2048<x+s and y<=(1-v)*2048<y+s))
    assert occupied==set(REGIONS)
    exposure=[v.uv.x for v in mesh.uv_layers[1].data]
    assert 0<=min(exposure)<.38 and .98<max(exposure)<=1.00001
    fact={"kind":"metro-trade","triangles":len(mesh.loop_triangles),"size":list(dims)}
    manifest=recipe.parent/"expansion-models-v001/几何.json"
    items=json.loads(manifest.read_text(encoding="utf-8"))
    assert sum(i["kind"]==fact["kind"] for i in items)==1
    for i in items:
        if i["kind"]==fact["kind"]:
            i.update(fact)
    manifest.write_text(json.dumps(items,indent=2),encoding="utf-8")
    report={"Blender":bpy.app.version_string,"源场景分件":len(parts),"内嵌贴图":len(maps),
            "共享网格":1,"材质槽":len(mesh.materials),"三角面":len(mesh.loop_triangles),
            "宽深高毫米":[round(x*1000,3) for x in dims],"底面高度毫米":round(lo.z*1000,5),
            "三座拱廊净深毫米":galleries,"表面分区":len(occupied),
            "天光可见度范围":[round(min(exposure),3),round(max(exposure),3)]}
    PREVIEWS.mkdir(parents=True,exist_ok=True)
    (PREVIEWS/"模型回读.json").write_text(json.dumps(report,indent=2,ensure_ascii=False),encoding="utf-8")
    return report


result=check()
