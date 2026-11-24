"""通过 Blender MCP 从分件源场景出图；摄影棚只存在于本次渲染。"""
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT/"Unity/Catan/Assets/Art/NaturalPieces/v001"
OUT = Path(globals().get("OUT_DIR", ROOT/"art/previews/round-door-village"))
OUT.mkdir(parents=True,exist_ok=True)
source = Path(globals().get("SOURCE", ROOT/"art/source/round-door-village/圆门村庄.blend"))
if Path(bpy.data.filepath).resolve()==source:
    scene = bpy.data.scenes["圆门村庄"].copy()
else:
    with bpy.data.libraries.load(str(source)) as (src, dst):
        dst.scenes = [src.scenes[0]]
    scene = dst.scenes[0]
bpy.context.window.scene = scene
scene.render.engine = "CYCLES"
scene.cycles.samples = 38
scene.cycles.use_denoising = True
scene.cycles.transparent_max_bounces = 3
scene.cycles.max_bounces = 5
prefs = bpy.context.preferences.addons["cycles"].preferences
for backend in ("HIP","OPTIX","METAL"):
    try:
        prefs.compute_device_type = backend
        prefs.refresh_devices()
        if any(d.type == backend for d in prefs.devices):
            for device in prefs.devices:
                device.use = device.type == backend
            scene.cycles.device = "GPU"
            break
    except (TypeError,RuntimeError):
        pass
scene.world = bpy.data.worlds.new("漫射天光")
scene.world.use_nodes = True
bg = scene.world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (.70,.78,.90,1)
bg.inputs[1].default_value = .31
scene.view_settings.view_transform = "AgX"
scene.render.image_settings.file_format = "PNG"
scene.render.resolution_percentage = 100
target = Vector((0,0,.0062))


def aim(obj):
    obj.rotation_euler = (target-obj.location).to_track_quat("-Z","Y").to_euler()


for name,at,power,size,tint in (
    ("左前柔光",(-.024,-.034,.038),.045,.030,(1,.86,.69)),
    ("右侧补光",(.032,-.008,.018),.015,.030,(.75,.86,1)),
    ("后方轮廓光",(.006,.030,.035),.041,.024,(1,.92,.79)),
):
    light = bpy.data.lights.new(name,"AREA")
    light.energy,light.shape,light.size,light.color = power,"DISK",size,tint
    obj = bpy.data.objects.new(name,light)
    scene.collection.objects.link(obj)
    obj.location = at
    aim(obj)
cam = bpy.data.cameras.new("正交镜头")
cam.type,cam.clip_start,cam.clip_end = "ORTHO",.0001,1
camera = bpy.data.objects.new("棚拍相机",cam)
scene.collection.objects.link(camera)
scene.camera = camera


def shot(path, azimuth, elevation, width, pixels):
    az,el = map(math.radians,(azimuth,elevation))
    camera.location = target + .065*Vector((math.sin(az)*math.cos(el),-math.cos(az)*math.cos(el),math.sin(el)))
    aim(camera)
    cam.ortho_scale = width
    scene.render.resolution_x,scene.render.resolution_y = pixels
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


view = globals().get("VIEW","hero")
if view == "icons":
    scene.render.film_transparent = True
    scene.cycles.samples = 20
    roof = next(o for o in scene.objects if o.name.startswith("主屋搭接陶瓦"))
    paint = roof.data.materials[1].node_tree.nodes["SeatTint"].inputs[2]
    saved = tuple(paint.default_value)
    try:
        for key,rgb in (("red",(.85,.24,.16)),("blue",(.23,.56,.78)),
                        ("orange",(.94,.74,.30)),("beige",(.79,.79,.72))):
            paint.default_value = tuple(((v+.055)/1.055)**2.4 for v in rgb)+(1,)
            shot(ART/("thumbnail-village-"+key+".png"),22,36,.022,(392,392))
    finally:
        paint.default_value = saved
else:
    # 棚拍用的接影地面独立于模型和 FBX。
    mesh = bpy.data.meshes.new("背景纸")
    mesh.from_pydata([(-1,-1,-.000012),(1,-1,-.000012),(1,1,-.000012),(-1,1,-.000012)],[],[(0,1,2,3)])
    floor = bpy.data.objects.new("摄影棚背景纸",mesh)
    scene.collection.objects.link(floor)
    paper = bpy.data.materials.new("暖灰纸")
    paper.use_nodes = True
    paper.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (.29,.26,.215,1)
    paper.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = .92
    mesh.materials.append(paper)
    if view == "hero":
        target = Vector((-.00063,-.0002,.00565))
        shot(OUT/"圆门村庄.png",24,19,.0245,(1220,900))
    elif view == "roof":
        roof = next(o for o in scene.objects if o.name.startswith("主屋搭接陶瓦"))
        paint = roof.data.materials[1].node_tree.nodes["SeatTint"].inputs[2]
        saved = tuple(paint.default_value)
        try:
            paint.default_value = tuple(((v+.055)/1.055)**2.4 for v in (.23,.56,.78))+(1,)
            target = Vector((.0009,-.0001,.0084))
            shot(OUT/"蓝方屋顶.png",56,32,.0168,(1080,800))
        finally:
            paint.default_value = saved
    else:
        floor.hide_render = True
        for label,az,el in (("正面",0,0),("右侧",90,0),("俯视",0,89.99)):
            shot(OUT/(label+".png"),az,el,.022,(760,650))

result = {"view":view,"device":scene.cycles.device,"output":str(OUT)}
