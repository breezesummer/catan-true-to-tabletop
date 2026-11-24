"""在独立 Blender MCP 会话内生成分件母版、正式模型和轻量拾取网格。"""
from pathlib import Path
import sys
import json

import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from surfaces import ART, MODEL, PREVIEW, SOURCE
from joinery import Part, RNG, V, UP, tube
from warehouse import (warehouse, emblem, sign_point, SIGN_HALF, SIGN_RISE, BED, FLOOR,
                       TOWER_X,TOWER_HALF,TOWER_FRONT,TOWER_BACK,TOWER_EAVE,TOWER_RIDGE,
                       STORE_LEFT,STORE_RIGHT,STORE_FRONT,STORE_BACK,STORE_MID,STORE_EAVE,STORE_RIDGE)
from waterside import (goods, landing, hoist, PIVOT, TIP, LOAD, SEA_END, QUAY_FRONT, QUAY_BACK,
                       QUAY_LEFT, QUAY_RIGHT, PIER_LEFT, PIER_RIGHT, PILE_X, PILE_Y)


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import trade_plaque


def material():
    mat=bpy.data.materials.new("LumberHarbor")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    link=mat.node_tree.links.new
    shader=nodes.get("Principled BSDF")
    for kind,x,y in (("albedo",-650,360),("normal",-650,70),("finish",-650,-220)):
        img=bpy.data.images.load(str(ART/("lumber-"+kind+".png")))
        if kind!="albedo": img.colorspace_settings.name="Non-Color"
        if kind=="finish": img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        tex=nodes.new("ShaderNodeTexImage")
        tex.image=img
        tex.location=(x,y)
        if kind=="albedo":
            link(tex.outputs["Color"],shader.inputs["Base Color"])
            albedo=tex
        elif kind=="normal":
            bump=nodes.new("ShaderNodeNormalMap")
            bump.location=(-300,70)
            bump.inputs["Strength"].default_value=.82
            link(tex.outputs["Color"],bump.inputs["Color"])
            link(bump.outputs["Normal"],shader.inputs["Normal"])
        else:
            separate=nodes.new("ShaderNodeSeparateColor")
            separate.location=(-430,-215)
            link(tex.outputs["Color"],separate.inputs[0])
            link(separate.outputs[0],shader.inputs["Metallic"])
            invert=nodes.new("ShaderNodeMath")
            invert.operation="SUBTRACT"
            invert.inputs[0].default_value=1
            invert.location=(-210,-305)
            link(tex.outputs["Alpha"],invert.inputs[1])
            link(invert.outputs[0],shader.inputs["Roughness"])
    nodes.active=albedo
    return mat


def proxy_geometry(scene,mat):
    pick=Part("PickVolume")
    footprint=Part("ShoreFootprint")
    def sheet(p,points):
        p.face(p.verts(points),"oak",[(.2,.3)]*len(points))
    def prism(ring,lower):
        sheet(pick,ring)
        sheet(pick,lower[::-1])
        for i in range(len(ring)):
            j=(i+1)%len(ring)
            sheet(pick,[ring[i],lower[i],lower[j],ring[j]])
    def rectangle(x0,x1,y0,y1,top,bottom,project=False):
        ring=[(x0,y0,top),(x1,y0,top),(x1,y1,top),(x0,y1,top)]
        prism(ring,[(x,y,bottom) for x,y,_ in ring])
        if project:
            sheet(footprint,[(x,y,0) for x,y,_ in ring])
    def roof(ring):
        prism(ring,[(x,y,z-.28) for x,y,z in ring])
        sheet(footprint,[(x,y,0) for x,y,z in ring])
    rectangle(TOWER_X-TOWER_HALF,TOWER_X+TOWER_HALF,TOWER_FRONT,TOWER_BACK,TOWER_EAVE,BED)
    rectangle(STORE_LEFT,STORE_RIGHT,STORE_FRONT,STORE_BACK,STORE_EAVE,BED)
    for s in (-1,1):
        x=TOWER_X+s*(TOWER_HALF+.44)
        a,b=TOWER_FRONT-.55,TOWER_BACK+.44
        roof([(TOWER_X,a,TOWER_RIDGE+.39),(x,a,TOWER_EAVE-.23),(x,b,TOWER_EAVE-.23),(TOWER_X,b,TOWER_RIDGE+.39)])
        y=STORE_FRONT-.45 if s<0 else STORE_BACK+.45
        a,b=STORE_LEFT,STORE_RIGHT+.43
        roof([(a,STORE_MID,STORE_RIDGE+.39),(a,y,STORE_EAVE-.27),(b,y,STORE_EAVE-.27),(b,STORE_MID,STORE_RIDGE+.39)])
    frame=[(-SIGN_HALF,-SIGN_RISE),(SIGN_HALF,-SIGN_RISE),(SIGN_HALF,SIGN_RISE),(-SIGN_HALF,SIGN_RISE)]
    prism([sign_point(x,v,.25) for x,v in frame],[sign_point(x,v,-.04) for x,v in frame])
    rectangle(QUAY_LEFT,QUAY_RIGHT,QUAY_FRONT-.10,QUAY_BACK,FLOOR,BED,True)
    rectangle(PIER_LEFT,PIER_RIGHT,SEA_END,QUAY_FRONT+.19,FLOOR,-.66,True)
    for x in PILE_X:
        for y in PILE_Y:
            tube(pick,[(x,y,-4.8),(x,y,-.49)],.47,"oak",8)
    rectangle(2.62,6.18,-2.79,-1.00,1.76,.18)
    axis=V((.47,1,0)).normalized()
    cross=axis.cross(UP)
    for u,z in ((-.69,0),(.69,0),(0,1.18)):
        a=V((8.37,-2.93,1.16))+u*cross+z*UP
        tube(pick,[a,a+axis*3.98],.73,"bark",10)
    for a,b,r in ((PIVOT,TIP,.39),(PIVOT-UP*3,TIP-UP*.3,.25),(TIP,LOAD+UP*2.6,.14)):
        tube(pick,[a,b],r,"oak",8)
    rectangle(LOAD.x-1.70,LOAD.x+1.70,LOAD.y-.80,LOAD.y+.80,LOAD.z+1.13,LOAD.z-.42)
    return [p.object(scene,mat) for p in (pick,footprint)]


def export(objs,path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objs: obj.select_set(True)
    bpy.context.view_layer.objects.active=objs[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,
        use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    SOURCE.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    if not bpy.app.background or bpy.data.filepath:
        raise RuntimeError("请在新开的后台 Blender MCP 会话中制作母版。")
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    RNG.seed(20261120)
    source=bpy.context.scene
    source.name="木材港"
    source.unit_settings.system="METRIC"
    source.unit_settings.length_unit="MILLIMETERS"
    bpy.context.window.scene=source
    mat=material()
    groups={"HarborHouse":warehouse(),"TradePlaque":(emblem(),),"LogStack":goods(),
            "PierDeck":landing(),"TimberHoist":hoist()}
    objects={name:[p.object(source,mat) for p in parts] for name,parts in groups.items()}
    objects["TradePlaque"]=trade_plaque(source,sign_point,2*SIGN_HALF,objects["TradePlaque"][0])
    for label,names in (("吊楼和石仓",("HarborHouse",)),("屋面交易牌",("TradePlaque",)),("库前备货",("LogStack",)),
                        ("驳岸与挑栈",("PierDeck",)),("原木起吊",("TimberHoist",))):
        collection=bpy.data.collections.new(label)
        source.collection.children.link(collection)
        for name in names:
            for item in objects[name]:
                collection.objects.link(item)
                source.collection.objects.unlink(item)
    # 母版按工种分件；打开文件时直接看向临海正面。
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            space=area.spaces.active
            space.region_3d.view_distance=.061
            space.region_3d.view_location=(0,.002,.005)
            space.region_3d.view_rotation=Vector((-.46,1,-.45)).to_track_quat("-Z","Y")
            space.region_3d.view_perspective="ORTHO"
            space.clip_start=.00007
            space.overlay.show_floor=False
            space.overlay.show_axis_x=False
            space.overlay.show_axis_y=False
            space.shading.color_type="TEXTURE"
            space.shading.type="MATERIAL"
    # 保存独立源场景，附着的数据块和贴图都随文件写入。
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"原木商港.blend"),compress=True,copy=True)
    game=bpy.data.scenes.new("木材港导出")
    bpy.context.window.scene=game
    finished=[]
    counts={}
    for name,items in objects.items():
        bpy.ops.object.select_all(action="DESELECT")
        for src in items:
            obj=src.copy()
            obj.data=src.data.copy()
            game.collection.objects.link(obj)
            obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        if len(items)>1: bpy.ops.object.join()
        obj.name=obj.data.name=name
        obj.data.calc_loop_triangles()
        counts[name]=len(obj.data.loop_triangles)
        finished.append(obj)
    export(finished,MODEL/"lumber-harbor.fbx")
    pick=proxy_geometry(game,mat)
    export(pick,MODEL/"lumber-selection.fbx")
    (PREVIEW/"模型统计.json").write_text(json.dumps(counts,indent=2),encoding="utf-8")
    return {"source_parts":sum(map(len,objects.values())),"triangles":counts,"renderers":len(finished)}


result=build()
