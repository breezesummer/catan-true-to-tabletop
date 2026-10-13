"""Read-only Blender audit of wall hole versus the existing estimated city.

Run separately after the normal pipeline. This is a geometric clearance check
in source coordinate space, not measured physical fit or Unity validation.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy

REPO = Path(__file__).resolve().parents[4]
WALL = REPO / 'art/source/entity-conversion-v001/city-wall/city-wall-red.blend'
CITY = REPO / 'art/source/entity-conversion-v001/city/city-red.blend'
REPORT = REPO / 'art/exports/entity-conversion-v001/city-wall/insert-clearance.json'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def edge_distance_xy(a, b):
    dx, dy = b.x-a.x, b.y-a.y
    length = dx*dx+dy*dy
    t = max(0, min(1, -(a.x*dx+a.y*dy)/length)) if length else 0
    return math.hypot(a.x+t*dx, a.y+t*dy)


bpy.ops.wm.open_mainfile(filepath=str(WALL))
objects = [o for o in bpy.context.scene.objects if o.type == 'MESH']
assert len(objects) == 1
wall = objects[0]
vertices = [wall.matrix_world @ v.co for v in wall.data.vertices]
hole_radius = min(edge_distance_xy(vertices[e.vertices[0]], vertices[e.vertices[1]])
                  for e in wall.data.edges)
wall_height = max(v.z for v in vertices)
adjacent = [set() for _ in vertices]
for e in wall.data.edges:
    a, b = e.vertices
    adjacent[a].add(b)
    adjacent[b].add(a)
unvisited = set(range(len(vertices)))
shell_count = 0
while unvisited:
    shell_count += 1
    pending = [unvisited.pop()]
    while pending:
        for vertex in adjacent[pending.pop()]:
            if vertex in unvisited:
                unvisited.remove(vertex)
                pending.append(vertex)
assert shell_count == 99
assert hole_radius > .00929

bpy.ops.wm.open_mainfile(filepath=str(CITY))
city_vertices = [o.matrix_world @ v.co for o in bpy.context.scene.objects
                 if o.type == 'MESH' for v in o.data.vertices]
lower_insert = [v for v in city_vertices if v.z <= wall_height+1e-8]
assert lower_insert
city_radius = max(math.hypot(v.x, v.y) for v in lower_insert)
assert city_radius < hole_radius
report = {
    'status': 'passed',
    'method': 'minimum XY distance to source wall edges; maximum city vertex radius below wall top',
    'wallSource': WALL.relative_to(REPO).as_posix(), 'wallSha256': sha(WALL),
    'citySource': CITY.relative_to(REPO).as_posix(), 'citySha256': sha(CITY),
    'auditScriptSha256': sha(Path(__file__)),
    'closedShellCount': shell_count,
    'wallHeightMm': wall_height*1000,
    'minimumHoleDiameterMm': hole_radius*2000,
    'cityDiameterBelowWallTopMm': city_radius*2000,
    'minimumRadialClearanceMm': (hole_radius-city_radius)*1000,
    'limitations': [
        'Both pieces use estimated dimensions; this does not validate physical fit.',
        'Only centred source geometry checked, not runtime picking, animation or Unity transforms.',
        'Metropolis fit is separate and has not been checked here.'
    ]
}
REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print('CATAN_CITY_WALL_CLEARANCE_PASSED '+str(report))
