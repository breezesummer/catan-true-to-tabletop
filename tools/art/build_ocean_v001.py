"""通过 Blender MCP 执行；新增独立场景，不覆盖其他建模工作。"""
import bpy
import json
from pathlib import Path

root = Path("D:/Project/catan-true-to-tabletop")
output = root / "Unity/Catan/Assets/Resources/Ocean/v001"
source = root / "art/source/ocean-v001"
output.mkdir(parents=True, exist_ok=True)
source.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new("连续海面 v001")
bpy.context.window.scene = scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1
records = []
for name, subdivisions in [("ocean-desktop", 160), ("ocean-mobile", 80)]:
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=subdivisions, y_subdivisions=subdivisions, size=2)
    obj = bpy.context.object
    obj.name = name
    for vertex in obj.data.vertices:
        for axis in (0, 1):
            value = vertex.co[axis]
            distance = abs(value)
            vertex.co[axis] = (1 if value >= 0 else -1) * (
                distance * 1.6 if distance <= .75 else 1.2 + ((distance - .75) / .25) ** 2 * 6.8)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=str(output / (name + ".fbx")), use_selection=True,
                             object_types={"MESH"}, axis_forward="-Z", axis_up="Y",
                             apply_unit_scale=True, bake_anim=False)
    records.append(dict(name=name, vertices=len(obj.data.vertices),
                        triangles=len(obj.data.polygons) * 2, width_metres=16))
bpy.ops.wm.save_as_mainfile(filepath=str(source / "ocean-v001.blend"))
(source / "mesh-report.json").write_text(json.dumps(records, indent=2), encoding="utf8")
result = {"assets": records, "output": str(output), "source": str(source)}
