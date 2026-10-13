"""Independently reimport FBX, inspect source geometry and compare clean rebuilds."""
import argparse
from collections import Counter
import hashlib
import importlib.util
import json
from pathlib import Path
import struct
import sys

import bpy

REPO = Path(__file__).resolve().parents[2]
VERSION = 'entity-conversion-v002'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def require(condition, message):
    if not condition:
        raise AssertionError(message)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--output-root', type=Path, default=REPO / 'art')
    parser.add_argument('--compare-root', type=Path)
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    recipe_path = args.recipe.resolve() if args.recipe.is_absolute() else REPO / args.recipe
    recipe = json.loads(recipe_path.read_text(encoding='utf-8-sig'))
    type_id = recipe['typeId']
    root = args.output_root.resolve()
    directory = root / 'exports' / VERSION / type_id
    source = root / 'source' / VERSION / type_id
    manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8'))
    require(manifest['typeId'] == type_id and manifest['schemaVersion'] == 1, 'Type/schema mismatch')
    require(sha(recipe_path) == manifest['recipeSha256'], 'Recipe changed since generation')
    require(sha(REPO / manifest['scriptPath']) == manifest['scriptSha256'], 'Generator changed since generation')
    for builder in manifest['customBuilders']:
        require(sha(REPO / builder['path']) == builder['sha256'], 'Custom builder changed since generation')
    for reference in manifest['references']:
        require(sha(REPO / reference['originalPath']) == reference['sha256'], 'Original reference changed')
    textures = []
    for record in manifest['textureDependencies']:
        path = directory / record['path']
        require(sha(path) == record['sha256'], 'Texture hash mismatch')
        data = path.read_bytes()
        require(data[:8] == b'\x89PNG\r\n\x1a\n', 'Texture must be actual PNG')
        w, h = struct.unpack('>II', data[16:24])
        require(w > 0 and h > 0, 'Empty PNG')
        textures.append({'path': record['path'], 'width': w, 'height': h, 'verified': True})
    spec = importlib.util.spec_from_file_location('entity_pipeline_signature', REPO / 'tools/art/entity_pipeline_v002.py')
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    reports = []
    for asset in manifest['assets']:
        fbx = directory / (asset['id'] + '.fbx')
        blend = source / (asset['id'] + '.blend')
        require(sha(fbx) == asset['fbxSha256'], 'FBX hash mismatch: ' + asset['id'])
        require(sha(blend) == asset['blendSha256'], 'Blend hash mismatch: ' + asset['id'])
        bpy.ops.wm.open_mainfile(filepath=str(blend))
        original = [obj for obj in bpy.context.scene.objects if obj.type == 'MESH']
        require(module.geometry_signature(original) == asset['geometrySha256'], 'Source geometry/UV differs from manifest')
        require(module.material_records(original) == asset['materials'], 'Source materials differ from manifest')
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(fbx))
        objects = list(bpy.context.scene.objects)
        require(len(objects) == asset['meshObjects'] and all(obj.type == 'MESH' for obj in objects), 'FBX mesh count/type mismatch')
        points = []
        triangles = 0
        used_materials = Counter()
        for obj in objects:
            require(obj.matrix_world.translation.length < 1e-6, obj.name + ': nonzero origin')
            uv = obj.data.uv_layers.active
            require(uv is not None and len(uv.data) == len(obj.data.loops), obj.name + ': missing UV')
            require(all(all(abs(float(value)) < 1e6 for value in loop.uv) for loop in uv.data), obj.name + ': invalid UV')
            edges = Counter()
            require(len(obj.data.materials) > 0, obj.name + ': missing materials')
            for polygon in obj.data.polygons:
                require(len(polygon.vertices) == 3 and polygon.area > 1e-17, obj.name + ': nontriangle/degenerate face')
                require(polygon.material_index < len(obj.data.materials), obj.name + ': invalid material index')
                used_materials[obj.data.materials[polygon.material_index].name] += 1
                triangles += 1
                for index, vertex in enumerate(polygon.vertices):
                    edges[tuple(sorted((vertex, polygon.vertices[(index + 1) % 3])))] += 1
            require(bool(edges) and all(count == 2 for count in edges.values()), obj.name + ': open/nonmanifold mesh')
            points.extend(obj.matrix_world @ vertex.co for vertex in obj.data.vertices)
        minimum = [float(min(point[i] for point in points)) for i in range(3)]
        maximum = [float(max(point[i] for point in points)) for i in range(3)]
        require(all(abs(minimum[i] - asset['blenderBounds']['min'][i]) < 1e-6 and
                    abs(maximum[i] - asset['blenderBounds']['max'][i]) < 1e-6 for i in range(3)), 'FBX bounds changed')
        require(abs(minimum[2]) < 1e-6 and triangles == asset['triangles'], 'Bottom/triangle count mismatch')
        require(all(abs((minimum[i] + maximum[i]) / 2) < 1e-6 for i in (0, 1)), 'Footprint not centred')
        for material in asset['materials']:
            require(used_materials[material['name']] > 0, 'Material has no faces after import: ' + material['name'])
            if material['texture']:
                require((directory / 'textures' / material['texture']).exists(), 'Missing texture dependency')
        dimensions = [maximum[i] - minimum[i] for i in range(3)]
        if asset['kind'] in ('card', 'tile', 'panel'):
            require(all(abs(dimensions[i] * 1000 - float(asset['dimensionsMm'][i])) < .01 for i in range(3)), 'Planar asset dimensions differ from recipe')
        reports.append({'id': asset['id'], 'meshObjects': len(objects), 'triangles': triangles,
                        'closedMeshes': True, 'uvMeshes': len(objects), 'bottomCentrePivot': True,
                        'blenderDimensionsMetres': dimensions, 'allMaterialSlotsUsed': True})
    views = []
    for name in manifest['previewViews']:
        path = root / 'previews' / VERSION / type_id / (name + '.png')
        data = path.read_bytes()
        require(data[:8] == b'\x89PNG\r\n\x1a\n', 'Preview must be PNG')
        require(list(struct.unpack('>II', data[16:24])) == manifest['previewResolution'], 'Preview resolution mismatch')
        require(sha(path) == manifest['previewHashes'][path.name], 'Preview hash mismatch')
        views.append(name)
    comparison = None
    if args.compare_root:
        previous = json.loads((args.compare_root.resolve() / 'exports' / VERSION / type_id / 'manifest.json').read_text(encoding='utf-8'))
        actual_assets = {item['id']: item for item in manifest['assets']}
        expected_assets = {item['id']: item for item in previous['assets']}
        require(actual_assets.keys() == expected_assets.keys(), 'Clean rebuild asset ids differ')
        for asset_id, actual in actual_assets.items():
            expected = expected_assets[asset_id]
            for field in ('id', 'kind', 'dimensionsMm', 'meshObjects', 'triangles', 'geometrySha256',
                          'blenderBounds', 'expectedUnityBounds', 'materials'):
                require(actual[field] == expected[field], asset_id + ': clean rebuild changed ' + field)
        for field in ('textureDependencies', 'references', 'seed', 'scaleStatus', 'exportSettings'):
            require(manifest[field] == previous[field], 'Clean rebuild changed ' + field)
        comparison = {'sameGeometryMaterialsTexturesReferences': True, 'referenceRoot': str(args.compare_root.resolve())}
    report = {'status': 'passed', 'typeId': type_id, 'assets': reports, 'textureDependencies': textures,
              'previews': views, 'originalReferencesUnchanged': True, 'cleanRebuild': comparison,
              'notCovered': ['Unity runtime rendering and input', 'user visual approval',
                             'physical measurements and unseen surfaces']}
    (directory / 'verification.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('CATAN_ENTITY_VERIFIED ' + type_id + ' assets=' + str(len(reports)) + ' cleanRebuild=' + str(comparison is not None))


if __name__ == '__main__':
    main()
