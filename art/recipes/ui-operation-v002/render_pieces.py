"""Render retained mesh sources into transparent inventory thumbnails using Blender.

blender --background --python-exit-code 1 --python this.py -- --output PATH
The default output is a new art/exports/ui-operation-v002/pieces directory.
No source blend, original photograph, or previous texture is modified.
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[3]
COLOURS = {'red': (.62, .018, .025, 1), 'blue': (.008, .024, .12, 1),
           'orange': (.95, .23, .018, 1), 'beige': (.82, .70, .43, 1)}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def canonical_png(path):
    # Blender writes Date/RenderTime/Cycles timing metadata even without a visible
    # stamp. Keep pixel data intact and omit textual ancillary chunks so identical
    # renders also have identical file hashes.
    source = path.read_bytes()
    result = bytearray(source[:8])
    offset = 8
    while offset < len(source):
        length = struct.unpack('>I', source[offset:offset + 4])[0]
        kind = source[offset + 4:offset + 8]
        if kind not in (b'tEXt', b'zTXt', b'iTXt', b'tIME'):
            result.extend(source[offset:offset + length + 12])
        offset += length + 12
    path.write_bytes(result)


def sources():
    for colour in COLOURS:
        for kind in ('road', 'settlement', 'city', 'ship', 'wall', 'knight-1', 'knight-2', 'knight-3'):
            if kind == 'road':
                source = 'photo-samples-v001/road.blend'
            elif kind == 'ship':
                source = 'entity-conversion-v001/ships/ship-' + ('ivory' if colour == 'beige' else colour) + '.blend'
            elif kind.startswith('knight'):
                level = kind[-1]
                source = ('entity-conversion-v001/knights/knight-level-' + level + '-resting.blend' if colour == 'red'
                          else 'entity-conversion-v002/player-knights/knight-' + colour + '-level-' + level + '-bare.blend')
            elif colour == 'red':
                source = {'settlement': 'photo-samples-v001/village.blend', 'city': 'entity-conversion-v001/city/city-red.blend',
                          'wall': 'entity-conversion-v001/city-wall/city-wall-red.blend'}[kind]
            else:
                source = 'entity-conversion-v002/player-buildings/' + {'settlement': 'village', 'city': 'city', 'wall': 'city-wall'}[kind] + '-' + colour + '.blend'
            yield kind + '-' + colour, REPO / 'art/source' / source, kind, colour


def render(source, kind, colour, target):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    with bpy.data.libraries.load(str(source), link=False) as (available, loaded):
        loaded.objects = available.objects
    objects = [obj for obj in loaded.objects if obj and obj.type == 'MESH']
    if not objects:
        raise RuntimeError('No mesh objects in ' + str(source))
    for obj in objects:
        transform = obj.matrix_world.copy()
        obj.parent = None
        obj.matrix_world = transform
        bpy.context.scene.collection.objects.link(obj)
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    low = Vector(tuple(min(p[axis] for p in points) for axis in range(3)))
    high = Vector(tuple(max(p[axis] for p in points) for axis in range(3)))
    centre = (high + low) / 2
    span = max(high - low)
    root = bpy.data.objects.new('ThumbnailNormalisation', None)
    bpy.context.scene.collection.objects.link(root)
    for obj in objects:
        obj.parent = root
    root.scale = (1 / span,) * 3
    root.location = -centre / span
    # 所有塑料统一采用村庄的线性色；独立窗洞和头盔保留原有材质。
    for material in {m for obj in objects for m in obj.data.materials if m and 'Plastic' in m.name}:
        material.diffuse_color = COLOURS[colour]
        if material.use_nodes:
            principled = material.node_tree.nodes.get('Principled BSDF')
            if principled:
                principled.inputs['Base Color'].default_value = COLOURS[colour]
    bpy.context.view_layer.update()
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 256
    scene.render.resolution_y = 256
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA'
    scene.render.image_settings.color_depth = '8'
    scene.render.film_transparent = True
    scene.render.filepath = str(target)
    scene.view_settings.view_transform = 'AgX'
    scene.world = bpy.data.worlds.new('ThumbnailWorld')
    scene.world.use_nodes = True
    scene.world.node_tree.nodes['Background'].inputs['Color'].default_value = (.35, .40, .46, 1)
    scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value = .5
    bpy.ops.object.camera_add(location=(2.6, -4, 3))
    camera = bpy.context.object
    camera.rotation_euler = (-camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera.data.type = 'ORTHO'
    camera.data.clip_start = .01
    camera.data.clip_end = 100
    scene.camera = camera
    bpy.context.view_layer.update()
    inverse = camera.matrix_world.inverted()
    projected = [inverse @ point for point in points]
    width = max(p.x for p in projected) - min(p.x for p in projected)
    height = max(p.y for p in projected) - min(p.y for p in projected)
    camera.data.ortho_scale = max(width, height) * 1.15
    # Centre the visible projected bounds, including tall hats and road footings.
    offset = Vector(((max(p.x for p in projected) + min(p.x for p in projected)) / 2,
                     (max(p.y for p in projected) + min(p.y for p in projected)) / 2, 0))
    camera.location += camera.matrix_world.to_quaternion() @ offset
    for name, location, power, size in [('Key', (-3, -4, 5), 550, 4), ('Fill', (4, -1, 2), 350, 3), ('Rim', (0, 3, 4), 600, 3)]:
        bpy.ops.object.light_add(type='AREA', location=location)
        light = bpy.context.object
        light.name = name
        light.data.energy = power
        light.data.shape = 'DISK'
        light.data.size = size
        light.rotation_euler = (-light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.render.render(write_still=True)
    canonical_png(target)
    return len(objects), sum(len(obj.data.polygons) for obj in objects)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=REPO / 'art/exports/ui-operation-v002/pieces')
    parser.add_argument('--only', help='Optional exact asset id for a single visual check')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    output = args.output.resolve()
    if output.exists() and any(output.iterdir()):
        raise RuntimeError('Output directory must be empty; rebuild into a new directory')
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for identity, source, kind, colour in sources():
        if args.only and identity != args.only:
            continue
        target = output / (identity + '.png')
        count, polygons = render(source, kind, colour, target)
        records.append({'id': identity, 'source': source.relative_to(REPO).as_posix(), 'sourceSha256': sha(source),
                        'file': target.name, 'sha256': sha(target), 'meshObjects': count, 'polygons': polygons,
                        'materialOverride': list(COLOURS[colour])})
        print('OPERATION_PIECE_RENDERED', identity, flush=True)
    if not records:
        raise RuntimeError('No assets matched')
    manifest = {'schemaVersion': 1, 'assetId': 'ui-operation-v002-pieces', 'blender': bpy.app.version_string,
                'recipe': 'art/recipes/ui-operation-v002/render_pieces.py', 'recipeSha256': sha(Path(__file__)),
                'method': 'Retained mesh sources rendered in Cycles CPU; no AI-generated bitmap or mesh conversion',
                'size': [256, 256], 'samples': 24, 'transparent': True,
                'camera': 'orthographic (2.6,-4,3) aimed at origin; mesh-normalized; 15 percent fit margin',
                'assets': records}
    (output / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
