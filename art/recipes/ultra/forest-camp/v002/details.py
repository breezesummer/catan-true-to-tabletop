"""补齐参考中的散木、额外树桩和桩上铁器；在米制源场景中通过MCP调用。"""
import bpy,math
from mathutils import Vector
scene=bpy.context.scene
if not scene.get('零散物补齐'):
 def detail_rod(name,a,b,r,mat='树皮'):
  scale=.0079;a=Vector((a[0]*.82,a[1]*.82,a[2]))*scale;b=Vector((b[0]*.82,b[1]*.82,b[2]))*scale;d=b-a
  bpy.ops.mesh.primitive_cylinder_add(vertices=10,radius=r*scale,depth=d.length,location=(a+b)/2)
  o=bpy.context.object;o.name=name;o['模块']='营地';o.rotation_mode='QUATERNION';o.rotation_quaternion=d.to_track_quat('Z','Y');o.data.materials.append(bpy.data.materials[mat]);o.data.materials.append(bpy.data.materials['浅木'])
  for face in o.data.polygons:
   end=abs(face.normal.z)>.99;face.material_index=1 if end and mat=='树皮' else 0;face.use_smooth=not end
   idx=1 if face.material_index else (7 if mat=='铁' else 0)
   for li in face.loop_indices:
    uv=o.data.uv_layers.active.data[li];uv.uv=((idx%4+.04+uv.uv.x*.92)/4,(idx//4+.04+uv.uv.y*.92)/4)
  return o
 for name,a,b,r in [('P13_散落长原木',(.35,-2.8,.065),(1.15,-2.5,.065),.06),('P14_散落粗原木',(.40,-3.0,.085),(1.04,-2.85,.085),.08),('P15_散落短枝',(.30,-2.6,.04),(.72,-2.45,.04),.035)]:detail_rod(name,a,b,r)
 for name,x,y in [('ST04_前景树桩',(.20),-3.65),('ST05_右侧小树桩',3.13,-2.7)]:
  detail_rod(name,(x,y,0),(x,y,.24),.13)
  for i in range(3):
   a=i*math.tau/3;detail_rod(name+'_根',(x,y,.07),(x+math.cos(a)*.23,y+math.sin(a)*.23,.015),.035)
 # 双端铁器放在已有ST营地树桩上，弧形两侧刃而非多余斧头。
 for i in range(6):
  a=-1.0+i/6*2;b=-1.0+(i+1)/6*2
  detail_rod('P08_弧形铁工具',(.85+a*.18,-1.4,.35+.025*(1-a*a)),(.85+b*.18,-1.4,.35+.025*(1-b*b)),.016,'铁')
 detail_rod('P08_木握柄',(.85,-1.40,.36),(.85,-1.20,.36),.024)
 for x,y in [(1.03,-.27),(3.35,-2.0)]:
  bpy.ops.mesh.primitive_torus_add(major_segments=16,minor_segments=4,major_radius=.195*.0079*.82,minor_radius=.012*.0079,location=(x*.82*.0079,y*.82*.0079,.22*.0079))
  o=bpy.context.object;o.name='P04_中段第三道桶箍';o['模块']='营地';o.data.materials.append(bpy.data.materials['铁'])
  for uv in o.data.uv_layers.active.data:uv.uv=((3+.04+uv.uv.x*.92)/4,(1+.04+uv.uv.y*.92)/4)
 scene['零散物补齐']=True
