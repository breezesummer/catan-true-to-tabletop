"""Blender MCP 补充检查：布线、真实室内、FBX几何回读。"""
import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('D:/Project/catan-true-to-tabletop');pre=root/'art/previews/three-kingdoms-pieces/B201-v001'
scene=bpy.data.scenes['B201_验证样板'];bpy.context.window.scene=scene;cam=scene.camera
old=(cam.location.copy(),cam.rotation_euler.copy(),cam.data.type,cam.data.ortho_scale,scene.render.filepath)
mat=bpy.data.materials.new('检查专用布线');mat.use_nodes=True;n=mat.node_tree.nodes;n.clear()
wire=n.new('ShaderNodeWireframe');wire.inputs['Size'].default_value=.000018
mix=n.new('ShaderNodeMixRGB');mix.inputs[1].default_value=(.22,.35,.42,1);mix.inputs[2].default_value=(.005,.009,.012,1)
em=n.new('ShaderNodeEmission');em.inputs[1].default_value=3;out=n.new('ShaderNodeOutputMaterial')
mat.node_tree.links.new(wire.outputs[0],mix.inputs[0]);mat.node_tree.links.new(mix.outputs[0],em.inputs[0]);mat.node_tree.links.new(em.outputs[0],out.inputs[0])
scene.view_layers[0].material_override=mat;scene.render.filepath=str(pre/'wireframe.png');bpy.ops.render.render(write_still=True)
scene.view_layers[0].material_override=None
cam.data.type='PERSP';cam.data.lens=17;cam.location=(0,-.009,.012)
cam.rotation_euler=(Vector((.008,.008,.011))-cam.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.lights.new('室内检查辅助灯','POINT');d.energy=.008;d.shadow_soft_size=.004
l=bpy.data.objects.new('室内检查辅助灯',d);scene.collection.objects.link(l);l.location=(0,0,.018)
scene.render.filepath=str(pre/'interior-check.png');bpy.ops.render.render(write_still=True)
bpy.data.objects.remove(l,do_unlink=True)
cam.location,cam.rotation_euler,cam.data.type,cam.data.ortho_scale,scene.render.filepath=old
check=bpy.data.scenes.new('B201_FBX几何回读');bpy.context.window.scene=check
bpy.ops.import_scene.fbx(filepath=str(root/'art/exports/three-kingdoms-pieces/B201-v001/B201.fbx'))
bpy.context.view_layer.update();bb=[];count=0;meshes=0
for o in check.objects:
    if o.type=='MESH':
        meshes+=1;o.data.calc_loop_triangles();count+=len(o.data.loop_triangles);bb.extend(o.matrix_world@v.co for v in o.data.vertices)
r={'FBX三角面':count,'FBX网格数':meshes,'FBX尺寸毫米':[round((max(v[i] for v in bb)-min(v[i] for v in bb))*1000,3) for i in range(3)],'材质说明':'FBX仅完成几何回读；目标引擎需按材质说明恢复灰瓦0.26与旧木0.67的线性乘色。GLB为本轮已完成材质回读的交付格式。'}
assert count==5976
(root/'docs/verification/three-kingdoms-pieces/B201-v001/fbx-check.json').write_text(json.dumps(r,ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
bpy.context.window.scene=scene
for o in list(check.objects):bpy.data.objects.remove(o,do_unlink=True)
bpy.data.scenes.remove(check)
print(r)
