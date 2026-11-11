"""通过 Blender MCP 回读正式 FBX，按棋盘镜头渲染透明绘制视图及特写。"""
from pathlib import Path
import json
import math
import bpy
from mathutils import Vector

root=Path(__file__).resolve().parents[3]
assets=root/'Unity/Catan/Assets/Resources'
proof=root/'docs/verification/robber-2113'
proof.mkdir(parents=True,exist_ok=True)
with bpy.data.libraries.load(str(Path(__file__).with_name('山匪三人.blend'))) as (available,loaded):
    loaded.materials=[name for name in available.materials if name.startswith('RobberSurface')]
paint=loaded.materials[0]
scene=bpy.data.scenes.new('山匪棋子验收')
bpy.context.window.scene=scene
bpy.ops.import_scene.fbx(filepath=str(assets/'NeutralPieces/v001/robber-band.fbx'),use_anim=False)
piece=next(obj for obj in scene.objects if obj.type=='MESH')
piece.data.materials.clear()
piece.data.materials.append(paint)
piece.data.calc_loop_triangles()
bounds=[piece.matrix_world@Vector(point) for point in piece.bound_box]
minimum=min(p.z for p in bounds); height=max(p.z for p in bounds)-minimum
assert abs(minimum)<.00001 and abs(height-.02036)<.00005
assert len(piece.data.uv_layers)==1 and len(piece.data.materials)==1
report={'三角面':len(piece.data.loop_triangles),'高度毫米':round(1000*height,3),'网格数':1,'材质数':1}
(proof/'模型回读.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),'utf-8')
scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.film_transparent=True;scene.render.image_settings.color_mode='RGBA'
scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('棚光');scene.world.use_nodes=True
scene.world.node_tree.nodes.get('Background').inputs[0].default_value=(.55,.60,.68,1)
scene.world.node_tree.nodes.get('Background').inputs[1].default_value=.28
target=Vector((0,0,.009))
def point(obj,spot):
    obj.rotation_euler=(spot-obj.location).to_track_quat('-Z','Y').to_euler()
for position,power,color in [((-.034,-.033,.047),.040,(1,.87,.71)),
                             ((.031,-.023,.022),.013,(.77,.87,1)),
                             ((.012,.034,.039),.036,(1,.91,.74))]:
    lamp=bpy.data.lights.new('柔光','AREA');lamp.energy=power;lamp.shape='DISK';lamp.size=.027;lamp.color=color
    obj=bpy.data.objects.new('柔光',lamp);scene.collection.objects.link(obj);obj.location=position;point(obj,target)
cam=bpy.data.cameras.new('镜头');cam.type='ORTHO';cam.clip_start=.0001
eye=bpy.data.objects.new('镜头',cam);scene.collection.objects.link(eye);scene.camera=eye
def render(path,pitch,scale,res):
    angle=math.radians(pitch);eye.location=target+Vector((0,-math.cos(angle),math.sin(angle)))*.08
    point(eye,target);cam.ortho_scale=scale
    scene.render.resolution_x=scene.render.resolution_y=res;scene.render.resolution_percentage=100
    scene.render.filepath=str(path);bpy.ops.render.render(write_still=True,scene=scene.name)
render(proof/'三人特写.png',17,.026,800)
from tempfile import TemporaryDirectory
with TemporaryDirectory(dir=root/'.local') as stage:
    image=Path(stage)/'robber.png'
    render(image,45,.026,512)
    image.replace(assets/'FantasyPieces/Views/robber.png')
result=report
