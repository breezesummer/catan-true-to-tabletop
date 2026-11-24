"""回读正式 FBX，核查开口、图集、底心枢轴及带墙组合的实体间隙。"""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

sys.path.insert(0,str(Path(__file__).resolve().parent))
from finishes import ART, BLEND, KIND, SPAN, VIEWS_DIR
from palace import TOWER_X,TOWER_Y
from surfaces import REGIONS


def tree(objects,scale=(1,1,1),height=0,min_z=None,forward=0):
    vs,faces=[],[]
    for obj in objects:
        offset=len(vs)
        for v in obj.data.vertices:
            p=obj.matrix_world@v.co
            vs.append((p.x*scale[0],p.y*scale[1]-forward,p.z*scale[2]+height))
        obj.data.calc_loop_triangles()
        for tri in obj.data.loop_triangles:
            ids=tuple(offset+i for i in tri.vertices)
            if min_z is None or max(vs[k][2] for k in ids)>min_z:
                faces.append(ids)
    return BVHTree.FromPolygons(vs,faces,all_triangles=True)


def verify():
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    source=list(bpy.context.scene.objects)
    assert all(o.type=="MESH" for o in source)
    maps={node.image for obj in source for mat in obj.data.materials for node in mat.node_tree.nodes if node.type=="TEX_IMAGE"}
    assert len(maps)==4 and all(img.packed_file for img in maps)
    def component(prefix):
        return next(o for o in source if o.name.startswith(prefix))
    def box(obj):
        vertices=[obj.matrix_world@v.co for v in obj.data.vertices]
        return [Vector(tuple(fun(p[k] for p in vertices) for k in range(3))) for fun in (min,max)]
    west,east=box(component("西南门塔 · 两层")),box(component("东南门塔 · 两层"))
    west_center,east_center=(west[0]+west[1])*.5,(east[0]+east[1])*.5
    factor=(east_center.x-west_center.x)/(2*TOWER_X)
    origin_y=east_center.y-factor*TOWER_Y
    assert abs(east_center.x+west_center.x)<1e-7 and (east[1]-east[0]-west[1]+west[0]).length<1e-7
    assert box(component("主殿 · 四坡"))[1].z>box(component("东南门塔 · 四坡"))[1].z
    for side in ("西","东"):
        gallery=tree([component(side+"翼 · 穿透")])
        x=(-TOWER_X if side=="西" else TOWER_X)
        for bay in range(4):
            y=-2.32+(bay+.5)*4.89/4
            origin=Vector(((x-1.1)*factor,y*factor+origin_y,1.20*factor))
            assert gallery.ray_cast(origin,Vector((1,0,0)),2.2*factor)[0] is None,"回廊被封堵"
    bpy.context.window.scene=bpy.data.scenes.new("导出模型回读")
    bpy.ops.import_scene.fbx(filepath=str(ART/(KIND+".fbx")),use_anim=False)
    obj=bpy.context.selected_objects[0]
    mesh=obj.data
    low,high=box(obj)
    span=high-low
    assert mesh is not None and len(mesh.uv_layers)==2 and len(mesh.materials)==2
    assert abs(span.y-SPAN)<1e-6 and .84<span.x/span.y<.90 and .84<span.z/span.x<.92
    assert abs(low.z)<1e-7 and abs(low.x+high.x)<1e-7 and abs(low.y+high.y)<1e-7
    mesh.calc_loop_triangles()
    points=[obj.matrix_world@v.co for v in mesh.vertices]
    uv=mesh.uv_layers[0].data
    regions=set()
    smallest=1.0
    for tri in mesh.loop_triangles:
        a,b,c=(points[i] for i in tri.vertices)
        area=(b-a).cross(c-a).length/2
        assert math.isfinite(area) and area>1e-16,"退化三角面"
        smallest=min(smallest,area)
        for i in tri.loops:
            u,v=uv[i].uv
            assert 0<u<1 and 0<v<1
            swatch=next(k for k,(x,y,s) in REGIONS.items() if x<=u*2048<x+s and y<=(1-v)*2048<y+s)
            assert ("Seat" in mesh.materials[tri.material_index].name)==(swatch=="slate")
            regions.add(swatch)
    assert regions==set(REGIONS)
    sky=[v.uv.x for v in mesh.uv_layers[1].data]
    assert 0<=min(sky)<.40 and .98<max(sky)<=1.00001
    bpy.ops.import_scene.fbx(filepath=str(ART/"wall.fbx"),use_anim=False)
    enclosure=bpy.context.selected_objects[0]
    # 与棋盘的带墙装配相同。略过院地、门槛及地下石基的承托接触。
    wall=tree([enclosure],(1.21,1.21,1.14),min_z=.00081*1.14)
    insert=tree([obj],(.76,.76,.76),.00077*1.14,forward=.00025)
    crossings=len(wall.overlap(insert))
    assert crossings==0,f"宫体与门楼或城墙相交 {crossings} 面对"
    report={"Blender":bpy.app.version_string,"源文件分件":len(source),"内嵌贴图":len(maps),
            "运行网格":1,"材质槽":2,"三角面":len(mesh.loop_triangles),"图集分区":len(regions),
            "宽深高毫米":[round(v*1000,3) for v in span],"枢轴底高毫米":round(low.z*1000,5),
            "最小三角形面积平方毫米":round(smallest*1e6,10),"天光可见度":[round(min(sky),3),round(max(sky),3)],
            "通透廊跨":8,"墙垣与宫体三角面相交数":crossings}
    VIEWS_DIR.mkdir(parents=True,exist_ok=True)
    (VIEWS_DIR/"模型回读.json").write_text(json.dumps(report,indent=2,ensure_ascii=False)+"\n",encoding="utf-8")
    return report


result=verify()
