"""通过 Blender MCP 执行。独立场景制作原照厚板、显式FBX、图标和三视图。"""
import bpy
import bmesh
import hashlib
import json
import math
import shutil
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
RECIPE = Path(__file__).parent
OUT = Path(globals().get('OUTPUT_ROOT', ROOT / 'Unity/Catan/Assets/Resources/HarborFishing/v002'))
SOURCE = Path(globals().get('SOURCE_ROOT', ROOT / 'art/source/harbor-fishing-v002'))
PREVIEW = SOURCE / 'previews'
TEXTURES = Path(globals().get('TEXTURE_ROOT', ROOT / 'art/source/harbor-fishing-v002/textures'))
for folder in (OUT, SOURCE, PREVIEW):
    folder.mkdir(parents=True, exist_ok=True)
scene = bpy.data.scenes.new('捕鱼实体原照_v002')
bpy.context.window.scene = scene
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
scene.render.engine = 'CYCLES'
scene.cycles.samples = 12
scene.render.resolution_x = 256
scene.render.resolution_y = 256
scene.render.resolution_percentage = 100
scene.render.film_transparent = True
scene.view_settings.view_transform = 'Standard'
scene.world = bpy.data.worlds.new('原照中性柔光_v002')
scene.world.use_nodes = True
scene.world.node_tree.nodes['Background'].inputs[0].default_value = (1, 1, 1, 1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value = .8
bpy.ops.object.camera_add(location=(0, 0, .16))
camera = bpy.context.object
camera.data.type = 'ORTHO'
camera.data.clip_start = .0001
camera.data.clip_end = 10
scene.camera = camera
bpy.ops.object.light_add(type='AREA', location=(-.06, -.04, .15))
light = bpy.context.object
light.data.energy = .018
light.data.size = .15


def material(name, image=None):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = .75
    if image:
        texture = mat.node_tree.nodes.new('ShaderNodeTexImage')
        texture.image = bpy.data.images.load(str(image), check_existing=True)
        texture.image.pack()
        mat.node_tree.links.new(texture.outputs['Color'], bsdf.inputs['Base Color'])
    else:
        mat.diffuse_color = (.32, .25, .16, 1)
        bsdf.inputs['Base Color'].default_value = mat.diffuse_color
    return mat


def create(name):
    if name.startswith('ground-'):
        width, height, depth = .042, .030310889, .0015
        outline = [(0, 0), (.5, .4), (1, 0), (1, .6), (.5, 1), (0, .6)]
        top_faces = [(0, 5, 4, 1), (1, 4, 3, 2)]
    elif name == 'lake':
        width, height, depth = .068416007, .079, .008
        outline = [(.5, 0), (1, .25), (1, .75), (.5, 1), (0, .75), (0, .25)]
        top_faces = [(0, 5, 4, 3, 2, 1)]
    else:
        width, height, depth = .024, .024, .0015
        outline = [(0, 0), (1, 0), (1, 1), (0, 1)]
        top_faces = [(0, 3, 2, 1)]
    # 三个厚边环＋微倒角；底面中心为原点，印刷面独立材质。
    vertices, faces, slots = [], [], []
    rings = [(0, .994), (.00012, 1), (depth-.00012, 1), (depth, .994)]
    count = len(outline)
    for z, factor in rings:
        vertices.extend([((u-.5)*width*factor, (.5-v)*height*factor, z) for u, v in outline])
    for ring in range(3):
        for i in range(count):
            j = (i+1) % count
            faces.append((ring*count+i, ring*count+j, (ring+1)*count+j, (ring+1)*count+i))
            slots.append(1)
    for face in top_faces:
        faces.append(tuple(3*count+i for i in face)); slots.append(0)
        faces.append(tuple(reversed(face))); slots.append(1)
    mesh = bpy.data.meshes.new(name + '_原照厚板')
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    source = TEXTURES / (name+'-albedo.png')
    texture = OUT / source.name
    shutil.copyfile(source, texture)
    mesh.materials.append(material('Photo_'+name, texture))
    mesh.materials.append(material('PaperEdge_'+name))
    uv = mesh.uv_layers.new(name='原像素UV')
    for face, slot in zip(mesh.polygons, slots):
        face.material_index = slot
        for loop in face.loop_indices:
            p = mesh.vertices[mesh.loops[loop].vertex_index].co
            uv.data[loop].uv = (p.x/width+.5, p.y/height+.5) if slot == 0 else (0, 0)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    # 法线只在当前新增网格处理，不修改输入场景。
    edit = bmesh.new()
    edit.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(edit, faces=list(edit.faces))
    edit.to_mesh(mesh)
    edit.free()
    return obj, (width, height, depth)


def capture(obj, size, view, path):
    w, h, d = size
    extent = max(w, h)
    if view == 'top':
        camera.location = (0, 0, .2)
        target = Vector((0, 0, 0))
    elif view == 'side':
        camera.location = (0, -extent*2, extent*.14)
        target = Vector((0, 0, d*.5))
    else:
        camera.location = (extent*.65, -extent*.9, extent*1.6)
        target = Vector((0, 0, d*.4))
    camera.rotation_euler = (target-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.ortho_scale = extent*1.12
    scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)


records = []
references = json.loads((RECIPE/'参考清单.json').read_text())['照片']
for ref in references:
    name = ref['id']
    obj, size = create(name)
    bpy.context.view_layer.update()
    bpy.ops.export_scene.fbx(filepath=str(OUT/(name+'.fbx')), use_selection=True,
                             object_types={'MESH'}, apply_unit_scale=True,
                             axis_forward='-Z', axis_up='Y', bake_anim=False,
                             path_mode='RELATIVE', use_custom_props=False)
    capture(obj, size, 'top', OUT/(name+'.png'))
    shutil.copyfile(OUT/(name+'.png'), PREVIEW/(name+'-顶视.png'))
    capture(obj, size, 'side', PREVIEW/(name+'-侧视.png'))
    capture(obj, size, 'game', PREVIEW/(name+'-默认相机.png'))
    geometry = {'vertices': [list(v.co) for v in obj.data.vertices],
                'polygons': [list(p.vertices) for p in obj.data.polygons],
                'uv': [list(loop.uv) for loop in obj.data.uv_layers.active.data]}
    records.append({'id': name, '参考': ref['路径'], '原图SHA256': ref['SHA256'],
                    '尺寸米': size, '枢轴': '底面中心', '面数': len(obj.data.polygons),
                    '三角面数': sum(len(p.vertices)-2 for p in obj.data.polygons),
                    '几何UVSHA256': hashlib.sha256(json.dumps(geometry, sort_keys=True).encode()).hexdigest(),
                    'FBXSHA256': hashlib.sha256((OUT/(name+'.fbx')).read_bytes()).hexdigest(),
                    '贴图SHA256': hashlib.sha256((OUT/(name+'-albedo.png')).read_bytes()).hexdigest(),
                    '图标SHA256': hashlib.sha256((OUT/(name+'.png')).read_bytes()).hexdigest(),
                    '尺寸依据': '开发估算，非实体测量；湖泊匹配现有尖顶六边形与8毫米桌面'})
    obj.hide_render = True
    obj.hide_set(True)
bpy.data.libraries.write(str(SOURCE/'捕鱼实体原照.blend'), {scene})
(OUT/'资产清单.json').write_text(json.dumps({'Blender': bpy.app.version_string, '资产': records}, ensure_ascii=False, indent=2)+'\n')
result = {'资产数': len(records), '三角面数': sum(r['三角面数'] for r in records), '输出': str(OUT)}
