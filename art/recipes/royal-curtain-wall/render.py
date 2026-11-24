"""从有材质的 Blender 母版拍摄门券、整墙及缩略图。摄影棚只存在于本次拍摄会话。"""
from math import cos, radians, sin
from pathlib import Path
import sys
from tempfile import TemporaryDirectory
from time import sleep
import bpy
import bmesh
from mathutils import Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from wall_maps import ART, PREVIEW, ROOT, SOURCE


def render():
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False)
    scene=bpy.context.scene
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
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
        except (TypeError,RuntimeError):
            continue
    # 图标和建筑棚拍均从棋盘表面以上取景，临海地下石基只存在于实机网格中。
    for o in list(scene.objects):
        bm=bmesh.new()
        bm.from_mesh(o.data)
        bmesh.ops.bisect_plane(bm,geom=bm.verts[:]+bm.edges[:]+bm.faces[:],
                               plane_co=(0,0,-.000005),plane_no=(0,0,1),clear_inner=True,dist=1e-8)
        bm.to_mesh(o.data)
        bm.free()
    scene.world=bpy.data.worlds.new("北向天光")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes["Background"]
    bg.inputs[0].default_value=(.77,.84,.94,1)
    bg.inputs[1].default_value=.34
    scene.render.resolution_percentage=100
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.view_settings.view_transform="AgX"
    for name,pos,energy,size,rgb in (
        ("左前斜窗",(-.047,-.049,.060),.126,.032,(1,.88,.74)),
        ("右侧反光板",(.047,-.017,.040),.048,.047,(.75,.84,1)),
        ("后上柔光",(.014,.049,.067),.14,.040,(1,.90,.77))):
        light=bpy.data.lights.new(name,"AREA")
        light.energy,light.shape,light.size,light.color=energy,"DISK",size,rgb
        lamp=bpy.data.objects.new(name,light)
        scene.collection.objects.link(lamp)
        lamp.location=pos
        lamp.rotation_euler=(Vector((0,0,.0025))-lamp.location).to_track_quat("-Z","Y").to_euler()
    bpy.ops.mesh.primitive_plane_add(size=1,location=(0,0,-.00001))
    floor=bpy.context.object
    mat=bpy.data.materials.new("暖灰摄影台")
    mat.use_nodes=True
    mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value=(.235,.235,.221,1)
    floor.data.materials.append(mat)
    eye=bpy.data.objects.new("正交镜头",bpy.data.cameras.new("正交镜头"))
    scene.collection.objects.link(eye)
    scene.camera=eye
    eye.data.type="ORTHO"
    eye.data.clip_start,eye.data.clip_end=.0001,1
    def shot(path,az,el,aim,span,pixels,samples,cutout=False):
        az,el=radians(az),radians(el)
        direction=Vector((sin(az)*cos(el),-cos(az)*cos(el),sin(el)))
        eye.location=Vector(aim)+direction*.14
        eye.rotation_euler=(-direction).to_track_quat("-Z","Y").to_euler()
        eye.data.ortho_scale=span
        scene.render.resolution_x,scene.render.resolution_y=pixels
        scene.cycles.samples=samples
        floor.hide_render=cutout
        scene.render.film_transparent=cutout
        # Unity 可能同时观察贴图。待 PNG 写完再原子替换，避免导入半张图。
        with TemporaryDirectory(dir=PREVIEW) as scratch:
            rendered=Path(scratch)/"frame.png"
            scene.render.filepath=str(rendered)
            bpy.ops.render.render(write_still=True)
            for attempt in range(10):
                try:
                    rendered.replace(path)
                    break
                except PermissionError:
                    if attempt==9:
                        raise
                    sleep(.20)
    PREVIEW.mkdir(parents=True,exist_ok=True)
    shot(PREVIEW/"王城石墙.png",21,28,(0,0,.0023),.039,(1408,1016),48)
    shot(PREVIEW/"券石与门旗.png",12,13,(0,-.012,.0038),.013,(1320,900),64)
    shot(PREVIEW/"俯视.png",0,89.99,(0,0,0),.032,(960,960),28)
    shot(PREVIEW/"正视.png",0,0,(0,0,.0035),.030,(1080,380),28,True)
    shot(PREVIEW/"右视.png",90,0,(0,0,.0035),.030,(1080,380),28,True)
    tint=bpy.data.materials["Expansion_Seat"].node_tree.nodes["SeatTint"].inputs[2]
    for key,rgb in (("blue",(.15,.48,.80)),("red",(.79,.115,.072)),("orange",(.90,.52,.105)),("beige",(.86,.82,.68))):
        tint.default_value=(*rgb,1)
        shot(ART/f"thumb-wall-{key}.png",15,30,(0,0,.0024),.035,(360,360),20,True)
        if key=="red":
            book=ROOT/"Unity/Catan/Assets/Resources/OperationUI/v002/pieces/wall-red.png"
            shot(book,17,29,(0,0,.0024),.036,(768,680),40,True)
    return {"设备":scene.cycles.device,"输出图像":10}


result=render()
