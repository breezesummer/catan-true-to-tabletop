"""通过 Blender MCP 回读 FBX，并比较干净重建的几何、UV和原照贴图。"""
import bpy
from array import array
import hashlib
import json
import math
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
RECIPE = Path(__file__).parent
runtime = ROOT / 'Unity/Catan/Assets/Resources/HarborFishing/v002'
clean = ROOT / '.local/fishing-photo-rebuild/runtime'
expected = json.loads((runtime/'资产清单.json').read_text())['资产']
rebuilt = {r['id']: r for r in json.loads((clean/'资产清单.json').read_text())['资产']}
records = []
for row in expected:
    name = row['id']
    other = rebuilt[name]
    for field in ('几何UVSHA256', '贴图SHA256'):
        if row[field] != other[field]:
            raise ValueError('干净重建不一致：'+name+' '+field)
    # Blender PNG含渲染日期、文件路径和耗时。比较解码像素，避免把元数据差异算成画面差异。
    icon_pixels = []
    for folder in (runtime, clean):
        image = bpy.data.images.load(str(folder/(name+'.png')), check_existing=False)
        icon_pixels.append(hashlib.sha256(array('f', image.pixels[:]).tobytes()).hexdigest())
        bpy.data.images.remove(image)
    if icon_pixels[0] != icon_pixels[1]:
        raise ValueError('干净重建图标像素不一致：'+name)
    scene = bpy.data.scenes.new('回读_'+name)
    bpy.context.window.scene = scene
    bpy.ops.import_scene.fbx(filepath=str(runtime/(name+'.fbx')))
    objects = [o for o in scene.objects if o.type == 'MESH']
    if len(objects) != 1:
        raise ValueError('不是单网格：'+name)
    obj = objects[0]
    coords = [obj.matrix_world @ v.co for v in obj.data.vertices]
    lo = [min(p[i] for p in coords) for i in range(3)]
    hi = [max(p[i] for p in coords) for i in range(3)]
    size = [hi[i]-lo[i] for i in range(3)]
    if any(abs(a-b) > .000001 for a,b in zip(size,row['尺寸米'])) or abs(lo[2]) > .000001:
        raise ValueError('尺寸或底面不符：'+name+' '+str(size))
    if len(obj.data.materials) != 2 or not obj.data.uv_layers:
        raise ValueError('缺少两材质或UV：'+name)
    if any(not math.isfinite(c) for loop in obj.data.uv_layers.active.data for c in loop.uv):
        raise ValueError('UV非法：'+name)
    triangles = sum(len(p.vertices)-2 for p in obj.data.polygons)
    if triangles != row['三角面数']:
        raise ValueError('面数不符：'+name)
    records.append({'id':name,'尺寸米':size,'三角面数':triangles,'回读':'单网格、两材质、UV与底面通过','干净重建':'几何、UV、原照贴图及解码图标像素一致','图标像素SHA256':icon_pixels[0]})
result = {'状态':'通过','Blender':bpy.app.version_string,'资产数':len(records),'三角面数':sum(r['三角面数'] for r in records),'资产':records}
(RECIPE/'Blender验证.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n')
