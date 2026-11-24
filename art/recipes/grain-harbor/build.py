"""在独立 Blender MCP 后台会话中制作谷仓母版、正式游戏网格和拾取网格。"""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from grain_atlas import ART, MODEL, PREVIEW, SOURCE
from grain_mesh import Part, R, UP, V
from granary import (BOTTOM, HB, HE, HF, HH, HL, HR, HY, RUN, SIGN_H, SIGN_W, TB, TE, TF,
                     TR, TW, TX, building, emblem, sp)
from grain_goods import (APRONTIP, BASKET_X, BASKET_Y, CRANE_BASE, CRANE_TIP, JET_LEFT, JET_RIGHT,
                         QUAY_RIGHT, SEA_END, TRAY_X, TRAY_Y, TRAY_Z, crane, goods, landing)
from quay_craft import R as stone_random
from joinery import RNG as timber_random


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import trade_plaque


def create_material():
    mat=bpy.data.materials.new("GrainHarbor")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    link=mat.node_tree.links.new
    shader=nodes.get("Principled BSDF")
    for name,y in (("albedo",360),("normal",40),("finish",-280)):
        img=bpy.data.images.load(str(ART/f"grain-{name}.png"))
        if name!="albedo": img.colorspace_settings.name="Non-Color"
        if name=="finish": img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        tex=nodes.new("ShaderNodeTexImage")
        tex.image=img
        tex.location=(-650,y)
        if name=="albedo":
            link(tex.outputs["Color"],shader.inputs["Base Color"])
            albedo=tex
        elif name=="normal":
            node=nodes.new("ShaderNodeNormalMap")
            node.inputs["Strength"].default_value=.82
            node.location=(-310,y)
            link(tex.outputs["Color"],node.inputs["Color"])
            link(node.outputs[0],shader.inputs["Normal"])
        else:
            split=nodes.new("ShaderNodeSeparateColor")
            split.location=(-390,y)
            link(tex.outputs["Color"],split.inputs[0])
            link(split.outputs[0],shader.inputs["Metallic"])
            sub=nodes.new("ShaderNodeMath")
            sub.operation="SUBTRACT"
            sub.inputs[0].default_value=1
            sub.location=(-170,y-135)
            link(tex.outputs["Alpha"],sub.inputs[1])
            link(sub.outputs[0],shader.inputs["Roughness"])
    nodes.active=albedo
    return mat


def selection(scene,mat):
    pick=Part("PickVolume")
    footprint=Part("ShoreFootprint")

    def face(part,pts):
        part.face(part.verts(pts),"oak",[(.5,.5)]*len(pts))

    def block(top,base,ground=True):
        bottom=[(x,y,base) for x,y,z in top]
        face(pick,top)
        face(pick,bottom[::-1])
        for i in range(len(top)):
            j=(i+1)%len(top)
            face(pick,[top[i],bottom[i],bottom[j],top[j]])
        if ground: face(footprint,[(x,y,0) for x,y,z in top])

    def box(x0,x1,y0,y1,low,high,ground=True):
        block([(x0,y0,high),(x1,y0,high),(x1,y1,high),(x0,y1,high)],low,ground)

    box(TX-TW,TX+TW,TF,TB,BOTTOM,TE)
    for s in (-1,1):
        x=TX+s*(TW+.49)
        block([(TX,TF-.62,TR+.34),(x,TF-.62,TE+.12),(x,TB+.62,TE+.12),(TX,TB+.62,TR+.34)],TE-.26)
    box(HL,HR,HF,HB,BOTTOM,HE)
    for s in (-1,1):
        y=HY+s*RUN
        block([(HL-.50,HY,HH+.31),(HL-.50,y,HE+.13),(HR+.50,y,HE+.13),(HR+.50,HY,HH+.31)],HE-.23)
    face(pick,[sp(x,y,.61) for x,y in ((-SIGN_W/2,-SIGN_H/2),(SIGN_W/2,-SIGN_H/2),
                                         (SIGN_W/2,SIGN_H/2),(-SIGN_W/2,SIGN_H/2))])
    box(-11.21,QUAY_RIGHT,APRONTIP,HF+.12,-.44,.51)
    box(JET_LEFT,JET_RIGHT,SEA_END,APRONTIP,-.44,.51)
    box(5.99,10.28,-2.24,.49,.51,1.52,False)
    for x,h in ((7.17,3.72),(9.07,3.55)):
        box(x-.70,x+.70,-1.09,.37,1.0,h,False)
    box(BASKET_X-.96,BASKET_X+.96,BASKET_Y-.83,BASKET_Y+.83,.51,1.84,False)
    box(TRAY_X-1.32,TRAY_X+1.32,TRAY_Y-.91,TRAY_Y+.91,TRAY_Z-.10,TRAY_Z+.31)
    box(TRAY_X-1.11,TRAY_X+1.11,TRAY_Y-.67,TRAY_Y+.73,TRAY_Z+.23,TRAY_Z+1.83,False)
    axis=(CRANE_TIP-CRANE_BASE).normalized()
    side=V((-axis.y,axis.x,0))*.245
    a,b=CRANE_BASE-axis*.35,CRANE_TIP+axis*.43
    block([a-side+UP*.30,b-side+UP*.30,b+side+UP*.30,a+side+UP*.30],8.80)
    return [p.object(scene,mat) for p in (pick,footprint)]


def export(objects,path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,
        use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    if not bpy.app.background: raise RuntimeError("请使用独立的 Blender MCP 后台会话。")
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    R.seed(21720)
    stone_random.seed(2117)
    timber_random.seed(20261120)
    scene=bpy.context.scene
    scene.name="谷物商港"
    scene.unit_settings.system="METRIC"
    scene.unit_settings.length_unit="MILLIMETERS"
    mat=create_material()
    groups={}
    for role,label,parts in (
        ("HarborHouse","仓房",building()), ("TradePlaque","交易屋牌",(emblem(),)),
        ("PierDeck","码头",landing()), ("GrainCargo","粮货",goods()), ("Hoist","悬臂吊机与粮盘",crane()),
    ):
        folder=bpy.data.collections.new(label)
        scene.collection.children.link(folder)
        objs=[p.object(scene,mat) for p in parts]
        if role=="TradePlaque": objs=trade_plaque(scene,lambda x,y,h: sp(x,y,.35+h),SIGN_W,objs[0])
        for obj in objs:
            folder.objects.link(obj)
            scene.collection.objects.unlink(obj)
        groups[role]=objs
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view=area.spaces.active
            view.region_3d.view_rotation=Vector((-.37,1,-.50)).to_track_quat("-Z","Y")
            view.region_3d.view_location=(0,.0029,.0048)
            view.region_3d.view_distance=.043
            view.region_3d.view_perspective="ORTHO"
            view.clip_start=.00006
            view.overlay.show_overlays=False
            view.shading.type="MATERIAL"
    SOURCE.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"谷物商港.blend"),compress=True,copy=True)
    scene=bpy.data.scenes.new("谷物商港正式网格")
    bpy.context.window.scene=scene
    visible=[]
    triangles={}
    for role,parts in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for part in parts:
            copy=part.copy()
            copy.data=part.data.copy()
            scene.collection.objects.link(copy)
            copy.select_set(True)
        bpy.context.view_layer.objects.active=copy
        if len(parts)>1: bpy.ops.object.join()
        copy.name=copy.data.name=role
        copy.data.calc_loop_triangles()
        triangles[role]=len(copy.data.loop_triangles)
        visible.append(copy)
    export(visible,MODEL/"grain-harbor.fbx")
    proxies=selection(scene,mat)
    export(proxies,MODEL/"grain-selection.fbx")
    stats={"母版分件":sum(len(parts) for parts in groups.values()),"正式三角面":triangles,
           "拾取三角面":len(proxies[0].data.loop_triangles),"避让轮廓三角面":len(proxies[1].data.loop_triangles)}
    (PREVIEW/"模型统计.json").write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding="utf-8")
    return stats


result=build()
