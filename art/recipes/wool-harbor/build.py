"""由 Blender MCP 制作羊毛商港母版、运行网格和两种轻量交互轮廓。"""
from pathlib import Path
import json
import sys
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from wool_surfaces import ART,MODEL,PREVIEW,SOURCE
from wool_craft import Part,R
from wool_buildings import house,TOWER_X,BACK
from wool_landing import (PIER_LEFT,PIER_RIGHT,PIER_END,QUAY_LEFT,QUAY_RIGHT,QUAY_FRONT,
                          LOAD_X,LOAD_Y,LOAD_Z,POST_X,POST_Y,crane,goods,quay)
from wool_sign import WIDTH,HEIGHT,mount,sheep
from quay_craft import R as stone_random
from joinery import RNG as wood_random


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import trade_plaque


def surface():
    mat=bpy.data.materials.new("WoolHarbor")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    connect=mat.node_tree.links.new
    shader=nodes.get("Principled BSDF")
    for i,key in enumerate(("albedo","normal","finish")):
        img=bpy.data.images.load(str(ART/f"wool-{key}.png"))
        if key!="albedo": img.colorspace_settings.name="Non-Color"
        if key=="finish": img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        tex=nodes.new("ShaderNodeTexImage")
        tex.image=img
        tex.location=(-720,340-320*i)
        if key=="albedo":
            color=tex
            connect(tex.outputs["Color"],shader.inputs["Base Color"])
        elif key=="normal":
            n=nodes.new("ShaderNodeNormalMap")
            n.location=(-270,30)
            n.inputs["Strength"].default_value=.82
            connect(tex.outputs["Color"],n.inputs["Color"])
            connect(n.outputs[0],shader.inputs["Normal"])
        else:
            sep=nodes.new("ShaderNodeSeparateColor")
            sep.location=(-370,-210)
            connect(tex.outputs[0],sep.inputs[0])
            connect(sep.outputs[0],shader.inputs["Metallic"])
            minus=nodes.new("ShaderNodeMath")
            minus.operation="SUBTRACT"
            minus.inputs[0].default_value=1
            minus.location=(-200,-380)
            connect(tex.outputs["Alpha"],minus.inputs[1])
            connect(minus.outputs[0],shader.inputs["Roughness"])
    nodes.active=color
    return mat


def selection(scene,mat):
    pick,foot=Part("PickVolume"),Part("ShoreFootprint")
    def face(part,points):
        part.face(part.verts(points),"oak",[(.15,.12),(.85,.12),(.85,.89),(.15,.89)][:len(points)])
    def prism(top,base,project=True):
        bottom=[(x,y,base) for x,y,_ in top]
        face(pick,top)
        face(pick,bottom[::-1])
        for i in range(len(top)):
            j=(i+1)%len(top)
            face(pick,[top[i],bottom[i],bottom[j],top[j]])
        if project: face(foot,[(x,y,0) for x,y,z in top])
    def box(x0,x1,y0,y1,z,base,project=True):
        prism([(x0,y0,z),(x1,y0,z),(x1,y1,z),(x0,y1,z)],base,project)
    for a,b,za,zb in ((TOWER_X-4.36,TOWER_X,10.42,14.98),(TOWER_X,TOWER_X+4.36,14.98,10.42)):
        prism([(a,.64,za),(b,.64,zb),(b,9.94,zb),(a,9.94,za)],-.1)
    for a,b,za,zb in ((1.,5.48,6.35,10.79),(5.48,9.96,10.79,6.35)):
        prism([(-4.53,a,za),(12.39,a,za),(12.39,b,zb),(-4.53,b,zb)],-.1)
    box(PIER_LEFT,PIER_RIGHT,PIER_END,1.51,.16,-.71)
    box(QUAY_LEFT-.03,QUAY_RIGHT+.02,QUAY_FRONT-.07,1.65,.19,-3.51)
    box(-11.95,12.04,BACK-.12,10.42,.05,-.45)
    box(1.14,4.29,-1.64,.59,2.02,.18,False)
    box(6.91,11.29,-1.75,.59,3.16,.18,False)
    box(LOAD_X-1.43,LOAD_X+1.43,LOAD_Y-1.01,LOAD_Y+1.01,2.99,LOAD_Z)
    # 吊臂拾取沿悬臂而行，悬绳只占细条。包、牌和桩桥之间的海水仍可点透。
    from mathutils import Vector as V
    a,b=V((POST_X,POST_Y+.25)),V((LOAD_X,LOAD_Y))
    d=(b-a).normalized()
    s=V((-d.y,d.x))*.30
    prism([(p.x,p.y,9.24) for p in (a-s,b-s,b+s,a+s)],8.56)
    box(LOAD_X-.14,LOAD_X+.14,LOAD_Y-.34,LOAD_Y-.06,8.64,2.88,False)
    sign=[mount(x,-.39,z) for x,z in ((-WIDTH/2,-HEIGHT/2),(WIDTH/2,-HEIGHT/2),
                                      (WIDTH/2,HEIGHT/2),(-WIDTH/2,HEIGHT/2))]
    prism(sign,min(pt[2] for pt in sign)-.12,False)
    return [p.object(scene,mat) for p in (pick,foot)]


def export(objects,filename):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(MODEL/filename),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,
        use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    if not bpy.app.background:
        raise RuntimeError("羊毛港母版在独立的后台 Blender MCP 会话中制作。")
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    R.seed(211717)
    stone_random.seed(531117)
    wood_random.seed(282117)
    source=bpy.context.scene
    source.name="羊毛港"
    source.unit_settings.system="METRIC"
    source.unit_settings.length_unit="MILLIMETERS"
    mat=surface()
    emblem=sheep()
    emblem.vertices=[tuple(.001*v for v in mount(*(k*1000 for k in pt))) for pt in emblem.vertices]
    groups={}
    for key,label,parts in (("HarborHouse","双仓",house()),("TradePlaque","羊头招牌",(emblem,)),
                            ("PierDeck","石岸与桩桥",quay()),("WoolBales","码头原毛",goods()),
                            ("Hoist","起卸吊架",crane())):
        group=bpy.data.collections.new(label)
        source.collection.children.link(group)
        items=[part.object(source,mat) for part in parts]
        if key=="TradePlaque": items=trade_plaque(source,lambda x,y,h: Vector(mount(x,-.11-h,y)),WIDTH,items[0])
        for obj in items:
            group.objects.link(obj)
            source.collection.objects.unlink(obj)
        groups[key]=items
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view=area.spaces.active
            view.region_3d.view_rotation=Vector((-.44,1,-.43)).to_track_quat("-Z","Y")
            view.region_3d.view_location=(0,.003,.0049)
            view.region_3d.view_distance=.044
            view.region_3d.view_perspective="ORTHO"
            view.clip_start=.00006
            view.overlay.show_overlays=False
            view.shading.type="MATERIAL"
    PREVIEW.mkdir(parents=True,exist_ok=True)
    SOURCE.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"羊毛港.blend"),compress=True,copy=True)
    game=bpy.data.scenes.new("羊毛港正式网格")
    bpy.context.window.scene=game
    active=[]
    triangles={}
    for key,items in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for item in items:
            obj=item.copy()
            obj.data=item.data.copy()
            game.collection.objects.link(obj)
            obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.ops.object.join()
        obj.name=obj.data.name=key
        obj.data.calc_loop_triangles()
        triangles[key]=len(obj.data.loop_triangles)
        active.append(obj)
    export(active,"wool-harbor.fbx")
    proxies=selection(game,mat)
    export(proxies,"wool-selection.fbx")
    stats={"母版网格":sum(map(len,groups.values())),"正式三角面":triangles}
    (PREVIEW/"模型统计.json").write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding="utf-8")
    return stats


result=build()
