"""在独立 Blender MCP 会话中分件装配、烘焙天光、保存源件并导出游戏网格。"""
import json
from pathlib import Path
import sys
import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from finishes import ART, BLEND, COLORS, KIND, ROOT, SPAN
from palace import assemble
from daylight import bake_exposure


def materials():
    maps={}
    for key in ("surface","normal","finish","mask"):
        location=COLORS if key=="mask" else ART
        image=bpy.data.images.load(str(location/f"{KIND}-{key}.png"))
        if key!="surface":
            image.colorspace_settings.name="Non-Color"
        if key=="finish":
            image.alpha_mode="CHANNEL_PACKED"
        image.pack()
        maps[key]=image
    result=[]
    for name,roof in (("Expansion_Natural",False),("Expansion_Seat",True)):
        material=bpy.data.materials.new(name)
        material.use_nodes=True
        nodes,links=material.node_tree.nodes,material.node_tree.links
        bsdf=nodes.get("Principled BSDF")
        bsdf.location=(260,160)
        nodes.get("Material Output").location=(610,160)
        def node(kind,x,y):
            n=nodes.new(kind)
            n.location=(x,y)
            return n
        def texture(key,x,y):
            n=node("ShaderNodeTexImage",x,y)
            n.image=maps[key]
            return n
        color=texture("surface",-920,410).outputs["Color"]
        if roof:
            # 源件保留原型中的天然灰瓦，席位涂层可用同一套风化遮罩预览。
            strength=node("ShaderNodeMath",-544,680)
            strength.operation="MULTIPLY"
            strength.name,strength.label="PigmentAmount","席位着色强度"
            strength.inputs[1].default_value=0
            links.new(texture("mask",-925,720).outputs["Color"],strength.inputs[0])
            overlay=node("ShaderNodeMixRGB",-360,430)
            overlay.blend_type="MULTIPLY"
            overlay.inputs[0].default_value=1
            overlay.inputs[2].default_value=(.50,1.10,1.56,1)
            links.new(color,overlay.inputs[1])
            mix=node("ShaderNodeMixRGB",-68,460)
            links.new(strength.outputs[0],mix.inputs[0])
            links.new(color,mix.inputs[1])
            links.new(overlay.outputs[0],mix.inputs[2])
            color=mix.outputs[0]
        weather=node("ShaderNodeUVMap",-710,-675)
        weather.uv_map="天光可见度与石脚风化"
        channels=node("ShaderNodeSeparateXYZ",-480,-645)
        links.new(weather.outputs[0],channels.inputs[0])
        tide=node("ShaderNodeMixRGB",26,270)
        tide.name,tide.label,tide.blend_type="Weathering","檐下积尘与墙脚潮痕","MULTIPLY"
        tide.inputs[2].default_value=(.74,.71,.65,1)
        links.new(channels.outputs["Y"],tide.inputs[0])
        links.new(color,tide.inputs[1])
        links.new(tide.outputs[0],bsdf.inputs["Base Color"])
        bump=node("ShaderNodeNormalMap",-226,100)
        bump.inputs["Strength"].default_value=.70
        links.new(texture("normal",-921,90).outputs["Color"],bump.inputs["Color"])
        links.new(bump.outputs[0],bsdf.inputs["Normal"])
        metal=texture("finish",-921,-235)
        split=node("ShaderNodeSeparateColor",-538,-178)
        links.new(metal.outputs["Color"],split.inputs[0])
        links.new(split.outputs["Red"],bsdf.inputs["Metallic"])
        rough=node("ShaderNodeMath",-235,-195)
        rough.operation="SUBTRACT"
        rough.inputs[0].default_value=1
        links.new(metal.outputs["Alpha"],rough.inputs[1])
        links.new(rough.outputs[0],bsdf.inputs["Roughness"])
        result.append(material)
    return result


def build():
    if not bpy.app.background:
        raise RuntimeError("请在独立的 Blender MCP 后台会话中打开此配方")
    scene=bpy.data.scenes.new("冠冕议政宫")
    bpy.context.window.scene=scene
    for other in list(bpy.data.scenes):
        if other!=scene:
            bpy.data.scenes.remove(other)
    bpy.data.orphans_purge(do_recursive=True)
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    parts=assemble().export_parts(scene,materials(),span=SPAN)
    bpy.context.view_layer.update()
    bake_exposure(parts)
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active
                view.clip_start,view.clip_end=.00005,2
                view.region_3d.view_perspective="ORTHO"
                view.region_3d.view_distance=.064
                view.region_3d.view_location=(0,0,.0103)
                view.region_3d.view_rotation=Vector((-.44,.95,-.57)).to_track_quat("-Z","Y")
                view.overlay.show_floor=view.overlay.show_cursor=False
                view.overlay.show_axis_x=view.overlay.show_axis_y=False
                view.shading.type="MATERIAL"
    BLEND.parent.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND),compress=True)
    for obj in parts:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name=obj.data.name=KIND
    bm=bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    tiny=[f for f in bm.faces if f.calc_area()<2.5e-16]
    if tiny:
        bmesh.ops.delete(bm,geom=tiny,context="FACES")
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()
    bpy.ops.export_scene.fbx(filepath=str(ART/(KIND+".fbx")),use_selection=True,object_types={"MESH"},
                             axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_space_transform=True,
                             use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    dimensions=[float(v) for v in obj.dimensions]
    report={"kind":KIND,"triangles":len(obj.data.polygons),"size":dimensions}
    manifest=ROOT/"art/recipes/expansion-models-v001/几何.json"
    previous=json.loads(manifest.read_text(encoding="utf-8-sig"))
    assert sum(item["kind"]==KIND for item in previous)==1
    manifest.write_text(json.dumps([report if rec["kind"]==KIND else rec for rec in previous],indent=2),encoding="utf-8")
    return {"分件":len(parts),"三角面":report["triangles"],"宽深高毫米":[round(v*1000,3) for v in dimensions]}


result=build()
