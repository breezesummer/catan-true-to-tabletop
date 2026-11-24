"""通过 Blender MCP 在独立场景制作母版，按可交互部位合并正式 FBX。坐标输入以毫米计。"""
import json
from pathlib import Path
import sys
import bpy
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from surfaces_ore import MODEL,PREVIEW,SOURCE,surface
from craft import (BACK,FINGER_END,FINGER_X0,FINGER_X1,FLOOR,HALL_EAVE,HALL_FRONT,HALL_LEFT,HALL_MID,
                   HALL_PEAK,HALL_RIGHT,QUAY_FRONT,QUAY_X,SHORE,TOWER_EAVE,TOWER_FRONT,TOWER_PEAK,TOWER_W,
                   TOWER_X,Part,R,V,beam,skin)
from warehouse_ore import houses
from roof_sign import SIGN_H,SIGN_W,at,lettering,plaque,roofs
from landing_ore import LIFT,PIVOT,SAMPLE,STOCK,STONE_START,cargo,hoist,wharf
from joinery import RNG
from exposure import bake


def selection(scene,mat):
    pick,foot=Part("PickVolume"),Part("ShoreFootprint")
    def prism(top,bottom,project=False):
        skin(pick,top,"oak"); skin(pick,bottom[::-1],"oak")
        for i in range(len(top)):
            j=(i+1)%len(top)
            skin(pick,[bottom[i],bottom[j],top[j],top[i]],"oak")
        if project: skin(foot,[(x,y,0) for x,y,z in top],"oak")
    def box(x0,x1,y0,y1,z0,z1,project=False):
        xy=[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
        prism([(x,y,z1) for x,y in xy],[(x,y,z0) for x,y in xy],project)
    def roof(a,b,c,d):
        points=[a,b,c,d]
        prism(points,[(x,y,z-.30) for x,y,z in points])
    box(-QUAY_X,QUAY_X,QUAY_FRONT,SHORE,-.45,FLOOR,True)
    box(FINGER_X0,FINGER_X1,FINGER_END,QUAY_FRONT,-.47,FLOOR,True)
    box(STONE_START,QUAY_X,QUAY_FRONT-.08,QUAY_FRONT+.64,-4.55,FLOOR)
    box(QUAY_X-.64,QUAY_X+.06,QUAY_FRONT+.40,SHORE-.04,-4.55,FLOOR)
    box(STONE_START-.08,STONE_START+.64,QUAY_FRONT+.40,1.17,-4.55,FLOOR)
    tower_left,tower_right=TOWER_X-TOWER_W,TOWER_X+TOWER_W
    box(tower_left,tower_right,TOWER_FRONT,BACK,4.45,TOWER_EAVE)
    for a,b in ((tower_left,TOWER_X-1.53),(TOWER_X+1.53,tower_right)):
        box(a,b,TOWER_FRONT-.11,BACK,FLOOR,4.45)
    box(tower_left,tower_right,TOWER_FRONT+1.30,BACK,FLOOR,4.46)
    for x in (tower_left-.39,tower_right+.39):
        roof((x,TOWER_FRONT-.43,TOWER_EAVE-.26),(x,BACK+.32,TOWER_EAVE-.26),
             (TOWER_X,BACK+.32,TOWER_PEAK+.29),(TOWER_X,TOWER_FRONT-.43,TOWER_PEAK+.29))
    box(HALL_LEFT,HALL_RIGHT,HALL_FRONT,BACK,FLOOR,HALL_EAVE)
    for y in (HALL_FRONT-.44,BACK+.44):
        roof((HALL_LEFT-.09,y,HALL_EAVE-.29),(HALL_RIGHT+.38,y,HALL_EAVE-.29),
             (HALL_RIGHT+.38,HALL_MID,HALL_PEAK+.30),(HALL_LEFT-.09,HALL_MID,HALL_PEAK+.30))
    plaque_xy=[(-SIGN_W/2,-SIGN_H/2),(SIGN_W/2,-SIGN_H/2),(SIGN_W/2,SIGN_H/2),(-SIGN_W/2,SIGN_H/2)]
    prism([at(x,y,.31) for x,y in plaque_xy],[at(x,y,-.09) for x,y in plaque_xy])
    for (x,y),w,d,lo,hi in ((STOCK,4.44,2.70,FLOOR,2.85),(SAMPLE,2.77,1.68,FLOOR,1.54),
                             (LIFT,2.70,1.83,1.07,2.60)):
        box(x-w/2,x+w/2,y-d/2,y+d/2,lo,hi)
    x,y=LIFT
    box(x-.10,x+.10,y-.10,y+.10,2.55,8.77)
    # 吊臂的平行四边形轮廓避免吞掉臂前的水面操作区。
    anchor=V(PIVOT); end=V((x,y,8.98))
    side=(end-anchor).cross(V((0,0,1))).normalized()*.26
    ring=[anchor+side,end+side,end-side,anchor-side]
    prism([v+V((0,0,.22)) for v in ring],[v-V((0,0,.27)) for v in ring])
    return [part.object(scene,mat) for part in (pick,foot)]


def export(objects,path):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={"MESH"},axis_forward="-Z",
        axis_up="Y",apply_scale_options="FBX_SCALE_ALL",bake_space_transform=True,use_triangles=True,
        use_tspace=True,bake_anim=False,add_leaf_bones=False,path_mode="STRIP")


def build():
    if not bpy.app.background: raise RuntimeError("请在独立后台 Blender MCP 会话制作。")
    bpy.ops.wm.read_homefile(use_factory_startup=True,use_empty=True)
    R.seed(211721); RNG.seed(721117)
    scene=bpy.context.scene; scene.name="矿石港 · 原矿商港"
    scene.unit_settings.system="METRIC"; scene.unit_settings.length_unit="MILLIMETERS"
    mat=surface()
    frame,rig,bucket=hoist()
    items={"HarborHouse":("双仓",(*houses(),*roofs())),
           "TradePlaque":("坡面交易牌",(plaque(),)),"PierDeck":("桩埠与短指桥",wharf()),
           "OreCargo":("矿箱与浅盘",cargo()),"LoadingCrane":("吊机",(frame,rig)),"OreSling":("吊斗",(bucket,))}
    groups={}
    for key,(label,parts) in items.items():
        coll=bpy.data.collections.new(label); scene.collection.children.link(coll)
        groups[key]=[part.object(scene,mat) for part in parts]
        if key=="TradePlaque": groups[key].append(lettering(scene,mat))
        for obj in groups[key]:
            coll.objects.link(obj); scene.collection.objects.unlink(obj)
    for area in bpy.context.screen.areas:
        if area.type=="VIEW_3D":
            space=area.spaces.active; view=space.region_3d
            view.view_rotation=V((-.46,1,-.51)).to_track_quat("-Z","Y"); view.view_location=(0,.002,.0044)
            view.view_distance=.043; view.view_perspective="ORTHO"; space.clip_start=.00004
            space.overlay.show_overlays=False; space.shading.type="MATERIAL"
    bake([obj for parts in groups.values() for obj in parts])
    SOURCE.mkdir(parents=True,exist_ok=True); PREVIEW.mkdir(parents=True,exist_ok=True)
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/"原矿商港.blend"),compress=True,copy=True)
    game=bpy.data.scenes.new("棋盘实体"); bpy.context.window.scene=game
    visible=[]; counts={}
    for key,parts in groups.items():
        bpy.ops.object.select_all(action="DESELECT")
        for src in parts:
            obj=src.copy(); obj.data=src.data.copy()
            game.collection.objects.link(obj); obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        if len(parts)>1: bpy.ops.object.join()
        obj.name=obj.data.name=key
        obj.data.calc_loop_triangles(); counts[key]=len(obj.data.loop_triangles)
        visible.append(obj)
    export(visible,MODEL/"ore-harbor.fbx")
    proxy=selection(game,mat)
    export(proxy,MODEL/"ore-selection.fbx")
    report={"分件":sum(map(len,groups.values())),"分部三角面":counts}
    (PREVIEW/"模型统计.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    return report

result=build()
