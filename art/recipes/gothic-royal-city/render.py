"""在 Blender MCP 中读取分件源场景，按正交相机取四视图和实模透明缩略图。"""
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector

folder=str(Path(__file__).resolve().parent)
if folder in sys.path:
    sys.path.remove(folder)
sys.path.insert(0,folder)
from surfaces import ART, BLEND, PREVIEWS, ROOT, SEATS


def shoot(views):
    PREVIEWS.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    scene=bpy.context.scene
    points=[obj.matrix_world@v.co for obj in scene.objects for v in obj.data.vertices]
    mat=next(m for m in list(scene.objects)[0].data.materials if "Player" in m.name)
    tint=mat.node_tree.nodes["SeatTint"].inputs[2]
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for mode in ("HIP","OPTIX","METAL"):
        try:
            prefs.compute_device_type=mode
            prefs.refresh_devices()
            if any(d.type==mode for d in prefs.devices):
                for device in prefs.devices:
                    device.use=device.type==mode
                scene.cycles.device="GPU"
                break
        except (TypeError,RuntimeError):
            pass
    world=bpy.data.worlds.new("阴天天光")
    scene.world=world
    world.use_nodes=True
    bg=world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value=(.71,.79,.90,1)
    bg.inputs[1].default_value=.34
    scene.view_settings.view_transform="AgX"
    scene.render.image_settings.file_format="PNG"
    scene.render.resolution_percentage=100
    scene.render.image_settings.color_mode="RGBA"
    target=Vector((0,0,.0115))
    for name,xyz,power,size,rgb in (
        ("左前窗光",(-.044,-.052,.069),.133,.043,(1,.84,.66)),
        ("右侧补光",(.047,-.016,.040),.058,.048,(.73,.85,1)),
        ("高处轮廓光",(.008,.055,.064),.17,.036,(1,.91,.77))):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.shape,light.size,light.color=power,"DISK",size,rgb
        lamp=bpy.data.objects.new(name,light)
        scene.collection.objects.link(lamp)
        lamp.location=xyz
        lamp.rotation_euler=(target-lamp.location).to_track_quat("-Z","Y").to_euler()
    camera=bpy.data.cameras.new("正交摄影镜头")
    camera.type,camera.clip_start,camera.clip_end="ORTHO",.0001,1
    obj=bpy.data.objects.new("摄影机",camera)
    scene.collection.objects.link(obj)
    scene.camera=obj
    plane=bpy.data.meshes.new("背景纸")
    plane.from_pydata([(-1,-1,-.000014),(1,-1,-.000014),(1,1,-.000014),(-1,1,-.000014)],[],[(0,1,2,3)])
    floor=bpy.data.objects.new("摄影棚接影地面",plane)
    scene.collection.objects.link(floor)
    paper=bpy.data.materials.new("灰褐纸")
    paper.use_nodes=True
    paper.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.215,.218,.205,1)
    paper.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value=.94
    plane.materials.append(paper)

    def shot(file,az,el,pixels,margin,samples,transparent=False,focus=None):
        az,el=map(math.radians,(az,el))
        view=Vector((math.sin(az)*math.cos(el),-math.cos(az)*math.cos(el),math.sin(el)))
        right=Vector((math.cos(az),math.sin(az),0))
        up=view.cross(right)
        bounds=[[p.dot(axis) for p in points] for axis in (right,up)]
        center=[(max(b)+min(b))*.5 for b in bounds]
        ratio=pixels[0]/pixels[1]
        width=max((max(bounds[0])-min(bounds[0])),ratio*(max(bounds[1])-min(bounds[1])))*margin
        aim=right*center[0]+up*center[1]
        if focus:
            aim,width=focus
        obj.location=aim+view*.135
        obj.rotation_euler=Matrix((right,up,view)).transposed().to_euler()
        camera.ortho_scale=width
        scene.render.resolution_x,scene.render.resolution_y=pixels
        scene.render.film_transparent=transparent
        floor.hide_render=transparent or el<math.radians(1)
        scene.cycles.samples=samples
        scene.render.filepath=str(file)
        bpy.ops.render.render(write_still=True)
    for view in views:
        tint.default_value=tuple(((v+.055)/1.055)**2.4 for v in SEATS["blue"])+(1,)
        if view=="draft":
            shot(PREVIEWS/"试形.png",22,18,(720,720),1.10,12)
        elif view=="hero":
            shot(PREVIEWS/"哥特王城.png",22,18,(1320,1320),1.12,64)
        elif view=="detail":
            shot(PREVIEWS/"门廊与窗花.png",14,9,(1250,940),1,64,
                 focus=(Vector((.0001,-.0061,.0067)),.0146))
        elif view=="views":
            for label,az,el in (("正视",0,0),("右视",90,0),("俯视",0,89.99)):
                shot(PREVIEWS/(label+".png"),az,el,(860,860),1.12,28,True)
        elif view=="icons":
            for key,rgb in SEATS.items():
                tint.default_value=tuple(((v+.055)/1.055)**2.4 for v in rgb)+(1,)
                shot(ART/f"thumbnail-city-{key}.png",22,32,(392,392),1.12,24,True)
        elif view=="book":
            tint.default_value=tuple(((v+.055)/1.055)**2.4 for v in SEATS["red"])+(1,)
            book=ROOT/"Unity/Catan/Assets/Resources/OperationUI/v002/pieces/city-red.png"
            book.parent.mkdir(parents=True,exist_ok=True)
            shot(book,22,19,(768,768),1.08,48,True)
    return {"输出":views,"设备":scene.cycles.device}


result=shoot(globals().get("VIEW") or ["hero","detail","views","icons","book"])
