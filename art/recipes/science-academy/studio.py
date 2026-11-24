"""读取已保存的分件源文件拍摄，摄影棚不参与游戏导出。"""
from math import cos, radians, sin
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

folder=str(Path(__file__).resolve().parent)
if folder not in sys.path:
    sys.path.insert(0,folder)
from finishes import BLEND, PREVIEW, ROOT


def photographs(views):
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    scene=bpy.context.scene
    groups={o.name:[o.matrix_world@v.co for v in o.data.vertices] for o in scene.objects}
    all_points=[p for part in groups.values() for p in part]
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=7
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for backend in ("OPTIX","HIP","METAL"):
        try:
            prefs.compute_device_type=backend
            prefs.refresh_devices()
            if any(d.type==backend for d in prefs.devices):
                for device in prefs.devices:
                    device.use=device.type==backend
                scene.cycles.device="GPU"
                break
        except (TypeError,RuntimeError):
            continue
    scene.world=bpy.data.worlds.new("北窗漫射天光")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value=(.78,.84,.94,1)
    bg.inputs[1].default_value=.32
    scene.view_settings.view_transform="AgX"
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGB"
    target=Vector((0,0,.014))
    for name,xyz,power,size,rgb in (
        ("左前摄影窗",(-.042,-.060,.075),.16,.049,(1,.87,.72)),
        ("右侧反光板",(.057,-.021,.048),.08,.054,(.76,.86,1)),
        ("黄铜边缘光",(.013,.052,.080),.21,.046,(1,.91,.77))):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.shape,light.size,light.color=power,"DISK",size,rgb
        lamp=bpy.data.objects.new(name,light)
        scene.collection.objects.link(lamp)
        lamp.location=xyz
        lamp.rotation_euler=(target-lamp.location).to_track_quat("-Z","Y").to_euler()
    camera=bpy.data.cameras.new("移轴摄影镜头")
    camera.type,camera.clip_start,camera.clip_end="ORTHO",.0001,2
    eye=bpy.data.objects.new("建筑摄影机",camera)
    scene.collection.objects.link(eye)
    scene.camera=eye
    clay=bpy.data.materials.new("暖灰接影纸")
    clay.use_nodes=True
    clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.235,.228,.203,1)
    clay.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.95
    floor=bpy.data.meshes.new("摄影台")
    floor.from_pydata([(-1,-1,-.000014),(1,-1,-.000014),(1,1,-.000014),(-1,1,-.000014)],[],[(0,1,2,3)])
    stand=bpy.data.objects.new("摄影棚接影面",floor)
    scene.collection.objects.link(stand)
    floor.materials.append(clay)

    def shot(name,az,el,width,height,samples,margin=1.12,parts=(),destination=PREVIEW):
        az,el=radians(az),radians(el)
        direction=Vector((sin(az)*cos(el),-cos(az)*cos(el),sin(el)))
        right=Vector((cos(az),sin(az),0))
        up=direction.cross(right)
        points=[p for label in parts for p in groups[label]] if parts else all_points
        spans=[tuple(fn(p.dot(axis) for p in points) for fn in (min,max)) for axis in (right,up)]
        aim=right*(sum(spans[0])/2)+up*(sum(spans[1])/2)
        eye.location=aim+direction*.14
        eye.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
        camera.ortho_scale=max(spans[0][1]-spans[0][0],(spans[1][1]-spans[1][0])*width/height)*margin
        scene.render.resolution_x,scene.render.resolution_y=width,height
        scene.cycles.samples=samples
        scene.render.filepath=str(destination/(name+".png"))
        bpy.ops.render.render(write_still=True)

    for view in views:
        if view=="draft":
            shot("试形",22,17,780,780,16)
        elif view=="hero":
            shot("穹顶观星学宫",22,17,1420,1420,80,1.10)
        elif view=="instruments":
            shot("黄道浑仪",27,20,1080,940,64,1.29,("浑仪转盘与铸铜托架","交错厚星环与刻度","黄道星球与极轴"))
            shot("开缝穹顶与物镜",18,10,1080,940,64,1.14,("铜穹顶外板与咬边","伸缩望远镜与物镜"))
        elif view=="views":
            for label,az,el in (("正视",0,5),("右视",90,5),("俯视",0,89.99)):
                shot(label,az,el,820,820,32)
        elif view=="book":
            for mat in (bpy.data.materials["Expansion_Natural"],bpy.data.materials["Expansion_Seat"]):
                nodes=mat.node_tree.nodes
                nodes["SeatStrength"].inputs[1].default_value=1
                nodes["SeatTint"].inputs[2].default_value=tuple(((v+.055)/1.055)**2.4 for v in (.23,.56,.78))+(1,)
            clay.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.66,.63,.55,1)
            shot("大都会",20,19,960,640,56,1.07,destination=ROOT/"Unity/Catan/Assets/Resources/Encyclopedia/规则示意")
    return {"视图":views,"设备":scene.cycles.device}


result=photographs(globals().get("VIEWS") or ["hero","instruments","views","book"])
