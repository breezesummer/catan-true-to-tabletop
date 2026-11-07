"""通过 Blender MCP 生成圆角玉签。剖面从玉背、金包边一路收至正面的微凸玉芯。"""
import bpy
import math
from pathlib import Path

root = Path(__file__).resolve().parents[3]
mesh = bpy.data.meshes.new("玉芯与包金边")
verts, faces = [], []
# 半宽为 0.5，半高为 0.61；每圈共用相同的圆角圆心。
profile = [(.037,-.037),(.019,-.036),(.007,-.029),(0,-.018),
           (0,.009),(.004,.022),(.011,.031),(.020,.034),(.028,.031),
           (.033,.022),(.039,.020),(.047,.027),(.062,.035),(.084,.039)]
for inset, depth in profile:
    for corner in range(4):
        angle = corner * math.pi / 2
        cx = .345 * (1 if corner in (0,3) else -1)
        cz = .455 * (1 if corner in (0,1) else -1)
        for j in range(21):
            a = angle + j * math.pi / 40
            r = .155 - inset
            verts.append((cx+r*math.cos(a), -depth, cz+r*math.sin(a)))
count = 84
faces.append(tuple(reversed(range(count))))
for ring in range(1,len(profile)):
    for j in range(count):
        a = (ring-1)*count+j
        b = (ring-1)*count+(j+1)%count
        faces.append((a,b,b+count,a+count))
faces.append(tuple(range(len(verts)-count,len(verts))))
mesh.from_pydata(verts, [], faces)
mesh.update()
uv = mesh.uv_layers.new(name="玉面")
for polygon in mesh.polygons:
    polygon.use_smooth = True
    for loop_index in polygon.loop_indices:
        x,y,z = mesh.vertices[mesh.loops[loop_index].vertex_index].co
        # 背面采样玉芯边缘的素玉；包边和正面使用相同的连续展开。
        coordinates = (.5+x, .5+z/1.22)
        if polygon.index == 0:
            coordinates = (.5+x*.5, .88+z*.035)
        uv.data[loop_index].uv = coordinates
obj = bpy.data.objects.new("JadeSlip",mesh)
scene = bpy.data.scenes.new("悬浮玉薄签")
scene.collection.objects.link(obj)
window = bpy.context.window
previous = window.scene
window.scene = scene
try:
    obj.select_set(True)
    scene.view_layers[0].objects.active=obj
    file = root / "Unity/Catan/Assets/Resources/TabletopUI/JadeNumbers/jade-slip.fbx"
    bpy.ops.export_scene.fbx(filepath=str(file), use_selection=True, object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False)
finally:
    window.scene = previous
bpy.data.scenes.remove(scene)
bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.meshes.remove(mesh)
