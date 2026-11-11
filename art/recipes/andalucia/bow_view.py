"""在完成船模导出的 Blender MCP 会话中重现原参考照片的低机位。"""
import bpy
import numpy as np
from math import cos,sin
from mathutils import Vector
from pathlib import Path

scene=bpy.context.scene
ship=bpy.data.objects["安达卢西亚号"]
for o in list(scene.objects):
    if o!=ship:bpy.data.objects.remove(o,do_unlink=True)
scene.render.engine="CYCLES"
scene.cycles.samples=48
scene.cycles.use_denoising=True
scene.view_settings.view_transform="AgX"
scene.world.use_nodes=True
nodes=scene.world.node_tree.nodes
links=scene.world.node_tree.links
nodes.clear()
bg=nodes.new("ShaderNodeBackground")
out=nodes.new("ShaderNodeOutputWorld")
coord=nodes.new("ShaderNodeTexCoord")
sep=nodes.new("ShaderNodeSeparateXYZ")
links.new(coord.outputs["Generated"],sep.inputs[0])
ramp=nodes.new("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color=(.18,.56,1.05,1)
ramp.color_ramp.elements[1].color=(.008,.075,.36,1)
ramp.color_ramp.elements[1].position=.50
links.new(sep.outputs["Z"],ramp.inputs[0])
links.new(ramp.outputs[0],bg.inputs["Color"])
bg.inputs["Strength"].default_value=.77
links.new(bg.outputs[0],out.inputs[0])
bpy.ops.object.light_add(type="SUN")
sun=bpy.context.object
sun.rotation_euler=Vector((-.62,.57,-.71)).to_track_quat("-Z","Y").to_euler()
sun.data.energy=4.8
sun.data.angle=.046
sun.data.color=(1,.90,.78)
res=270
axis=np.linspace(-1,1,res)
axis=axis*42+axis**7*2100
points=[]; faces=[]
for j,y in enumerate(axis):
    for i,x in enumerate(axis):
        h=.075*sin(.69*x+.78*y+.55*sin(.31*y))+.047*sin(1.41*x-.94*y)+.033*cos(2.29*y-.48*x)
        points.append((x,y,-.46+h))
        if i and j:
            k=j*res+i
            faces.append((k-res-1,k-res,k,k-1))
mesh=bpy.data.meshes.new("取景海面")
mesh.from_pydata(points,[],faces)
mesh.update()
mesh.polygons.foreach_set("use_smooth",[True]*len(faces))
sea=bpy.data.objects.new("取景海面",mesh)
scene.collection.objects.link(sea)
mat=bpy.data.materials.new("取景海水")
mat.use_nodes=True
n=mat.node_tree.nodes
l=mat.node_tree.links
bs=n.get("Principled BSDF")
bs.inputs["Base Color"].default_value=(.007,.047,.092,1)
bs.inputs["Roughness"].default_value=.14
bs.inputs["IOR"].default_value=1.333
tex=n.new("ShaderNodeTexCoord")
noise=n.new("ShaderNodeTexNoise")
l.new(tex.outputs["Object"],noise.inputs[0])
noise.inputs["Scale"].default_value=3.7
noise.inputs["Detail"].default_value=3.2
bump=n.new("ShaderNodeBump")
bump.inputs["Strength"].default_value=.27
bump.inputs["Distance"].default_value=.11
l.new(noise.outputs["Fac"],bump.inputs["Height"])
l.new(bump.outputs[0],bs.inputs["Normal"])
mesh.materials.append(mat)
bpy.ops.object.camera_add(location=(45.56,-21.40,.95))
camera=bpy.context.object
yaw,tilt=.52614,.21750
camera.rotation_euler=Vector((-cos(yaw)*cos(tilt),sin(yaw)*cos(tilt),sin(tilt))).to_track_quat("-Z","Y").to_euler()
camera.data.lens=40
camera.data.clip_end=5000
scene.camera=camera
scene.render.resolution_x=1536
scene.render.resolution_y=1022
scene.render.resolution_percentage=100
scene.render.filepath=str(Path.cwd()/".local/andalucia-finish/模型低机位.png")
bpy.ops.render.render(write_still=True)
result={"渲染":scene.render.filepath}
