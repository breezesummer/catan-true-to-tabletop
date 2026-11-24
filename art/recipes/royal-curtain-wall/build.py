"""在独立 Blender MCP 会话中制作可拆件母版。四色共用一个含天光坐标的运行网格。"""
import json
from pathlib import Path
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
from wall_maps import ART, COLOURS, ROOT, SOURCE
from masonry import Stonework
from enclosure import assemble
from daylight import bake_exposure


def surfaces():
    maps = {}
    for kind in ("surface", "normal", "finish", "mask"):
        folder = COLOURS if kind == "mask" else ART
        image = bpy.data.images.load(str(folder / f"wall-{kind}.png"))
        if kind != "surface":
            image.colorspace_settings.name = "Non-Color"
        if kind == "finish":
            image.alpha_mode = "CHANNEL_PACKED"
        image.pack()
        maps[kind] = image
    materials = []
    for seat, name in enumerate(("Expansion_Natural", "Expansion_Seat")):
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = nodes.get("Principled BSDF")
        bsdf.location = (330, 170)
        nodes.get("Material Output").location = (640, 170)
        def node(kind, x, y):
            n = nodes.new(kind)
            n.location = (x,y)
            return n
        def texture(key, x, y):
            n = node("ShaderNodeTexImage", x,y)
            n.image = maps[key]
            n.label = key
            return n
        base = texture("surface",-850,450).outputs["Color"]
        if seat:
            tint = node("ShaderNodeMixRGB",-300,520)
            tint.name, tint.label, tint.blend_type = "SeatTint", "亚麻门旗染色", "MULTIPLY"
            tint.inputs[2].default_value = (.15,.48,.80,1)
            links.new(texture("mask",-850,715).outputs["Color"], tint.inputs[0])
            links.new(base,tint.inputs[1])
            base = tint.outputs[0]
        links.new(base,bsdf.inputs["Base Color"])
        normal = node("ShaderNodeNormalMap",-240,110)
        normal.inputs["Strength"].default_value = .78
        links.new(texture("normal",-850,120).outputs["Color"],normal.inputs["Color"])
        links.new(normal.outputs["Normal"],bsdf.inputs["Normal"])
        pack = texture("finish",-850,-220)
        split = node("ShaderNodeSeparateColor",-495,-160)
        links.new(pack.outputs["Color"],split.inputs[0])
        links.new(split.outputs["Red"],bsdf.inputs["Metallic"])
        rough = node("ShaderNodeMath",-220,-220)
        rough.operation = "SUBTRACT"
        rough.inputs[0].default_value = 1
        links.new(pack.outputs["Alpha"],rough.inputs[1])
        links.new(rough.outputs[0],bsdf.inputs["Roughness"])
        materials.append(mat)
    return materials


def build():
    if not bpy.app.background:
        raise RuntimeError("请在独立的后台 Blender MCP 会话执行。")
    bpy.ops.wm.read_homefile(use_empty=True, use_factory_startup=True)
    scene = bpy.context.scene
    scene.name = "王城石墙"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "MILLIMETERS"
    m = Stonework()
    assemble(m)
    # 以棋盘表面为 z=0 保存母版，临海石基向下延伸，正门轴线穿过原点。
    points = [p for verts, *_ in m.groups.values() for p in verts]
    low = Vector([min(p[a] for p in points) for a in range(3)])
    high = Vector([max(p[a] for p in points) for a in range(3)])
    mm = (high-low)*.001
    anchor = Matrix.Translation(Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))*.001)
    objects = m.export_parts(scene, surfaces(), max(mm.x,mm.y))
    for obj in objects:
        obj.data.transform(anchor)
        obj.data.update()
    bake_exposure(objects)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type == "VIEW_3D":
                space = area.spaces.active
                space.clip_start, space.clip_end = .00005, 1
                space.region_3d.view_distance = .051
                space.region_3d.view_location = (0,0,.0015)
                space.region_3d.view_perspective = "ORTHO"
                space.region_3d.view_rotation = Vector((-.42,.94,-.65)).to_track_quat("-Z","Y")
                space.overlay.show_floor = space.overlay.show_cursor = False
                space.overlay.show_axis_x = space.overlay.show_axis_y = False
                space.shading.type = "MATERIAL"
    SOURCE.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE),compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    model = bpy.context.object
    model.name = model.data.name = "wall"
    bm = bmesh.new()
    bm.from_mesh(model.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    # UV 图集边界会从弧砖切出共线角点；导出前清掉低于 FBX 单精度分辨率的细片。
    tiny = [f for f in bm.faces if f.calc_area() < max(1e-14,max(e.calc_length() for e in f.edges)*1e-9)]
    if tiny:
        bmesh.ops.delete(bm, geom=tiny, context="FACES")
    bm.to_mesh(model.data)
    bm.free()
    model.data.update()
    bpy.ops.export_scene.fbx(filepath=str(ART/"wall.fbx"), use_selection=True, object_types={"MESH"},
                             axis_forward="-Z", axis_up="Y", apply_unit_scale=True, bake_space_transform=True,
                             use_mesh_modifiers=False, add_leaf_bones=False, bake_anim=False, path_mode="STRIP")
    manifest = ROOT/"art/recipes/expansion-models-v001/几何.json"
    spec = json.loads(manifest.read_text(encoding="utf-8"))
    wall = next(row for row in spec if row["kind"] == "wall")
    wall.update(triangles=len(model.data.polygons), size=[round(v,9) for v in mm], baseY=round(low.z*.001,9))
    manifest.write_text(json.dumps(spec,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    return {"分件":len(objects), "三角面":wall["triangles"], "宽深高毫米":[round(v*1000,3) for v in mm]}


result = build()
