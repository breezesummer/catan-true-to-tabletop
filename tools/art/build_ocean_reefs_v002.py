"""通过 Blender MCP 执行：独立礁石场景，不改动原海面场景。"""
import bpy, math, random, json
from mathutils import Vector, noise
from pathlib import Path
root=Path("D:/Project/catan-true-to-tabletop")
out=root/"Unity/Catan/Assets/Resources/Ocean/v003"
source=root/"art/source/ocean-reefs-v002"
out.mkdir(parents=True,exist_ok=True);source.mkdir(parents=True,exist_ok=True)
previous=bpy.context.window.scene
scene=bpy.data.scenes.new("海洋主题碎礁 v002")
bpy.context.window.scene=scene
scene.unit_settings.system="METRIC"
random.seed(217)
rocks=[]
for index,(x,y,r,h) in enumerate([(-.38,.08,.38,.35),(-.05,.0,.46,.48),(.36,.12,.33,.30),(.64,.03,.22,.22),(-.33,-.3,.24,.18),(.15,-.29,.28,.24),(.47,-.24,.20,.17),(-.63,.24,.23,.20),(-.72,-.15,.15,.12),(.76,-.16,.14,.10),(-.46,-.43,.12,.08),(.02,-.47,.15,.09),(.38,-.43,.13,.10),(.64,.30,.12,.11),(-.18,.36,.18,.13),(.24,.36,.14,.09)]):
 bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2,radius=1)
 obj=bpy.context.object;obj.name="礁石-%02d"%index
 for v in obj.data.vertices:
  n=noise.noise_vector(v.co*2.7+Vector((index*4,2,7)))
  v.co*=1+n.x*.32
  v.co.x*=r;v.co.y*=r*.78;v.co.z*=h
  v.co.z=max(-.07,v.co.z*.65+.035)
 obj.location=(x,y,0);obj.rotation_euler.z=random.uniform(-1,1)
 for poly in obj.data.polygons:poly.use_smooth=False
 rocks.append(obj)
bpy.ops.object.select_all(action="DESELECT")
for obj in rocks:obj.select_set(True)
bpy.context.view_layer.objects.active=rocks[0]
bpy.ops.object.join()
obj=bpy.context.object;obj.name="ocean-reef-cluster"
bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
bpy.ops.export_scene.fbx(filepath=str(out/"ocean-reef-cluster.fbx"),use_selection=True,object_types={"MESH"},axis_forward="-Z",axis_up="Y",apply_unit_scale=True,bake_anim=False)
bpy.data.libraries.write(str(source/"ocean-reefs-v002.blend"),{scene},fake_user=True)
report={"vertices":len(obj.data.vertices),"triangles":sum(len(p.vertices)-2 for p in obj.data.polygons),"bounds":list(obj.dimensions),"objects":1}
(source/"mesh-report.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf8")
bpy.context.window.scene=previous
result=report
