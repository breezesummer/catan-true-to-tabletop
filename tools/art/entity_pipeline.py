"""Blender batch conversion of registered physical references into versioned meshes.

Run with Blender --background --python-exit-code 1 --python this.py --
--recipe art/recipes/entity-conversion-v001/TYPE/recipe.json [--output-root art].
Custom builders expose build(asset, helpers), return mesh objects (or None), and
use metres. helpers is a dict: reset, mat, box, mesh, finish, repo, seed.
"""
import argparse
import hashlib
import importlib.util
import json
import math
import re
import shutil
import sys
from pathlib import Path

import bpy
from mathutils import Vector

VERSION = 'entity-conversion-v001'
REPO = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def repo_path(value):
    path = Path(value)
    if not path.is_absolute():
        path = REPO / path
    path = path.resolve()
    if not path.is_relative_to(REPO):
        raise ValueError('Reference, recipe and builder paths must be inside repository')
    return path


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = 'METRIC'
    bpy.context.scene.unit_settings.scale_length = 1


def geometry_signature(objects):
    geometry = []
    for obj in sorted(objects, key=lambda item: item.name):
        uv = obj.data.uv_layers.active
        geometry.append({
            'name': obj.name,
            'vertices': [[round(float(c), 9) for c in obj.matrix_world @ v.co] for v in obj.data.vertices],
            'faces': [list(p.vertices) for p in obj.data.polygons],
            'materialIndices': [p.material_index for p in obj.data.polygons],
            'uv': [[round(float(c), 7) for c in loop.uv] for loop in uv.data] if uv else None,
        })
    return hashlib.sha256(json.dumps(geometry, sort_keys=True).encode('utf-8')).hexdigest()


def material_records(objects):
    materials = {m.name: m for obj in objects for m in obj.data.materials if m}
    records = []
    for name, material in sorted(materials.items()):
        shader = material.node_tree.nodes.get('Principled BSDF')
        textures = [node.image for node in material.node_tree.nodes if node.type == 'TEX_IMAGE' and node.image]
        records.append({
            'name': name,
            'baseColorLinearRgba': [float(v) for v in shader.inputs['Base Color'].default_value],
            'roughness': float(shader.inputs['Roughness'].default_value),
            'texture': Path(textures[0].filepath).name if textures else None,
        })
    return records


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, default=REPO / 'art')
    parser.add_argument('--skip-previews', action='store_true')
    cli = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    args = parser.parse_args(cli)
    recipe_path = repo_path(args.recipe)
    recipe = json.loads(recipe_path.read_text(encoding='utf-8-sig'))
    if recipe.get('schemaVersion') != 1:
        raise ValueError('Recipe schemaVersion must be 1')
    type_id = recipe['typeId']
    if not re.fullmatch(r'[a-z0-9][a-z0-9-]*', type_id):
        raise ValueError('typeId must be a stable lowercase path identifier')
    assets = recipe['assets']
    if not assets or len({a['id'] for a in assets}) != len(assets):
        raise ValueError('Assets must be nonempty and have unique ids')
    for asset in assets:
        if not re.fullmatch(r'[a-z0-9][a-z0-9-]*', asset['id']):
            raise ValueError('Asset id must be a stable lowercase path identifier')
        if asset['kind'] not in ('card', 'panel', 'tile', 'custom'):
            raise ValueError('Unknown asset kind')
        if len(asset['dimensionsMm']) != 3 or any(float(n) <= 0 for n in asset['dimensionsMm']):
            raise ValueError('dimensionsMm must contain three positive estimated/measured dimensions')
    root = args.output_root.resolve()
    export = root / 'exports' / VERSION / type_id
    source = root / 'source' / VERSION / type_id
    preview = root / 'previews' / VERSION / type_id
    if (export / 'manifest.json').exists():
        raise RuntimeError('Version manifest already exists; use a new --output-root for rebuilding')
    # Refuse partially generated asset files as well; retry into a clean directory.
    if any(directory.exists() and any(directory.iterdir()) for directory in (export, preview)) or (source.exists() and any(source.glob('*.blend'))):
        raise RuntimeError('Output type directory is not empty; use a clean --output-root')
    references = []
    for reference in recipe.get('references', []):
        original = repo_path(reference['originalPath'])
        digest = sha(original)
        if reference.get('sha256') and reference['sha256'].lower() != digest:
            raise ValueError('Original reference hash differs: ' + reference['originalPath'])
        references.append({**reference, 'sha256': digest})
    for directory in (export, source, preview, export / 'textures'):
        directory.mkdir(parents=True, exist_ok=True)
    texture_map = {}
    builder_records = {}

    def copy_texture(value):
        original = repo_path(value)
        key = str(original)
        if key in texture_map:
            return texture_map[key]
        if original.suffix.lower() != '.png' or original.read_bytes()[:8] != b'\x89PNG\r\n\x1a\n':
            raise ValueError('Corrected textures must be actual PNG files: ' + str(original))
        target = export / 'textures' / original.name
        if target.exists() and sha(target) != sha(original):
            target = target.with_name(original.stem + '-' + sha(original)[:10] + '.png')
        if not target.exists():
            shutil.copyfile(original, target)
        texture_map[key] = target
        return target

    def mat(name, color, roughness=.7, texture=None):
        rgba = tuple(float(v) for v in color)
        if len(rgba) == 3:
            rgba += (1.0,)
        if len(rgba) != 4:
            raise ValueError('mat color must have three or four components')
        material = bpy.data.materials.new(name)
        material.diffuse_color = rgba
        material.use_nodes = True
        shader = material.node_tree.nodes.get('Principled BSDF')
        shader.inputs['Base Color'].default_value = rgba
        shader.inputs['Roughness'].default_value = roughness
        if texture:
            image = bpy.data.images.load(str(copy_texture(texture)), check_existing=True)
            image.pack()
            node = material.node_tree.nodes.new('ShaderNodeTexImage')
            node.image = image
            material.node_tree.links.new(node.outputs['Color'], shader.inputs['Base Color'])
        return material

    def finish(obj, material=None):
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
        if not obj.data.uv_layers:
            bpy.ops.object.mode_set(mode='EDIT')
            bpy.ops.mesh.select_all(action='SELECT')
            bpy.ops.uv.smart_project(island_margin=.02)
            bpy.ops.object.mode_set(mode='OBJECT')
        bpy.context.scene.cursor.location = (0, 0, 0)
        bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
        if material is not None and material not in obj.data.materials[:]:
            obj.data.materials.append(material)
        return obj

    def box(name, dimensions, location, material, bevel=0):
        bpy.ops.mesh.primitive_cube_add(size=1, location=location)
        obj = bpy.context.object
        obj.name = name
        obj.dimensions = dimensions
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        if bevel:
            modifier = obj.modifiers.new('Moulded edges', 'BEVEL')
            modifier.width = min(float(bevel), min(dimensions) * .22)
            modifier.segments = 2
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        return finish(obj, material)

    def mesh(name, vertices, faces, material):
        data = bpy.data.meshes.new(name + '_Mesh')
        data.from_pydata(vertices, [], faces)
        data.update()
        obj = bpy.data.objects.new(name, data)
        bpy.context.collection.objects.link(obj)
        # Recalculate normals on procedural closed shells before export.
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode='EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.normals_make_consistent(inside=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        return finish(obj, material)

    helpers = {'reset': reset, 'mat': mat, 'box': box, 'mesh': mesh,
               'finish': finish, 'repo': REPO, 'seed': recipe.get('seed', 0)}

    def build_flat(asset):
        w, d, h = [float(v) / 1000 for v in asset['dimensionsMm']]
        edge = mat(asset['id'] + '_PaperEdge', (.66, .58, .43), .85)
        front = mat(asset['id'] + '_PhotoFront', (1, 1, 1), .8, asset.get('frontTexture'))
        back_name = '_PhotoBack' if asset.get('backTexture') else '_UnseenBackNeutral'
        back = mat(asset['id'] + back_name, (1, 1, 1) if asset.get('backTexture') else (.66, .61, .49), .8, asset.get('backTexture'))
        obj = box(asset['id'], (w, d, h), (0, 0, h / 2), edge)
        obj.data.materials.append(front)
        obj.data.materials.append(back)
        uv = obj.data.uv_layers.active
        for polygon in obj.data.polygons:
            if abs(polygon.normal.z) > .9:
                polygon.material_index = 1 if polygon.normal.z > 0 else 2
                for index in polygon.loop_indices:
                    point = obj.data.vertices[obj.data.loops[index].vertex_index].co
                    uv.data[index].uv = ((point.x / w + .5) if polygon.normal.z > 0 else (.5 - point.x / w), point.y / d + .5)
                if polygon.normal.z > 0 and asset.get('frontUv'):
                    if len(asset['frontUv']) != len(polygon.loop_indices):
                        raise ValueError('flat frontUv requires four points in top face loop order')
                    for index, coordinates in zip(polygon.loop_indices, asset['frontUv']):
                        uv.data[index].uv = coordinates
        return [obj]

    def build_tile(asset):
        w, d, h = [float(v) / 1000 for v in asset['dimensionsMm']]
        # Same 30-degree hexagon and +Z photo orientation as photo-samples-v001.
        radius = d / 2
        natural_w = math.sqrt(3) * radius
        corners = [(radius * math.cos(math.radians(30 + i * 60)) * w / natural_w,
                    radius * math.sin(math.radians(30 + i * 60))) for i in range(6)]
        vertices = [(x, y, z) for z in (0, h) for x, y in corners]
        faces = [tuple(reversed(range(6))), tuple(range(6, 12))]
        faces += [(i, (i + 1) % 6, (i + 1) % 6 + 6, i + 6) for i in range(6)]
        edge = mat(asset['id'] + '_TileEdge', (.55, .40, .20), .85)
        front = mat(asset['id'] + '_PhotoTop', (1, 1, 1), .8, asset.get('frontTexture'))
        obj = mesh(asset['id'], vertices, faces, edge)
        obj.data.materials.append(front)
        if asset.get('backTexture'):
            obj.data.materials.append(mat(asset['id'] + '_PhotoBack', (1, 1, 1), .8, asset['backTexture']))
        uv = obj.data.uv_layers.active
        for polygon in obj.data.polygons:
            if polygon.normal.z > .9:
                polygon.material_index = 1
                if asset.get('frontUv') and len(asset['frontUv']) != 6:
                    raise ValueError('tile frontUv requires six points in corner order, starting at 30 degrees')
                for index in polygon.loop_indices:
                    vertex_index = obj.data.loops[index].vertex_index
                    point = obj.data.vertices[vertex_index].co
                    uv.data[index].uv = asset['frontUv'][vertex_index % 6] if asset.get('frontUv') else (point.x / .08 + .5, point.y / .08 + .5)
            elif polygon.normal.z < -.9 and asset.get('backTexture'):
                polygon.material_index = 2
                for index in polygon.loop_indices:
                    point = obj.data.vertices[obj.data.loops[index].vertex_index].co
                    uv.data[index].uv = (.5 - point.x / .08, point.y / .08 + .5)
        return [obj]

    records = []
    for asset in assets:
        reset()
        if asset['kind'] == 'custom':
            builder_path = repo_path(asset['customBuilder'])
            spec = importlib.util.spec_from_file_location('entity_builder_' + asset['id'].replace('-', '_'), builder_path)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            result = module.build(asset, helpers)
            objects = list(result) if result is not None else [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
            builder_records[asset['customBuilder']] = {'path': asset['customBuilder'], 'sha256': sha(builder_path)}
        else:
            objects = build_tile(asset) if asset['kind'] == 'tile' else build_flat(asset)
        if not objects or any(obj.type != 'MESH' for obj in objects):
            raise ValueError('Builder must return actual mesh objects')
        for obj in objects:
            finish(obj)
            if not len(obj.data.materials):
                raise ValueError('Mesh has no material: ' + obj.name)
        bpy.ops.object.select_all(action='DESELECT')
        for obj in objects:
            obj.select_set(True)
        points = [obj.matrix_world @ v.co for obj in objects for v in obj.data.vertices]
        minimum = [float(min(point[i] for point in points)) for i in range(3)]
        maximum = [float(max(point[i] for point in points)) for i in range(3)]
        if abs(minimum[2]) > 1e-6:
            raise ValueError('Mesh must have its bottom at z=0: ' + asset['id'])
        if any(abs((maximum[i] + minimum[i]) / 2) > 1e-6 for i in (0, 1)):
            raise ValueError('Mesh bounding footprint must be centred on root: ' + asset['id'])
        triangles = sum(len(p.vertices) - 2 for obj in objects for p in obj.data.polygons)
        blend = source / (asset['id'] + '.blend')
        fbx = export / (asset['id'] + '.fbx')
        signature = geometry_signature(objects)
        materials = material_records(objects)
        bpy.ops.wm.save_as_mainfile(filepath=str(blend))
        bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH'},
                                 axis_forward='-Z', axis_up='Y', global_scale=1, apply_unit_scale=True,
                                 apply_scale_options='FBX_SCALE_UNITS', use_triangles=True,
                                 bake_anim=False, path_mode='RELATIVE', embed_textures=False)
        records.append({'id': asset['id'], 'label': asset.get('label', asset['id']), 'kind': asset['kind'],
                        'dimensionsMm': asset['dimensionsMm'], 'meshObjects': len(objects), 'triangles': triangles,
                        'geometrySha256': signature, 'blenderBounds': {'min': minimum, 'max': maximum},
                        'expectedUnityBounds': {'min': [minimum[0], minimum[2], -maximum[1]],
                                                'max': [maximum[0], maximum[2], -minimum[1]]},
                        'materials': materials, 'fbxSha256': sha(fbx), 'blendSha256': sha(blend),
                        'pivot': 'bottom centre; all mesh origins at (0,0,0)',
                        'backStatus': 'photo-backed' if asset.get('backTexture') else 'unseen; neutral material only'})
    if not args.skip_previews:
        render_previews(assets, records, source, preview, mat, box)
    manifest = {'schemaVersion': 1, 'version': VERSION, 'typeId': type_id, 'label': recipe.get('label', type_id),
                'recipePath': recipe_path.relative_to(REPO).as_posix(), 'recipeSha256': sha(recipe_path),
                'scriptPath': Path(__file__).relative_to(REPO).as_posix(), 'scriptSha256': sha(Path(__file__)),
                'customBuilders': sorted(builder_records.values(), key=lambda item: item['path']),
                'blenderVersion': bpy.app.version_string, 'seed': recipe.get('seed', 0), 'units': 'metres',
                'scaleStatus': recipe.get('scaleStatus', 'estimated; no physical measurement'),
                'references': references, 'assets': records,
                'textureDependencies': [{'path': 'textures/' + path.name, 'sha256': sha(path)} for path in sorted((export / 'textures').glob('*.png'))],
                'limitations': recipe.get('limitations', []),
                'exportSettings': {'axisForward': '-Z', 'axisUp': 'Y', 'globalScale': 1, 'applyUnitScale': True,
                                   'applyScaleOptions': 'FBX_SCALE_UNITS', 'triangulated': True},
                'previewsGenerated': not args.skip_previews,
                'previewViews': ['top', 'side', 'game-camera', 'crowded-vertex'] if not args.skip_previews else [],
                'previewResolution': [1600, 1000],
                'previewHashes': {path.name: sha(path) for path in sorted(preview.glob('*.png'))}}
    (export / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('CATAN_ENTITY_GENERATED ' + type_id + ' assets=' + str(len(records)) + ' ' + str(export))


def render_previews(assets, records, source, preview, mat, box):
    reset()
    columns = min(5, max(1, math.ceil(math.sqrt(len(assets) * 1.6))))
    rows = math.ceil(len(assets) / columns)
    width = max(record['blenderBounds']['max'][0] - record['blenderBounds']['min'][0] for record in records)
    depth = max(record['blenderBounds']['max'][1] - record['blenderBounds']['min'][1] for record in records)
    height = max(record['blenderBounds']['max'][2] for record in records)
    cell_x = max(width * 1.32, .025)
    cell_y = max(depth * 1.32, .035)
    groups = []
    for index, asset in enumerate(assets):
        with bpy.data.libraries.load(str(source / (asset['id'] + '.blend')), link=False) as (incoming, outgoing):
            outgoing.objects = incoming.objects
        objects = []
        offset = ((index % columns - (columns - 1) / 2) * cell_x,
                  ((rows - 1) / 2 - index // columns) * cell_y, 0)
        for obj in outgoing.objects:
            if obj and obj.type == 'MESH':
                bpy.context.collection.objects.link(obj)
                obj.location = offset
                objects.append(obj)
        groups.append(objects)
    table = mat('PreviewTable', (.025, .065, .075), .85)
    span_x, span_y = columns * cell_x, rows * cell_y
    box('PreviewTable', (span_x * 1.2, span_y * 1.2, .003), (0, 0, -.0015), table)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 8
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'Standard'
    scene.world = bpy.data.worlds.new('Studio')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs[0].default_value = (.28, .32, .36, 1)
    scene.world.node_tree.nodes['Background'].inputs[1].default_value = .65
    span = max(span_x, span_y, .06)
    for name, position, energy in [('Key', (-span * .5, -span * .6, span), 2.0 * span * span),
                                    ('Fill', (span * .6, span * .4, span * .8), 1.4 * span * span)]:
        bpy.ops.object.light_add(type='AREA', location=position)
        lamp = bpy.context.object
        lamp.name = name
        lamp.data.energy = energy
        lamp.data.shape = 'DISK'
        lamp.data.size = span
        lamp.rotation_euler = (Vector((0, 0, 0)) - lamp.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    scene.camera = camera
    camera.data.type = 'ORTHO'
    camera.data.clip_start = .0001
    camera.data.clip_end = max(10, span * 20)
    aspect = 1.6
    scale = max(span_x, span_y * aspect) * 1.12
    views = [('top', (0, 0, span * 3), (0, 0, 0)),
             ('side', (0, -span * 3, max(height * .5, .001)), (0, 0, max(height * .5, .001))),
             ('game-camera', (0, -span * 2, span * 2.3), (0, 0, height * .25)),
             ('crowded-vertex', (span * .5, -span * 1.7, span * 2), (0, 0, height * .3))]
    original_locations = {obj.name: obj.location.copy() for group in groups for obj in group}
    for name, position, target in views:
        # Side view uses vertical shelves so every asset is visible instead of
        # rows hiding one another. Geometry and physical scale remain unchanged.
        for index, group in enumerate(groups):
            for obj in group:
                obj.location = original_locations[obj.name]
                if name == 'side':
                    obj.location.y = 0
                    obj.location.z = (rows - 1 - index // columns) * max(height * 1.7, .012)
        if name == 'side':
            side_height = (rows - 1) * max(height * 1.7, .012) + height
            target = (0, 0, side_height / 2)
            position = (0, -span * 3, side_height / 2)
            camera.data.ortho_scale = max(span_x, side_height * aspect) * 1.15
        else:
            camera.data.ortho_scale = scale
        camera.location = position
        camera.rotation_euler = (Vector(target) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(preview / (name + '.png'))
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(source / 'preview-stage.blend'))


if __name__ == '__main__':
    main()
