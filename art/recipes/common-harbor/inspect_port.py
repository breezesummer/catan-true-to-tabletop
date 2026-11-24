"""从游戏 FBX 回读轴向、贴图、用水线和岸线校尺，再按四视原型拍摄。"""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from quay_atlas import MODEL, PREVIEW, SOURCE
from quay_house import BOARD_SHIFT
from quay_landing import (TABLET_CENTER, TABLET_FIELD, TABLET_HEIGHT, TABLET_NORMAL,
                          TABLET_UP, TABLET_WIDTH, tablet_point)


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import bind_preview


def measure_roof_plaque(plaque,house):
    shift = Vector((0,BOARD_SHIFT,0))
    center = TABLET_CENTER+shift
    vertices = [(plaque.matrix_world @ v.co)*1000 for v in plaque.data.vertices]
    local = [pt-center for pt in vertices]
    us = [v.x for v in local]
    vs = [v.dot(TABLET_UP) for v in local]
    depths = [v.dot(TABLET_NORMAL) for v in local]
    assert abs(min(us)+5.65)<.015 and abs(max(us)-5.65)<.015, "铭牌未对准仓门或牌宽错误"
    assert abs(min(vs)+TABLET_HEIGHT*.51)<.015 and abs(max(vs)-TABLET_HEIGHT*.51)<.015, "牌高或坡面中心错误"
    assert min(pt.z for pt in vertices)>6.7 and -.21<min(depths)<-.17 and .48<max(depths)<.55, "铭牌脱离屋面"
    # 取铸字正面和倒角。通用港与矿石港的 1 共用 1.807 × 2.605 毫米的字模。
    letters=[(u,v) for u,v,d in zip(us,vs,depths) if d>TABLET_FIELD+.07 and abs(u)<4]
    one=[(u,v) for u,v in letters if u>.9]
    three=[v for u,v in letters if u<-.55]
    bottom,top=min(v for _,v in one),max(v for _,v in one)
    assert abs(max(u for u,_ in one)-min(u for u,_ in one)-1.807)<.01 and abs(top-bottom-2.605)<.01, "1 的字号偏离矿港"
    assert abs(min(three)-bottom)<.01 and abs(max(three)-top)<.01, "3 与 1 的顶线或基线不齐"
    back = [pt for pt,d in zip(vertices,depths) if d<min(depths)+.005]
    rise,run = (max(v[i] for v in back)-min(v[i] for v in back) for i in (2,1))
    pitch = math.degrees(math.atan2(rise,run))
    assert 44<pitch<45, "牌背倾角与仓房临海屋坡不符"
    inverse = house.matrix_world.inverted()
    direction = (inverse.to_3x3() @ -TABLET_NORMAL).normalized()
    gaps = []
    for u in (-.36,0,.36):
        for v in (-.32,0,.32):
            origin = (tablet_point(u*TABLET_WIDTH,v*TABLET_HEIGHT,1)+shift)*.001
            hit,pt,_,_ = house.ray_cast(inverse @ origin,direction)
            assert hit, "铭牌下方缺少仓房屋面"
            gap = TABLET_FIELD-((house.matrix_world @ pt)*1000-center).dot(TABLET_NORMAL)
            assert 0<gap<.31, "瓦片穿出牌面或铭牌悬空"
            gaps.append(gap)
    return {"牌宽毫米":round(max(us)-min(us),2),"沿坡高度毫米":round(max(vs)-min(vs),2),
            "临海屋坡倾角度":round(pitch,2),"字面至瓦面九点间距毫米":[round(min(gaps),2),round(max(gaps),2)]}


def contact_sheet():
    from PIL import Image, ImageDraw, ImageFont
    views = (("正面","正面 · 临海"),("右侧","右侧"),("俯视","俯视"),("海侧斜视","海侧斜视"))
    if not all((PREVIEW/(name+".png")).is_file() for name,_ in views): return
    sheet = Image.new("RGB",(1654,1416),(241,238,231))
    pen = ImageDraw.Draw(sheet)
    font = ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
    for i,(name,label) in enumerate(views):
        x,y = 18+(i%2)*817,18+(i//2)*703
        with Image.open(PREVIEW/(name+".png")) as frame:
            sheet.paste(frame.convert("RGB").resize((800,640),Image.Resampling.LANCZOS),(x,y))
        pen.text((x+3,y+648),label,fill=(64,58,48),font=font)
    sheet.save(PREVIEW/"吊楼商港四视图.jpg",quality=93,subsampling=0)


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene = bpy.context.scene
    with bpy.data.libraries.load(str(SOURCE/"吊楼商港.blend")) as (src,dst):
        count = len(src.objects)
        dst.materials = ["CommonHarbor","OreHarbor"]
    mat, ore = dst.materials
    maps = [n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(maps)==3 and all(i.packed_file and tuple(i.size)==(2048,2048) for i in maps)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"common-harbor.fbx"),use_anim=False)
    objects = {o.name:o for o in scene.objects if o.type=="MESH"}
    assert set(objects)=={"HarborHouse","HarborCrane","DockCargo","PierDeck","TradePlaque"}
    visible,bounds = [],{}
    for name,obj in objects.items():
        bind_preview(obj, mat, ore)
        pts = [obj.matrix_world @ v.co for v in obj.data.vertices]
        bounds[name] = [(min(p[i] for p in pts),max(p[i] for p in pts)) for i in (0,1,2)]
        visible.extend(p for p in pts if p.z>=-.00355)
    assert bounds["PierDeck"][2][0]<-.0048 and bounds["HarborHouse"][2][0]<-.0045
    assert .0097<bounds["HarborHouse"][1][1]<.0108 and bounds["HarborHouse"][1][0]<0
    plaque = measure_roof_plaque(objects["TradePlaque"],objects["HarborHouse"])
    assert bounds["HarborCrane"][0][0]<0
    assert abs(bounds["PierDeck"][1][0]+.012)<.0001
    if view=="海侧斜视":
        sizes = [round((max(b[i][1] for b in bounds.values())-min(b[i][0] for b in bounds.values()))*1000,2) for i in (0,1,2)]
        report = {"Blender":bpy.app.version_string,"母版分件":count,"正式网格":len(objects),
                  "尺寸毫米":sizes,"水面相对岸面毫米":-3.5,"木桩最低毫米":round(bounds["PierDeck"][2][0]*1000,2),
                  "栈桥外沿距岸线毫米":round(9.90-bounds["PierDeck"][1][0]*1000,2),"内嵌图集":[2048,2048,3],
                  "屋面铭牌":plaque}
        (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 28
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 6
    scene.world = bpy.data.worlds.new("沿岸散射天光")
    scene.world.use_nodes = True
    bg = scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (.79,.85,.94,1)
    bg.inputs[1].default_value = .40
    # 摄影承影面沿棋盘水位布置，侧视可以读到驳岸潮带与浸水木桩。
    floor = bpy.data.meshes.new("水线摄影纸")
    floor.from_pydata([(-1,-1,-.00354),(1,-1,-.00354),(1,1,-.00354),(-1,1,-.00354)],[],[(0,1,2,3)])
    ob = bpy.data.objects.new("棚拍承影面",floor)
    scene.collection.objects.link(ob)
    paper = bpy.data.materials.new("青灰承影面")
    paper.diffuse_color = (.37,.421,.416,1)
    ob.data.materials.append(paper)
    aim = Vector((0,0,.0044))
    def light(name,loc,energy,size,color):
        data = bpy.data.lights.new(name,"AREA")
        data.energy,data.shape,data.size,data.color = energy,"DISK",size,color
        ob = bpy.data.objects.new(name,data)
        scene.collection.objects.link(ob)
        ob.location = loc
        ob.rotation_euler = (aim-ob.location).to_track_quat("-Z","Y").to_euler()
    light("左前窗光",(-.044,-.051,.073),.120,.042,(1,.91,.79))
    light("海面回光",(.048,-.042,.034),.031,.058,(.79,.91,1))
    light("屋瓦边光",(.027,.055,.063),.072,.047,(1,.91,.78))
    views = {"海侧斜视":(.39,-1,.50),"正面":(0,-1,.19),"右侧":(1,-.018,.13),"俯视":(0,0,1)}
    offset = Vector(views[view]).normalized()
    rotation = Quaternion((1,0,0,0)) if view=="俯视" else (-offset).to_track_quat("-Z","Y")
    right,up = (rotation @ Vector(v) for v in ((1,0,0),(0,1,0)))
    xs,zs = [p.dot(right) for p in visible],[p.dot(up) for p in visible]
    target = right*((min(xs)+max(xs))*.5)+up*((min(zs)+max(zs))*.5)+offset*aim.dot(offset)
    data = bpy.data.cameras.new("正交机位")
    data.type = "ORTHO"
    data.clip_start = .00010
    data.ortho_scale = 1.15*max(max(xs)-min(xs),1.25*(max(zs)-min(zs)))
    cam = bpy.data.objects.new("商港摄影机",data)
    scene.collection.objects.link(cam)
    cam.location = target+offset*.14
    cam.rotation_euler = rotation.to_euler()
    scene.camera = cam
    scene.render.resolution_x,scene.render.resolution_y = 1400,1120
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.view_settings.look = "AgX - Medium High Contrast"
    scene.render.filepath = str(PREVIEW/(view+".png"))
    bpy.ops.render.render(write_still=True)
    if view=="俯视": contact_sheet()
    return {"机位":view,"网格":len(objects),"母版分件":count}


result = inspect(globals().get("view","海侧斜视"))
