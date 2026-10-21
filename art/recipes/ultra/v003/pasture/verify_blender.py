"""Blender 源文件与 FBX 回读；不把仅生成成功当成导出验证。"""
import argparse
import json
import math
import hashlib
import gzip
import sys
from pathlib import Path
import bpy

ap=argparse.ArgumentParser();ap.add_argument('--root',required=True)
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root)
export=root/'exports/ultra/v003/pasture'
data=json.loads((export/'pasture-mesh.json').read_text())
assert gzip.decompress((export/'pasture-mesh.bytes').read_bytes())==(export/'pasture-mesh.json').read_bytes(),'压缩网格与原始网格不一致'
for lod in data['lods']:
    for part in lod['parts']:
        assert all(math.isfinite(v) for v in part['vertices']),'存在无效坐标'
        assert len(part['colors'])//4==len(part['vertices'])//3,'颜色数量不符'
        for index in part['triangles']+part['paintTriangles']:
            assert 0<=index<len(part['vertices'])//3,'网格索引越界'
        assert min(part['vertices'][1::3])>=-0.000001,'底面以下存在模型'
expected=sum((len(p['triangles'])+len(p['paintTriangles']))//3 for lod in data['lods'] for p in lod['parts'])
def inspect():
    objects=[obj for obj in bpy.context.scene.objects if obj.type=='MESH'];triangles=0
    for obj in objects:
        obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
        assert len(obj.data.color_attributes)>0,'缺少顶点颜色'
        assert len(obj.data.materials)==2,'缺少自然色或玩家色材质槽'
    assert len(objects)==38,'LOD或模块缺失'
    assert triangles==expected,'源文件与运行网格三角形不一致'
    return {'meshes':len(objects),'triangles':triangles,'vertexColors':True,'materialSlots':2}
bpy.ops.wm.open_mainfile(filepath=str(root/'source/ultra/v003/pasture/pasture.blend'))
source=inspect()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(export/'pasture.fbx'))
fbx=inspect()
(export/'blender-verification.json').write_text(json.dumps({'source':source,'fbx':fbx,'status':'通过','finiteVertices':True,'validIndices':True,'compressedRoundTrip':True,'meshSha256':hashlib.sha256((export/'pasture-mesh.json').read_bytes()).hexdigest()},ensure_ascii=False,indent=2),encoding='utf-8')
print('CATAN_U2_FBX_OK')
