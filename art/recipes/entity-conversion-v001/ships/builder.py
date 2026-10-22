"""Photo-guided ship token; closed parametric geometry, not a scanned replica.

Construction units are nominal millimetres, then this builder normalises its
geometry to a 9 x 26 x 28 mm bounding box in metres before returning objects.
X is transverse, Y is the route direction, Z is up.
The pipeline converts metres and Y-forward to Unity's Z-forward.
"""
import math
import bpy


def build(asset, helpers):
    objects = []
    make_mesh = helpers['mesh']
    make_box = helpers['box']
    color = tuple(asset['builderArgs']['color'])
    plastic = helpers['mat'](asset['id'] + '_Moulded_Plastic', color, roughness=.31)
    recess = helpers['mat'](asset['id'] + '_Window_Shadow', tuple(c*.34 for c in color), roughness=.65)

    def mesh(name, vertices, faces, material=plastic, smooth=False):
        obj = make_mesh(name, vertices, faces, material)
        if smooth:
            for face in obj.data.polygons:
                face.use_smooth = True
        objects.append(obj)
        return obj

    def box(name, dimensions, location, material=plastic):
        obj = make_box(name, dimensions, location, material)
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=True):
        """Cap a sequence of equal-sized cross-section rings."""
        n = len(rings[0])
        vertices = [point for ring in rings for point in ring]
        faces = [tuple(reversed(range(n)))]
        for i in range(len(rings)-1):
            for j in range(n):
                k = i*n+j
                q = i*n+(j+1)%n
                faces.append((k, q, q+n, k+n))
        faces.append(tuple((len(rings)-1)*n+j for j in range(n)))
        return mesh(name, vertices, faces, smooth=smooth)

    def tube(name, points, radius, sides=10):
        """Closed sweep along a path, with stable parallel-ish local frames."""
        rings = []
        for index, point in enumerate(points):
            p0, p1 = points[max(index-1, 0)], points[min(index+1, len(points)-1)]
            tangent = [p1[i]-p0[i] for i in range(3)]
            size = math.sqrt(sum(c*c for c in tangent))
            tangent = [c/size for c in tangent]
            reference = (1, 0, 0) if abs(tangent[0]) < .9 else (0, 1, 0)
            normal = [tangent[1]*reference[2]-tangent[2]*reference[1],
                      tangent[2]*reference[0]-tangent[0]*reference[2],
                      tangent[0]*reference[1]-tangent[1]*reference[0]]
            size = math.sqrt(sum(c*c for c in normal))
            normal = [c/size for c in normal]
            binormal = [tangent[1]*normal[2]-tangent[2]*normal[1],
                        tangent[2]*normal[0]-tangent[0]*normal[2],
                        tangent[0]*normal[1]-tangent[1]*normal[0]]
            rings.append([tuple(point[k]+radius*(math.cos(j*math.tau/sides)*normal[k]
                          + math.sin(j*math.tau/sides)*binormal[k]) for k in range(3))
                          for j in range(sides)])
        return loft(name, rings)

    # The ends rise continuously out of a rounded, concave-deck hull; it is
    # deliberately unlike a road token with an added mast.
    stations = [-13, -12.75, -12.25, -11.5, -10.5, -9, -7, -4, 0,
                4, 7, 9, 10.5, 11.5, 12.25, 12.75, 13]

    def section(y):
        ratio = abs(y)/13
        w = .20 + 4.30*max(0, 1-ratio*ratio)**.55
        lift = 5.5*ratio**7
        bottom = .1+lift
        top = 4.25+lift
        deck = 3.55+lift
        return w, bottom, top, deck

    rings = []
    for y in stations:
        w, bottom, top, deck = section(y)
        cross_section = [(-w, top), (-w*.98, top-.60), (-w*.83, bottom+.70),
                         (-w*.48, bottom+.15), (0, bottom), (w*.48, bottom+.15),
                         (w*.83, bottom+.70), (w*.98, top-.60), (w, top),
                         (w*.73, top-.18), (w*.64, deck), (0, deck),
                         (-w*.64, deck), (-w*.73, top-.18)]
        rings.append([(x, y, z) for x, z in cross_section])
    loft('Ship_Concave_Hull', rings)

    # Moulded strakes follow the upward-turning hull, rather than remaining
    # straight strips. They remain independent closed meshes for inspection.
    for side in (-1, 1):
        for level, fraction in enumerate((.29, .53, .80)):
            points = []
            for y in stations[1:-1]:
                w, bottom, top, _ = section(y)
                points.append((side*w*(.85+.10*fraction), y, bottom+(top-bottom)*fraction))
            tube(f'Ship_Strake_{side}_{level}', points, .18, sides=8)
        points = [(side*section(y)[0]*.90, y, section(y)[2]+.08) for y in stations]
        tube(f'Ship_Gunwale_{side}', points, .23)
    for side in (-1, 1):
        points = [(0, side*12.65, 8.55), (0, side*12.88, 9.50),
                  (0, side*12.85, 10.25)]
        tube(f'Ship_Upturned_Stem_{side}', points, .50, sides=12)

    # Stern cabin: narrow framed double-window relief on both broad sides.
    # The unseen second side is a symmetric working approximation.
    box('Ship_Stern_Cabin', (4.20, 5.20, 4.35), (0, -6.85, 6.12))
    box('Ship_Stern_Cabin_Roof', (4.65, 5.75, .55), (0, -6.85, 8.60))
    for side in (-1, 1):
        for window, y in enumerate((-7.95, -5.70)):
            box(f'Ship_Window_Recess_{side}_{window}', (.10, 1.48, 1.72),
                (side*2.12, y, 6.25), recess)
            for edge in (-1, 1):
                box(f'Ship_Window_Jamb_{side}_{window}_{edge}', (.24, .21, 2.05),
                    (side*2.24, y+edge*.80, 6.25))
                box(f'Ship_Window_Horizontal_{side}_{window}_{edge}', (.24, 1.75, .20),
                    (side*2.24, y, 6.25+edge*.93))
    # Visible front cabin doorway is relief geometry, not procedural text.
    box('Ship_Cabin_Door_Recess', (1.15, .10, 2.40), (0, -4.23, 5.78), recess)
    for side in (-1, 1):
        box(f'Ship_Cabin_Door_Jamb_{side}', (.20, .22, 2.65), (side*.67, -4.11, 5.78))
    box('Ship_Cabin_Door_Lintel', (1.52, .22, .23), (0, -4.11, 7.08))

    # A tall, single continuous sail skin: closed thickness at all four
    # boundaries, with a visibly bowed silhouette and rounded raised border.
    def sail_point(u, v, layer=0):
        leading = -.85+.35*v
        width = 5.50-3.10*v+2.90*math.sin(math.pi*v)
        y = leading+width*u
        z = 4.22+21.45*v
        billow = 1.85*math.sin(math.pi*u)*(.28+.72*math.sin(math.pi*v))
        return (billow+layer*.31, y, z)

    nu, nv = 12, 24
    vertices = [sail_point(i/nu, j/nv, layer)
                for layer in (-1, 1) for j in range(nv+1) for i in range(nu+1)]
    layer_stride = (nu+1)*(nv+1)
    faces = []
    for layer in range(2):
        offset = layer*layer_stride
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
        faces.append((p, q, q+layer_stride, p+layer_stride))
    mesh('Ship_Closed_Bowed_Sail', vertices, faces, smooth=True)
    tube('Ship_Sail_Outer_Lip', [sail_point(1, j/nv) for j in range(nv+1)], .32)
    tube('Ship_Sail_Bottom_Lip', [sail_point(i/nu, 0) for i in range(nu+1)], .28)
    tube('Ship_Sail_Top_Lip', [sail_point(i/nu, 1) for i in range(nu+1)], .30)
    tube('Ship_Mast', [(0, -.85+.35*j/nv, 3.50+23.65*j/nv) for j in range(nv+1)], .43, sides=12)

    # The moulded disc and round masthead account for the topmost reference
    # silhouette. Elliptical rings keep each piece closed and smooth.
    finial_rings = []
    for z, radius in [(26.90,.85), (27.10,1.14), (27.38,.98), (27.44,.54)]:
        finial_rings.append([(radius*math.cos(i*math.tau/16),
                              -.50+radius*math.sin(i*math.tau/16), z) for i in range(16)])
    loft('Ship_Masthead_Collar', finial_rings)
    finial_rings = []
    for z, radius in [(27.35,.40), (27.48,.54), (27.70,.53), (27.93,.36), (28.0,.10)]:
        finial_rings.append([(radius*math.cos(i*math.tau/16),
                              -.50+radius*math.sin(i*math.tau/16), z) for i in range(16)])
    loft('Ship_Masthead_Round_Top', finial_rings)
    # Bake metre-scale vertices, exact estimated footprint and bottom-centre
    # pivot. Helpers already bake each object's transform into its mesh data.
    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    minimum = [min(point[i] for point in points) for i in range(3)]
    maximum = [max(point[i] for point in points) for i in range(3)]
    offset = [(minimum[0]+maximum[0])/2, (minimum[1]+maximum[1])/2, minimum[2]]
    scale = [float(asset['dimensionsMm'][i])/1000/(maximum[i]-minimum[i]) for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-offset[i])*scale[i] for i in range(3))
        obj.data.update()
    # Preserve the closed components and UV islands, but combine equal-material
    # pieces into two render meshes to avoid one draw call per moulded detail.
    combined = []
    groups = [(name, [obj for obj in objects if obj.data.materials[0] == material])
              for material, name in ((plastic, 'Ship_Plastic_Sculpt'), (recess, 'Ship_Window_Recesses'))]
    for name, group in groups:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in group:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = group[0]
        bpy.ops.object.join()
        obj = bpy.context.object
        obj.name = name
        obj.data.name = name + '_Mesh'
        combined.append(obj)
    return combined
