"""回读棋盘 FBX。检查拱洞、材质槽、定向 UV、四套城内建筑与地上墙体的真实三角面。"""
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from wall_maps import ART, PREVIEW, REGIONS, SOURCE
from enclosure import X, Y, GRADE


def imported(path):
    bpy.ops.import_scene.fbx(filepath=str(path),use_anim=False)
    obj=next(o for o in bpy.context.selected_objects if o.type=="MESH")
    return obj, [obj.matrix_world@v.co for v in obj.data.vertices]


def bvh(mesh, points, indices=None):
    mesh.calc_loop_triangles()
    faces=[tuple(t.vertices) for t in mesh.loop_triangles] if indices is None else indices
    return BVHTree.FromPolygons(points,faces,all_triangles=True)


def verify():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False)
    parts=list(bpy.context.scene.objects)
    assert len(parts)==19 and all(o.type=="MESH" for o in parts)
    images={n.image for o in parts for m in o.data.materials for n in m.node_tree.nodes if n.type=="TEX_IMAGE"}
    assert len(images)==4 and all(i.packed_file for i in images)
    bpy.context.window.scene=bpy.data.scenes.new("棋盘 FBX 回读")
    wall,points=imported(ART/"wall.fbx")
    mesh=wall.data
    mesh.calc_loop_triangles()
    assert len(mesh.uv_layers)==2 and len(mesh.materials)==2
    for tri in mesh.loop_triangles:
        a,b,c=(points[i] for i in tri.vertices)
        assert (b-a).cross(c-a).length*.5>1e-16
    first=mesh.uv_layers[0].data
    assert all(0<v.uv.x<1 and 0<v.uv.y<1 for v in first)
    contact=mesh.uv_layers[1].data
    exposure=[u.uv.x for u in contact]
    assert 0<=min(exposure)<.5 and .98<max(exposure)<=1.00001
    u,v,s=REGIONS["cloth"]
    flag_faces=0
    for face in mesh.polygons:
        loops=[first[i].uv for i in face.loop_indices]
        in_cloth=all(u<=p.x*2048<=u+s and v<=(1-p.y)*2048<=v+s for p in loops)
        player="Seat" in mesh.materials[face.material_index].name
        assert in_cloth==player
        flag_faces+=int(player)
    assert 0<flag_faces<len(mesh.polygons)*.1
    tree=bvh(mesh,points)
    for x,z in ((0,1.12),(0,4.9),(1.78,2.1),(-1.78,2.1)):
        assert tree.ray_cast(Vector((x*.001,-.015,z*.001)),Vector((0,1,0)),.006)[0] is None
    for x in (-3.26,3.26):
        assert tree.ray_cast(Vector((x*.001,-.015,.003)),Vector((0,1,0)),.006)[0] is not None
    fits=[]
    for label,kind,span,sz,insert,forward in (
        ("哥特王城","city",1,1,1,.25),("哥特商会","metro-trade",1.40,1.14,1,2.35),
        ("冠冕议政宫","metro-politics",1.21,1.14,.76,.25),("观星学宫","metro-science",1.40,1.14,1,1.70)):
        shell=[Vector((p.x*span,p.y*span,p.z*sz)) for p in points]
        # 石基、铺石和庭土是合法的承托面。保留全部露出院地的墙、拱腹、塔及旗杆三角面。
        exposed=[tuple(t.vertices) for t in mesh.loop_triangles if
                 max(shell[i].z for i in t.vertices)>.00081*sz]
        enclosure=bvh(mesh,shell,exposed)
        path=ART if kind!="city" else ART.parents[1]/"NaturalPieces/v001"
        body,verts=imported(path/(kind+".fbx"))
        vertices=[Vector((p.x*insert,p.y*insert-forward*.001,p.z*insert+GRADE*.001*sz)) for p in verts]
        intersections=len(enclosure.overlap(bvh(body.data,vertices)))
        margins=[(X-.78)*span-max(abs(p.x)*1000 for p in vertices),
                 (Y-.78)*span-max(p.y*1000 for p in vertices),
                 (Y-.78)*span+min(p.y*1000 for p in vertices)]
        assert min(margins)>0 and intersections==0, (label,margins,intersections)
        fits.append({"建筑":label,"墙体相交面对":intersections,
                     "侧后前直墙保守净距毫米":[round(x,3) for x in margins]})
    report={"Blender":bpy.app.version_string,"可编辑分件":len(parts),"打包贴图":len(images),
            "运行三角面":len(mesh.polygons),"退化面":0,"着色门旗三角面":flag_faces,
            "UV越界":0,"天光可见度":[round(min(exposure),3),round(max(exposure),3)],
            "门洞射线通过":4,"门墩命中":2,"组装":fits}
    PREVIEW.mkdir(parents=True,exist_ok=True)
    (PREVIEW/"合围回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    return report


result=verify()
