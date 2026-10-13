"""Generate photo-guided actual meshes; frozen imagegen PNGs are input dependencies."""
import argparse
import hashlib
import json
import math
import random
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument('--output-root', type=Path, required=True)
parser.add_argument('--skip-previews', action='store_true')
args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:])
repo = Path(__file__).resolve().parents[2]
recipe_path = repo / 'art/recipes/photo-samples-v001/recipe.json'
recipe = json.loads(recipe_path.read_text(encoding='utf-8'))
out = args.output_root.resolve()
export = out / 'exports/photo-samples-v001'
source = out / 'source/photo-samples-v001'
previews = out / 'previews/photo-samples-v001'
if (export / 'manifest.json').exists():
    raise RuntimeError('Version exists; use a new output root rather than overwrite an asset.')
for directory in (export, source, previews, export / 'textures'):
    directory.mkdir(parents=True, exist_ok=True)
for name in ('wood-front.png', 'resource-back.png', 'forest-top.png'):
    shutil.copyfile(repo / 'art/source/photo-samples-v001/textures' / name, export / 'textures' / name)

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1

def mat(name, color, roughness=.5, texture=None):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    n = m.node_tree.nodes.get('Principled BSDF')
    n.inputs['Base Color'].default_value = (*color, 1)
    n.inputs['Roughness'].default_value = roughness
    if texture:
        image = bpy.data.images.load(str(export / 'textures' / texture))
        image.pack()
        tex = m.node_tree.nodes.new('ShaderNodeTexImage')
        tex.image = image
        m.node_tree.links.new(tex.outputs['Color'], n.inputs['Base Color'])
    return m

def finish(obj, material):
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(island_margin=.02)
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    obj.data.materials.append(material)
    return obj

def box(name, location, dimensions, material, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    o = bpy.context.object
    o.name = name
    o.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        mod = o.modifiers.new('Moulded edges', 'BEVEL')
        mod.width = min(bevel, min(dimensions)*.22)
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return finish(o, material)

def mesh(name, verts, faces, material):
    data = bpy.data.meshes.new(name + '_Mesh')
    data.from_pydata(verts, [], faces)
    data.update()
    o = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(o)
    return finish(o, material)

def make_forest():
    paper = mat('Photo_Forest_Edge', (.55, .40, .20), .85)
    face = mat('Photo_Forest_Top', (1, 1, 1), .8, 'forest-top.png')
    radius = .0396
    corners = [(radius * math.cos(math.radians(30+i*60)), radius * math.sin(math.radians(30+i*60))) for i in range(6)]
    verts = [(x, y, z) for z in (0, .008) for x, y in corners]
    faces = [tuple(reversed(range(6))), tuple(range(6, 12))]
    faces += [(i, (i+1)%6, (i+1)%6+6, i+6) for i in range(6)]
    o = mesh('Forest_Base', verts, faces, paper)
    o.data.materials.append(face)
    o.data.polygons[1].material_index = 1
    uv = o.data.uv_layers.active
    for li in o.data.polygons[1].loop_indices:
        p = o.data.vertices[o.data.loops[li].vertex_index].co
        uv.data[li].uv = (p.x/.08+.5, p.y/.08+.5)

def make_road():
    red = mat('Photo_Red_Plastic', (.62, .018, .025), .32)
    # A closed irregular oval plinth and raised cobbles, inspired by visible sculpting.
    rng = random.Random(recipe['seed'])
    n = 24
    outline = [(math.cos(i*math.tau/n)*.0025, math.sin(i*math.tau/n)*.013) for i in range(n)]
    verts = [(x, y, z) for z in (0, .0018) for x, y in outline]
    faces = [tuple(reversed(range(n))), tuple(range(n, n*2))]
    faces += [(i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n)]
    mesh('Road_Footing', verts, faces, red)
    for row in range(12):
        y = -.011 + row*.002
        for col in range(3):
            x = (col-1)*.00125 + .0002*math.sin(row*.8)
            height = rng.uniform(.00045, .00085)
            box(f'Road_Cobble_{row}_{col}', (x, y + rng.uniform(-.00012,.00012), .0018+height/2),
                (.00105, .0016, height), red, .00012)
    for side in (-1, 1):
        for row in range(7):
            y = -.010+row*.0033
            h = .0032 if row == 3 else rng.uniform(.0010,.0016)
            box(f'Road_Verge_{side}_{row}', (side*.0019, y, .0018+h/2), (.0009,.0018,h), red, .00018)

def gable(name, x, y, w, d, wall, ridge, red):
    # Closed roof cross-section, ridge along local Y; all vertices baked in metres.
    section = [(-w/2, wall), (w/2, wall), (0, ridge)]
    verts = [(x+xx, y+yy, z) for yy in (-d/2,d/2) for xx,z in section]
    roof = mesh(name+'_Roof', verts, [(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)], red)
    box(name+'_House', (x,y,(.003+wall)/2), (w*.92,d*.92,wall-.003), red, .0002)
    for side in (-1,1):
        box(name+f'_Eave_{side}', (x+side*w*.49,y,wall), (.00025,d,.00032), red, .00007)
    # Shallow relief roof ribs are geometry, not a painted placeholder.
    for side in (-1,1):
        for k in range(1,5):
            t=k/5
            box(name+f'_Tile_{side}_{k}', (x+side*w*.5*t,y,ridge+(wall-ridge)*t), (.00025,d*.98,.00018), red, .00004)
    for k in (-1,1):
        box(name+f'_DoorFrame_{k}', (x+k*.00055,y-d*.465,.0060), (.00022,.00023,.003), red, .00004)
    box(name+'_DoorLintel', (x,y-d*.465,.0075), (.0014,.00025,.0003), red, .00004)
    return roof

def make_village():
    red = mat('Photo_Red_Plastic', (.62, .018, .025), .32)
    box('Village_BottomTerrace', (0,0,.0006), (.012,.012,.0012), red, .001)
    box('Village_MiddleTerrace', (0,0,.0017), (.0115,.0113,.0010), red, .0009)
    box('Village_TopTerrace', (0,0,.0026), (.0108,.0105,.0008), red, .0008)
    gable('Village_West', -.0028,-.0007,.0043,.0060,.0105,.0138,red)
    gable('Village_East', .0024,.0010,.0047,.0062,.0118,.016,red)
    gable('Village_Rear', -.0016,.0033,.0030,.0034,.010,.0127,red)
    box('Village_Chimney', (-.0032,.0006,.0129), (.0014,.0014,.0048), red, .00015)
    box('Village_ChimneyCap', (-.0032,.0006,.0151), (.0018,.0018,.00045), red, .0001)
    # Front staircase and small moulded wall details visible in the photograph.
    for step in range(4):
        height=.0007*(step+1)
        box(f'Village_Stair_{step}', (.001,-.0051+step*.00055,height/2), (.0021,.0006,height), red,.00008)
    for x in (-.0032,.0024):
        for z in (.0045,.006,.008):
            box(f'Village_Masonry_{x}_{z}', (x,-.00375,z), (.0032,.00012,.00016), red,.00004)

def make_card():
    edge = mat('Photo_Card_Edge', (.83,.77,.60),.7)
    front = mat('Photo_Wood_Front', (1,1,1),.75,'wood-front.png')
    back = mat('Photo_Resource_Back', (1,1,1),.75,'resource-back.png')
    o = box('ResourceCard', (0,0,.00015), (.054,.080,.0003),edge)
    o.data.materials.append(front)
    o.data.materials.append(back)
    uv=o.data.uv_layers.active
    for p in o.data.polygons:
        if abs(p.normal.z)>.9:
            p.material_index=1 if p.normal.z>0 else 2
            for li in p.loop_indices:
                v=o.data.vertices[o.data.loops[li].vertex_index].co
                uv.data[li].uv=((v.x/.054+.5) if p.normal.z>0 else (.5-v.x/.054),v.y/.080+.5)

records=[]
builders={'forest':make_forest,'road':make_road,'village':make_village,'resource-card':make_card}
for asset,builder in builders.items():
    reset()
    builder()
    objects=list(bpy.context.scene.objects)
    bpy.ops.object.select_all(action='SELECT')
    points=[o.matrix_world@v.co for o in objects for v in o.data.vertices]
    minimum=[min(v[i] for v in points) for i in range(3)]
    maximum=[max(v[i] for v in points) for i in range(3)]
    triangles=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
    blend=source/(asset+'.blend')
    fbx=export/(asset+'.fbx')
    bpy.ops.wm.save_as_mainfile(filepath=str(blend))
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',use_triangles=True,bake_anim=False,
        path_mode='RELATIVE',embed_textures=False)
    materials=[]
    for m in bpy.data.materials:
        if not m.users: continue
        node=m.node_tree.nodes.get('Principled BSDF')
        textures=[n.image.name for n in m.node_tree.nodes if n.type=='TEX_IMAGE' and n.image]
        materials.append({'name':m.name,'baseColorLinearRgba':list(node.inputs['Base Color'].default_value),
                          'roughness':node.inputs['Roughness'].default_value,'texture':textures[0] if textures else None})
    geometry=[]
    for o in sorted(objects,key=lambda obj:obj.name):
        geometry.append({'name':o.name,'vertices':[[round(c,9) for c in o.matrix_world@v.co] for v in o.data.vertices],
                         'faces':[list(p.vertices) for p in o.data.polygons]})
    signature=hashlib.sha256(json.dumps(geometry,sort_keys=True).encode()).hexdigest()
    records.append({'id':asset,'meshObjects':len(objects),'triangles':triangles,'geometrySha256':signature,
        'blenderBounds':{'min':minimum,'max':maximum},'expectedUnityBounds':{'min':[minimum[0],minimum[2],-maximum[1]],'max':[maximum[0],maximum[2],-minimum[1]]},
        'materials':materials,'fbxSha256':sha(fbx),'blendSha256':sha(blend),
        'pivot':'bottom centre, root at origin; length axis Z in Unity for road'})

references=[{'id':'physical-'+key+'-001','originalPath':path,'sha256':sha(repo/path),
    'measuredDimensionsMm':None,'referenceRole':key,'unknowns':recipe['unknownFeatures']} for key,path in recipe['references'].items()]
manifest={'assetId':recipe['assetId'],'recipeSha256':sha(recipe_path),'scriptSha256':sha(Path(__file__)),
    'blenderVersion':bpy.app.version_string,'seed':recipe['seed'],'units':'metres','scaleStatus':recipe['scaleStatus'],
    'assets':records,'references':references,'textureDependencies':[{'path':'textures/'+p.name,'sha256':sha(p)} for p in sorted((export/'textures').glob('*.png'))],
    'limits':[recipe['imagePolicy'],recipe['modelPolicy'],*recipe['unknownFeatures']],
    'previewsGenerated':not args.skip_previews,'previewViews':['top','side','game-camera','crowded-vertex'] if not args.skip_previews else []}
(export/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')

if not args.skip_previews:
    reset()
    offsets={'forest':(0,0,0),'road':(.064,-.028,0),'village':(.064,.020,0),'resource-card':(-.077,0,0)}
    for asset in builders:
        with bpy.data.libraries.load(str(source/(asset+'.blend')),link=False) as (data_from,data_to):
            data_to.objects=data_from.objects
        for o in data_to.objects:
            if o:
                bpy.context.collection.objects.link(o)
                o.location=offsets[asset]
    # A second real card turned over shows its back in the same stage.
    original=bpy.data.objects.get('ResourceCard')
    flipped=original.copy();flipped.data=original.data.copy();bpy.context.collection.objects.link(flipped)
    flipped.name='ResourceCard_BackPreview';flipped.location=(-.137,0,.0003);flipped.rotation_euler[1]=math.pi
    table=mat('Preview_Table',(.025,.065,.075),.8)
    box('Preview_Table',(-.03,0,-.002),(.31,.14,.004),table,.003)
    scene=bpy.context.scene
    scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=12
    scene.render.resolution_x=1600;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='Standard'
    scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.28,.32,.36,1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
    for name,pos,power,size in [('Key',(-.13,-.12,.25),.08,.20),('Fill',(.16,.06,.20),.055,.16)]:
        bpy.ops.object.light_add(type='AREA',location=pos)
        lamp=bpy.context.object;lamp.name=name;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=size
        lamp.rotation_euler=(Vector((-.025,0,0))-lamp.location).to_track_quat('-Z','Y').to_euler()
    bpy.ops.object.camera_add()
    camera=bpy.context.object;scene.camera=camera;camera.data.type='ORTHO';camera.data.lens=50;camera.data.clip_start=.001
    views={'top':((-.03,0,.55),(-.03,0,0),.32),'side':((-.03,-.45,.055),(-.03,0,.006),.32),
           'game-camera':((-.03,-.30,.35),(-.03,0,.002),.32),
           'crowded-vertex':((.10,-.11,.12),(.038,-.018,.004),.12)}
    # A duplicate village and road near a forest corner check actual edge crowding.
    for asset,offset in [('village',(.0343,-.0198,.008)),('road',(.020,-.028,.008))]:
        for o in list(scene.objects):
            if o.name.startswith('Village_' if asset=='village' else 'Road_'):
                dup=o.copy();bpy.context.collection.objects.link(dup);dup.location=offset;dup.name='Crowded_'+o.name
    for name,(pos,target,scale) in views.items():
        camera.location=pos;camera.rotation_euler=(Vector(target)-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=scale
        scene.render.filepath=str(previews/(name+'.png'))
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/'preview-stage.blend'))
print('CATAN_PHOTO_SAMPLES_GENERATED '+str(export))
