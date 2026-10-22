"""Photo-guided black three-person robber sculpture, one asset / one material.

The two references show the same small group. A tall capped rear figure rests
his arms around two shorter companions: the left crosses his arms over a coat
with two buttons; the right wears a hood and carries a diagonal club. Hidden
backs, foot connections and details are conservative sculptural approximations.

Every part is a closed shell. Nominal millimetres are normalised to recipe
metres with a bottom-centre origin. Deterministic face UVs avoid Smart Project.
No textures, imported objects or cross-type recipes are required.
"""
import math

import bpy
from mathutils import Vector


def build(asset, helpers):
    objects = []
    args = asset['builderArgs']
    plastic = helpers['mat'](asset['id'] + '_Black_Moulded_Plastic',
                             tuple(args['color']), args['roughness'])

    def mesh(name, vertices, faces, smooth=True):
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
        for face in data.polygons:
            face.use_smooth = smooth
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=True):
        count = len(rings[0])
        faces = [tuple(reversed(range(count)))]
        for row in range(len(rings) - 1):
            for i in range(count):
                j = (i + 1) % count
                faces.append((row*count+i, row*count+j,
                              (row+1)*count+j, (row+1)*count+i))
        faces.append(tuple((len(rings)-1)*count+i for i in range(count)))
        return mesh(name, [v for ring in rings for v in ring], faces, smooth)

    def elliptical(name, cx, cy, rings, sides=28):
        return loft(name, [[(cx+rx*math.cos(i*math.tau/sides),
                              cy+ry*math.sin(i*math.tau/sides), z)
                             for i in range(sides)]
                            for z, rx, ry in rings])

    def ellipsoid(name, centre, radius, sides=20, stacks=10):
        # Truncated poles create small real caps rather than zero-area faces.
        rings = []
        for j in range(stacks + 1):
            angle = math.radians(5 + 170*j/stacks)
            z = centre[2] + radius[2]*math.cos(angle)
            rings.append([(centre[0]+radius[0]*math.sin(angle)*math.cos(i*math.tau/sides),
                           centre[1]+radius[1]*math.sin(angle)*math.sin(i*math.tau/sides), z)
                          for i in range(sides)])
        return loft(name, list(reversed(rings)))

    def tube(name, centres, radii, sides=12):
        rings = []
        for i, (centre, radius) in enumerate(zip(centres, radii)):
            if i == 0:
                direction = Vector(centres[1]) - Vector(centre)
            elif i == len(centres)-1:
                direction = Vector(centre) - Vector(centres[i-1])
            else:
                direction = Vector(centres[i+1]) - Vector(centres[i-1])
            direction.normalize()
            reference = Vector((0, 1, 0)) if abs(direction.y) < .9 else Vector((1, 0, 0))
            first = direction.cross(reference).normalized()
            second = direction.cross(first).normalized()
            point = Vector(centre)
            rings.append([tuple(point+radius*(first*math.cos(k*math.tau/sides)
                                               +second*math.sin(k*math.tau/sides)))
                          for k in range(sides)])
        return loft(name, rings)

    def prism(name, outline, y, thickness):
        count = len(outline)
        vertices = [(x, y+side*thickness/2, z) for side in (-1, 1)
                    for x, z in outline]
        faces = [tuple(reversed(range(count))), tuple(range(count, 2*count))]
        faces += [(i, (i+1) % count, (i+1) % count+count, i+count)
                  for i in range(count)]
        return mesh(name, vertices, faces, smooth=False)

    def face(prefix, cx, cy, cz, rx, rz):
        # Same-colour brow, nose, cheeks and moustache/lips are sculpt relief.
        # Fine expression cannot be recovered from the dark source photograph.
        ellipsoid(prefix+'_Face', (cx, cy, cz), (rx, .47, rz))
        ellipsoid(prefix+'_Long_Nose', (cx, cy-.45, cz+.04),
                  (rx*.19, .36, rz*.35), sides=12, stacks=6)
        for side in (-1, 1):
            ellipsoid(prefix+'_Brow_'+str(side),
                      (cx+side*rx*.43, cy-.35, cz+rz*.35),
                      (rx*.31, .19, rz*.11), sides=12, stacks=4)
            ellipsoid(prefix+'_Cheek_'+str(side),
                      (cx+side*rx*.50, cy-.31, cz-rz*.13),
                      (rx*.26, .20, rz*.28), sides=12, stacks=5)
            tube(prefix+'_Mouth_Edge_'+str(side),
                 [(cx,cy-.46,cz-rz*.42),
                  (cx+side*rx*.39,cy-.43,cz-rz*.33)], [.10,.09], sides=8)
        ellipsoid(prefix+'_Chin', (cx, cy-.31, cz-rz*.62),
                  (rx*.44, .24, rz*.21), sides=12, stacks=5)

    def boot(prefix, centre, radius):
        x, y, z = centre
        rx, ry, rz = radius
        # Flat bottom, broad toe and narrower ankle form actual boot support.
        elliptical(prefix, x, y,
                   [(z,rx*.75,ry*.78), (z+.15,rx,ry),
                    (z+rz*.55,rx,ry), (z+rz,rx*.72,ry*.67)], sides=20)
        tube(prefix+'_Toe_Ridge',
             [(x-rx*.68,y-ry*.72,z+.42), (x,y-ry*.90,z+.51),
              (x+rx*.68,y-ry*.72,z+.42)], [.10,.11,.10], sides=8)

    # Photographs show clustered feet, without a visible separate circular disc.
    # A shallow irregular underfoot connection approximates the hidden mould.
    outline = [(-5.8,-2.2),(-5.6,-3.2),(-4.2,-3.5),(-2.2,-3.4),
               (-.8,-3.1),(1.3,-3.5),(4.8,-3.4),(5.8,-2.5),
               (5.6,.8),(3.5,2.9),(.3,3.3),(-2.8,2.7),(-5.7,.4)]
    loft('Robber_Hidden_Thin_Underfoot_Connection',
         [[(x,y,z) for x,y in outline] for z in (0,.25)], smooth=False)

    # Tall rear companion: separated boots and legs support a long coat.
    for side in (-1, 1):
        x = side*1.18
        boot('Rear_Boot_'+str(side), (x,1.04,.10), (.85,1.40,1.05))
        tube('Rear_Leg_'+str(side),
             [(x,1.47,.75),(x,1.36,4.8),(side*.72,1.25,9.7)],
             [.62,.74,.84])
    elliptical('Rear_Long_Coat_Hem', 0,1.2,
               [(5.55,2.27,1.51),(6.15,2.55,1.67),
                (9.2,2.02,1.46),(11.5,1.90,1.37)])
    elliptical('Rear_Torso_Coat', 0,1.12,
               [(9.55,1.57,1.13),(12.0,1.65,1.18),
                (16.6,2.16,1.36),(18.0,2.49,1.33),
                (18.55,1.64,1.16),(19.1,1.05,.75)])
    tube('Rear_Left_Arm_Around_Companion',
         [(-1.96,.98,18.0),(-2.88,.45,17.0),(-3.69,-.04,15.45),
          (-4.43,-.61,14.0)], [.80,.91,.68,.57])
    tube('Rear_Right_Arm_Around_Companion',
         [(1.97,1.15,17.97),(2.91,.92,16.20),(3.48,.42,14.5),
          (4.05,-.15,12.04)], [.82,.88,.77,.58])
    ellipsoid('Rear_Left_Hand_On_Shoulder',(-4.45,-1.30,13.75),(.53,.61,.61))
    ellipsoid('Rear_Right_Hand_On_Shoulder',(4.27,-.82,11.61),(.54,.61,.58))
    for j in range(4):
        tube('Rear_Left_Hand_Finger_'+str(j),
             [(-4.84+j*.21,-1.55,14.03),(-4.70+j*.21,-1.80,13.39)],
             [.09,.08], sides=8)
    prism('Rear_Left_Coat_Lapel',
          [(-1.66,18.55),(-.61,18.65),(-.06,15.0),(-1.08,16.01),
           (-2.03,17.41)], -.19,.32)
    prism('Rear_Right_Coat_Lapel',
          [(1.54,18.57),(.50,18.65),(-.02,15.0),(1.08,16.3),
           (2.01,17.35)], -.19,.32)
    tube('Rear_Front_Coat_Seam', [(0,-.18,10.1),(0,-.19,14.8)], [.10,.11], sides=8)
    for j in range(3):
        ellipsoid('Rear_Coat_Button_'+str(j), (0,-.40,11.1+j*1.5),
                  (.19,.16,.20), sides=12, stacks=4)
    for j in range(5):
        x = (j-2)*.64
        tube('Rear_Back_Coat_Fold_'+str(j),
             [(x,2.74,6.4),(x*.80,2.52,10.5),(x*.85,2.40,16.1)],
             [.10,.12,.09], sides=8)
    elliptical('Rear_Neck', 0,1.10,
               [(18.5,.64,.62),(19.9,.65,.62)], sides=20)
    ellipsoid('Rear_Elongated_Head', (0,1.05,21.05), (1.13,.96,2.11))
    face('Rear',0,.25,20.86,.85,1.54)
    for side in (-1,1):
        ellipsoid('Rear_Ear_'+str(side), (side*1.09,1.05,21.05),
                  (.22,.36,.51), sides=12, stacks=6)
    # The tall figure's broad soft cap/beret is not a hood or pointed cone.
    elliptical('Rear_Cap_Rim', -.02,1.12,
               [(22.59,1.34,1.17),(22.87,1.61,1.34),
                (23.1,1.56,1.28)], sides=28)
    elliptical('Rear_Soft_Cap_Crown', -.02,1.13,
               [(22.9,1.48,1.25),(23.23,1.59,1.29),
                (23.90,1.21,1.04),(24.52,.50,.50),
                (24.78,.23,.22)], sides=28)
    for j in range(4):
        a = math.tau*j/4
        tube('Rear_Cap_Fold_'+str(j),
             [(.24*math.cos(a),1.13+.24*math.sin(a),24.65),
              (1.2*math.cos(a),1.13+1.02*math.sin(a),23.83)],
             [.075,.085], sides=8)

    # Stocky front-left companion: a round face, short coat and crossed arms.
    for side in (-1,1):
        x = -3.14+side*.94
        boot('Left_Boot_'+str(side),(x,-1.93,.04),(.81,1.44,1.22))
        tube('Left_Trouser_Leg_'+str(side),
             [(x,-1.42,.94),(x,-1.35,3.1),(x+side*.16,-1.35,5.9)],
             [.73,.94,1.01])
        for k in range(2):
            elliptical('Left_Trouser_Cuff_'+str(side)+'_'+str(k), x,-1.35,
                       [(1.8+k*.45,.85,.96),(2.04+k*.45,.89,1.02)], sides=20)
    elliptical('Left_Short_Coat', -3.16,-1.45,
               [(3.99,2.13,1.68),(4.65,2.45,1.84),
                (7.7,2.25,1.83),(10.75,2.25,1.71),
                (12.69,2.02,1.34),(13.21,1.36,1.14)])
    elliptical('Left_Raised_Belt', -3.16,-1.47,
               [(6.8,2.34,1.83),(7.14,2.34,1.84)], sides=28)
    prism('Left_Belt_Buckle',
          [(-3.72,6.87),(-2.99,6.87),(-2.99,7.26),(-3.72,7.26)], -3.39,.19)
    # Bright region in the photo is reflective black plastic, not added paint.
    prism('Left_Coat_Chest_Facing',
          [(-4.46,12.8),(-2.06,12.8),(-1.88,9.27),(-4.34,9.27)], -3.10,.20)
    for i,z in enumerate((11.12,12.20)):
        ellipsoid('Left_Visible_Button_'+str(i),(-2.98,-3.34,z),
                  (.29,.25,.30), sides=16, stacks=6)
    tube('Left_Outer_Upper_Arm',
         [(-4.98,-1.60,12.18),(-5.42,-1.98,10.48),(-4.91,-2.82,8.74)],
         [.80,.86,.75])
    tube('Left_Crossed_Lower_Arm',
         [(-4.91,-2.82,8.74),(-3.45,-3.47,8.97),(-1.60,-3.02,9.30)],
         [.75,.72,.57])
    tube('Left_Inner_Upper_Arm',
         [(-1.31,-1.62,12.07),(-.93,-2.11,10.25),(-1.26,-2.97,9.24)],
         [.78,.77,.66])
    tube('Left_Crossed_Upper_Arm',
         [(-1.26,-2.97,9.24),(-2.54,-3.68,9.95),(-4.0,-3.40,10.10)],
         [.64,.64,.52])
    ellipsoid('Left_Crossed_Hand',(-4.09,-3.5,10.16),(.60,.35,.44))
    for j in range(4):
        tube('Left_Hand_Finger_'+str(j),
             [(-4.5,-3.70,9.87+j*.18),(-3.74,-3.73,9.93+j*.18)],
             [.075,.085], sides=8)
    elliptical('Left_Neck',-3.12,-1.43,
               [(12.57,.58,.55),(14.0,.62,.60)], sides=20)
    ellipsoid('Left_Round_Head',(-3.12,-1.40,15.44),(1.55,1.35,2.13))
    face('Left',-3.10,-2.52,15.23,1.17,1.40)
    for side in (-1,1):
        ellipsoid('Left_Ear_'+str(side),(-3.12+side*1.46,-1.62,15.10),
                  (.29,.35,.48), sides=12, stacks=6)
    elliptical('Left_Short_Hair_Cap',-3.12,-1.37,
               [(16.25,1.32,1.18),(16.70,1.46,1.29),
                (17.12,1.30,1.15),(17.46,.91,.81),
                (17.64,.34,.32)], sides=24)
    for side in (-1,1):
        prism('Left_Collar_'+str(side),
              [(-3.10+side*.29,12.65),(-3.10+side*1.48,13.1),
               (-3.10+side*1.16,11.90)], -2.95,.25)

    # Shorter front-right companion with a deep hood, tunic and diagonal club.
    for side in (-1,1):
        x = 3.18+side*.79
        boot('Right_Boot_'+str(side),(x,-1.92,.04),(.74,1.29,1.08))
        tube('Right_Trouser_Leg_'+str(side),
             [(x,-1.47,.72),(x,-1.31,3.16),(x,-1.28,5.58)],
             [.65,.83,.80])
    elliptical('Right_Tunic',3.18,-1.43,
               [(3.05,1.73,1.49),(3.80,2.04,1.63),
                (6.6,1.88,1.60),(8.73,1.79,1.52),
                (10.49,1.49,1.24),(11.13,1.04,.93)])
    elliptical('Right_Belt',3.18,-1.46,
               [(4.72,1.99,1.68),(5.05,1.99,1.68)], sides=28)
    elliptical('Right_Hood_Back_And_Cowl',3.18,-1.17,
               [(9.41,1.47,1.17),(10.39,1.87,1.34),
                (12.56,1.83,1.52),(14.02,1.54,1.35),
                (14.80,.69,.77),(14.98,.26,.31)], sides=28)
    # Front face projects from the front of the closed hood. The curved rim
    # leaves an inset-looking opening through relief rather than an open mesh.
    face('Right',3.18,-2.62,12.15,1.02,1.13)
    rim = []
    for i in range(13):
        a = math.radians(-27+234*i/12)
        rim.append((3.18+1.42*math.cos(a),-2.64,12.44+1.66*math.sin(a)))
    tube('Right_Hood_Opening_Rim',rim,[.18]*len(rim),sides=10)
    for side in (-1,1):
        prism('Right_Hood_Lapel_'+str(side),
              [(3.18+side*.35,10.88),(3.18+side*1.69,11.42),
               (3.18+side*1.53,9.45),(3.18+side*.73,8.96)],-2.67,.28)
    tube('Right_Outer_Upper_Arm',
         [(4.62,-1.26,9.88),(5.03,-1.81,8.21),(4.36,-2.64,6.41)],
         [.68,.74,.60])
    tube('Right_Outer_Forearm_Club_Grip',
         [(4.36,-2.64,6.41),(4.09,-3.19,7.02),(4.08,-3.22,8.10)],
         [.59,.54,.42])
    tube('Right_Inner_Arm_Club_Grip',
         [(1.82,-1.70,9.75),(1.59,-2.54,8.60),(3.47,-3.17,7.21)],
         [.69,.67,.43])
    tube('Right_Diagonal_Heavy_Club',
         [(3.42,-3.34,6.60),(3.90,-3.17,7.82),(4.83,-2.98,9.75),
          (6.01,-2.72,12.16),(6.94,-2.53,14.12),(7.04,-2.49,14.40)],
         [.41,.46,.69,.89,.98,.59], sides=20)
    ellipsoid('Right_Club_Rounded_Tip',(6.91,-2.50,14.18),(.94,.94,.94))
    ellipsoid('Right_Club_Upper_Hand',(4.16,-3.69,8.16),(.56,.35,.49))
    ellipsoid('Right_Club_Lower_Hand',(3.47,-3.78,7.12),(.55,.33,.42))
    for j in range(4):
        tube('Right_Grip_Finger_'+str(j),
             [(3.78,-3.91,7.90+j*.18),(4.47,-3.94,8.03+j*.18)],
             [.077,.082],sides=8)
    for side in (-1,1):
        tube('Right_Tunic_Fold_'+str(side),
             [(3.18+side*1.08,-2.79,3.7),(3.18+side*.94,-2.96,4.54)],
             [.10,.13],sides=8)

    # Make exact estimated metric bounds. This preserves the deliberately
    # documented approximate front silhouette / depth while fixing root pivot.
    points = [vertex.co[:] for obj in objects for vertex in obj.data.vertices]
    lower = [min(point[i] for point in points) for i in range(3)]
    upper = [max(point[i] for point in points) for i in range(3)]
    origin = [(lower[0]+upper[0])/2,(lower[1]+upper[1])/2,lower[2]]
    scales = [float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i])
              for i in range(3)]
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
    obj.name = 'Robber_Three_Companions_Moulded_Sculpt'
    obj.data.name = obj.name+'_Mesh'
    uv = obj.data.uv_layers.active
    uv.name = 'DeterministicFaceAtlas'
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face_polygon in obj.data.polygons:
        omit = max(range(3),key=lambda i:abs(face_polygon.normal[i]))
        axes = [i for i in range(3) if i != omit]
        points = [obj.data.vertices[obj.data.loops[i].vertex_index].co
                  for i in face_polygon.loop_indices]
        lo = [min(p[a] for p in points) for a in axes]
        hi = [max(p[a] for p in points) for a in axes]
        for loop_index,point in zip(face_polygon.loop_indices,points):
            projected = [(point[axis]-lo[j])/max(hi[j]-lo[j],1e-12)
                         for j,axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(
                ((face_polygon.index%tiles if j == 0 else face_polygon.index//tiles)
                 +.08+.84*projected[j])/tiles for j in range(2))
    return [obj]
