"""在 Blender MCP 后台会话组装商人，先保存分件母版，再制作棋盘单网格。"""
from pathlib import Path
import os
import shutil
import sys
import tempfile

import bpy
from mathutils import Matrix,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from costume import belt,boots_and_breeches,layers,overcoat,sleeves
from equipment import bag_and_harness,purse,rug
from merchant_hands import OFFER,HOLD,hands,indent_wool,prepare_sources
from portrait import bonnet,hair,sculpt_head
from surfaces import ART,HEIGHT,ROOT,SOURCE,make_maps
from workshop import Workshop


def material(stage,art,source):
    mat=bpy.data.materials.new("PeddlerSurface")
    mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    bsdf=nodes.get("Principled BSDF")
    bsdf.location=(430,140)
    nodes["Material Output"].location=(700,140)
    for tag,y in (("surface",330),("normal",25),("finish",-290)):
        filename=f"peddler-{tag}.png"
        pic=bpy.data.images.load(str(stage/filename))
        if tag!="surface": pic.colorspace_settings.name="Non-Color"
        if tag=="finish": pic.alpha_mode="CHANNEL_PACKED"
        pic.pack()
        pic.filepath_raw="//"+os.path.relpath(art/filename,source.parent).replace(os.sep,"/")
        tex=nodes.new("ShaderNodeTexImage")
        tex.image=pic
        tex.location=(-480,y)
        if tag=="surface":
            links.new(tex.outputs["Color"],bsdf.inputs["Base Color"])
        elif tag=="normal":
            normal=nodes.new("ShaderNodeNormalMap")
            normal.location=(50,25)
            normal.inputs["Strength"].default_value=.58
            links.new(tex.outputs["Color"],normal.inputs["Color"])
            links.new(normal.outputs["Normal"],bsdf.inputs["Normal"])
        else:
            separate=nodes.new("ShaderNodeSeparateColor")
            separate.location=(-70,-218)
            links.new(tex.outputs["Color"],separate.inputs[0])
            links.new(separate.outputs["Red"],bsdf.inputs["Metallic"])
            rough=nodes.new("ShaderNodeMath")
            rough.location=(110,-350)
            rough.operation="SUBTRACT"
            rough.inputs[0].default_value=1
            links.new(tex.outputs["Alpha"],rough.inputs[1])
            links.new(rough.outputs[0],bsdf.inputs["Roughness"])
    return mat


def build(stage,art,source):
    if not bpy.app.background: raise RuntimeError("请使用独立的后台 Blender MCP 会话。")
    # 后台会话可多次打样；每次打开空场景，避免复用先前模型或被编号的材质。
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene=bpy.context.scene
    scene.name="无底座行商"
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    g=Workshop()
    sculpt_head(g);hair(g);bonnet(g)
    boots_and_breeches(g);layers(g);overcoat(g);belt(g)
    rug(g)
    right,left=hands(g)
    sleeves(g,True,right);sleeves(g,False,left);bag_and_harness(g);purse(g)
    make_maps(g.charts,stage)
    mat=material(stage,art,source)
    all_pts=[p for item in g.parts for p in item[1]]+g.verts
    lo=Vector([min(p[i] for p in all_pts) for i in range(3)])
    hi=Vector([max(p[i] for p in all_pts) for i in range(3)])
    center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    scale=HEIGHT/(hi.z-lo.z)
    parts=g.export_parts(scene,mat,scale,center)
    prepare_sources(parts)
    indent_wool(scene,scale)
    scene["原型中心"]=list(center)
    scene["原型比例"]=scale
    for screen in bpy.data.screens:
        for area in screen.areas:
            if area.type=="VIEW_3D":
                view=area.spaces.active
                view.clip_start,view.clip_end=.00002,1
                view.region_3d.view_perspective="ORTHO"
                view.region_3d.view_distance=.028
                view.region_3d.view_location=(0,0,HEIGHT*.50)
                view.region_3d.view_rotation=Vector((0,1,-.08)).to_track_quat("-Z","Y")
                view.overlay.show_floor=view.overlay.show_cursor=False
                view.overlay.show_axis_x=view.overlay.show_axis_y=False
                view.shading.type="MATERIAL"
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(stage/source.name),compress=True,relative_remap=False)
    def triangles(obj):
        obj.data.calc_loop_triangles()
        return len(obj.data.loop_triangles)
    def join(items):
        bpy.ops.object.select_all(action="DESELECT")
        for item in items: item.select_set(True)
        bpy.context.view_layer.objects.active=items[0]
        bpy.ops.object.join()
        return bpy.context.object
    def reduce(obj,limit):
        mod=obj.modifiers.new("棋盘网格","DECIMATE")
        mod.ratio=min(1,limit/triangles(obj))
        mod.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # 全身仍为同一个游戏网格。分配手指轮廓的面数，避免全局减面把指甲和指蹼抹掉。
    hand_parts=[p for p in parts if p.name in (OFFER,HOLD)]
    others=[p for p in parts if p not in hand_parts]
    for item in parts: item.data.transform(Matrix.Scale(1/scale,4))
    for item in hand_parts:
        bpy.context.view_layer.objects.active=item
        bpy.ops.object.modifier_apply(modifier=item.modifiers[0].name)
    full=sum(triangles(p) for p in parts)
    for item in hand_parts:
        bpy.context.view_layer.objects.active=item
        reduce(item,3600)
    body=join(others)
    reduce(body,74000-sum(triangles(p) for p in hand_parts))
    obj=join([body,*hand_parts])
    for group in list(obj.vertex_groups): obj.vertex_groups.remove(group)
    obj.name=obj.data.name="peddler-stall"
    final_lo=Vector([min(v.co[i] for v in obj.data.vertices) for i in range(3)])
    final_hi=Vector([max(v.co[i] for v in obj.data.vertices) for i in range(3)])
    final_center=Vector(((final_lo.x+final_hi.x)/2,(final_lo.y+final_hi.y)/2,final_lo.z))
    obj.data.transform(Matrix.Scale(HEIGHT/(final_hi.z-final_lo.z),4)@Matrix.Translation(-final_center))
    obj.data.update()
    obj.data.calc_loop_triangles()
    bpy.ops.export_scene.fbx(filepath=str(stage/"peddler-stall.fbx"),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",bake_space_transform=True,apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
    return {"材质":mat.name,"分件":len(parts),"母版三角面":full,"棋盘三角面":len(obj.data.loop_triangles),
            "宽深高毫米":[round((hi[i]-lo[i])*scale*1000,4) for i in range(3)]}


def produce(art,source):
    art.mkdir(parents=True,exist_ok=True)
    source.parent.mkdir(parents=True,exist_ok=True)
    work=ROOT/".local"
    work.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(prefix="merchant-work-",dir=work) as folder:
        stage=Path(folder)
        report=build(stage,art,source)
        targets=[art/f"peddler-{tag}.png" for tag in ("surface","normal","finish")]+[art/"peddler-stall.fbx",source]
        updated=[]
        try:
            for target in targets:
                backup=stage/("previous-"+target.name)
                if target.exists(): shutil.copyfile(target,backup)
                (stage/target.name).replace(target)
                updated.append((target,backup))
        except OSError:
            for target,backup in reversed(updated):
                if backup.exists(): backup.replace(target)
                else: target.unlink()
            raise
        return report


result=produce(Path(globals().get("art_dir",ART)),Path(globals().get("source_file",SOURCE)))
