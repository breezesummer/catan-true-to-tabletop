"""从 Blender MCP 当前扩展棋子场景输出库存透明图和造型检查图。"""
import bpy,math
from pathlib import Path
from mathutils import Vector
root=Path('D:/Project/catan-true-to-tabletop')
res=root/'Unity/Catan/Assets/Resources/ExpansionPieces/v001'
out=root/'art/recipes/expansion-models-v001'
scene=bpy.context.scene
models={o.name.split('.')[0]:o for o in scene.objects if o.type=='MESH'}
cam=scene.camera
def camera_at(point,angle,scale):
    angle=math.radians(angle);point=Vector(point)
    cam.location=point+Vector((.040,-.12*math.cos(angle),.12*math.sin(angle)))
    cam.rotation_euler=(point-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
colors={'red':(.82,.23,.145),'blue':(.27,.48,.77),'orange':(.98,.54,.18),'beige':(1,.98,.89)}
def tint(key):
    for ob in models.values():
        m=next(n for n in ob.data.materials[1].node_tree.nodes if n.type=='MIX_RGB')
        m.inputs[2].default_value=tuple(((v+.055)/1.055)**2.4 for v in colors[key])+(1,)
for o in models.values():o.hide_render=True
scene.render.resolution_x=384;scene.render.resolution_y=384;scene.render.film_transparent=True
for key,o in models.items():
    if key.startswith('metro') or key.endswith('active'):continue
    o.hide_render=False
    for colour in colors:
        tint(colour);camera_at((0,0,o.dimensions.z*.45),66 if key=='wall' else 42,max(o.dimensions)*1.43)
        scene.render.filepath=str(res/('thumb-'+key+'-'+colour+'.png'));bpy.ops.render.render(write_still=True)
    o.hide_render=True
tint('red')
order=['ship','wall','metro-trade','metro-politics','metro-science','knight-1-active','knight-2-active','knight-3-active']
if 'ship' not in models:
    order=['knight-'+str(i)+'-'+state for state in ('rest','active') for i in (1,2,3)]
columns=len(order)//2
for i,key in enumerate(order):
    o=models[key];o.hide_render=False;o.location=((i%columns-(columns-1)/2)*.041,(.5-i//columns)*.047,0)
cam.location=(0,-.145,.15);cam.rotation_euler=(Vector((0,0,.009))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.175
scene.render.resolution_x=1440;scene.render.resolution_y=860;scene.render.film_transparent=False
scene.render.filepath=str(out/('骑士造型检查.png' if columns==3 else '造型检查.png'));bpy.ops.render.render(write_still=True)
for o in models.values():o.location=(0,0,0);o.hide_render=True
result={'inspection':scene.render.filepath}

