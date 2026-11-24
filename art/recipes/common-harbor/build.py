"""通过 Blender MCP 制作可编辑母版、正式 FBX、拾取体及分区避让轮廓。"""
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from quay_atlas import ART, MODEL, PREVIEW, SOURCE, uv
from quay_craft import Part, R, V
from joinery import RNG as timber_rng
from quay_house import (BOARD_SHIFT, EAST, FLOOR, FRONT, HALF, HALL_EAVE, HALL_PEAK, JOINT, LEFT, REAR,
                        SEA_END, STORE_BACK, STORE_FRONT, TOWER_EAVE, TOWER_PEAK, TOWER_X, buildings)
from quay_landing import (CRATE, JETTY_HALF, JETTY_X, QUAY, TABLET_FIELD, TABLET_HEIGHT, TABLET_LETTER_TOP,
                          TABLET_WIDTH, TIP, crane, deck, stores, tablet_point)


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import trade_plaque


def material():
    mat = bpy.data.materials.new("CommonHarbor")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    link = mat.node_tree.links.new
    shader = nodes.get("Principled BSDF")
    for name,y in (("albedo",290),("normal",0),("finish",-300)):
        image = bpy.data.images.load(str(ART/f"common-{name}.png"))
        if name!="albedo": image.colorspace_settings.name="Non-Color"
        if name=="finish": image.alpha_mode="CHANNEL_PACKED"
        image.pack()
        tex = nodes.new("ShaderNodeTexImage")
        tex.image = image
        tex.location = (-670,y)
        if name=="albedo":
            link(tex.outputs["Color"],shader.inputs["Base Color"])
            base = tex
        elif name=="normal":
            bump = nodes.new("ShaderNodeNormalMap")
            bump.location = (-310,y)
            bump.inputs["Strength"].default_value = .80
            link(tex.outputs["Color"],bump.inputs["Color"])
            link(bump.outputs["Normal"],shader.inputs["Normal"])
        else:
            channels = nodes.new("ShaderNodeSeparateColor")
            channels.location = (-430,y)
            link(tex.outputs["Color"],channels.inputs[0])
            link(channels.outputs[0],shader.inputs["Metallic"])
            rough = nodes.new("ShaderNodeMath")
            rough.location = (-184,y-90)
            rough.operation = "SUBTRACT"
            rough.inputs[0].default_value = 1
            link(tex.outputs["Alpha"],rough.inputs[1])
            link(rough.outputs[0],shader.inputs["Roughness"])
    nodes.active = base
    return mat


def selection(scene,mat):
    pick,foot = Part("PickVolume"),Part("ShoreFootprint")
    def face(part,points):
        part.face(part.verts(points),"oak",[(.32,.44)]*len(points))
    def shell(ring,bottom):
        face(pick,ring)
        face(pick,bottom[::-1])
        for i in range(len(ring)):
            j = (i+1)%len(ring)
            face(pick,[ring[i],bottom[i],bottom[j],ring[j]])
    def prism(ring,lower,project=True):
        shell(ring,[(x,y,lower) for x,y,z in ring])
        if project: face(foot,[(x,y,0) for x,y,z in ring])
    def box(x0,x1,y0,y1,z0,z1,project=True):
        prism([(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)],z0,project)
    def gable(a,b,left,right,eave,ridge,along_y):
        mid = (left+right)/2
        def point(u,v,z):
            return (u,v,z) if along_y else (v,u,z)
        for side in (left,right):
            ring = [point(mid,a,ridge),point(side,a,eave),point(side,b,eave),point(mid,b,ridge)]
            prism(ring,eave-.18)
    box(LEFT,JOINT+.15,FRONT-.18,REAR,-4.87,TOWER_EAVE)
    gable(FRONT-.59,REAR+.42,TOWER_X-HALF-.49,TOWER_X+HALF+.49,TOWER_EAVE-.10,TOWER_PEAK+.24,True)
    box(JOINT,EAST,STORE_FRONT,STORE_BACK,-4.87,HALL_EAVE)
    gable(JOINT-.39,EAST+.40,STORE_FRONT-.44,STORE_BACK+.44,HALL_EAVE-.10,HALL_PEAK+.24,False)
    box(LEFT,EAST,QUAY-.17,FRONT,-4.90,FLOOR)
    box(JOINT,EAST,FRONT,STORE_FRONT,-4.90,FLOOR)
    box(JETTY_X-JETTY_HALF,JETTY_X+JETTY_HALF,SEA_END,QUAY,.12,FLOOR)
    corners = [(u*TABLET_WIDTH*.5,v*TABLET_HEIGHT*.5) for u,v in ((-1,-1),(1,-1),(1,1),(-1,1))]
    shell([tablet_point(u,v,TABLET_LETTER_TOP) for u,v in corners],
          [tablet_point(u,v,.045) for u,v in corners])
    box(5.58,9.34,12.00,15.47,FLOOR,2.18,False)
    box(5.91,7.36,12.21,13.66,2.18,3.56,False)
    for x,y,r,h in ((10.40,12.93,.89,2.22),(10.43,14.85,.78,1.94)):
        box(x-r,x+r,y-r,y+r,FLOOR,FLOOR+h,False)
    x,y,z = CRATE
    box(x-1.07,x+1.07,y-.86,y+.86,z,z+2,False)
    heel = V((-5.76,FRONT-.19,8.80))
    axis = (TIP-heel).normalized()
    side = V((-axis.y,axis.x,0)).normalized()*.30
    top = [heel-axis*.28+side,TIP+axis*.36+side,TIP+axis*.36-side,heel-axis*.28-side]
    prism([(v.x,v.y,v.z+.30) for v in top],8.43,False)
    box(x-.16,x+.16,y-.12,y+.12,z+1.9,9.04,False)
    for p in (pick,foot):
        p.vertices = [(x,y+BOARD_SHIFT*.001,z) for x,y,z in p.vertices]
    return [p.object(scene,mat) for p in (pick,foot)]


def export(objects,path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",bake_space_transform=True,apply_scale_options="FBX_SCALE_ALL",
        use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    if not bpy.app.background: raise RuntimeError("请在独立的 Blender MCP 后台场景生成母版。")
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    R.seed(312117)
    timber_rng.seed(211731)
    scene = bpy.context.scene
    scene.name = "通用港口·吊楼商港"
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.length_unit = "MILLIMETERS"
    mat = material()
    parts = {"HarborHouse":("石砌仓栈与吊楼",buildings()),"HarborCrane":("货吊与索具",crane()),
             "DockCargo":("靠仓待运商货",stores()),"PierDeck":("驳岸与挑栈",deck()),"TradePlaque":("交易铭牌",[])}
    groups = {}
    for key,(label,pieces) in parts.items():
        collection = bpy.data.collections.new(label)
        scene.collection.children.link(collection)
        items = [part.object(scene,mat) for part in pieces]
        if key=="TradePlaque": items=trade_plaque(scene,lambda x,y,h: tablet_point(x,y,.19+h),TABLET_WIDTH)
        for obj in items:
            for v in obj.data.vertices: v.co.y += BOARD_SHIFT*.001
            obj.data.update()
            collection.objects.link(obj)
            scene.collection.objects.unlink(obj)
        groups[key] = items
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view = area.spaces.active
            view.region_3d.view_rotation = Vector((-.36,1,-.48)).to_track_quat("-Z","Y")
            view.region_3d.view_location = (0,0,.005)
            view.region_3d.view_distance = .048
            view.region_3d.view_perspective = "ORTHO"
            view.clip_start = .00008
            view.overlay.show_overlays = False
            view.shading.type = "MATERIAL"
    SOURCE.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"吊楼商港.blend"),copy=True,compress=True)
    runtime = bpy.data.scenes.new("正式游戏模型")
    bpy.context.window.scene = runtime
    visible,counts = [],{}
    for key,items in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for src in items:
            obj = src.copy()
            obj.data = src.data.copy()
            runtime.collection.objects.link(obj)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        if len(items)>1: bpy.ops.object.join()
        obj.name = obj.data.name = key
        obj.data.calc_loop_triangles()
        counts[key] = len(obj.data.loop_triangles)
        visible.append(obj)
    export(visible,MODEL/"common-harbor.fbx")
    proxies = selection(runtime,mat)
    export(proxies,MODEL/"common-selection.fbx")
    stats = {"母版分件":sum(map(len,groups.values())),"正式三角面":counts}
    (PREVIEW/"模型统计.json").write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding="utf-8")
    return stats


result = build()
