import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path('D:/Project/catan-true-to-tabletop')
OUT=ROOT/'art/recipes/natural-pieces-v003'
RES=ROOT/'Unity/Catan/Assets/Resources/NaturalPieces/v001'
scene=bpy.context.scene
models={o.name.split('.')[0]:o for o in scene.objects if o.type=='MESH'}
camera=scene.camera
roofmat=models['city'].data.materials[1]
colors={'red':(.80,.27,.17,1),'blue':(.27,.48,.77,1),'orange':(.98,.54,.18,1),'beige':(1,.98,.89,1)}
def linear(col):return tuple(c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4 for c in col[:3])+(1,)
def camera_at(target,angle,size):
    target=Vector(target);a=math.radians(angle)
    camera.location=target+Vector((.014,-math.cos(a)*.12,math.sin(a)*.12))
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=size
def run():
    scene.render.resolution_x=512;scene.render.resolution_y=512
    for obj in models.values():obj.hide_render=True
    for kind,obj in models.items():
        roofmat=obj.data.materials[1]
        stone_tint=next(n for n in obj.data.materials[0].node_tree.nodes if n.type=='MIX_RGB').inputs[2]
        obj.hide_render=False
        for key,col in colors.items():
            stone_tint.default_value=linear((.76,.80,.81,1)) if key=='beige' else (1,1,1,1)
            next(n for n in roofmat.node_tree.nodes if n.type=='MIX_RGB').inputs[2].default_value=linear(col)
            roofmat.diffuse_color=col
            obj.rotation_euler[2]=math.radians(-18 if kind!='road' else 55)
            camera_at((0,0,obj.dimensions.z*.45),58,.038 if kind=='city' else .027 if kind=='village' else .044)
            scene.render.filepath=str(RES/('thumbnail-'+kind+'-'+key+'.png'));bpy.ops.render.render(write_still=True)
        stone_tint.default_value=(1,1,1,1)
        obj.rotation_euler[2]=0;obj.hide_render=True
    for obj in models.values():
        roofmat=obj.data.materials[1]
        next(n for n in roofmat.node_tree.nodes if n.type=='MIX_RGB').inputs[2].default_value=linear(colors['red']);roofmat.diffuse_color=colors['red']
    scene.render.resolution_x=1000;scene.render.resolution_y=1000
    for kind in ('village','city'):
        obj=models[kind];obj.hide_render=False
        camera_at((0,0,obj.dimensions.z*.45),66,.038 if kind=='city' else .027)
        scene.render.filepath=str(OUT/(kind+'-inspection.png'));bpy.ops.render.render(write_still=True);obj.hide_render=True
    bpy.data.libraries.write(str(OUT/'natural-pieces.blend'),{scene},fake_user=True,compress=True)
run()
result={'thumbnails':12,'inspection':['village-inspection.png','city-inspection.png']}
