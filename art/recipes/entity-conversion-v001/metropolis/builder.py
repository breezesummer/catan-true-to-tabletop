"""Photo-guided metropolis with twin spires and square crenellated towers.

Self-contained builder: disconnected closed moulded shells are joined as one
material/mesh. Construction is nominal millimetres, normalised to estimated
recipe dimensions in metres. Hidden structure is an explicit approximation.
No outer city-wall ring or unreferenced improvement-track marker is generated.
"""
import math

import bpy


def build(asset, helpers):
    objects = []
    plastic = helpers['mat'](asset['id'] + '_Moulded_Plastic',
                             tuple(asset['builderArgs']['color']), roughness=.32)

    def box(name, dimensions, location, bevel=.06):
        obj = helpers['box'](name, dimensions, location, plastic, bevel=bevel)
        objects.append(obj)
        return obj

    def mesh(name, vertices, faces, smooth=False):
        obj = helpers['mesh'](name, vertices, faces, plastic)
        if smooth:
            for polygon in obj.data.polygons:
                polygon.use_smooth = True
        objects.append(obj)
        return obj

    def loft(name, rings, smooth=False):
        count = len(rings[0])
        vertices = [point for ring in rings for point in ring]
        faces = [tuple(reversed(range(count)))]
        for ring in range(len(rings)-1):
            for index in range(count):
                following = (index+1) % count
                faces.append((ring*count+index, ring*count+following,
                              (ring+1)*count+following, (ring+1)*count+index))
        faces.append(tuple((len(rings)-1)*count+index for index in range(count)))
        return mesh(name, vertices, faces, smooth)

    def cylinder(name, x, y, rings, sides=64):
        return loft(name, [[(x+radius*math.cos(index*math.tau/sides),
                            y+radius*math.sin(index*math.tau/sides), z)
                           for index in range(sides)] for z, radius in rings], True)

    def rect_ring(x, y, width, depth, z):
        return [(x-width/2,y-depth/2,z), (x+width/2,y-depth/2,z),
                (x+width/2,y+depth/2,z), (x-width/2,y+depth/2,z)]

    # The photograph shows a circular low stepped plinth without external
    # wall on the body-only reference. The separate city-wall type nests it.
    cylinder('Metropolis_Round_Lower_Plinth', 0, 0,
             [(0,9.76), (.16,10), (.62,10), (.86,9.82)])
    cylinder('Metropolis_Round_Upper_Terrace', 0, 0,
             [(.62,9.46), (.83,9.64), (1.22,9.64), (1.43,9.42)])

    def pointed_frame(name, x, y, bottom, width, height, thickness=.18):
        # Five-point Gothic doorway/window ring with a closed threshold and
        # back; the frame casts a recess shadow against the solid main wall.
        outer = [(-width/2,0), (width/2,0), (width/2,height*.70),
                 (0,height), (-width/2,height*.70)]
        rim = min(.21, width*.17)
        inner = [(-width/2+rim,rim), (width/2-rim,rim),
                 (width/2-rim,height*.70-rim*.35), (0,height-rim*1.3),
                 (-width/2+rim,height*.70-rim*.35)]
        vertices = [(x+u, y+side*thickness, bottom+v)
                    for side in (0,1) for shape in (outer,inner) for u,v in shape]
        faces = []
        for index in range(5):
            next_index = (index+1) % 5
            faces.extend([(index,next_index,5+next_index,5+index),
                          (10+index,15+index,15+next_index,10+next_index),
                          (index,10+index,10+next_index,next_index),
                          (5+index,5+next_index,15+next_index,15+index)])
        mesh(name, vertices, faces)

    def brick_front(name, x, y, width, bottom, rows, row_height=.56):
        for row in range(rows):
            count = max(2,int(width/1.2))
            size = width/count
            for col in range(count):
                px = x-width/2+size*(col+.5)+(row%2)*size*.20
                if px+size*.42 < x+width/2+.04:
                    box(name+f'_{row}_{col}', (size*.84,.13,row_height*.70),
                        (px,y,bottom+row*row_height), bevel=.027)

    # A taller rear-left and a shorter front-left spire dominate the source
    # silhouette. These are steep faceted roofs, not city gabled roofs scaled.
    def spire(name, x, y, width, depth, wall_top, roof_top):
        bottom = 1.34
        box(name+'_Body', (width,depth,wall_top-bottom),
            (x,y,(wall_top+bottom)/2), bevel=.14)
        box(name+'_Roof_Collar', (width+.42,depth+.42,.48),
            (x,y,wall_top+.04), bevel=.09)
        loft(name+'_Steep_Point_Roof', [
            rect_ring(x,y,width+.46,depth+.46,wall_top-.16),
            rect_ring(x,y,width+.41,depth+.41,wall_top+.25),
            rect_ring(x,y,.30,.34,roof_top-.23),
            rect_ring(x,y,.16,.18,roof_top)])
        # Long buttresses follow the tall front face visible in the photo.
        for side in (-1,1):
            box(name+'_Front_Buttress_'+str(side), (.27,.33,wall_top-1.5),
                (x+side*(width/2-.18),y-depth/2-.06,(wall_top+1.5)/2), bevel=.07)
            box(name+'_Side_Buttress_'+str(side), (.25,.24,wall_top-2.0),
                (x+side*(width/2+.04),y+depth*.25,(wall_top+2.0)/2), bevel=.06)
        pointed_frame(name+'_Tall_Front_Window',x,y-depth/2-.20,
                      wall_top*.49,width*.43,wall_top*.31)
        box(name+'_Lower_Front_Course',(width+.07,.16,.22),
            (x,y-depth/2-.05,3.2),bevel=.035)

    spire('Metropolis_Back_Left_Spire',-4.6,3.05,2.90,3.05,17.5,24.0)
    spire('Metropolis_Front_Left_Spire',-4.05,-.80,2.95,3.25,14.0,21.0)

    # Joining hall behind the front village houses has its own steep gable.
    # Photo dark areas are represented as closed stepped stone masses.
    box('Metropolis_Central_Castle_Hall',(7.50,5.00,9.8),
        (.70,.65,6.24),bevel=.16)
    box('Metropolis_Back_Link_Gallery',(8.00,2.20,3.1),
        (-.10,3.70,12.8),bevel=.12)

    def gabled_roof(name,x,y,width,depth,eave,ridge):
        outline = [(-width/2,eave-.14), (width/2,eave-.14),
                   (width/2,eave+.14), (0,ridge), (-width/2,eave+.14)]
        loft(name, [[(x+u,y+sign*depth/2,z) for u,z in outline]
                    for sign in (-1,1)])
        box(name+'_Ridge',(.15,depth+.12,.16),(x,y,ridge+.015),bevel=.035)
        for sign in (-1,1):
            box(name+'_Eave_'+str(sign),(.17,depth+.04,.21),
                (x+sign*width/2,y,eave+.015),bevel=.035)
            for row in range(1,4):
                t=row/4
                box(name+f'_Raised_Tile_{sign}_{row}',(.10,depth-.08,.09),
                    (x+sign*width/2*t,y,ridge+(eave+.14-ridge)*t+.04),bevel=.022)

    gabled_roof('Metropolis_Lower_Right_Hall_Roof',2.45,-.90,5.75,4.65,8.75,12.05)
    for index,x in enumerate((-.50,2.1,4.05)):
        pointed_frame('Metropolis_Main_Hall_Window_'+str(index),x,-1.97,5.5,1.15,2.7)
    box('Metropolis_Hall_Front_Course',(7.45,.20,.32),(.7,-1.93,4.9),bevel=.045)

    def square_tower(name,x,y,width,top):
        bottom=1.34
        box(name+'_Body',(width,width,top-bottom),
            (x,y,(top+bottom)/2),bevel=.13)
        box(name+'_Crown_Collar',(width+.52,width+.52,.66),
            (x,y,top+.12),bevel=.09)
        outer=width+.55
        wall=.52
        # Four cap walls leave a visible open centre. Each rectangular shell
        # is closed; this avoids open roof planes and survives FBX reimport.
        for sign in (-1,1):
            box(name+'_Crown_FrontBack_'+str(sign),(outer,wall,1.02),
                (x,y+sign*(outer-wall)/2,top+.91),bevel=.09)
            box(name+'_Crown_Side_'+str(sign),(wall,outer-wall*2,1.02),
                (x+sign*(outer-wall)/2,y,top+.91),bevel=.09)
        for i,offset in enumerate((-outer*.34,0,outer*.34)):
            for sign in (-1,1):
                box(name+f'_Crenel_FrontBack_{i}_{sign}',(.70,.70,.75),
                    (x+offset,y+sign*(outer-wall)/2,top+1.70),bevel=.12)
                if i == 1:
                    box(name+f'_Crenel_Side_{sign}',(.70,.70,.75),
                        (x+sign*(outer-wall)/2,y,top+1.70),bevel=.12)
        for height in (4.5,8.3,12.1):
            box(name+'_Front_Stone_Course_'+str(height),(width+.06,.17,.20),
                (x,y-width/2-.045,height),bevel=.035)
        for side in (-1,1):
            box(name+'_Corner_Buttress_'+str(side),(.25,.22,top-1.8),
                (x+side*(width/2-.13),y-width/2-.065,(top+1.8)/2),bevel=.04)
        pointed_frame(name+'_Front_Lancet',x,y-width/2-.24,top-4.2,1.13,2.55)
        brick_front(name+'_Lower_Stone',x,y-width/2-.07,width-.40,2.15,4)

    square_tower('Metropolis_Left_Square_Tower',-.15,3.37,3.4,15.3)
    square_tower('Metropolis_Right_Square_Tower',4.00,3.37,3.4,15.1)

    # Visible tiny front-row gabled buildings produce the distinctive layered
    # skyline beneath the towers. Back/undersides are deliberately simple.
    for index,(x,y,width,depth,wall_top,ridge) in enumerate((
        (-2.75,-4.20,1.75,2.20,3.80,5.75),
        (-.55,-4.70,1.85,2.45,4.35,6.65),
        (1.80,-4.48,2.00,2.65,4.20,6.60),
        (4.18,-3.92,1.85,2.75,4.38,6.48))):
        name='Metropolis_Front_House_'+str(index)
        box(name+'_Walls',(width,depth,wall_top-1.34),
            (x,y,(wall_top+1.34)/2),bevel=.095)
        gabled_roof(name+'_Roof',x,y,width+.32,depth+.34,wall_top,ridge)
        pointed_frame(name+'_Door',x,y-depth/2-.18,1.53,width*.43,1.58,.14)
        box(name+'_Window_Sill',(.50,.17,.11),(x,y-depth/2-.08,3.12),bevel=.025)
        brick_front(name+'_Brick',x,y-depth/2-.065,width-.10,1.66,2,.47)
    for step,(depth,height) in enumerate(((1.70,.18),(1.15,.36),(.60,.55))):
        box('Metropolis_Front_Step_'+str(step),(3.10,depth,height),
            (.10,-6.05-depth/2,1.32+height/2),bevel=.055)

    # Low sculpted masonry areas visible around the front buildings are relief
    # on the metropolis plinth; these do not create an independent wall ring.
    for index,(x,y,angle) in enumerate(((-6.7,-3.8,-.5),(6.7,-2.7,.4),
                                       (-5.6,-6.05,-.35),(4.8,-6.0,.30))):
        obj=box('Metropolis_Low_Stone_'+str(index),(1.30,.58,.70),
                (x,y,1.63),bevel=.10)
        # Bake local rotation in already bottom-centred world geometry.
        c,s=math.cos(angle),math.sin(angle)
        for v in obj.data.vertices:
            px,py=v.co.x-x,v.co.y-y
            v.co.x=x+c*px-s*py
            v.co.y=y+s*px+c*py
        obj.data.update()

    points=[v.co[:] for obj in objects for v in obj.data.vertices]
    lower=[min(point[i] for point in points) for i in range(3)]
    upper=[max(point[i] for point in points) for i in range(3)]
    origin=[(lower[0]+upper[0])/2,(lower[1]+upper[1])/2,lower[2]]
    scale=[float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i]) for i in range(3)]
    for obj in objects:
        for vertex in obj.data.vertices:
            vertex.co=tuple((vertex.co[i]-origin[i])*scale[i] for i in range(3))
        obj.data.update()

    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name='Metropolis_Moulded_Sculpt'
    obj.data.name='Metropolis_Moulded_Sculpt_Mesh'
    # Deterministic per-face atlas: geometry and UV signatures do not depend
    # on Blender's Smart Project floating-point island packing choices.
    uv=obj.data.uv_layers.active
    uv.name='DeterministicFaceAtlas'
    tiles=math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omit=max(range(3),key=lambda i:abs(face.normal[i]))
        axes=[i for i in range(3) if i != omit]
        points=[obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        lo=[min(p[a] for p in points) for a in axes]
        hi=[max(p[a] for p in points) for a in axes]
        for loop_index,point in zip(face.loop_indices,points):
            projected=[(point[axis]-lo[j])/(hi[j]-lo[j]) for j,axis in enumerate(axes)]
            uv.data[loop_index].uv=tuple(
                ((face.index % tiles if j == 0 else face.index // tiles)+.08+.84*projected[j])/tiles
                for j in range(2))
    return [obj]
