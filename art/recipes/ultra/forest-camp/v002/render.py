"""从当前同一场景分批输出真实模型图；VIEW由MCP调用者指定。"""
import bpy
from pathlib import Path
from mathutils import Vector
root=Path(__file__).resolve().parents[5]
scene=bpy.context.scene;cam=scene.camera
views={'hero':((0,-.12,.145),(0,0,.004),.091),'reverse':((0,.12,.13),(0,0,.006),.091),'front':((0,-.14,.012),(0,0,.012),.087),'right':((.14,0,.012),(0,0,.012),.087),'back':((0,.14,.012),(0,0,.012),.087),'top':((0,0,.15),(0,0,0),.087),'camp':((.035,-.075,.06),(.013,-.009,.004),.044)}
loc,target,scale=views[VIEW];cam.location=loc;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=scale
prefix='gray-' if scene.get('阶段')=='gray' else ''
scene.render.filepath=str(root/'art/previews/ultra/forest-camp/v002'/(prefix+VIEW+'.png'))
bpy.ops.render.render(write_still=True)
print('真实模型渲染完成',scene.render.filepath)
