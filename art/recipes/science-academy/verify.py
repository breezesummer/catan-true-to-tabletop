"""Blender MCP 回读交付的 FBX，按真实角点检查网格、材料分区、枢轴及穹顶透空。"""
import json
from math import isfinite
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

folder=str(Path(__file__).resolve().parent)
if folder not in sys.path:
    sys.path.insert(0,folder)
from finishes import ART, BLEND, PREVIEW, REGIONS, SIZE
from academy_mesh import WIDTH


def verify():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    source=bpy.context.scene
    parts={o.name:o for o in source.objects}
    assert len(parts)==18 and all(o.type=="MESH" for o in parts.values()),"源场景混入了建筑以外的对象"
    maps={n.image for o in parts.values() for mat in o.data.materials for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"}
    assert len(maps)==8 and all(image.packed_file for image in maps),"源文件未内嵌全部贴图"
    front=parts["学厅门廊与石阶"]
    side=parts["交错厚星环与刻度"]
    dome=parts["铜穹顶外板与咬边"]
    def extent(obj,axis):
        values=[v.co[axis] for v in obj.data.vertices]
        return min(values),max(values)
    assert extent(side,0)[0]>extent(dome,0)[1] and extent(front,1)[1]<extent(dome,1)[0]
    glass=parts["伸缩望远镜与物镜"]
    # 实际物镜表面朝正前方及右上方；在光轴上不应碰到铜壳或快门轨道。
    lens=[]
    for face in glass.data.polygons:
        if face.material_index!=1:
            continue
        u,v=glass.data.uv_layers[0].data[face.loop_start].uv
        l,t,w,h=REGIONS["lens"]
        if l<u*SIZE<l+w and t<(1-v)*SIZE<t+h:
            lens.append(face)
    assert len(lens)>=128
    optical_axis=Vector((.74,-.52,.44)).normalized()
    assert all(face.normal.dot(optical_axis)>.92 for face in lens)
    for key in ("铜穹顶外板与咬边","穹顶内衬与快门轨道"):
        data=parts[key].data
        data.calc_loop_triangles()
        tree=BVHTree.FromPolygons([v.co.copy() for v in data.vertices],
                                 [list(t.vertices) for t in data.loop_triangles],all_triangles=True)
        for face in lens[::16]:
            assert tree.ray_cast(face.center+optical_axis*.000003,optical_axis,.035)[0] is None,"穹顶挡住了物镜"
    scene=bpy.data.scenes.new("科学大都会 FBX 回读")
    bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(ART/"metro-science.fbx"),use_anim=False)
    assert len(scene.objects)==1 and scene.objects[0].type=="MESH"
    obj=scene.objects[0]
    mesh=obj.data
    mesh.calc_loop_triangles()
    assert len(mesh.uv_layers)==2 and len(mesh.materials)==2
    assert [m.name.split(".")[0] for m in mesh.materials]==["Expansion_Natural","Expansion_Seat"]
    points=[obj.matrix_world@v.co for v in mesh.vertices]
    lo=Vector(tuple(min(p[i] for p in points) for i in range(3)))
    hi=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    span=hi-lo
    assert abs(lo.z)<1e-7 and abs(lo.x+hi.x)<1e-7 and abs(lo.y+hi.y)<1e-7
    assert abs(span.x-WIDTH)<1e-6 and .98<span.z/span.x<1.12
    occupied=set()
    min_area=float("inf")
    floor_span=0
    atlas=mesh.uv_layers[0].data
    for tri in mesh.loop_triangles:
        a,b,c=[points[i] for i in tri.vertices]
        area=.5*(b-a).cross(c-a).length
        assert isfinite(area) and area>1e-16,"导出含退化三角面"
        min_area=min(min_area,area)
        if max(a.z,b.z,c.z)<1e-7:
            floor_span=max(floor_span,(a-b).length,(b-c).length,(c-a).length)
        for idx in tri.loops:
            u,v=atlas[idx].uv
            assert 0<u<1 and 0<v<1,"图集坐标越界"
        if tri.material_index==1:
            uv=sum((atlas[i].uv for i in tri.loops),Vector((0,0)))/3
            name=next(k for k,(l,t,w,h) in REGIONS.items() if l<=uv.x*SIZE<l+w and t<=(1-uv.y)*SIZE<t+h)
            occupied.add(name)
    assert floor_span<span.x*.65,"落地面跨越多个建筑"
    assert occupied==set(REGIONS),"仪器材质分区不完整"
    ao=[loop.uv.x for loop in mesh.uv_layers[1].data]
    assert 0<=min(ao)<.40 and .98<max(ao)<=1.00001
    report={"Blender":bpy.app.version_string,"可编辑分件":len(parts),"内嵌贴图":len(maps),
            "游戏网格":len(scene.objects),"三角面":len(mesh.loop_triangles),"材质槽":len(mesh.materials),
            "宽深高毫米":[round(v*1000,3) for v in span],"最低点毫米":round(lo.z*1000,5),
            "最小三角面平方毫米":round(min_area*1e6,10),"仪器图集分区":len(occupied),
            "物镜出射方向":list(optical_axis),"物镜被挡射线数":0,"天光可见度范围":[round(min(ao),3),round(max(ao),3)]}
    (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf8")
    return report


result=verify()
