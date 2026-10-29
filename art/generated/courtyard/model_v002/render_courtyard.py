"""实际模型渲染；通过Blender MCP执行，可用VIEWS选择视角。"""
import bpy
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parent
S=bpy.context.scene
views={
 'front':((9,-20,22),(0,0,2.4),15.8,2048,64),
 'rear':((-12,18,17),(0,0,2.5),16,1400,48),
 'left':((-17,-9,16),(0,0,2.5),16,1400,48),
 'right':((18,8,17),(0,0,2.5),16,1400,48),
 'top':((0,0,25),(0,0,0),15.8,1400,48),
 'lion':((2.0,-1.3,4.1),(2.5,.53,2.7),1.5,1200,64),
 'ornament':((-4.3,-.5,8),(-2.9,2.7,6.95),1.9,1200,64),
}
for name in globals().get('VIEWS',list(views)):
 loc,target,scale,res,samples=views[name];S.camera.location=loc;S.camera.rotation_euler=(Vector(target)-S.camera.location).to_track_quat('-Z','Y').to_euler();S.camera.data.ortho_scale=scale;S.render.resolution_x=res;S.render.resolution_y=res;S.cycles.samples=samples;S.render.filepath=str(ROOT/'renders'/f'{name}.png');bpy.ops.render.render(write_still=True)
S.camera.location=(9,-20,22);S.camera.rotation_euler=(Vector((0,0,2.4))-S.camera.location).to_track_quat('-Z','Y').to_euler();S.camera.data.ortho_scale=15.8;S.render.resolution_x=2048;S.render.resolution_y=2048;S.cycles.samples=64
result={'视角':globals().get('VIEWS',list(views))}
