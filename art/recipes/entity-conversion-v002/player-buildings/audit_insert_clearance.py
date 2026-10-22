"""Independent source audit of six assemblies and three separate ring holes.

Conservative geometric envelope below wall top; this checks centred model
nesting, not measured physical fit or Unity input/animation transforms.
"""
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import bpy

REPO=Path(__file__).resolve().parents[4]
SOURCE=REPO/'art/source/entity-conversion-v002/player-buildings'
EXPORT=REPO/'art/exports/entity-conversion-v002/player-buildings'
COLOURS=['orange','beige','blue']


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def edge_distance(a,b):
    dx,dy=b.x-a.x,b.y-a.y
    length=dx*dx+dy*dy
    t=max(0,min(1,-(a.x*dx+a.y*dy)/length)) if length else 0
    return math.hypot(a.x+t*dx,a.y+t*dy)


def signature(obj):
    # Independent from generator signatures; require identical vertex order,
    # face topology and UV values, ignoring intentionally different object IDs.
    geometry={
        'vertices':[[round(float(c),9) for c in obj.matrix_world@v.co] for v in obj.data.vertices],
        'faces':[list(face.vertices) for face in obj.data.polygons],
        'uv':[[round(float(c),7) for c in loop.uv] for loop in obj.data.uv_layers.active.data]}
    return hashlib.sha256(json.dumps(geometry,sort_keys=True).encode()).hexdigest()


def read(asset):
    path=SOURCE/(asset+'.blend')
    bpy.ops.wm.open_mainfile(filepath=str(path))
    return path,[o for o in bpy.context.scene.objects if o.type=='MESH']


def shell_count(obj):
    adjacent=[set() for _ in obj.data.vertices]
    for edge in obj.data.edges:
        a,b=edge.vertices
        adjacent[a].add(b)
        adjacent[b].add(a)
    unvisited=set(range(len(adjacent)))
    total=0
    while unvisited:
        total+=1
        pending=[unvisited.pop()]
        while pending:
            for neighbour in adjacent[pending.pop()]:
                if neighbour in unvisited:
                    unvisited.remove(neighbour)
                    pending.append(neighbour)
    return total


reports=[]
for colour in COLOURS:
    wall_path,wall_objects=read('city-wall-'+colour)
    assert len(wall_objects)==1
    wall=wall_objects[0]
    wall_sig=signature(wall)
    wall_points=[wall.matrix_world@v.co for v in wall.data.vertices]
    hole_radius=min(edge_distance(wall_points[e.vertices[0]],wall_points[e.vertices[1]]) for e in wall.data.edges)
    wall_height=max(v.z for v in wall_points)
    shells=shell_count(wall)
    assert shells==99 and hole_radius>.00929
    for kind in ('city','metropolis'):
        body_path,body_objects=read(kind+'-'+colour)
        assert len(body_objects)==1
        body=body_objects[0]
        body_sig=signature(body)
        body_points=[body.matrix_world@v.co for v in body.data.vertices]
        lower=[v for v in body_points if v.z<=wall_height+1e-8]
        assert lower
        body_radius=max(math.hypot(v.x,v.y) for v in lower)
        clearance=hole_radius-body_radius
        assert clearance>.00029
        combined_path,combined=read(kind+'-with-wall-'+colour)
        assert len(combined)==2
        combined_wall=next(o for o in combined if o.name=='Combined_Independent_City_Wall')
        combined_body=next(o for o in combined if o is not combined_wall)
        assert signature(combined_wall)==wall_sig
        assert signature(combined_body)==body_sig
        assert all(o.matrix_world.translation.length<1e-8 for o in combined)
        shades=[tuple(o.data.materials[0].node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value) for o in combined]
        assert shades[0]==shades[1]
        reports.append({
            'id':kind+'-with-wall-'+colour,'status':'passed','meshObjects':2,'bothOriginsZero':True,
            'standaloneGeometryUvUnchangedInAssembly':True,'bodyWallColourMatches':True,
            'wallClosedShells':shells,'wallHeightMm':wall_height*1000,
            'minimumHoleDiameterMm':hole_radius*2000,'bodyDiameterBelowWallTopMm':body_radius*2000,
            'minimumRadialClearanceMm':clearance*1000,
            'sourceFiles':[{'path':p.relative_to(REPO).as_posix(),'sha256':sha(p)} for p in (wall_path,body_path,combined_path)]})
report={
    'status':'passed','assets':reports,'auditScriptSha256':sha(Path(__file__)),
    'method':'minimum XY distance to every source wall edge; maximum body vertex radius below wall top; direct geometry/UV comparison with separate parts',
    'limitations':['Estimated geometry, not measured physical insert clearance.',
                   'Centred, stationary source transforms only; Unity runtime input/animation is separate.',
                   'Closed intersecting shells within each sculpture, without Boolean union.']}
(EXPORT/'insert-clearance.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('CATAN_PLAYER_BUILDING_CLEARANCE_PASSED assemblies='+str(len(reports)))
