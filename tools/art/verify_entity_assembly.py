"""Independent source-mesh clearance of estimated city/metropolis inside wall."""
import hashlib
import json
import math
from pathlib import Path
import bpy

REPO=Path(__file__).resolve().parents[2]
SOURCE=REPO/'art/source/entity-conversion-v001'

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def edge_radius(a,b):
    dx,dy=b.x-a.x,b.y-a.y
    denominator=dx*dx+dy*dy
    t=max(0,min(1,-(a.x*dx+a.y*dy)/denominator)) if denominator else 0
    return math.hypot(a.x+t*dx,a.y+t*dy)

wall_path=SOURCE/'city-wall/city-wall-red.blend'
bpy.ops.wm.open_mainfile(filepath=str(wall_path))
wall_objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
hole_radius=float('inf'); wall_height=0
for obj in wall_objects:
    points=[obj.matrix_world@v.co for v in obj.data.vertices]
    wall_height=max(wall_height,max(p.z for p in points))
    hole_radius=min(hole_radius,min(edge_radius(points[e.vertices[0]],points[e.vertices[1]]) for e in obj.data.edges))
    obj.data.calc_loop_triangles()
    for triangle in obj.data.loop_triangles:
        a,b,c=[points[i] for i in triangle.vertices]
        area=(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x)
        if abs(area)<1e-12:continue
        crosses=[p.x*q.y-p.y*q.x for p,q in ((a,b),(b,c),(c,a))]
        assert not (all(x>=-1e-12 for x in crosses) or all(x<=1e-12 for x in crosses)), 'Wall face covers centre hole'
assert hole_radius>.009
reports=[]
for type_id,asset_id in [('city','city-red'),('metropolis','metropolis-red')]:
    path=SOURCE/type_id/(asset_id+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    points=[o.matrix_world@v.co for o in bpy.context.scene.objects if o.type=='MESH' for v in o.data.vertices]
    low=[p for p in points if p.z<=wall_height+1e-8]
    assert low
    radius=max(math.hypot(p.x,p.y) for p in low)
    assert radius<hole_radius,asset_id+' intersects estimated opening'
    reports.append({'id':asset_id,'source':path.relative_to(REPO).as_posix(),'sha256':sha(path),
                    'lowerDiameterMm':radius*2000,'minimumRadialClearanceMm':(hole_radius-radius)*1000})
report={'status':'passed','wallSource':wall_path.relative_to(REPO).as_posix(),'wallSha256':sha(wall_path),
        'minimumOpeningDiameterMm':hole_radius*2000,'wallHeightMm':wall_height*1000,'inserts':reports,
        'method':'projected source wall edges and faces; insert vertices below wall height',
        'scriptSha256':sha(Path(__file__)),'limitation':'estimated centred meshes; physical measurements and interaction not verified'}
output=REPO/'docs/verification/entity-conversion-v001/assembly-clearance.json'
output.parent.mkdir(parents=True,exist_ok=True)
output.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('CATAN_ENTITY_ASSEMBLY_CLEARANCE_OK')
