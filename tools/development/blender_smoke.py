"""Synthetic metre-scale toolchain sample; not a measured Catan component."""
import json
import os
import sys
import bpy

output = os.path.abspath(sys.argv[sys.argv.index("--") + 1])
os.makedirs(output, exist_ok=True)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system = "METRIC"
bpy.context.scene.unit_settings.scale_length = 1.0
bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, 0))
cube = bpy.context.object
cube.name = "EnvironmentCube_v001"
cube.dimensions = (0.08, 0.08, 0.01)
bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
# Place geometry above the origin so the pivot is the bottom centre.
for vertex in cube.data.vertices:
    vertex.co.z += 0.005
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(output, "environment-cube-v001.blend"))
bpy.ops.export_scene.fbx(
    filepath=os.path.join(output, "environment-cube-v001.fbx"),
    use_selection=True, object_types={"MESH"}, global_scale=1.0,
    apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
    axis_forward="-Z", axis_up="Y", bake_anim=False,
    add_leaf_bones=False, path_mode="AUTO",
)
with open(os.path.join(output, "manifest.json"), "w", encoding="utf-8") as stream:
    json.dump({
        "version": "v001", "source": "synthetic environment validation cube",
        "reference": None, "physical_dimensions_confirmed": False,
        "dimensions_metres_blender": [0.08, 0.08, 0.01],
        "expected_dimensions_metres_unity": [0.08, 0.01, 0.08],
        "pivot": "bottom centre", "blender_version": bpy.app.version_string,
        "export": {"format": "FBX", "global_scale": 1.0, "axis_forward": "-Z", "axis_up": "Y", "apply_scale_options": "FBX_SCALE_UNITS"},
    }, stream, ensure_ascii=False, indent=2)
print("CATAN_ENVIRONMENT_BLENDER_OK")
