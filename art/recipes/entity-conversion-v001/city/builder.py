"""Closed photo-guided city sculpture, not a scan or an enlarged village.

Nominal millimetre construction is normalised into recipe bounding dimensions
in metres. X/Y footprint centre and bottom Z are always the root origin.
Visible reference features are a round stepped plinth, several gabled roofs,
a projecting colonnaded portico, wall relief and stairs. Unseen sides are
simple symmetric approximations. Joined components remain closed shells.
"""
import math

import bpy


def build(asset, helpers):
    objects = []
    plastic = helpers['mat'](asset['id'] + '_Moulded_Plastic',
                             tuple(asset['builderArgs']['color']), roughness=.32)

    def box(name, dimensions, location, bevel=.05):
        obj = helpers['box'](name, dimensions, location, plastic, bevel=bevel)
        objects.append(obj)
        return obj

    def mesh(name, vertices, faces, smooth=False):
        obj = helpers['mesh'](name, vertices, faces, plastic)
        if smooth:
            for face in obj.data.polygons:
                face.use_smooth = True
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=False):
        n = len(rings[0])
        vertices = [point for ring in rings for point in ring]
        faces = [tuple(reversed(range(n)))]
        for ring in range(len(rings)-1):
            for p in range(n):
                q = (p+1) % n
                faces.append((ring*n+p, ring*n+q, (ring+1)*n+q, (ring+1)*n+p))
        faces.append(tuple((len(rings)-1)*n+p for p in range(n)))
        return mesh(name, vertices, faces, smooth)

    def cylinder(name, x, y, rings, sides=64):
        return loft(name, [[(x+r*math.cos(p*math.tau/sides),
                              y+r*math.sin(p*math.tau/sides), z)
                             for p in range(sides)] for z, r in rings], smooth=True)

    # Two rounded circular steps match the visible moulded support, with a
    # shallow upper terrace under the clustered buildings.
    cylinder('City_Round_Lower_Plinth', 0, 0,
             [(0, 8.75), (.16, 9), (.65, 9), (.83, 8.83)])
    cylinder('City_Round_Upper_Terrace', 0, 0,
             [(.65, 8.52), (.84, 8.63), (1.30, 8.63), (1.46, 8.40)])

    def roof(name, x, y, width, depth, eave, ridge):
        # A thick pentagonal prism has a completely closed underside and two
        # gable caps; inset wall and projecting eaves produce natural shadows.
        points = [(-width/2, eave-.18), (width/2, eave-.18),
                  (width/2, eave+.18), (0, ridge), (-width/2, eave+.18)]
        loft(name, [[(x+a, y+side*depth/2, z) for a, z in points]
                    for side in (-1, 1)])
        box(name+'_Ridge', (.27, depth+.09, .20), (x, y, ridge+.02))
        for side in (-1, 1):
            box(name+'_Eave_'+str(side), (.23, depth+.05, .28),
                (x+side*width/2, y, eave+.07))
        # Roof-tile ridges are raised closed bars following each sloped plane.
        # Rotate in place and bake before combining so mesh origins stay root.
        half = width/2
        for side in (-1, 1):
            for i in range(1, 5):
                t = i/5
                xpos = x+side*half*t
                zpos = ridge+(eave+.18-ridge)*t+.05
                box(name+f'_Tile_Row_{side}_{i}', (.13, depth-.08, .11),
                    (xpos, y, zpos), bevel=.03)

    def facade(x, y, width, bottom, top, prefix):
        # Same-colour jambs, lintels and sill geometry preserve the sculpted
        # appearance without inventing photographed window paint or lettering.
        for col, px in enumerate((x-width*.24, x+width*.24)):
            z = bottom+(top-bottom)*.62
            for side in (-1, 1):
                box(prefix+f'_Window_{col}_Jamb_{side}', (.15, .18, 1.65),
                    (px+side*.47, y-.06, z))
                box(prefix+f'_Window_{col}_Lintel_{side}', (1.06, .20, .14),
                    (px, y-.07, z+side*.78))
            box(prefix+f'_Window_{col}_Mullion', (.11, .18, 1.58),
                (px, y-.08, z), bevel=.025)
        # Horizontal courses and staggered brick relief on the lower wall.
        for row in range(3):
            z = bottom+.35+row*.44
            for col in range(5):
                px = x-width*.42+col*width*.21+(row % 2)*width*.065
                if px < x+width*.46:
                    box(prefix+f'_Brick_{row}_{col}', (width*.18, .10, .28),
                        (px, y-.045, z), bevel=.025)

    def house(name, x, y, width, depth, wall_top, ridge):
        floor = 1.34
        box(name+'_Walls', (width, depth, wall_top-floor),
            (x, y, (wall_top+floor)/2), bevel=.10)
        roof(name+'_Gabled_Roof', x, y, width+.56, depth+.62, wall_top, ridge)
        facade(x, y-depth/2, width, floor, wall_top, name+'_Front_Relief')
        # Back is not visible in the source; deliberately simpler than front.
        box(name+'_Back_Stringcourse', (width+.04, .12, .17),
            (x, y+depth/2+.025, wall_top-1.6), bevel=.025)

    # Three distinguishable heights and footprints create the compact city
    # silhouette, rather than one scaled-up village house.
    house('City_Back_Left_Hall', -2.40, 2.25, 4.55, 5.80, 10.5, 15.7)
    house('City_Back_Right_Hall', 2.25, 2.65, 4.50, 5.60, 10.3, 14.9)
    house('City_Front_Right_Hall', 2.50, -2.55, 4.55, 5.40, 7.80, 12.5)

    # The lower front/left projection in the photograph is a colonnaded
    # covered entrance. Its roof, columns and three entrance steps are mesh.
    box('City_Portico_Floor', (6.2, 3.4, .40), (-2.80, -3.45, 1.55))
    box('City_Portico_Rear_Wall', (5.85, .48, 5.90), (-2.80, -1.90, 4.62))
    for i, x in enumerate((-5.35, -3.65, -1.95, -.25)):
        cylinder('City_Portico_Column_'+str(i), x, -4.70,
                 [(1.65, .39), (1.84, .43), (2.03, .30), (6.85, .26),
                  (7.00, .43), (7.22, .43)], sides=12)
    box('City_Portico_Header', (6.35, .72, .55), (-2.80, -4.70, 7.34))
    box('City_Portico_Roof_Slab', (6.65, 3.75, .48), (-2.80, -3.35, 7.69))
    box('City_Portico_Roof_Back_Ridge', (6.30, .24, .48), (-2.80, -1.57, 8.02))
    # Three visible shallow stairs sit on the terrace before the portico.
    for step, (depth, height) in enumerate(((1.85,.30), (1.30,.52), (.72,.74))):
        box('City_Portico_Step_'+str(step), (5.35, depth, height),
            (-2.80, -5.25-depth/2, 1.35+height/2), bevel=.07)

    # Right hall entry sits above a smaller staircase and has a deep-looking
    # sculpted frame. Geometry relief is approximate where the photo is dark.
    for side in (-1, 1):
        box('City_Right_Door_Jamb_'+str(side), (.22, .24, 2.9),
            (2.5+side*.64, -5.35, 3.13))
    box('City_Right_Door_Lintel', (1.55, .27, .23), (2.50, -5.35, 4.58))
    for step, (depth, height) in enumerate(((1.65,.28), (1.10,.51), (.55,.72))):
        box('City_Right_Hall_Step_'+str(step), (2.15, depth, height),
            (2.5, -5.33-depth/2, 1.35+height/2), bevel=.05)
    # Front-right gable exhibits raised horizontal wall accents.
    box('City_Right_Gable_Stringcourse', (4.70, .16, .23),
        (2.50, -5.31, 7.35), bevel=.04)
    for side in (-1, 1):
        box('City_Right_Corner_Pilaster_'+str(side), (.24, .20, 5.05),
            (2.50+side*2.15, -5.31, 4.13), bevel=.04)

    # Source metric units, exact estimated bounds and bottom-centre pivot.
    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    lower = [min(point[i] for point in points) for i in range(3)]
    upper = [max(point[i] for point in points) for i in range(3)]
    origin = [(lower[0]+upper[0])/2, (lower[1]+upper[1])/2, lower[2]]
    scale = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i])
             for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-origin[i])*scale[i] for i in range(3))
        obj.data.update()

    # One render mesh/material keeps the many moulded relief details from
    # generating a draw call per feature. Its disconnected shells stay closed.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'City_Moulded_Sculpt'
    obj.data.name = 'City_Moulded_Sculpt_Mesh'

    # Blender Smart Project/packing can differ in the seventh UV decimal even
    # when geometry is identical. Use a deterministic per-face projected atlas
    # for this untextured sculpt, with distinct nonoverlapping face islands.
    # Polygon order, metre-space vertices and arithmetic fix the layout.
    uv = obj.data.uv_layers.active
    uv.name = 'DeterministicFaceAtlas'
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omit = max(range(3), key=lambda i: abs(face.normal[i]))
        axes = [i for i in range(3) if i != omit]
        points = [obj.data.vertices[obj.data.loops[i].vertex_index].co
                  for i in face.loop_indices]
        lo = [min(p[a] for p in points) for a in axes]
        hi = [max(p[a] for p in points) for a in axes]
        for loop_index, point in zip(face.loop_indices, points):
            projected = [(point[axis]-lo[j])/(hi[j]-lo[j])
                         for j, axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(
                ((face.index % tiles if j == 0 else face.index // tiles)
                 + .08+.84*projected[j])/tiles for j in range(2))
    return [obj]
