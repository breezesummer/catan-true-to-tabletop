"""通过 Blender MCP 制作分件母版，并导出正式网格与独立的交互轮廓。"""
from pathlib import Path
import json
import sys
import bpy
from mathutils import Vector

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
from kiln_atlas import ART,MODEL,PREVIEW,SOURCE
from fabrication import Part,R
from joinery import RNG as timber_rng
from storehouse import (TOWER_X,TOWER_HALF,TOWER_FRONT,TOWER_BACK,TOWER_EAVE,TOWER_RIDGE,
                       HALL_LEFT,HALL_RIGHT,HALL_FRONT,HALL_BACK,HALL_EAVE,HALL_RIDGE,SIGN,SIGN_W,SIGN_H,
                       storehouse,emblem)
from landing import (FRONT,CARGO,FLOOR,SEA_END,JETTY_HALF,PIER_TOP,HOOK_X,HOOK_Y,
                     goods,crane,quay,jetty,offset_bank)


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import trade_plaque


def material():
    mat=bpy.data.materials.new("BrickHarbor")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    link=mat.node_tree.links.new
    bsdf=nodes.get("Principled BSDF")
    for kind,y in (("albedo",320),("normal",0),("finish",-310)):
        img=bpy.data.images.load(str(ART/f"brick-{kind}.png"))
        if kind!="albedo": img.colorspace_settings.name="Non-Color"
        if kind=="finish": img.alpha_mode="CHANNEL_PACKED"
        img.pack()
        n=nodes.new("ShaderNodeTexImage")
        n.image=img
        n.location=(-660,y)
        if kind=="albedo":
            link(n.outputs["Color"],bsdf.inputs["Base Color"])
            albedo=n
        elif kind=="normal":
            normal=nodes.new("ShaderNodeNormalMap")
            normal.location=(-300,y)
            normal.inputs["Strength"].default_value=.82
            link(n.outputs["Color"],normal.inputs["Color"])
            link(normal.outputs["Normal"],bsdf.inputs["Normal"])
        else:
            channels=nodes.new("ShaderNodeSeparateColor")
            channels.location=(-420,y)
            link(n.outputs["Color"],channels.inputs[0])
            link(channels.outputs[0],bsdf.inputs["Metallic"])
            invert=nodes.new("ShaderNodeMath")
            invert.operation="SUBTRACT"
            invert.inputs[0].default_value=1
            invert.location=(-180,y-90)
            link(n.outputs["Alpha"],invert.inputs[1])
            link(invert.outputs[0],bsdf.inputs["Roughness"])
    nodes.active=albedo
    return mat


def selection(scene,mat):
    pick,foot=Part("PickVolume"),Part("ShoreFootprint")
    def face(part,points):
        part.face(part.verts(points),"oak",[(.5+x*.024,.36+y*.023) for x,y,z in points])
    def prism(top,depth,project=False):
        bottom=[(x,y,depth) for x,y,z in top]
        face(pick,top)
        face(pick,bottom[::-1])
        for i in range(len(top)):
            j=(i+1)%len(top)
            face(pick,[top[i],bottom[i],bottom[j],top[j]])
        if project: face(foot,[(x,y,0) for x,y,z in top])
    def box(x0,x1,y0,y1,base,height,project=False):
        prism([(x0,y0,height),(x1,y0,height),(x1,y1,height),(x0,y1,height)],base,project)
    # 岸线遮罩只取真实石岸和挑栈的并集，栈桥侧边的海水仍可见。
    prism([(p.x,p.y,FLOOR) for p in offset_bank(-.14)],-4.90,True)
    box(TOWER_X-JETTY_HALF,TOWER_X+JETTY_HALF,SEA_END,FRONT+.47,PIER_TOP-.76,PIER_TOP,True)
    def saddle(cx,cy,run,length,eave,ridge,along_x):
        def at(s,t,h): return (cx+t,cy+s,h) if along_x else (cx+s,cy+t,h)
        for s in (-1,1):
            top=[at(0,-length,ridge),at(s*run,-length,eave),at(s*run,length,eave),at(0,length,ridge)]
            prism(top,FLOOR)
    saddle(TOWER_X,(TOWER_FRONT+TOWER_BACK)/2,TOWER_HALF+.43,(TOWER_BACK-TOWER_FRONT)/2+.43,
           TOWER_EAVE-.45,TOWER_RIDGE+.20,False)
    saddle((HALL_LEFT+HALL_RIGHT)/2,(HALL_FRONT+HALL_BACK)/2,(HALL_BACK-HALL_FRONT)/2+.45,
           (HALL_RIGHT-HALL_LEFT)/2+.45,HALL_EAVE-.43,HALL_RIDGE+.22,True)
    plaque_top=[tuple(SIGN.at(x,y,-.61)) for x,y in ((-SIGN_W/2,-SIGN_H/2),(SIGN_W/2,-SIGN_H/2),
                (SIGN_W/2,SIGN_H/2),(-SIGN_W/2,SIGN_H/2))]
    prism(plaque_top,FLOOR)
    for x,y,nx,ny,layers in CARGO:
        box(x-nx*.375-.07,x+nx*.375+.07,y-ny*.375-.07,y+ny*.375+.07,FLOOR,FLOOR+.32+layers*.4125)
    box(HOOK_X-1.0,HOOK_X+1.0,HOOK_Y-.69,HOOK_Y+.69,1.00,2.05)
    box(HOOK_X-.13,HOOK_X+.13,HOOK_Y-.13,HOOK_Y+.13,2.05,7.76)
    a,b=Vector((-7.26,1.00,7.70)),Vector((HOOK_X+.19,HOOK_Y-.15,8.00))
    side=Vector((-(b-a).y,(b-a).x,0)).normalized()*.25
    top=[a-side,b-side,b+side,a+side]
    # 只包住吊臂本身，臂下空处由墙面及码头处理。
    face(pick,top)
    lower=[v-Vector((0,0,.50)) for v in top]
    face(pick,lower[::-1])
    for i in range(4):
        j=(i+1)%4
        face(pick,[top[i],lower[i],lower[j],top[j]])
    return [p.object(scene,mat) for p in (pick,foot)]


def export(objects,path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},
        axis_forward="-Z",axis_up="Y",apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,
        use_triangles=True,use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    if not bpy.app.background: raise RuntimeError("砖港制作需要独立的 Blender MCP 后台会话。")
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    R.seed(211720)
    timber_rng.seed(20261120)
    source=bpy.context.scene
    source.name="砖港"
    source.unit_settings.system="METRIC"
    source.unit_settings.length_unit="MILLIMETERS"
    mat=material()
    parts={"HarborHouse":("石木双仓",storehouse()),"TradePlaque":("屋面交易牌",(emblem(),)),
           "BrickStack":("砖垛托盘",goods()),"LoadingCrane":("悬臂吊装",crane()),
           "PierDeck":("木栈桥",jetty()),"QuayWall":("石砌港岸",quay())}
    groups={}
    for key,(label,pieces) in parts.items():
        coll=bpy.data.collections.new(label)
        source.collection.children.link(coll)
        groups[key]=[p.object(source,mat) for p in pieces]
        if key=="TradePlaque": groups[key]=trade_plaque(source,lambda x,y,h: SIGN.at(x,y,-.38-h),SIGN_W,groups[key][0])
        for obj in groups[key]:
            coll.objects.link(obj)
            source.collection.objects.unlink(obj)
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            view=area.spaces.active
            view.region_3d.view_rotation=Vector((-.31,1,-.48)).to_track_quat("-Z","Y")
            view.region_3d.view_location=(0,.003,.0044)
            view.region_3d.view_distance=.055
            view.region_3d.view_perspective="ORTHO"
            view.clip_start=.00006
            view.overlay.show_overlays=False
            view.shading.type="MATERIAL"
    SOURCE.mkdir(parents=True,exist_ok=True)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"砖港.blend"),compress=True,copy=True)
    game=bpy.data.scenes.new("正式网格")
    bpy.context.window.scene=game
    visible=[]
    triangles={}
    for key,items in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for src in items:
            obj=src.copy()
            obj.data=src.data.copy()
            game.collection.objects.link(obj)
            obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        if len(items)>1: bpy.ops.object.join()
        obj.name=obj.data.name=key
        obj.data.calc_loop_triangles()
        triangles[key]=len(obj.data.loop_triangles)
        visible.append(obj)
    export(visible,MODEL/"brick-harbor.fbx")
    proxies=selection(game,mat)
    export(proxies,MODEL/"brick-selection.fbx")
    stats={"分件数":sum(map(len,groups.values())),"正式三角面":triangles}
    (PREVIEW/"模型统计.json").write_text(json.dumps(stats,ensure_ascii=False,indent=2),encoding="utf-8")
    return stats


result=build()
