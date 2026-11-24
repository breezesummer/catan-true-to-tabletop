"""在独立 Blender MCP 会话执行，保存可编辑的分件源文件并导出贸易大都会共享网格。"""
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector

recipe=Path(__file__).resolve().parent
sys.path[:0]=[str(recipe),str(recipe.parent/"gothic-royal-city")]
from daylight import bake_exposure
from stonework import Mason
from guild_architecture import build_guild
from guild_maps import ART, BLEND, COLORS, REGIONS, SOURCE, SPAN


def material(name,images,tinted):
    mat=bpy.data.materials.new(name)
    mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    bsdf=nodes["Principled BSDF"]
    bsdf.location=(475,140)
    nodes["Material Output"].location=(780,140)
    def node(kind,x,y):
        item=nodes.new(kind)
        item.location=(x,y)
        return item
    def sample(key,x,y):
        item=node("ShaderNodeTexImage",x,y)
        item.image=images[key]
        return item
    base=sample("surface",-800,330).outputs["Color"]
    if tinted:
        mono=node("ShaderNodeRGBToBW",-540,460)
        links.new(base,mono.inputs[0])
        value=node("ShaderNodeMath",-340,470)
        value.operation="DIVIDE"
        value.inputs[1].default_value=.29
        links.new(mono.outputs[0],value.inputs[0])
        tint=node("ShaderNodeMixRGB",-130,520)
        tint.name,tint.label,tint.blend_type="SeatTint","板岩矿物着色","MULTIPLY"
        tint.inputs[0].default_value=1
        tint.inputs[2].default_value=(.134,.205,.245,1)
        links.new(value.outputs[0],tint.inputs[1])
        mix=node("ShaderNodeMixRGB",112,355)
        links.new(sample("mask",-520,740).outputs["Color"],mix.inputs[0])
        links.new(base,mix.inputs[1])
        links.new(tint.outputs[0],mix.inputs[2])
        base=mix.outputs[0]
    weather=node("ShaderNodeUVMap",-795,-465)
    weather.uv_map="天光可见度与石脚风化"
    channels=node("ShaderNodeSeparateXYZ",-465,-450)
    links.new(weather.outputs["UV"],channels.inputs[0])
    stonefoot=node("ShaderNodeMixRGB",183,145)
    stonefoot.blend_type="MULTIPLY"
    stonefoot.inputs[2].default_value=(.74,.71,.65,1)
    links.new(channels.outputs["Y"],stonefoot.inputs[0])
    links.new(base,stonefoot.inputs[1])
    links.new(stonefoot.outputs[0],bsdf.inputs["Base Color"])
    normal=node("ShaderNodeNormalMap",0,56)
    normal.inputs["Strength"].default_value=.74
    links.new(sample("normal",-445,66).outputs["Color"],normal.inputs["Color"])
    links.new(normal.outputs[0],bsdf.inputs["Normal"])
    packed=sample("finish",-810,-240)
    separate=node("ShaderNodeSeparateColor",-470,-220)
    links.new(packed.outputs["Color"],separate.inputs[0])
    links.new(separate.outputs["Red"],bsdf.inputs["Metallic"])
    rough=node("ShaderNodeMath",-180,-255)
    rough.operation="SUBTRACT"
    rough.inputs[0].default_value=1
    links.new(packed.outputs["Alpha"],rough.inputs[1])
    links.new(rough.outputs[0],bsdf.inputs["Roughness"])
    return mat


def build():
    if not bpy.app.background:
        raise RuntimeError("请为商会新开一份后台 Blender MCP 会话")
    scene=bpy.data.scenes.new("哥特商会之都")
    bpy.context.window.scene=scene
    for previous in list(bpy.data.scenes):
        if scene!=previous:
            bpy.data.scenes.remove(previous)
    bpy.data.orphans_purge(do_recursive=True)
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    images={}
    for key in ("surface","normal","finish","mask"):
        img=bpy.data.images.load(str((COLORS if key=="mask" else ART)/f"metro-trade-{key}.png"))
        if key!="surface":
            img.colorspace_settings.name="Non-Color"
        if key=="finish":
            img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        images[key]=img
    materials=[material("Expansion_Natural",images,False),material("Expansion_Seat",images,True)]
    build=Mason(REGIONS)
    build_guild(build)
    parts=build.export_parts(scene,materials,SPAN)
    bake_exposure(parts)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active
                view.clip_start,view.clip_end=.00005,1
                view.region_3d.view_perspective="ORTHO"
                view.region_3d.view_distance=.057
                view.region_3d.view_location=(0,0,.0120)
                view.region_3d.view_rotation=Vector((-.48,.88,-.43)).to_track_quat("-Z","Y")
                view.overlay.show_floor=view.overlay.show_cursor=False
                view.overlay.show_axis_x=view.overlay.show_axis_y=False
                view.shading.type="MATERIAL"
    SOURCE.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND),compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=obj.data.name="metro-trade"
    mesh=bmesh.new()
    mesh.from_mesh(obj.data)
    bmesh.ops.dissolve_limit(mesh,angle_limit=.00045,verts=list(mesh.verts),edges=list(mesh.edges),
                             delimit={"MATERIAL","UV","NORMAL"},use_dissolve_boundaries=False)
    bmesh.ops.triangulate(mesh,faces=list(mesh.faces))
    tiny=[face for face in mesh.faces if face.calc_area()<2e-16]
    if tiny:
        bmesh.ops.delete(mesh,geom=tiny,context="FACES")
    mesh.to_mesh(obj.data)
    mesh.free()
    obj.data.update()
    bpy.ops.export_scene.fbx(filepath=str(ART/"metro-trade.fbx"),use_selection=True,object_types={"MESH"},
                             axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_space_transform=True,
                             use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    return {"分件":len(parts),"三角面":len(obj.data.polygons),
            "宽深高毫米":[round(x*1000,4) for x in obj.dimensions]}


result=build()
