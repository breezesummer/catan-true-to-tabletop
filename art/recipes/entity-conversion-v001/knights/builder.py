"""Photo-guided red knight sculpture: six level/helmet display variants.

The single-view references determine visible silhouette, not unseen anatomy.
Every component is a closed shell; one joined mesh and deterministic face UVs
preserve a reproducible low-draw-call sculpt. Construction uses nominal mm
then normalises to recipe metre bounds and bottom/footprint centre.
"""
import math

import bpy
from mathutils import Vector


def build(asset, helpers):
    objects = []
    args = asset['builderArgs']
    level = int(args['level'])
    helmet = bool(args['helmet'])
    red = helpers['mat'](asset['id'] + '_Red_Moulded_Plastic',
                         tuple(args['color']), roughness=.34)
    grey = helpers['mat'](asset['id'] + '_Grey_Green_Helmet',
                          (.23, .26, .19), roughness=.46) if helmet else None

    def mesh(name, vertices, faces, material=red, smooth=False):
        # Precreate UVs so the shared finisher never invokes Smart Project.
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
        for face in obj.data.polygons:
            face.use_smooth = smooth
        objects.append(obj)
        return obj

    def loft(name, rings, material=red, smooth=True):
        n = len(rings[0])
        faces = [tuple(reversed(range(n)))]
        for r in range(len(rings) - 1):
            for i in range(n):
                j = (i + 1) % n
                faces.append((r*n+i, r*n+j, (r+1)*n+j, (r+1)*n+i))
        faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
        return mesh(name, [point for ring in rings for point in ring],
                    faces, material, smooth)

    def elliptical(name, cx, cy, rings, material=red, sides=24):
        return loft(name, [[(cx+rx*math.cos(i*math.tau/sides),
                              cy+ry*math.sin(i*math.tau/sides), z)
                             for i in range(sides)]
                            for z, rx, ry in rings], material)

    def ellipsoid(name, centre, radius, material=red, sides=18, stacks=10):
        # Small planar end caps avoid duplicate pole vertices/zero triangles.
        rings = []
        for j in range(stacks+1):
            angle = math.radians(7 + 166*j/stacks)
            z = centre[2] + radius[2]*math.cos(angle)
            rings.append([(centre[0]+radius[0]*math.sin(angle)*math.cos(i*math.tau/sides),
                           centre[1]+radius[1]*math.sin(angle)*math.sin(i*math.tau/sides), z)
                          for i in range(sides)])
        return loft(name, list(reversed(rings)), material)

    def rod(name, start, end, radii, material=red, sides=12):
        axis = Vector(end) - Vector(start)
        direction = axis.normalized()
        first = direction.cross(Vector((0, 1, 0))).normalized()
        second = direction.cross(first).normalized()
        rings = []
        for t, radius in radii:
            middle = Vector(start) + axis*t
            rings.append([tuple(middle+radius*(first*math.cos(i*math.tau/sides)
                                               +second*math.sin(i*math.tau/sides)))
                          for i in range(sides)])
        return loft(name, rings, material)

    def prism(name, outline, y, thickness, material=red):
        n = len(outline)
        vertices = [(x, y+side*thickness/2, z) for side in (-1, 1)
                    for x, z in outline]
        faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
        faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
        return mesh(name, vertices, faces, material)

    def bar(name, location, size, material=red):
        x, y, z = location
        w, d, h = size
        vertices = [(x+sx*w/2, y+sy*d/2, z+sz*h/2)
                    for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        return mesh(name, vertices, [(0, 2, 3, 1), (4, 5, 7, 6),
                                     (0, 1, 5, 4), (2, 6, 7, 3),
                                     (0, 4, 6, 2), (1, 3, 7, 5)], material)

    # Rounded two-tier disc is the actual sculpted support in all six photos.
    elliptical('Knight_Lower_Round_Plinth', 0, 0,
               [(0, 5.8, 5.8), (.16, 6, 6), (.70, 6, 6), (.87, 5.86, 5.86)], sides=48)
    elliptical('Knight_Upper_Round_Terrace', 0, 0,
               [(.69, 5.66, 5.66), (.88, 5.77, 5.77),
                (1.28, 5.77, 5.77), (1.42, 5.60, 5.60)], sides=48)
    # Shallow paving segments visible around the boots, all closed geometry.
    for i in range(8):
        angle = i*math.tau/8
        rr = 4.1
        ellipsoid('Knight_Base_Stone_'+str(i),
                  (rr*math.cos(angle), rr*math.sin(angle), 1.42),
                  (1.13, .61, .14), sides=8, stacks=3)

    # Distinct legs, forward-pointing boots, joints and layered leg armour.
    # Negative Y is the photographed/front-facing side.
    for side, foot_x, hip_x in [(-1, -.15, .45), (1, 2.65, 1.95)]:
        prefix = 'Knight_Left' if side == -1 else 'Knight_Right'
        ellipsoid(prefix+'_Boot', (foot_x, -.78, 2.04), (.78, 1.56, .65))
        rod(prefix+'_Shin', (foot_x, -.13, 2.25), (foot_x+.10, .05, 7.6),
            [(0, .58), (.15, .76), (.82, .60), (1, .74)])
        ellipsoid(prefix+'_Knee', (foot_x+.10, -.08, 7.55), (.82, .71, .77))
        rod(prefix+'_Thigh', (foot_x+.10, .05, 7.7), (hip_x, 0, 11.7),
            [(0, .72), (.75, .95), (1, 1.00)])
        for j in range(3):
            ellipsoid(prefix+'_Shin_Armour_Ridge_'+str(j),
                      (foot_x+.06, -.66, 4.0+j*.84), (.62, .19, .20),
                      sides=12, stacks=4)
        for j in range(3):
            ellipsoid(prefix+'_Thigh_Armour_Ridge_'+str(j),
                      (hip_x-.10*side, -.80, 9.0+j*.68), (.75, .22, .21),
                      sides=12, stacks=4)
    elliptical('Knight_Hip', 1.2, 0,
               [(10.8, 1.42, 1.06), (11.2, 1.76, 1.20),
                (12.4, 1.71, 1.23), (12.9, 1.50, 1.05)])
    elliptical('Knight_Cuirass_Torso', 1.2, .10,
               [(12.0, 1.37, .95), (13.4, 1.43, 1.04),
                (16.25, 1.83, 1.20), (17.35, 1.66, 1.12),
                (18.0, 1.13, .88)])
    # Front armour seam, belt and a moulded fastening remain red like source.
    bar('Knight_Cuirass_Front_Seam', (1.2, -1.025, 15.3), (.23, .20, 4.9))
    elliptical('Knight_Waist_Belt', 1.2, 0,
               [(12.0, 1.75, 1.24), (12.35, 1.75, 1.24)], sides=24)
    bar('Knight_Belt_Clasp', (1.2, -1.32, 12.19), (.62, .24, .39))
    for j in range(5):
        xpos = 1.2+(j-2)*.52
        prism('Knight_Tunic_Armour_Strip_'+str(j),
              [(xpos-.21, 12.0), (xpos+.21, 12.0),
               (xpos+.32, 10.3), (xpos-.28, 10.35)], -1.06, .29)

    # Back cloak is a conservative symmetric approximation; folds are mesh.
    elliptical('Knight_Back_Cloak', 1.2, .82,
               [(9.65, 1.40, .34), (11.3, 1.66, .46),
                (16.8, 1.50, .45), (17.6, 1.12, .33)])
    for j in range(5):
        xpos = 1.2+(j-2)*.47
        rod('Knight_Cloak_Fold_'+str(j), (xpos, 1.28, 10.0),
            (1.2+(j-2)*.38, 1.28, 16.8), [(0,.14), (1,.12)], sides=8)

    # Flag arm extends out; shield arm bends forward. Hands visibly grip.
    for name, shoulder, elbow, wrist in [
        ('Flag', (-.30, .05, 17.0), (-1.23, -.24, 15.9), (-2.42, -.60, 15.7)),
        ('Shield', (2.71, .05, 17.0), (3.23, -.49, 15.2), (3.45, -1.20, 13.7))]:
        ellipsoid('Knight_'+name+'_Shoulder_Armour', shoulder, (.85,.88,1.05))
        rod('Knight_'+name+'_Upper_Arm', shoulder, elbow,
            [(0,.68), (.65,.56), (1,.58)])
        ellipsoid('Knight_'+name+'_Elbow', elbow, (.60,.60,.58))
        rod('Knight_'+name+'_Forearm', elbow, wrist,
            [(0,.57), (.7,.49), (1,.47)])
        ellipsoid('Knight_'+name+'_Hand', wrist, (.62,.56,.66))
        for j in range(3):
            ellipsoid('Knight_'+name+'_Finger_'+str(j),
                      (wrist[0], wrist[1]-.42, wrist[2]+(j-1)*.20),
                      (.48,.16,.115), sides=10, stacks=4)

    # Photo-visible kite shield, raised rim and an approximate flowing motif.
    shield = [(1.98,14.55), (3.45,15.0), (5.12,14.35),
              (4.75,11.5), (3.52,8.75), (2.20,11.55)]
    prism('Knight_Kite_Shield', shield, -1.57, .48)
    for j, (a, b) in enumerate(zip(shield, shield[1:]+shield[:1])):
        rod('Knight_Shield_Rim_'+str(j), (a[0],-1.89,a[1]),
            (b[0],-1.89,b[1]), [(0,.13),(1,.13)], sides=8)
    motif = [(3.1,13.8),(4.3,13.45),(3.28,12.4),(4.1,11.75),(3.65,10.25)]
    for j, (a, b) in enumerate(zip(motif, motif[1:])):
        rod('Knight_Shield_Relief_'+str(j), (a[0],-1.93,a[1]),
            (b[0],-1.93,b[1]), [(0,.12),(1,.12)], sides=8)

    rod('Knight_Long_Flag_Pole', (-1.75,-.58,2.2), (-2.85,-.58,23.8),
        [(0,.25),(1,.25)])
    ellipsoid('Knight_Flag_Pole_Tip', (-2.85,-.58,23.8), (.28,.28,.20))
    flags = {
        1: [(-2.85,23.63),(-5.74,20.57),(-2.69,21.02)],
        2: [(-2.85,23.63),(-5.75,23.40),(-4.72,22.10),
            (-5.72,20.61),(-2.69,20.80)],
        3: [(-2.85,23.63),(-5.72,23.52),(-4.95,22.80),
            (-5.73,22.02),(-4.96,21.33),(-5.72,20.60),(-2.69,20.78)]}
    prism('Knight_Level_'+str(level)+'_Flag', flags[level], -.58, .30)
    rod('Knight_Flag_Upper_Seam', (flags[level][0][0],-.77,flags[level][0][1]),
        (flags[level][1][0],-.77,flags[level][1][1]), [(0,.09),(1,.09)], sides=8)

    # Bare head includes brow, nose, ears and hair relief, not a peg cylinder.
    elliptical('Knight_Neck', 1.2, 0, [(17.8,.63,.65),(19.0,.64,.66)])
    ellipsoid('Knight_Head', (1.2,-.10,20.15), (1.04,.89,1.52))
    ellipsoid('Knight_Nose', (1.2,-1.00,20.16), (.24,.35,.48), sides=12, stacks=6)
    for side in (-1,1):
        ellipsoid('Knight_Ear_'+str(side), (1.2+side*1.0,-.02,20.12),
                  (.20,.32,.47), sides=12, stacks=6)
        ellipsoid('Knight_Brow_'+str(side), (1.2+side*.40,-.91,20.66),
                  (.38,.16,.13), sides=12, stacks=4)
    if not helmet:
        ellipsoid('Knight_Hair_Cap', (1.2,.0,21.0), (1.09,.96,.85))
        for j in range(5):
            rod('Knight_Hair_Ridge_'+str(j), (.40+j*.40,-.64,21.37),
                (.46+j*.37,.46,21.56), [(0,.10),(1,.10)], sides=8)
    else:
        # Grey-green cap, large cheek/neck wings, nose bar and slit jaw guard
        # retain the distinctive removable helmet silhouette in source photos.
        elliptical('Knight_Helmet_Dome', 1.2,.03,
                   [(20.75,1.60,1.21),(21.24,1.72,1.30),
                    (22.1,1.25,1.01),(22.72,.62,.54),(22.91,.20,.20)], grey)
        prism('Knight_Helmet_Left_Cheek',
              [(-.98,20.68),(.51,20.51),(.75,19.16),(1.01,18.23),
               (-.10,18.75),(-1.12,20.1)], -1.19,.42,grey)
        prism('Knight_Helmet_Right_Cheek',
              [(3.38,20.68),(1.89,20.51),(1.65,19.16),(1.39,18.23),
               (2.50,18.75),(3.52,20.1)], -1.19,.42,grey)
        bar('Knight_Helmet_Nose_Bar', (1.2,-1.39,20.10), (.27,.31,1.18),grey)
        for j in range(3):
            bar('Knight_Helmet_Jaw_Slit_Bar_'+str(j),
                (1.2,-1.40,18.53+j*.33), (.64,.30,.16),grey)
        elliptical('Knight_Helmet_Rear_Neck_Guard',1.2,.35,
                   [(18.33,1.18,.73),(19.05,1.96,1.14),
                    (20.02,1.72,1.17)],grey)
        rod('Knight_Helmet_Central_Crest', (1.2,-.55,21.65), (1.2,.43,22.88),
            [(0,.15),(1,.14)],grey,sides=8)

    # Disc stays the widest/deepest element, centring the support and bounds.
    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    lower = [min(p[i] for p in points) for i in range(3)]
    upper = [max(p[i] for p in points) for i in range(3)]
    origin = [(lower[0]+upper[0])/2,(lower[1]+upper[1])/2,lower[2]]
    scales = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i]) for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co = tuple((vertex.co[i]-origin[i])*scales[i] for i in range(3))
        obj.data.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = 'Knight_Level_'+str(level)+('_Helmet' if helmet else '_Bare')+'_Sculpt'
    obj.data.name = obj.name+'_Mesh'
    uv = obj.data.uv_layers.active
    uv.name = 'DeterministicFaceAtlas'
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omit = max(range(3),key=lambda i:abs(face.normal[i]))
        axes = [i for i in range(3) if i != omit]
        points = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        lo = [min(p[a] for p in points) for a in axes]
        hi = [max(p[a] for p in points) for a in axes]
        for loop_index, point in zip(face.loop_indices,points):
            projected = [(point[axis]-lo[j])/max(hi[j]-lo[j],1e-12) for j,axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(((face.index%tiles if j == 0 else face.index//tiles)
                                          +.08+.84*projected[j])/tiles for j in range(2))
    return [obj]
