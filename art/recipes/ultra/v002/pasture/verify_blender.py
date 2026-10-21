"""Blender 源文件与 FBX 回读；不把仅生成成功当成导出验证。"""
import argparse
import json
import sys
from pathlib import Path
import bpy

ap=argparse.ArgumentParser();ap.add_argument('--root',required=True)
args=ap.parse_args(sys.argv[sys.argv.index('--')+1:]);root=Path(args.root)
export=root/'exports/ultra/v002/pasture'
data=json.loads((export/'pasture-mesh.json').read_text())
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
bpy.ops.wm.open_mainfile(filepath=str(root/'source/ultra/v002/pasture/pasture.blend'))
source=inspect()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(export/'pasture.fbx'))
fbx=inspect()
(export/'blender-verification.json').write_text(json.dumps({'source':source,'fbx':fbx,'status':'通过'},ensure_ascii=False,indent=2),encoding='utf-8')
print('CATAN_U2_FBX_OK')
