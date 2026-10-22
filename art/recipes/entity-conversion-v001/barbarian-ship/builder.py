"""Single-photo barbarian invasion ship, independent from player/sea-pirate ships.

Source filename calls the invasion token a pirate; asset semantics use the
Cities & Knights barbarian ship. Nominal mm construction is normalised to
12 x 28 x 26 mm in metres, X width / Y length / Z up, bottom-centre pivot.
Closed, untextured components have deterministic UVs and join by material.
No imports, meshes or runtime dependencies from any other asset type.
"""
import math

import bpy
from mathutils import Vector


def build(asset, helpers):
    objects = []
    args = asset['builderArgs']
    body = helpers['mat']('Barbarian_Dark_Moulded_Plastic', tuple(args['color']), .32)
    water = helpers['mat']('Barbarian_Wave_Base_Plastic', tuple(args['waveColor']), .37)

    def mesh(name, vertices, faces, material=body, smooth=False):
        # Precreate UVs before finish to avoid nondeterministic Smart Project.
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
        helpers['finish'](obj, material)
        for polygon in obj.data.polygons:
            polygon.use_smooth = smooth
        objects.append(obj)
        return obj

    def loft(name, rings, material=body, smooth=True):
        count = len(rings[0])
        faces = [tuple(reversed(range(count)))]
        for r in range(len(rings) - 1):
            for i in range(count):
                j = (i + 1) % count
                faces.append((r*count+i, r*count+j, (r+1)*count+j, (r+1)*count+i))
        faces.append(tuple((len(rings)-1)*count+i for i in range(count)))
        return mesh(name, [point for ring in rings for point in ring], faces, material, smooth)

    def tube(name, points, radii, material=body, sides=10):
        rings = []
        for index, point in enumerate(points):
            tangent = (Vector(points[min(index+1, len(points)-1)]) -
                       Vector(points[max(0, index-1)])).normalized()
            reference = Vector((1, 0, 0)) if abs(tangent.x) < .9 else Vector((0, 1, 0))
            normal = tangent.cross(reference).normalized()
            binormal = tangent.cross(normal).normalized()
            radius = radii[index] if isinstance(radii, (list, tuple)) else radii
            rings.append([tuple(Vector(point) + radius*(normal*math.cos(i*math.tau/sides)
                                   + binormal*math.sin(i*math.tau/sides)))
                          for i in range(sides)])
        return loft(name, rings, material)

    def ellipsoid(name, centre, size, material=body, sides=16, stacks=8):
        rings = []
        for j in range(stacks+1):
            angle = math.radians(7 + 166*j/stacks)
            rings.append([(centre[0]+size[0]*math.sin(angle)*math.cos(i*math.tau/sides),
                           centre[1]+size[1]*math.sin(angle)*math.sin(i*math.tau/sides),
                           centre[2]+size[2]*math.cos(angle)) for i in range(sides)])
        return loft(name, list(reversed(rings)), material)

    # Visible rectangular, scalloped water support is an actual closed surface.
    # It is a separate sculptural feature, absent from the player's route ships.
    n = 64
    boundary = []
    for i in range(n):
        angle = i*math.tau/n
        c, s = math.cos(angle), math.sin(angle)
        ripple = 1 + .032*math.sin(7*angle) + .022*math.cos(11*angle)
        boundary.append((5.8*math.copysign(abs(c)**.52, c)*ripple,
                         13.5*math.copysign(abs(s)**.52, s)*ripple))
    verts = [(x, y, 0) for x, y in boundary]
    for fraction in (1.0, .77, .52, .27):
        for x, y in boundary:
            x, y = x*fraction, y*fraction
            z = .85 + .31*math.sin(.70*y+.90*x) + .17*math.cos(1.0*x-.36*y)
            verts.append((x, y, z))
    faces = [tuple(reversed(range(n)))]
    for ring in range(4):
        for i in range(n):
            j = (i+1) % n
            faces.append((ring*n+i, ring*n+j, (ring+1)*n+j, (ring+1)*n+i))
    centre = len(verts)
    verts.append((0, 0, 1.05))
    for i in range(n):
        faces.append((4*n+i, 4*n+(i+1) % n, centre))
    mesh('Barbarian_Scalloped_Water_Support', verts, faces, water, True)

    # Moulded flowing crests run along both exposed sides of the support.
    # Their curls are simplified because the one dark photo cannot define them.
    for side in (-1, 1):
        for row in range(2):
            points = []
            radii = []
            for j in range(25):
                t = j/24
                y = -12.0 + 24.0*t
                x = side*(3.8+row*.85+.48*math.sin(2.7*math.pi*t+row))
                z = 1.00+.29*math.sin(3.5*math.pi*t+row)+row*.17
                points.append((x, y, z))
                radii.append(.10+.36*math.sin(math.pi*t)**.65)
            tube('Barbarian_Wave_Crest_'+str(side)+'_'+str(row), points, radii, water, 8)
        for curl, y in enumerate((-8.3, 2.1, 9.2)):
            points = []
            for j in range(12):
                angle = math.pi*.16 + math.pi*1.3*j/11
                points.append((side*(4.3+.60*math.cos(angle)),
                               y+1.8*math.sin(angle), 1.23+.22*math.sin(angle)))
            tube('Barbarian_Wave_Curl_'+str(side)+'_'+str(curl), points,
                 [.13+.22*math.sin(math.pi*j/11) for j in range(12)], water, 8)

    # Concave hull with high curved stems, ridged exterior and low open deck.
    stations = [-12.0, -11.7, -11.2, -10.2, -8.8, -6.6, -3.5, 0,
                3.5, 6.6, 8.8, 10.2, 11.2, 11.7, 12.0]

    def section(y):
        ratio = abs(y)/12
        width = .18 + 3.25*max(0, 1-ratio*ratio)**.58
        lift = 4.2*ratio**6
        # Keel intersects the sculpted wave surface so the ship is supported,
        # while the curved fore/aft ends rise above it as in the photograph.
        return width, .78+lift, 5.05+lift, 4.27+lift

    rings = []
    for y in stations:
        w, low, top, deck = section(y)
        cross = [(-w, top), (-w*.97, top-.55), (-w*.78, low+.72),
                 (-w*.40, low+.18), (0, low), (w*.40, low+.18),
                 (w*.78, low+.72), (w*.97, top-.55), (w, top),
                 (w*.76, top-.15), (w*.64, deck), (0, deck),
                 (-w*.64, deck), (-w*.76, top-.15)]
        rings.append([(x, y, z) for x, z in cross])
    loft('Barbarian_Long_Concave_Hull', rings)
    for side in (-1, 1):
        for strake, fraction in enumerate((.28, .56, .82)):
            points = [(side*section(y)[0]*(.83+.15*fraction), y,
                       section(y)[1]+(section(y)[2]-section(y)[1])*fraction)
                      for y in stations[1:-1]]
            tube('Barbarian_Raised_Strake_'+str(side)+'_'+str(strake), points, .17, sides=8)
        points = [(side*section(y)[0]*.92, y, section(y)[2]+.06) for y in stations]
        tube('Barbarian_Gunwale_'+str(side), points, .22)
        # Fine regular oval bosses read as moulded hull ornament; not skull art.
        for boss, y in enumerate((-7.2, -4.6, -2.0, .6, 3.2, 5.8, 8.4)):
            w, low, top, _ = section(y)
            ellipsoid('Barbarian_Hull_Boss_'+str(side)+'_'+str(boss),
                      (side*w*.98, y, low+(top-low)*.52), (.18, .46, .36),
                      sides=12, stacks=5)
    # Visible forward stem has a round terminal, rather than a cabin/road stub.
    tube('Barbarian_Forward_Upturned_Stem',
         [(0, 11.3, 7.7), (0, 11.9, 9.2), (0, 12.35, 10.5),
          (0, 12.95, 11.1), (0, 13.35, 11.45)], [.54, .57, .58, .59, .66], sides=12)
    ellipsoid('Barbarian_Forward_Rounded_Head', (0, 13.37, 11.62), (.83, .78, 1.00))
    tube('Barbarian_Rear_Curled_Stem',
         [(0, -11.0, 7.6), (0, -11.8, 8.9), (0, -12.25, 10.0),
          (0, -12.48, 10.8), (0, -12.12, 11.2)], [.50, .54, .56, .56, .48], sides=12)

    # A broad bowed sheet with thick sealed borders: the dominant dark photo
    # silhouette. Its upper edge curves into a rounded top, unlike player sails.
    def sail_point(u, v, layer=0):
        leading = -5.6 + .85*v
        width = 9.50 - 4.2*v + 2.5*math.sin(math.pi*v)
        y = leading + width*u
        z = 5.5 + 18.8*v + .70*math.sin(math.pi*u)*v**5
        x = 2.00*math.sin(math.pi*u)*(.38+.62*math.sin(math.pi*v)) + layer*.34
        return (x, y, z)

    nu, nv = 12, 20
    vertices = [sail_point(i/nu, j/nv, layer)
                for layer in (-1, 1) for j in range(nv+1) for i in range(nu+1)]
    stride = (nu+1)*(nv+1)
    faces = []
    for layer in range(2):
        offset = layer*stride
        for j in range(nv):
            for i in range(nu):
                p = offset+j*(nu+1)+i
                quad = (p, p+1, p+nu+2, p+nu+1)
                faces.append(tuple(reversed(quad)) if layer == 0 else quad)
    boundary = list(range(nu+1))
    boundary += [j*(nu+1)+nu for j in range(1, nv+1)]
    boundary += [nv*(nu+1)+i for i in range(nu-1, -1, -1)]
    boundary += [j*(nu+1) for j in range(nv-1, 0, -1)]
    for p, q in zip(boundary, boundary[1:]+boundary[:1]):
        faces.append((p, q, q+stride, p+stride))
    mesh('Barbarian_Closed_Billowed_Sail', vertices, faces, smooth=True)
    tube('Barbarian_Sail_Outer_Rim', [sail_point(1, j/nv) for j in range(nv+1)], .30)
    tube('Barbarian_Sail_Foot_Rim', [sail_point(i/nu, 0) for i in range(nu+1)], .28)
    tube('Barbarian_Sail_Rounded_Top_Rim', [sail_point(i/nu, 1) for i in range(nu+1)], .32)
    tube('Barbarian_Leaning_Mast', [(0, -5.6+.85*j/nv, 4.0+21.2*j/nv)
                                   for j in range(nv+1)], .43, sides=12)
    tube('Barbarian_Curled_Masthead',
         [(0, -4.75, 24.6), (0, -5.05, 25.15), (0, -5.75, 25.35),
          (0, -6.34, 25.20), (0, -6.60, 24.75)], [.43, .48, .50, .52, .56], sides=12)
    ellipsoid('Barbarian_Masthead_Round_End', (0, -6.61, 24.80), (.65, .64, .66))

    points = [v.co[:] for obj in objects for v in obj.data.vertices]
    lower = [min(p[i] for p in points) for i in range(3)]
    upper = [max(p[i] for p in points) for i in range(3)]
    origin = [(lower[0]+upper[0])/2, (lower[1]+upper[1])/2, lower[2]]
    scales = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i]) for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-origin[i])*scales[i] for i in range(3))
        obj.data.update()

    # Disconnected shells stay closed; each material becomes one render mesh.
    combined = []
    groups = [(name, [obj for obj in objects if obj.data.materials[0] == material])
              for material, name in ((body, 'Barbarian_Ship_Sculpt'),
                                     (water, 'Barbarian_Wave_Support'))]
    for name, group in groups:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        obj.data.name = name+'_Mesh'
        uv = obj.data.uv_layers.active
        tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
        for face in obj.data.polygons:
            omit = max(range(3), key=lambda i: abs(face.normal[i]))
            axes = [i for i in range(3) if i != omit]
            coords = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
            lo = [min(p[a] for p in coords) for a in axes]
            hi = [max(p[a] for p in coords) for a in axes]
            for loop_index, point in zip(face.loop_indices, coords):
                projected = [(point[a]-lo[j])/max(hi[j]-lo[j], 1e-12) for j, a in enumerate(axes)]
                uv.data[loop_index].uv = tuple(((face.index % tiles if j == 0 else face.index // tiles)
                                               +.08+.84*projected[j])/tiles for j in range(2))
        combined.append(obj)
    return combined
