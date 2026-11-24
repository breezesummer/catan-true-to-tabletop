"""从商会分件源文件拍摄。摄影棚和相机仅存在于拍摄会话。"""
from math import cos, radians, sin
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from guild_maps import BLEND, PREVIEWS, ROOT


def shoot(requested):
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    scene=bpy.context.scene
    points=[p.co.copy() for obj in scene.objects for p in obj.data.vertices]
    PREVIEWS.mkdir(parents=True,exist_ok=True)
    work=ROOT/".local/trade-guildhall"
    work.mkdir(parents=True,exist_ok=True)
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=6
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for backend in ("HIP","OPTIX","METAL"):
        try:
            prefs.compute_device_type=backend
            prefs.refresh_devices()
            if any(d.type==backend for d in prefs.devices):
                for device in prefs.devices:
                    device.use=device.type==backend
                scene.cycles.device="GPU"
                break
        except (RuntimeError,TypeError):
            continue
    world=bpy.data.worlds.new("阴天散射光")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs[0].default_value=(.80,.86,.94,1)
    world.node_tree.nodes["Background"].inputs[1].default_value=.35
    scene.world=world
    scene.view_settings.view_transform="AgX"
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    target=Vector((0,0,.011))
    for name,position,power,size,color in (
        ("左前大窗",(-.043,-.064,.079),.202,.044,(1,.854,.69)),
        ("街侧天光",(.052,-.018,.045),.069,.050,(.77,.86,1)),
        ("屋面轮廓光",(.001,.064,.073),.18,.047,(1,.90,.75))):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.shape,light.size,light.color=power,"DISK",size,color
        lamp=bpy.data.objects.new(name,light)
        scene.collection.objects.link(lamp)
        lamp.location=position
        lamp.rotation_euler=(target-lamp.location).to_track_quat("-Z","Y").to_euler()
    camera=bpy.data.cameras.new("正交镜头")
    camera.type,camera.clip_start,camera.clip_end="ORTHO",.0001,1
    obj=bpy.data.objects.new("拍摄相机",camera)
    scene.collection.objects.link(obj)
    scene.camera=obj
    plane=bpy.data.meshes.new("接影纸")
    plane.from_pydata([(-1,-1,-.000010),(1,-1,-.000010),(1,1,-.000010),(-1,1,-.000010)],[],[(0,1,2,3)])
    floor=bpy.data.objects.new("摄影棚地面",plane)
    scene.collection.objects.link(floor)
    paper=bpy.data.materials.new("暖灰摄影纸")
    paper.use_nodes=True
    paper.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.355,.335,.291,1)
    paper.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.95
    plane.materials.append(paper)

    def shot(path,azimuth,elevation,width,height,samples,margin=1.10,focus=None,isolated=False):
        az,el=radians(azimuth),radians(elevation)
        view=Vector((sin(az)*cos(el),-cos(az)*cos(el),sin(el)))
        right=Vector((cos(az),sin(az),0))
        up=view.cross(right)
        bounds=[[p.dot(axis) for p in points] for axis in (right,up)]
        center=[(min(b)+max(b))/2 for b in bounds]
        ratio=width/height
        span=max(max(bounds[0])-min(bounds[0]),ratio*(max(bounds[1])-min(bounds[1])))*margin
        aim=right*center[0]+up*center[1]
        if focus:
            aim,span=focus
        obj.location=aim+view*.15
        obj.rotation_euler=Matrix((right,up,view)).transposed().to_euler()
        camera.ortho_scale=span
        scene.render.resolution_x,scene.render.resolution_y=width,height
        scene.render.film_transparent=isolated
        floor.hide_render=isolated
        scene.cycles.samples=samples
        scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
    for label in requested:
        if label=="draft":
            shot(work/"试形.png",26,20,820,820,12)
        elif label=="hero":
            shot(PREVIEWS/"哥特商会之都.png",26,20,1500,1500,72,1.095)
        elif label=="detail":
            shop=next(o for o in scene.objects if o.name.startswith("中央商馆"))
            nearest=min(p.co.y for p in shop.data.vertices)
            shot(PREVIEWS/"交易拱廊与吊货架.png",19,10,1450,910,64,
                 focus=(Vector((.002,nearest+.0015,.0059)),.030))
        elif label=="views":
            for name,az,el in (("正视",0,0),("右视",90,0),("俯视",0,90)):
                shot(PREVIEWS/(name+".png"),az,el,900,900,32,isolated=True)
    return {"拍摄":requested,"设备":scene.cycles.device}


result=shoot(globals().get("VIEW") or ["hero","detail","views"])
