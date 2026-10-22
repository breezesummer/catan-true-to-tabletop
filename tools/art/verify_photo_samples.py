"""Independently reimport actual FBX exports and verify bounds, UV, closure and provenance."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import struct
import sys

import bpy

parser=argparse.ArgumentParser()
parser.add_argument('--output-root',type=Path,required=True)
parser.add_argument('--compare-root',type=Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
repo=Path(__file__).resolve().parents[2]
directory=args.output_root.resolve()/'exports/photo-samples-v001'
manifest=json.loads((directory/'manifest.json').read_text(encoding='utf-8'))
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
for ref in manifest['references']:
    assert sha(repo/ref['originalPath'])==ref['sha256'],'Original reference changed'
textures=[]
for tex in manifest['textureDependencies']:
    p=directory/tex['path'];assert sha(p)==tex['sha256']
    data=p.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n'
    w,h=struct.unpack('>II',data[16:24]);assert w>=512 and h>=512
    textures.append({'path':tex['path'],'width':w,'height':h,'verified':True})
reports=[]
for asset in manifest['assets']:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fbx=directory/(asset['id']+'.fbx');assert sha(fbx)==asset['fbxSha256']
    assert sha(args.output_root.resolve()/'source/photo-samples-v001'/(asset['id']+'.blend'))==asset['blendSha256']
    bpy.ops.import_scene.fbx(filepath=str(fbx))
    objects=list(bpy.context.scene.objects)
    assert len(objects)==asset['meshObjects'] and all(o.type=='MESH' for o in objects)
    points=[];triangles=0
    for o in objects:
        assert o.matrix_world.translation.length<1e-6,(o.name,'nonzero origin')
        assert o.data.uv_layers.active is not None
        edges=Counter()
        for p in o.data.polygons:
            assert len(p.vertices)==3 and p.area>1e-17,(o.name,'degenerate triangle')
            triangles+=1
            for i,a in enumerate(p.vertices):edges[tuple(sorted((a,p.vertices[(i+1)%3])))]+=1
        assert all(n==2 for n in edges.values()),(o.name,'open mesh')
        points.extend(o.matrix_world@v.co for v in o.data.vertices)
    minimum=[min(v[i] for v in points) for i in range(3)]
    maximum=[max(v[i] for v in points) for i in range(3)]
    assert all(abs(minimum[i]-asset['blenderBounds']['min'][i])<1e-6 and abs(maximum[i]-asset['blenderBounds']['max'][i])<1e-6 for i in range(3))
    assert abs(minimum[2])<1e-6 and triangles==asset['triangles']
    dimensions=[maximum[i]-minimum[i] for i in range(3)]
    if asset['id']=='road':assert abs(dimensions[1]-.026)<1e-6 and dimensions[0]<=.00501 and maximum[2]<=.00501
    if asset['id']=='village':assert dimensions[0]<=.01201 and dimensions[1]<=.01201 and maximum[2]<=.01601
    if asset['id']=='forest':assert abs(maximum[2]-.008)<1e-6 and abs(dimensions[1]-.0792)<1e-6
    if asset['id']=='resource-card':assert abs(dimensions[0]-.054)<1e-6 and abs(dimensions[1]-.08)<1e-6
    reports.append({'id':asset['id'],'meshObjects':len(objects),'triangles':triangles,'closedMeshes':True,'uvMeshes':len(objects),'bottomCentrePivot':True,'blenderDimensionsMetres':dimensions})
views=[]
for name in manifest['previewViews']:
    data=(args.output_root.resolve()/'previews/photo-samples-v001'/(name+'.png')).read_bytes()
    assert data[:8]==b'\x89PNG\r\n\x1a\n' and struct.unpack('>II',data[16:24])==(1600,1000)
    views.append(name)
comparison=None
if args.compare_root:
    original=json.loads((args.compare_root/'exports/photo-samples-v001/manifest.json').read_text(encoding='utf-8'))
    for actual,expected in zip(manifest['assets'],original['assets']):
        for field in ('id','meshObjects','triangles','geometrySha256','blenderBounds','expectedUnityBounds','materials'):
            assert actual[field]==expected[field],(actual['id'],'clean rebuild changed '+field)
    assert manifest['textureDependencies']==original['textureDependencies'] and manifest['references']==original['references']
    comparison={'sameGeometryMaterialsTexturesReferences':True,'referenceRoot':str(args.compare_root)}
report={'status':'passed','assets':reports,'textureDependencies':textures,'previews':views,'originalReferencesUnchanged':True,'cleanRebuild':comparison,
        'notCovered':['Unity runtime rendering and input','user visual approval','physical measurements and unseen surfaces']}
(directory/'verification.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
print('CATAN_PHOTO_FBX_VERIFIED '+json.dumps(report))
