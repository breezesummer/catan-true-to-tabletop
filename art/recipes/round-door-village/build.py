"""在 Blender MCP 会话内构造村庄，保存可编辑源场景并导出游戏的共享 FBX。"""
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Vector

RECIPE = Path(__file__).resolve().parent
if str(RECIPE) not in sys.path:
    sys.path.insert(0, str(RECIPE))
from carpentry import build_walls
from contact import apply_finish, bake, index_copy, unwrap
from mesh_parts import Parts
from roofing import build_roof
from surfaces import ART, ROOT


def srgb(value):
    return value / 12.92 if value <= .04045 else ((value + .055) / 1.055)**2.4


def material(name, images, roof=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    tree = mat.node_tree
    shader = tree.nodes.get("Principled BSDF")
    def socket(node, output, target, input_name):
        tree.links.new(node.outputs[output], target.inputs[input_name])
    def texture(key, x, y):
        n = tree.nodes.new("ShaderNodeTexImage")
        n.image, n.location = images[key], (x,y)
        return n
    albedo = texture("surface",-790,320)
    if roof:
        bw = tree.nodes.new("ShaderNodeRGBToBW")
        bw.location = (-540,350)
        socket(albedo,"Color",bw,"Color")
        shade = tree.nodes.new("ShaderNodeMath")
        shade.operation = "DIVIDE"
        shade.location = (-340,350)
        shade.inputs[1].default_value = .42
        socket(bw,0,shade,0)
        tint = tree.nodes.new("ShaderNodeMixRGB")
        tint.name, tint.label = "SeatTint","席位釉色"
        tint.blend_type, tint.location = "MULTIPLY",(-130,350)
        tint.inputs[0].default_value = 1
        tint.inputs[2].default_value = tuple(srgb(v) for v in (.85,.24,.16)) + (1,)
        socket(shade,0,tint,1)
        mask = texture("mask",-540,620)
        finish_tint = tree.nodes.new("ShaderNodeMixRGB")
        finish_tint.location = (80,480)
        socket(mask,"Color",finish_tint,0)
        socket(albedo,"Color",finish_tint,1)
        socket(tint,0,finish_tint,2)
        socket(finish_tint,0,shader,"Base Color")
    else:
        socket(albedo,"Color",shader,"Base Color")
    height = texture("normal",-790,5)
    normal = tree.nodes.new("ShaderNodeNormalMap")
    normal.location = (-310,30)
    normal.inputs["Strength"].default_value = .75
    socket(height,"Color",normal,"Color")
    socket(normal,"Normal",shader,"Normal")
    finish = texture("finish",-790,-300)
    channels = tree.nodes.new("ShaderNodeSeparateColor")
    channels.location = (-520,-210)
    socket(finish,"Color",channels,"Color")
    socket(channels,"Red",shader,"Metallic")
    rough = tree.nodes.new("ShaderNodeMath")
    rough.location,rough.operation = (-280,-210),"SUBTRACT"
    rough.inputs[0].default_value = 1
    socket(finish,"Alpha",rough,1)
    socket(rough,0,shader,"Roughness")
    return mat


def build():
    source = ROOT/"art/source/round-door-village/圆门村庄.blend"
    source.parent.mkdir(parents=True, exist_ok=True)
    scene = bpy.data.scenes.new("圆门村庄")
    bpy.context.window.scene = scene
    scene.unit_settings.system = "METRIC"
    images = {}
    for kind in ("surface","normal","finish","mask"):
        folder = ART.parents[1]/"PlayerColors" if kind=="mask" else ART
        img = bpy.data.images.load(str(folder/("village-"+kind+".png")))
        if kind != "surface":
            img.colorspace_settings.name = "Non-Color"
        if kind == "finish":
            img.alpha_mode = "CHANNEL_PACKED"
        img.pack()
        images[kind] = img
    materials = [material("Natural_Masonry",images),material("Natural_Player",images,True)]
    parts = Parts()
    build_walls(parts)
    build_roof(parts)
    objects, origin, scale = parts.objects(scene, materials, .018)
    copies = []
    start = 0
    for obj in objects:
        duplicate = obj.copy()
        duplicate.data = obj.data.copy()
        index_copy(duplicate.data,start)
        start += len(duplicate.data.loops)
        scene.collection.objects.link(duplicate)
        duplicate.select_set(True)
        copies.append(duplicate)
        obj.hide_render = True
    bpy.context.view_layer.objects.active = copies[0]
    bpy.ops.object.join()
    joined = bpy.context.object
    joined.name = joined.data.name = "village"
    unwrap(joined,objects)
    bm = bmesh.new()
    bm.from_mesh(joined.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    tiny = [face for face in bm.faces if face.calc_area()<1e-15]
    if tiny:
        bmesh.ops.delete(bm,geom=tiny,context="FACES")
    bm.to_mesh(joined.data)
    bm.free()
    joined.data.update()
    if len(joined.material_slots) != 2 or len(joined.data.uv_layers) != 2:
        raise ValueError("导出材质槽或 UV 数量异常")
    contact = bake(scene,joined,origin,scale,ART/"village-contact.png")
    for mat in materials:
        apply_finish(mat,contact)
    joined.data.uv_layers.active_index = 0
    joined.data.uv_layers[0].active_render = True
    bpy.ops.export_scene.fbx(filepath=str(ART/"village.fbx"),use_selection=True,
                             object_types={"MESH"},axis_forward="-Z",axis_up="Y",apply_unit_scale=True,
                             bake_space_transform=True,add_leaf_bones=False,bake_anim=False,
                             path_mode="STRIP",use_custom_props=False)
    count = len(joined.data.polygons)
    size = [round(v*1000,3) for v in joined.dimensions]
    bpy.data.objects.remove(joined,do_unlink=True)
    for obj in objects:
        obj.hide_render = False
    # 源文件只写分件建筑与内嵌贴图，摄影棚和烘焙临时网格不进入源场景。
    bpy.data.libraries.write(str(source), {scene}, path_remap="RELATIVE_ALL", fake_user=True, compress=True)
    return {"scene":scene.name,"parts":len(objects),"triangles":count,"size_mm":size,"source":str(source)}


result = build()
