"""Closed, photo-guided ring wall independent of its city/metropolis insert.

Dimensions are estimated millimetres before normalisation to recipe metres.
The two photographs show the same low circular wall, rounded projecting
towers, crenellations, moulded horizontal ledges and vertical buttresses.
The unseen inner/bottom profile and eight-fold tower layout are approximations.
No component from another asset builder is imported or included.
"""
import math

import bpy


def build(asset, helpers):
    objects = []
    args = asset['builderArgs']
    plastic = helpers['mat'](asset['id'] + '_Moulded_Plastic',
                             tuple(args['color']), roughness=.32)
    # No random detail is used; the fixed recipe seed is retained for provenance.
    inner_radius = float(args['innerDiameterMm']) / 2
    sides = 128

    def mesh(name, verts, faces, smooth=False):
        obj = helpers['mesh'](name, verts, faces, plastic)
        if smooth:
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
        objects.append(obj)
        return obj

    def box(name, dimensions, location, bevel=.035):
        obj = helpers['box'](name, dimensions, location, plastic, bevel=bevel)
        objects.append(obj)
        return obj

    def radial_profile(name, profile, centre=(0, 0), resolution=sides):
        # A closed radial-Z cross section swept around its centre is an actual
        # capped annulus, not a hollow-looking opaque cylinder or a flat image.
        verts = [(centre[0] + radius*math.cos(i*math.tau/resolution),
                  centre[1] + radius*math.sin(i*math.tau/resolution), z)
                 for radius, z in profile for i in range(resolution)]
        faces = []
        for level in range(len(profile)):
            next_level = (level + 1) % len(profile)
            for i in range(resolution):
                j = (i + 1) % resolution
                faces.append((level*resolution+i, level*resolution+j,
                              next_level*resolution+j, next_level*resolution+i))
        return mesh(name, verts, faces, smooth=True)

    def cylinder(name, centre, profile, resolution=32):
        verts = [(centre[0]+radius*math.cos(i*math.tau/resolution),
                  centre[1]+radius*math.sin(i*math.tau/resolution), z)
                 for radius, z in profile for i in range(resolution)]
        faces = [tuple(reversed(range(resolution)))]
        for level in range(len(profile)-1):
            for i in range(resolution):
                j = (i+1) % resolution
                faces.append((level*resolution+i, level*resolution+j,
                              (level+1)*resolution+j, (level+1)*resolution+i))
        faces.append(tuple((len(profile)-1)*resolution+i for i in range(resolution)))
        return mesh(name, verts, faces, smooth=True)

    def sector(name, centre, start, end, radial_z, resolution=6):
        # Each merlon is its own closed rounded-profile sector. End caps keep
        # the gaps between merlons visible while all shells stay watertight.
        points = resolution+1
        verts = [(centre[0]+radius*math.cos(start+(end-start)*i/resolution),
                  centre[1]+radius*math.sin(start+(end-start)*i/resolution), z)
                 for radius, z in radial_z for i in range(points)]
        faces = []
        for level in range(len(radial_z)):
            following = (level+1) % len(radial_z)
            for i in range(resolution):
                faces.append((level*points+i, level*points+i+1,
                              following*points+i+1, following*points+i))
        faces.append(tuple(reversed([level*points for level in range(len(radial_z))])))
        faces.append(tuple(level*points+resolution for level in range(len(radial_z))))
        return mesh(name, verts, faces)

    # Continuous wall with an open city-sized centre. The shallow stepped foot
    # and slightly projecting upper moulding are distinguishable in both views.
    radial_profile('Wall_Stepped_Continuous_Annulus', [
        (inner_radius, 0), (11.1, 0), (11.35, .20), (11.35, .65),
        (11.18, .82), (11.10, 3.40), (11.22, 3.52), (11.22, 3.80),
        (10.96, 3.94), (inner_radius, 3.94)])
    radial_profile('Wall_Lower_Stone_Course', [
        (11.05, .90), (11.19, .90), (11.19, 1.04), (11.05, 1.04)])
    radial_profile('Wall_Upper_Stone_Course', [
        (11.05, 2.44), (11.19, 2.44), (11.19, 2.59), (11.05, 2.59)])

    # The photo's repeating wall teeth are separated by shallow square notches.
    # Their exact count is occluded, so 32 teeth are a visual approximation.
    for i in range(32):
        centre = i*math.tau/32
        half = .032
        sector('Wall_Merlon_'+str(i), (0, 0), centre-half, centre+half,
               [(inner_radius+.03, 3.70), (11.17, 3.70), (11.17, 4.65),
                (11.08, 4.78), (inner_radius+.16, 4.78),
                (inner_radius+.03, 4.65)], resolution=3)

    # Eight round projecting towers infer the continuation behind the insert.
    # Minimum radius of every tower stays >9.3mm, preserving the city clearance.
    for tower in range(8):
        angle = tower*math.tau/8
        centre = (10.7*math.cos(angle), 10.7*math.sin(angle))
        cylinder('Wall_Tower_Barrel_'+str(tower), centre,
                 [(1.16, .04), (1.30, .18), (1.30, .60),
                  (1.21, .80), (1.16, 3.47), (1.20, 3.65)])
        radial_profile('Wall_Tower_Parapet_'+str(tower),
                       [(.62, 3.45), (1.20, 3.45), (1.20, 4.08),
                        (.62, 4.08)], centre=centre, resolution=32)
        for tooth in range(4):
            tooth_angle = angle+tooth*math.tau/4
            sector('Wall_Tower_Merlon_'+str(tower)+'_'+str(tooth), centre,
                   tooth_angle-.30, tooth_angle+.30,
                   [(.62, 3.92), (1.20, 3.92), (1.20, 4.86),
                    (1.10, 5.0), (.72, 5.0), (.62, 4.86)], resolution=4)

    # Radial buttress strips produce the visible moulded vertical relief along
    # each intermediate wall panel. Boxes remain closed after being rotated.
    for i in range(16):
        angle = (i+.5)*math.tau/16
        radius = 11.20
        obj = box('Wall_Vertical_Buttress_'+str(i), (.28, .42, 2.87),
                  (radius*math.cos(angle), radius*math.sin(angle), 1.785))
        # Helpers bake translation into vertices; rotate the closed box about
        # its own XY centre without changing the bottom-centre root pivot.
        cx, cy = radius*math.cos(angle), radius*math.sin(angle)
        c, s = math.cos(angle), math.sin(angle)
        for vertex in obj.data.vertices:
            x, y = vertex.co.x-cx, vertex.co.y-cy
            vertex.co.x, vertex.co.y = cx+c*x-s*y, cy+s*x+c*y
        obj.data.update()

    # Exact estimated bounding dimensions in metre space, root at bottom centre.
    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    lower = [min(p[i] for p in points) for i in range(3)]
    upper = [max(p[i] for p in points) for i in range(3)]
    origin = [(lower[0]+upper[0])/2, (lower[1]+upper[1])/2, lower[2]]
    scale = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i])
             for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-origin[i])*scale[i] for i in range(3))
        obj.data.update()

    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'City_Wall_Moulded_Ring'
    obj.data.name = 'City_Wall_Moulded_Ring_Mesh'

    # Deterministic per-face UV atlas overrides helper Smart Project islands.
    uv = obj.data.uv_layers.active
    uv.name = 'DeterministicFaceAtlas'
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omitted = max(range(3), key=lambda i: abs(face.normal[i]))
        axes = [i for i in range(3) if i != omitted]
        coords = [obj.data.vertices[obj.data.loops[i].vertex_index].co
                  for i in face.loop_indices]
        lo = [min(p[a] for p in coords) for a in axes]
        hi = [max(p[a] for p in coords) for a in axes]
        for loop_index, p in zip(face.loop_indices, coords):
            projected = [(p[axis]-lo[j])/(hi[j]-lo[j])
                         for j, axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(
                ((face.index % tiles if j == 0 else face.index // tiles)
                 + .08+.84*projected[j])/tiles for j in range(2))
    return [obj]
