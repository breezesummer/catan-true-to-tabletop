"""通过独立 Blender MCP 会话执行。先保存分件工作文件，再导出游戏的一份网格。"""
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector

folder=str(Path(__file__).resolve().parent)
if folder in sys.path:
    sys.path.remove(folder)
sys.path.insert(0,folder)
from architecture import build_city
from daylight import bake_exposure
from stonework import Mason
from surfaces import ART, BLEND, SEATS


def linear(v):
    return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4


def shader(name,images,roof):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    bsdf=nodes.get("Principled BSDF")
    bsdf.location=(360,120)
    def node(kind,x,y):
        n=nodes.new(kind)
        n.location=(x,y)
        return n
    def texture(key,x,y):
        n=node("ShaderNodeTexImage",x,y)
        n.image=images[key]
        return n
    color=texture("surface",-850,370).outputs["Color"]
    if roof:
        value=node("ShaderNodeRGBToBW",-570,300)
        links.new(color,value.inputs[0])
        shade=node("ShaderNodeMath",-410,300)
        shade.operation="DIVIDE"
        shade.inputs[1].default_value=.29
        links.new(value.outputs[0],shade.inputs[0])
        tint=node("ShaderNodeMixRGB",-200,300)
        tint.name,tint.label,tint.blend_type="SeatTint","席位板岩色","MULTIPLY"
        tint.inputs[0].default_value=1
        tint.inputs[2].default_value=tuple(map(linear,SEATS["blue"]))+(1,)
        links.new(shade.outputs[0],tint.inputs[1])
        wear=node("ShaderNodeMixRGB",50,390)
        links.new(texture("mask",-600,650).outputs["Color"],wear.inputs[0])
        links.new(color,wear.inputs[1])
        links.new(tint.outputs[0],wear.inputs[2])
        color=wear.outputs[0]
    links.new(color,bsdf.inputs["Base Color"])
    normal=node("ShaderNodeNormalMap",-220,30)
    normal.inputs["Strength"].default_value=.75
    links.new(texture("normal",-540,0).outputs["Color"],normal.inputs["Color"])
    links.new(normal.outputs["Normal"],bsdf.inputs["Normal"])
    finish=texture("finish",-800,-230)
    split=node("ShaderNodeSeparateColor",-530,-210)
    links.new(finish.outputs["Color"],split.inputs[0])
    links.new(split.outputs["Red"],bsdf.inputs["Metallic"])
    rough=node("ShaderNodeMath",-210,-180)
    rough.operation="SUBTRACT"
    rough.inputs[0].default_value=1
    links.new(finish.outputs["Alpha"],rough.inputs[1])
    links.new(rough.outputs[0],bsdf.inputs["Roughness"])
    return mat


def build():
    # 独立的 MCP 后台进程只保存本模型，避免将启动场景或上轮摄影棚带入交付。
    if not bpy.app.background:
        raise RuntimeError("请在独立 Blender MCP 后台会话中生成王城，避免覆盖正在编辑的场景")
    scene=bpy.data.scenes.new("哥特王城工作区")
    bpy.context.window.scene=scene
    for other in list(bpy.data.scenes):
        if other!=scene:
            bpy.data.scenes.remove(other)
    bpy.data.orphans_purge(do_recursive=True)
    for library in list(bpy.data.libraries):
        if Path(bpy.path.abspath(library.filepath)).resolve()==BLEND.resolve():
            bpy.data.libraries.remove(library)
    scene.name="哥特王城"
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    images={}
    for key in ("surface","normal","finish","mask"):
        loc=ART.parents[1]/"PlayerColors" if key=="mask" else ART
        img=bpy.data.images.load(str(loc/f"city-{key}.png"))
        if key!="surface":
            img.colorspace_settings.name="Non-Color"
        if key=="finish":
            img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        images[key]=img
    materials=[shader("Natural_Masonry",images,False),shader("Natural_Player",images,True)]
    city=Mason()
    build_city(city)
    objects=city.export_parts(scene,materials)
    bake_exposure(objects)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active
                view.clip_start,view.clip_end=.00005,1
                view.region_3d.view_perspective="ORTHO"
                view.region_3d.view_distance=.049
                view.region_3d.view_location=(0,0,.0117)
                view.region_3d.view_rotation=Vector((-.41,.92,-.34)).to_track_quat("-Z","Y")
                view.overlay.show_floor=view.overlay.show_cursor=False
                view.overlay.show_axis_x=view.overlay.show_axis_y=False
                view.shading.type="MATERIAL"
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND),compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    model=bpy.context.object
    model.name=model.data.name="city"
    bm=bmesh.new()
    bm.from_mesh(model.data)
    # 拱洞上方施工时按拱石分段。平面上连续的同一材质、同一 UV 可以合回一面。
    bmesh.ops.dissolve_limit(bm,angle_limit=.0005,verts=list(bm.verts),edges=list(bm.edges),
                             delimit={"MATERIAL","UV","NORMAL"},use_dissolve_boundaries=False)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    tiny=[f for f in bm.faces if f.calc_area()<2e-16]
    if tiny:
        bmesh.ops.delete(bm,geom=tiny,context="FACES")
    bm.to_mesh(model.data)
    bm.free()
    model.data.update()
    bpy.ops.export_scene.fbx(filepath=str(ART/"city.fbx"),use_selection=True,object_types={"MESH"},
                             axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_space_transform=True,
                             use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    return {"分件":len(objects),"三角面":len(model.data.polygons),
            "宽深高毫米":[round(float(x)*1000,3) for x in model.dimensions]}


result=build()
