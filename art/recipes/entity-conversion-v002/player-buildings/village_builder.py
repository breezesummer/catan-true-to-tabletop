"""Photo-guided grouped village sculpt in metre units, estimated dimensions.

Adapted from the preserved photo-samples-v001 geometry. The new photographs
show adjoining rounded footings, a tall chimney, grouped gables and relief.
Rear and underside are simple closed approximations, not measured/scan data.
"""
import math
import bpy


def build(asset, helpers):
    objects = []
    plastic = helpers['mat'](asset['id']+'_Moulded_Plastic',
                             tuple(asset['builderArgs']['color']), roughness=.32)

    def box(name, dimensions, location, bevel=0):
        obj = helpers['box'](name, dimensions, location, plastic, bevel)
        objects.append(obj)
        return obj

    def mesh(name, verts, faces):
        obj = helpers['mesh'](name, verts, faces, plastic)
        objects.append(obj)
        return obj

    def round_footing(name, x, y, radius, top):
        count = 48
        rings = [(0,radius*.91),(.10,radius), (top-.13,radius),(top,radius*.93)]
        verts = [(x+r*math.cos(i*math.tau/count), y+r*math.sin(i*math.tau/count), z)
                 for z,r in rings for i in range(count)]
        faces = [tuple(reversed(range(count)))]
        for ring in range(len(rings)-1):
            for i in range(count):
                j=(i+1)%count
                faces.append((ring*count+i,ring*count+j,(ring+1)*count+j,(ring+1)*count+i))
        faces.append(tuple((len(rings)-1)*count+i for i in range(count)))
        return mesh(name,verts,faces)

    def gable(name,x,y,width,depth,wall,ridge):
        section=[(-width/2,wall),(width/2,wall),(0,ridge)]
        verts=[(x+xx,y+yy,z) for yy in (-depth/2,depth/2) for xx,z in section]
        mesh(name+'_Roof',verts,[(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)])
        box(name+'_House',(width*.92,depth*.92,wall-2.5),(x,y,(2.5+wall)/2),.20)
        for side in (-1,1):
            box(name+f'_Eave_{side}',(.25,depth,.32),(x+side*width*.49,y,wall),.07)
            for k in range(1,5):
                t=k/5
                box(name+f'_Tile_{side}_{k}',(.25,depth*.98,.18),
                    (x+side*width*.5*t,y,ridge+(wall-ridge)*t),.04)
        for side in (-1,1):
            box(name+f'_DoorFrame_{side}',(.22,.23,3),(x+side*.55,y-depth*.465,6),.04)
        box(name+'_DoorLintel',(1.4,.25,.30),(x,y-depth*.465,7.5),.04)

    # Rounded joined footings retain the visible double-lobed moulded base.
    round_footing('Village_West_Round_Footing',-2.6,0,3.45,2.3)
    round_footing('Village_East_Round_Footing',2.45,.6,3.50,2.2)
    round_footing('Village_Rear_Round_Footing',-.60,2.5,3.2,2.45)
    gable('Village_West',-2.8,-.7,4.3,6,10.5,13.8)
    gable('Village_East',2.4,1,4.7,6.2,11.8,16)
    gable('Village_Rear',-1.6,3.3,3,3.4,10,12.7)
    box('Village_Chimney',(1.4,1.4,4.8),(-3.2,.6,12.9),.15)
    box('Village_ChimneyCap',(1.8,1.8,.45),(-3.2,.6,15.1),.1)
    for step in range(4):
        height=.7*(step+1)
        box(f'Village_Stair_{step}',(2.1,.6,height),(1,-5.1+step*.55,height/2),.08)
    for x in (-3.2,2.4):
        for z in (4.5,6,8):
            box(f'Village_Masonry_{x}_{z}',(3.2,.12,.16),(x,-3.75,z),.04)

    points=[v.co[:] for obj in objects for v in obj.data.vertices]
    lower=[min(p[i] for p in points) for i in range(3)]
    upper=[max(p[i] for p in points) for i in range(3)]
    origin=[(lower[0]+upper[0])/2,(lower[1]+upper[1])/2,lower[2]]
    scale=[float(asset['dimensionsMm'][i])/1000/(upper[i]-lower[i]) for i in range(3)]
    for obj in objects:
        for v in obj.data.vertices:
            v.co=tuple((v.co[i]-origin[i])*scale[i] for i in range(3))
        obj.data.update()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.object.join()
    obj=bpy.context.object
    obj.name='Village_Moulded_Sculpt'
    obj.data.name='Village_Moulded_Sculpt_Mesh'
    uv=obj.data.uv_layers.active
    uv.name='DeterministicFaceAtlas'
    tiles=math.ceil(math.sqrt(len(obj.data.polygons)))
    for face in obj.data.polygons:
        omit=max(range(3),key=lambda i:abs(face.normal[i]))
        axes=[i for i in range(3) if i != omit]
        coords=[obj.data.vertices[obj.data.loops[i].vertex_index].co for i in face.loop_indices]
        lo=[min(p[a] for p in coords) for a in axes]
        hi=[max(p[a] for p in coords) for a in axes]
        for index,p in zip(face.loop_indices,coords):
            projected=[(p[a]-lo[j])/max(hi[j]-lo[j],1e-12) for j,a in enumerate(axes)]
            uv.data[index].uv=tuple(((face.index%tiles if j==0 else face.index//tiles)+.08+.84*projected[j])/tiles for j in range(2))
    return [obj]
