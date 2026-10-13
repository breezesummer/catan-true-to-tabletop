"""Sea pirate from one dark oblique photo; a self-contained closed-shell sculpt.

The reference shows a ribbed low support, a bulky raised hull, asymmetrical
split sails and a little irregular masthead protrusion. The photo cannot
establish a skull emblem or hidden-side detail. Construction is in nominal
mm, normalised to the recipe's metre bounds before returning a single mesh.
X is transverse, Y is the long axis, Z is up; export maps length to Unity Z.
"""
import math

import bpy
from mathutils import Vector


def build(asset, helpers):
    objects = []
    plastic = helpers['mat'](asset['id'] + '_Black_Moulded_Plastic',
                             tuple(asset['builderArgs']['color']), roughness=.34)

    def mesh(name, vertices, faces, smooth=False):
        # Create an explicit layer before finish: avoid non-deterministic
        # operator UV packing, then fill a stable per-face atlas after joining.
        data = bpy.data.meshes.new(name + '_Mesh')
        data.from_pydata(vertices, [], faces)
        data.update()
        data.uv_layers.new(name='DeterministicFaceAtlas')
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        helpers['finish'](obj, plastic)
        for polygon in obj.data.polygons:
            polygon.use_smooth = smooth
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=True):
        n = len(rings[0])
        vertices = [point for ring in rings for point in ring]
        faces = [tuple(reversed(range(n)))]
        for r in range(len(rings)-1):
            for i in range(n):
                j = (i+1) % n
                faces.append((r*n+i, r*n+j, (r+1)*n+j, (r+1)*n+i))
        faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
        return mesh(name, vertices, faces, smooth)

    def tube(name, points, radius, sides=10):
        rings = []
        for index, point in enumerate(points):
            p0 = Vector(points[max(index-1, 0)])
            p1 = Vector(points[min(index+1, len(points)-1)])
            tangent = (p1-p0).normalized()
            reference = Vector((1, 0, 0)) if abs(tangent.x) < .9 else Vector((0, 1, 0))
            normal = tangent.cross(reference).normalized()
            binormal = tangent.cross(normal).normalized()
            rings.append([tuple(Vector(point)+radius*(math.cos(i*math.tau/sides)*normal
                          +math.sin(i*math.tau/sides)*binormal)) for i in range(sides)])
        return loft(name, rings)

    def elliptical(name, centre, rings, sides=20):
        cx, cy = centre
        return loft(name, [[(cx+rx*math.cos(i*math.tau/sides),
                             cy+ry*math.sin(i*math.tau/sides), z) for i in range(sides)]
                            for z, rx, ry in rings])

    def bar(name, dimensions, location):
        w, d, h = dimensions
        x, y, z = location
        vertices = [(x+sx*w/2, y+sy*d/2, z+sz*h/2)
                    for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        return mesh(name, vertices, [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4),
                                    (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)])

    # Broad low support is visible below the pirate, unlike the player's boat.
    # Slightly scalloped edges and separate ridges approximate the dark photo's
    # parallel sculpted wave strips. The underside is an unknown flat support.
    outline = [(-5.25, -14), (4.45, -14), (5.30, -13.45), (5.80, -11),
               (5.72, -7), (6.00, -3), (5.77, 1), (5.92, 5), (5.61, 9),
               (5.85, 12), (5.10, 13.50), (4.60, 14), (-4.72, 14),
               (-5.72, 13.20), (-5.90, 10), (-5.62, 6), (-5.95, 2),
               (-5.75, -2), (-6.00, -6), (-5.72, -10), (-5.90, -12.5)]
    loft('Pirate_Low_Wave_Support', [[(x, y, z) for x, y in outline]
                                     for z in (0, .83)], smooth=False)
    for i in range(9):
        x = -5.35+i*1.31
        start = -13.60+.20*math.sin(i*1.7)
        end = 13.55-.25*math.cos(i*1.3)
        points = [(x+.16*math.sin(t*math.tau+i*.80), start+(end-start)*t,
                   .83+.16*math.sin(t*math.pi+i*.60)) for t in [j/24 for j in range(25)]]
        tube('Pirate_Wave_Ridge_'+str(i), points, .27, sides=8)
    for side in (-1, 1):
        for i in range(3):
            y = side*(10.4+i*.88)
            tube('Pirate_End_Wave_'+str(side)+'_'+str(i),
                 [(-4.5+j*.45, y+.30*math.sin(j*.39), .95+.13*math.sin(j*.29))
                  for j in range(21)], .20, sides=8)

    stations = [-11.6, -11.2, -10.5, -9, -7, -4, 0, 4, 7, 9, 10.5, 11.2, 11.6]

    def hull_section(y):
        ratio = abs(y)/11.6
        w = .23+4.10*max(0, 1-ratio*ratio)**.58
        lift = 2.95*ratio**5
        bottom = 1.15+lift
        top = 6.40+lift
        return w, bottom, top, top-.98

    rings = []
    for y in stations:
        w, bottom, top, deck = hull_section(y)
        profile = [(-w, top), (-w*.98, top-.66), (-w*.83, bottom+1.03),
                   (-w*.50, bottom+.28), (0, bottom), (w*.50, bottom+.28),
                   (w*.83, bottom+1.03), (w*.98, top-.66), (w, top),
                   (w*.72, top-.10), (w*.66, deck), (0, deck),
                   (-w*.66, deck), (-w*.72, top-.10)]
        rings.append([(x, y, z) for x, z in profile])
    loft('Pirate_Bulky_Concave_Hull', rings)
    for side in (-1, 1):
        for i, fraction in enumerate((.27, .53, .78)):
            tube('Pirate_Hull_Strake_'+str(side)+'_'+str(i),
                 [(side*hull_section(y)[0]*(.88+.10*fraction), y,
                   hull_section(y)[1]+(hull_section(y)[2]-hull_section(y)[1])*fraction)
                  for y in stations[1:-1]], .21, sides=8)
        tube('Pirate_Heavy_Gunwale_'+str(side),
             [(side*hull_section(y)[0]*.93, y, hull_section(y)[2]+.09)
              for y in stations], .31)
        # Broadside ribs are simplified dark moulded relief, not a skull badge.
        for i, y in enumerate((-7.0, -3.5, 0, 3.5, 7.0)):
            w, bottom, top, _ = hull_section(y)
            tube('Pirate_Broadside_Rib_'+str(side)+'_'+str(i),
                 [(side*w*.84, y, bottom+.8), (side*w*.97, y+.17, bottom+2.2),
                  (side*w, y+.27, top-.35)], .17, sides=8)
    tube('Pirate_Curled_Bow', [(0, 10.9, 7.9), (0, 11.2, 9.6),
                             (0, 10.4, 10.0), (0, 9.8, 9.5)], .47)
    tube('Pirate_Raised_Stern', [(0, -11.4, 8.4), (0, -11.3, 9.3),
                               (0, -10.5, 9.5)], .46)
    # A squat raised aft deck preserves the photo's bulk; no invented windows.
    bar('Pirate_Aft_Deck_Block', (5.0, 4.5, 2.55), (0, -6.45, 6.55))
    bar('Pirate_Aft_Deck_Cap', (5.55, 4.90, .45), (0, -6.45, 7.95))
    elliptical('Pirate_Central_Mast_Base', (0, -.10),
               [(5.20, 1.1, 1.0), (6.10, 1.16, 1.04), (9.0, .69, .64)])
    tube('Pirate_Leaning_Mast', [(0, -.10, 6.0), (0, -.18, 16.0),
                                (0, -.50, 26.4), (0, -.56, 29.3)], .42, sides=12)
    elliptical('Pirate_Masthead_Collar', (0, -.54),
               [(27.10, .48, .49), (27.26, .85, .78), (27.66, .81, .76),
                (27.78, .45, .44)], sides=16)

    def main_sail(u, v, layer=0):
        width = 6.90-.90*v+2.35*math.sin(math.pi*v)
        bottom = 11.8+1.7*u+.7*math.sin(math.pi*u)
        top = 26.65+2.15*u
        return (1.62*math.sin(math.pi*u)*(.5+.5*math.sin(math.pi*v))+layer*.23,
                -.78-width*u, bottom+(top-bottom)*v)

    def fore_sail(u, v, layer=0):
        leading = .55+1.80*(1-v)**5
        width = 5.50+1.05*math.sin(math.pi*v)
        bottom = 11.9+2.25*u
        top = 26.10-4.50*u
        return (-.83*math.sin(math.pi*u)*math.sin(math.pi*v)+layer*.23,
                leading+width*u, bottom+(top-bottom)*v)

    def closed_sail(name, point, nu, nv):
        vertices = [point(i/nu, j/nv, layer) for layer in (-1, 1)
                    for j in range(nv+1) for i in range(nu+1)]
        stride = (nu+1)*(nv+1)
        faces = []
        for layer in range(2):
            for j in range(nv):
                for i in range(nu):
                    p = layer*stride+j*(nu+1)+i
                    quad = (p, p+1, p+nu+2, p+nu+1)
                    faces.append(tuple(reversed(quad)) if layer == 0 else quad)
        boundary = list(range(nu+1))
        boundary += [j*(nu+1)+nu for j in range(1, nv+1)]
        boundary += [nv*(nu+1)+i for i in range(nu-1, -1, -1)]
        boundary += [j*(nu+1) for j in range(nv-1, 0, -1)]
        for p, q in zip(boundary, boundary[1:]+boundary[:1]):
            faces.append((p, q, q+stride, p+stride))
        mesh(name, vertices, faces, smooth=True)
        for edge_name, points in [
            ('Outer', [point(1, j/nv) for j in range(nv+1)]),
            ('Lower', [point(i/nu, 0) for i in range(nu+1)]),
            ('Upper', [point(i/nu, 1) for i in range(nu+1)])]:
            tube(name+'_'+edge_name+'_Seam', points, .18, sides=8)
        for i, u in enumerate((.30, .65)):
            points = [point(u, j/nv) for j in range(nv+1)]
            tube(name+'_Raised_Fold_'+str(i),
                 [(x+.24, y, z) for x, y, z in points], .075, sides=8)

    closed_sail('Pirate_Large_Bowed_Main_Sail', main_sail, 14, 24)
    closed_sail('Pirate_Smaller_Sloped_Fore_Sail', fore_sail, 10, 20)
    # Tiny asymmetrical dark protrusion seen at the masthead. Its unreadable
    # surface is intentionally plain rather than carrying an invented emblem.
    flag = [(-.55, 29.20), (.20, 29.75), (1.28, 29.28), (.99, 28.66),
            (1.42, 28.12), (.50, 27.95), (-.55, 28.18)]
    n = len(flag)
    vertices = [(side*.24, y, z) for side in (-1, 1) for y, z in flag]
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    mesh('Pirate_Small_Irregular_Masthead_Flag', vertices, faces)

    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    lower = [min(p[i] for p in points) for i in range(3)]
    upper = [max(p[i] for p in points) for i in range(3)]
    offset = [(lower[0]+upper[0])/2, (lower[1]+upper[1])/2, lower[2]]
    scales = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i]) for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-offset[i])*scales[i] for i in range(3))
        obj.data.update()
    # Keep component shells closed while making the black sculpt one draw mesh.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'Pirate_Black_Double_Sail_Wave_Sculpt'
    obj.data.name = obj.name+'_Mesh'
    uv = obj.data.uv_layers.active
    uv.name = 'DeterministicFaceAtlas'
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omitted = max(range(3), key=lambda i: abs(face.normal[i]))
        axes = [i for i in range(3) if i != omitted]
        points = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        lo = [min(p[axis] for p in points) for axis in axes]
        hi = [max(p[axis] for p in points) for axis in axes]
        for loop_index, point in zip(face.loop_indices, points):
            projected = [(point[axis]-lo[j])/max(hi[j]-lo[j], 1e-12) for j, axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(((face.index % tiles if j == 0 else face.index//tiles)
                                          +.08+.84*projected[j])/tiles for j in range(2))
    return [obj]
