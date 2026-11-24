"""在独立 Blender MCP 后台场景建造学宫，先保存可编辑分件，再为游戏合并。"""
from pathlib import Path
import json
import sys

import bmesh
import bpy
from mathutils import Vector

folder=str(Path(__file__).resolve().parent)
if folder not in sys.path:
    sys.path.insert(0,folder)
from finishes import ART, BLEND, CITY, COLORS, PREVIEW, SOURCE
from academy_mesh import Academy
from academy_buildings import annex, hall, tower
from instruments import armillary, dome, sundial, telescope
from daylight import bake_exposure


def material(label,directory,prefix):
    mat=bpy.data.materials.new(label)
    mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    bsdf=nodes.get("Principled BSDF")
    bsdf.location=(520,140)
    nodes.get("Material Output").location=(810,140)
    def node(kind,x,y):
        n=nodes.new(kind)
        n.location=(x,y)
        return n
    def texture(key,x,y):
        path=(COLORS if key=="mask" else directory)/(prefix+"-"+key+".png")
        image=bpy.data.images.load(str(path),check_existing=True)
        if key!="surface":
            image.colorspace_settings.name="Non-Color"
        if key=="finish":
            image.alpha_mode="CHANNEL_PACKED"
        image.pack()
        tex=node("ShaderNodeTexImage",x,y)
        tex.image=image
        return tex
    color=texture("surface",-850,380).outputs["Color"]
    mask=texture("mask",-850,650)
    value=node("ShaderNodeRGBToBW",-630,270)
    links.new(color,value.inputs[0])
    exposure=node("ShaderNodeMath",-470,240)
    exposure.operation="DIVIDE"
    links.new(value.outputs[0],exposure.inputs[0])
    exposure.inputs[1].default_value=.29
    pigment=node("ShaderNodeMixRGB",-260,240)
    pigment.name,pigment.label,pigment.blend_type="SeatTint","席位颜料","MULTIPLY"
    pigment.inputs[0].default_value=1
    pigment.inputs[2].default_value=(.19,.31,.36,1)
    links.new(exposure.outputs[0],pigment.inputs[1])
    amount=node("ShaderNodeMath",-425,620)
    amount.name,amount.label,amount.operation="SeatStrength","展示席位色","MULTIPLY"
    links.new(mask.outputs["Color"],amount.inputs[0])
    amount.inputs[1].default_value=0
    mix=node("ShaderNodeMixRGB",20,420)
    links.new(amount.outputs[0],mix.inputs[0])
    links.new(color,mix.inputs[1])
    links.new(pigment.outputs[0],mix.inputs[2])
    links.new(mix.outputs[0],bsdf.inputs["Base Color"])
    normal=node("ShaderNodeNormalMap",-80,0)
    normal.inputs["Strength"].default_value=.75
    links.new(texture("normal",-605,-25).outputs["Color"],normal.inputs["Color"])
    links.new(normal.outputs[0],bsdf.inputs["Normal"])
    packing=texture("finish",-600,-320)
    channels=node("ShaderNodeSeparateColor",-320,-300)
    links.new(packing.outputs["Color"],channels.inputs[0])
    links.new(channels.outputs["Red"],bsdf.inputs["Metallic"])
    rough=node("ShaderNodeMath",-75,-210)
    rough.operation="SUBTRACT"
    rough.inputs[0].default_value=1
    links.new(packing.outputs["Alpha"],rough.inputs[1])
    links.new(rough.outputs[0],bsdf.inputs["Roughness"])
    return mat


def build():
    if not bpy.app.background:
        raise RuntimeError("请在独立 Blender MCP 后台场景制作")
    scene=bpy.data.scenes.new("观星学宫工作区")
    bpy.context.window.scene=scene
    for previous in list(bpy.data.scenes):
        if previous!=scene:
            bpy.data.scenes.remove(previous)
    bpy.data.orphans_purge(do_recursive=True)
    scene.name="穹顶观星学宫"
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    materials=[material("Expansion_Natural",CITY,"city"),material("Expansion_Seat",ART,"metro-science")]
    m=Academy()
    for craft in (hall,tower,annex,dome,telescope,armillary,sundial):
        craft(m)
    objects=m.export_parts(scene,materials)
    bake_exposure(objects)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active
                view.clip_start,view.clip_end=.00005,1
                view.region_3d.view_perspective="ORTHO"
                view.region_3d.view_distance=.060
                view.region_3d.view_location=(0,0,.0122)
                view.region_3d.view_rotation=Vector((-.48,.96,-.36)).to_track_quat("-Z","Y")
                view.overlay.show_floor=view.overlay.show_cursor=False
                view.overlay.show_axis_x=view.overlay.show_axis_y=False
                view.shading.type="MATERIAL"
    SOURCE.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND),compress=True)
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    mesh=bpy.context.object
    mesh.name=mesh.data.name="metro-science"
    bm=bmesh.new()
    bm.from_mesh(mesh.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.verts.index_update()
    seen=set()
    redundant=[]
    for face in bm.faces:
        key=tuple(sorted(v.index for v in face.verts))
        if key in seen or face.calc_area()<2e-16:
            redundant.append(face)
        else:
            seen.add(key)
    if redundant:
        bmesh.ops.delete(bm,geom=redundant,context="FACES")
    bm.to_mesh(mesh.data)
    bm.free()
    mesh.data.update()
    bpy.ops.export_scene.fbx(filepath=str(ART/"metro-science.fbx"),use_selection=True,object_types={"MESH"},
                             axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_space_transform=True,
                             use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    item={"kind":"metro-science","triangles":len(mesh.data.polygons),"size":list(mesh.dimensions)}
    manifest=Path(__file__).resolve().parents[1]/"expansion-models-v001/几何.json"
    items=json.loads(manifest.read_text(encoding="utf8"))
    manifest.write_text(json.dumps([item if v["kind"]==item["kind"] else v for v in items],indent=2),encoding="utf8")
    return {"分件":len(objects),"三角面":item["triangles"],"宽深高毫米":[round(c*1000,3) for c in item["size"]]}


result=build()
