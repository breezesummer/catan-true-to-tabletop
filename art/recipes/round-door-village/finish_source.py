"""独立 Blender MCP 后台会话中，将分件源文件保存为已取景的可编辑工作区。"""
from pathlib import Path

import bpy
from mathutils import Vector

path = Path(__file__).resolve().parents[3]/"art/source/round-door-village/圆门村庄.blend"
bpy.ops.wm.open_mainfile(filepath=str(path))
scene = bpy.context.scene
assert len(scene.objects)==14 and all(o.type=="MESH" for o in scene.objects)
scene.unit_settings.length_unit = "MILLIMETERS"
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type != "VIEW_3D":
            continue
        space = area.spaces.active
        space.clip_start,space.clip_end = .00005,1
        space.region_3d.view_perspective = "ORTHO"
        space.region_3d.view_distance = .024
        space.region_3d.view_location = Vector((0,0,.0058))
        space.region_3d.view_rotation = Vector((-.40,.91,-.35)).to_track_quat("-Z","Y")
        space.overlay.show_floor = False
        space.overlay.show_axis_x = space.overlay.show_axis_y = False
        space.overlay.show_cursor = False
        space.shading.type = "MATERIAL"
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(path),compress=True)
result = {"源文件":str(path),"分件":len(scene.objects),"默认视图":"材质预览"}
