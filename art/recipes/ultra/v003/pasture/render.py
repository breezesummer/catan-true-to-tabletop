"""渲染已保存的真实模型；相机与棚拍灯光也保存到独立 blend 文件。"""
from pathlib import Path
import bpy
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[5]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/source/ultra/v003/pasture/pasture.blend'))
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.render.resolution_x=1600;scene.render.resolution_y=1600;scene.render.resolution_percentage=100
scene.world.color=(.3,.3,.3)
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.70,.78,1,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.32
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
# 顶点色在游戏中作为 sRGB 读取；Cycles 使用等价线性值。
for mesh in bpy.data.meshes:
    attr=mesh.color_attributes.get('Color')
    if attr:
        for v in attr.data:
            c=v.color[:]
            v.color=tuple(q/12.92 if q<=.04045 else ((q+.055)/1.055)**2.4 for q in c[:3])+(c[3],)
mat=bpy.data.materials.new('暖米色摄影台');mat.diffuse_color=(.52,.43,.32,1);mat.use_nodes=True
mat.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.52,.43,.32,1)
mat.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.85
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.0001));bpy.context.object.name='摄影台';bpy.context.object.data.materials.append(mat)
def light(name,pos,power,size,color):
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=pos;o.rotation_euler=(Vector((0,0,.007))-o.location).to_track_quat('-Z','Y').to_euler()
light('暖光主灯',(-.06,-.035,.10),.25,.028,(1,.92,.79))
light('柔和补光',(.06,.02,.07),.045,.07,(.76,.86,1))
light('草尖轮廓光',(-.015,.07,.09),.12,.05,(1,.93,.75))
d=bpy.data.cameras.new('样板相机');o=bpy.data.objects.new('样板相机',d);scene.collection.objects.link(o);scene.camera=o
d.type='ORTHO';d.ortho_scale=.095;d.clip_start=.001;d.clip_end=100
out=ROOT/'art/previews/ultra/v003/pasture';out.mkdir(parents=True,exist_ok=True)
views=[('perspective',(.09,-.13,.13)),('top',(0,0,.18)),('side',(.11,-.16,.055)),('reverse',(-.09,.13,.11))]
for name,pos in views:
    o.location=pos;o.rotation_euler=(Vector((0,0,.006))-o.location).to_track_quat('-Z','Y').to_euler()
    scene.render.filepath=str(out/(name+'.png'))
    if name=='perspective':bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'art/source/ultra/v003/pasture/pasture-studio.blend'))
    bpy.ops.render.render(write_still=True)
