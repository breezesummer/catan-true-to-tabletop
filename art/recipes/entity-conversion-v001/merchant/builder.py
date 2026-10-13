"""Photo-guided merchant and cargo cart, not a scan or a generic pawn.

All geometry is self-contained and deterministic. Nominal mm construction is
normalised to the estimated metre bounds; bottom and XY bounds centre become
the root. Every component is a closed shell, joined into one render mesh.
Negative Y faces forward. The unseen back, wheel spokes and axle are working
approximations; the original photograph supplies no measured dimensions.
"""
import math

import bpy
from mathutils import Vector


def build(asset, helpers):
    objects = []
    args = asset['builderArgs']
    plastic = helpers['mat'](asset['id'] + '_Warm_Neutral_Plastic',
                             tuple(args['color']), roughness=args['roughness'])

    def mesh(name, vertices, faces, smooth=True):
        # Precreate UVs to bypass Smart Project's packing variability.
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
        for face in obj.data.polygons:
            face.use_smooth = smooth
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=True):
        n = len(rings[0])
        faces = [tuple(reversed(range(n)))]
        for r in range(len(rings)-1):
            for i in range(n):
                j = (i+1) % n
                faces.append((r*n+i, r*n+j, (r+1)*n+j, (r+1)*n+i))
        faces.append(tuple((len(rings)-1)*n+i for i in range(n)))
        return mesh(name, [p for ring in rings for p in ring], faces, smooth)

    def elliptical(name, cx, cy, rings, sides=24):
        return loft(name, [[(cx+rx*math.cos(i*math.tau/sides),
                              cy+ry*math.sin(i*math.tau/sides), z)
                             for i in range(sides)] for z, rx, ry in rings])

    def ellipsoid(name, centre, radius, sides=18, stacks=10):
        # Tiny planar end caps avoid duplicate poles and zero-area triangles.
        rings = []
        for j in range(stacks+1):
            angle = math.radians(7+166*j/stacks)
            rings.append([(centre[0]+radius[0]*math.sin(angle)*math.cos(i*math.tau/sides),
                           centre[1]+radius[1]*math.sin(angle)*math.sin(i*math.tau/sides),
                           centre[2]+radius[2]*math.cos(angle)) for i in range(sides)])
        return loft(name, list(reversed(rings)))

    def rod(name, start, end, radii, sides=12):
        axis = Vector(end)-Vector(start)
        direction = axis.normalized()
        guide = Vector((0, 0, 1)) if abs(direction.z) < .9 else Vector((0, 1, 0))
        first = direction.cross(guide).normalized()
        second = direction.cross(first).normalized()
        rings = []
        for t, radius in radii:
            middle = Vector(start)+axis*t
            rings.append([tuple(middle+radius*(first*math.cos(i*math.tau/sides)
                                               +second*math.sin(i*math.tau/sides)))
                          for i in range(sides)])
        return loft(name, rings)

    def bar(name, location, size):
        x, y, z = location
        w, d, h = size
        vertices = [(x+sx*w/2, y+sy*d/2, z+sz*h/2)
                    for sz in (-1, 1) for sy in (-1, 1) for sx in (-1, 1)]
        return mesh(name, vertices, [(0, 2, 3, 1), (4, 5, 7, 6),
                                     (0, 1, 5, 4), (2, 6, 7, 3),
                                     (0, 4, 6, 2), (1, 3, 7, 5)], smooth=False)

    def wheel_rim(name, x, y, z, radius, thickness, sides=36, section=8):
        vertices = []
        for i in range(sides):
            theta = i*math.tau/sides
            for j in range(section):
                phi = j*math.tau/section
                rr = radius+thickness*math.cos(phi)
                vertices.append((x+thickness*math.sin(phi),
                                 y+rr*math.cos(theta), z+rr*math.sin(theta)))
        faces = []
        for i in range(sides):
            for j in range(section):
                faces.append((i*section+j, ((i+1)%sides)*section+j,
                              ((i+1)%sides)*section+(j+1)%section,
                              i*section+(j+1)%section))
        return mesh(name, vertices, faces)

    # The front figure has separate staggered boots and short trouser legs,
    # with the long coat descending almost to the knees as in the photo.
    for side in (-1, 1):
        prefix = 'Merchant_Left' if side < 0 else 'Merchant_Right'
        boot_y = -6.00 if side < 0 else -5.40
        # Boots and wheel rims both meet the table, rather than floating over
        # the wheel-defined support plane. The capped ellipsoid ends at 7 deg.
        ellipsoid(prefix+'_Rounded_Boot', (side*1.30, boot_y, .90*math.cos(math.radians(7))),
                  (1.08, 1.70, .90))
        elliptical(prefix+'_Boot_Cuff', side*1.30, -5.20,
                   [(1.7,.79,.81),(2.15,.89,.91),(3.0,.77,.77)], sides=18)
        rod(prefix+'_Trouser_Leg', (side*1.30,-5.20,2.1),
            (side*1.25,-4.75,8.7), [(0,.76),(.55,.84),(1,1.05)])
        for j in range(2):
            ellipsoid(prefix+'_Trouser_Fold_'+str(j),
                      (side*1.3,-5.96,4.5+j*1.1), (.64,.13,.15), sides=12, stacks=4)

    elliptical('Merchant_Long_Coat', 0, -4.8,
               [(7.15,2.65,1.85),(7.5,2.96,2.00),(10.0,3.08,2.24),
                (12.9,3.05,2.18),(15.4,2.83,1.86),(17.15,2.46,1.54),
                (17.8,1.45,1.13)])
    # The slightly bulging front apron and raised hem retain the photo's
    # rounded coat silhouette. Their relief stays the same plastic colour.
    ellipsoid('Merchant_Rounded_Front_Apron', (0,-6.43,11.7), (2.42,.82,3.35))
    rod('Merchant_Apron_Lower_Hem', (-2.23,-6.63,8.10), (2.23,-6.63,8.10),
        [(0,.20),(.5,.23),(1,.20)], sides=10)
    for side in (-1,1):
        rod('Merchant_Coat_Lapel_'+str(side), (side*.68,-6.18,17.0),
            (side*1.88,-6.71,12.4), [(0,.26),(.4,.28),(1,.20)], sides=10)
        rod('Merchant_Coat_Front_Fold_'+str(side), (side*1.53,-6.92,9.0),
            (side*.76,-7.0,12.7), [(0,.10),(1,.12)], sides=8)
    # An oblique moulded strap and small chest pouch are discernible in the
    # original, but the exact fastening mechanism is not established.
    rod('Merchant_Diagonal_Chest_Strap', (-1.98,-6.57,14.8), (1.60,-6.44,17.0),
        [(0,.23),(1,.23)], sides=10)
    ellipsoid('Merchant_Small_Chest_Pouch', (-.73,-7.09,12.63), (.67,.24,.87),
              sides=14, stacks=6)
    bar('Merchant_Pouch_Flap', (-.73,-7.28,13.03), (1.10,.15,.32))

    # Sleeves hang beside the long coat and bend just slightly toward the
    # towing shafts. Both hands physically overlap the handle ends.
    for side in (-1, 1):
        prefix = 'Merchant_Left' if side < 0 else 'Merchant_Right'
        shoulder = (side*2.37,-4.64,16.50)
        elbow = (side*3.09,-5.20,12.75)
        hand = (side*3.15,-5.41,8.13)
        ellipsoid(prefix+'_Shoulder', shoulder, (.99,.92,1.21))
        rod(prefix+'_Sleeve_Upper', shoulder, elbow,
            [(0,.90),(.55,.83),(1,.68)])
        ellipsoid(prefix+'_Elbow', elbow, (.72,.70,.77))
        rod(prefix+'_Sleeve_Lower', elbow, (side*3.20,-5.48,8.91),
            [(0,.69),(.7,.61),(1,.56)])
        ellipsoid(prefix+'_Sleeve_Cuff', (side*3.20,-5.48,8.95), (.67,.61,.32),
                  sides=14, stacks=5)
        ellipsoid(prefix+'_Hand', hand, (.57,.60,.62), sides=16, stacks=7)
        for j in range(3):
            ellipsoid(prefix+'_Finger_'+str(j),
                      (side*3.15,-5.91,8.17+(j-1)*.20), (.42,.19,.13),
                      sides=10, stacks=4)

    elliptical('Merchant_Neck', 0, -4.80, [(17.2,.91,.81),(19.1,.91,.86)])
    elliptical('Merchant_Raised_Collar', 0, -4.76,
               [(17.05,1.27,1.18),(17.50,1.43,1.27),(18.1,1.36,1.15)], sides=24)
    ellipsoid('Merchant_Long_Head', (0,-4.88,20.64), (1.45,1.20,2.11))
    ellipsoid('Merchant_Long_Chin', (0,-5.72,19.45), (.91,.62,.78))
    ellipsoid('Merchant_Nose', (0,-6.09,20.84), (.35,.59,.51), sides=14, stacks=7)
    for side in (-1,1):
        ellipsoid('Merchant_Ear_'+str(side), (side*1.39,-4.89,20.60),
                  (.22,.33,.47), sides=12, stacks=6)
        rod('Merchant_Brow_'+str(side), (side*.18,-6.02,21.44),
            (side*.85,-5.93,21.38), [(0,.10),(1,.11)], sides=8)
    rod('Merchant_Mouth_Relief', (-.35,-6.21,19.99), (.35,-6.21,19.99),
        [(0,.07),(1,.07)], sides=8)
    # A rounded soft cap/cloth wrap with a broad lower band, not a brimmed
    # invented hat. Parallel raised seams approximate the visible wrap folds.
    elliptical('Merchant_Soft_Cap', -.12, -4.66,
               [(21.68,1.53,1.34),(22.20,1.69,1.43),(22.9,1.56,1.33),
                (23.72,1.31,1.16),(24.30,.78,.79),(24.49,.25,.30)], sides=28)
    for j in range(2):
        elliptical('Merchant_Cap_Wrap_Band_'+str(j), -.12,-4.66,
                   [(21.68+j*.42,1.65,1.44),(21.82+j*.42,1.65,1.44)], sides=28)
    for j in range(3):
        rod('Merchant_Cap_Crown_Fold_'+str(j), (-.96+j*.68,-5.61,23.0),
            (-.64+j*.52,-4.17,24.07), [(0,.08),(1,.07)], sides=8)

    # Two-wheel wooden handcart behind the merchant. Thin planks, side rails
    # and long shafts carry the cargo and meet the merchant's hands.
    for j in range(5):
        bar('Merchant_Cart_Bed_Plank_'+str(j), ((j-2)*1.65,4.1,6.65), (1.55,7.40,.65))
    for side in (-1,1):
        rod('Merchant_Cart_Towing_Shaft_'+str(side), (side*3.15,-5.58,7.95),
            (side*4.20,7.85,7.85), [(0,.33),(.55,.40),(1,.40)])
        for layer in range(2):
            bar('Merchant_Cart_Side_Rail_'+str(side)+'_'+str(layer),
                (side*4.45,4.25,7.90+layer*.88), (.64,7.82,.67))
        for j in range(3):
            bar('Merchant_Cart_Rail_Post_'+str(side)+'_'+str(j),
                (side*4.45,1.15+j*3.05,8.16), (.76,.60,2.26))
    for layer in range(2):
        bar('Merchant_Cart_Back_Rail_'+str(layer), (0,7.85,7.90+layer*.88), (8.72,.67,.67))
    rod('Merchant_Cart_Axle', (-5.90,4.2,3.60), (5.90,4.2,3.60),
        [(0,.43),(1,.43)], sides=14)
    for side in (-1,1):
        cx = side*5.05
        prefix = 'Merchant_Cart_Wheel_'+str(side)
        wheel_rim(prefix+'_Thick_Rim', cx,4.2,3.6,3.13,.47)
        # The photographed near wheel is partly obscured and shaded. These
        # low-relief radial spokes and hub are conservative structural detail.
        for j in range(8):
            angle = j*math.tau/8
            rod(prefix+'_Spoke_'+str(j), (cx,4.2,3.6),
                (cx,4.2+3.15*math.cos(angle),3.6+3.15*math.sin(angle)),
                [(0,.31),(1,.23)], sides=8)
        rod(prefix+'_Hub', (cx-.70,4.2,3.6), (cx+.70,4.2,3.6),
            [(0,.71),(.25,.84),(.75,.84),(1,.71)], sides=18)

    # Photo-visible cargo: a tall hooped barrel/sack form, a bulging sack and
    # parallel rolls above the bed. Unknown backs remain plain neutral plastic.
    elliptical('Merchant_Cart_Barrel', 1.60,3.18,
               [(7.0,1.72,1.66),(7.4,1.89,1.83),(9.3,2.15,2.03),
                (12.5,2.16,2.00),(14.55,1.90,1.80),(14.78,1.69,1.60)], sides=24)
    for j,(z,r) in enumerate(((7.9,1.97),(10.5,2.21),(13.8,2.02))):
        elliptical('Merchant_Cart_Barrel_Hoop_'+str(j),1.60,3.18,
                   [(z,r,r*.94),(z+.25,r,r*.94)], sides=24)
    for j in range(4):
        rod('Merchant_Cart_Barrel_Stave_'+str(j),
            (1.60+(j-1.5)*.67,1.37,8.4), (1.60+(j-1.5)*.64,1.35,13.5),
            [(0,.06),(1,.06)], sides=6)
    ellipsoid('Merchant_Cart_Bulging_Cargo_Sack', (-2.06,5.25,10.53),
              (2.10,2.21,3.44), sides=20, stacks=12)
    elliptical('Merchant_Cart_Sack_Neck',-2.06,5.25,
               [(13.0,.79,.65),(13.6,.66,.58),(14.0,.37,.33)], sides=18)
    ellipsoid('Merchant_Cart_Sack_Tie',(-2.06,5.25,13.56),(.81,.71,.22),
              sides=12, stacks=5)
    for j in range(3):
        rod('Merchant_Cart_Sack_Fold_'+str(j),(-3.50+j*.82,3.47,8.4),
            (-3.20+j*.59,3.40,12.7),[(0,.10),(1,.09)],sides=8)
    for j in range(3):
        yy = 5.50+j*.93
        zz = 14.10+(.45 if j == 1 else 0)
        rod('Merchant_Cart_Rolled_Goods_'+str(j),(-3.53,yy,zz),(3.05,yy,zz),
            [(0,.68),(.10,.80),(.90,.80),(1,.68)],sides=16)
        for side in (-1,1):
            rod('Merchant_Cart_Roll_End_'+str(j)+'_'+str(side),
                ((-3.63 if side<0 else 3.05),yy,zz),
                ((-3.53 if side<0 else 3.15),yy,zz),
                [(0,.45),(1,.45)],sides=14)

    # Exact estimated dimensions and bottom-centre pivot in metre source units.
    points = [v.co[:] for obj in objects for v in obj.data.vertices]
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
    obj.name = 'Merchant_Neutral_Figure_And_Cargo_Cart'
    obj.data.name = obj.name+'_Mesh'
    uv = obj.data.uv_layers.active
    tiles = math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omit = max(range(3),key=lambda i:abs(face.normal[i]))
        axes = [i for i in range(3) if i != omit]
        points = [obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        lo = [min(p[a] for p in points) for a in axes]
        hi = [max(p[a] for p in points) for a in axes]
        for loop_index,point in zip(face.loop_indices,points):
            projected = [(point[axis]-lo[j])/max(hi[j]-lo[j],1e-12) for j,axis in enumerate(axes)]
            uv.data[loop_index].uv = tuple(((face.index%tiles if j==0 else face.index//tiles)
                                          +.08+.84*projected[j])/tiles for j in range(2))
    return [obj]
