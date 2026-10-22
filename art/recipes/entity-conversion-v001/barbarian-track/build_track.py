"""Create a closed, concave, photo-backed thin panel; all lengths are metres."""
from mathutils import Vector
from mathutils.geometry import tessellate_polygon


def build(asset, helpers):
    width, depth, height = [float(value)/1000 for value in asset['dimensionsMm']]
    outline = asset['panelOutline']
    xy = [((u-.5)*width, (.5-v)*depth) for u,v in outline]
    # The imported image outline is clockwise; the Y reversal makes it CCW.
    area = sum(xy[i][0]*xy[(i+1)%len(xy)][1]-xy[(i+1)%len(xy)][0]*xy[i][1] for i in range(len(xy)))
    if area < 0:
        xy.reverse()
        outline = list(reversed(outline))
    vectors = [Vector((x,y,0)) for x,y in xy]
    index = {tuple(vector): i for i,vector in enumerate(vectors)}
    triangles = [tuple(point if isinstance(point, int) else index[tuple(point)] for point in triangle)
                 for triangle in tessellate_polygon([vectors])]
    if len(triangles) != len(xy)-2:
        raise ValueError('Concave track outline did not tessellate completely')
    n = len(xy)
    vertices = [(x,y,z) for z in (0,height) for x,y in xy]
    faces = [tuple(reversed(triangle)) for triangle in triangles]
    faces += [tuple(i+n for i in triangle) for triangle in triangles]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mat = helpers['mat']
    edge = mat(asset['id']+'_UnseenPaperEdgeNeutral', (.66,.58,.43), .85)
    front = mat(asset['id']+'_PhotoFront', (1,1,1), .8, asset['frontTexture'])
    back = mat(asset['id']+'_UnseenBackNeutral', (.66,.61,.49), .8)
    obj = helpers['mesh'](asset['id'], vertices, faces, edge)
    obj.data.materials.append(front)
    obj.data.materials.append(back)
    uv = obj.data.uv_layers.active
    for polygon in obj.data.polygons:
        polygon.material_index = 1 if polygon.normal.z > .9 else (2 if polygon.normal.z < -.9 else 0)
        if abs(polygon.normal.z) > .9:
            for loop_index in polygon.loop_indices:
                vertex_index = obj.data.loops[loop_index].vertex_index % n
                u,v = outline[vertex_index]
                uv.data[loop_index].uv = (u,1-v) if polygon.normal.z > 0 else (1-u,1-v)
    return [obj]
