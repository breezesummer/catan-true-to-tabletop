"""Run the existing deterministic mesh generator with a versioned v003 root."""
import importlib.util
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

REPO = Path(__file__).resolve().parents[4]
VERSION = 'entity-conversion-v003'


def main():
    spec = importlib.util.spec_from_file_location('entity_pipeline_v003', REPO / 'tools/art/entity_pipeline.py')
    generator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(generator)
    generator.VERSION = VERSION
    generator.main()
    cli = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    root = Path(cli[cli.index('--output-root') + 1]).resolve() if '--output-root' in cli else REPO / 'art'
    manifest_path = root / 'exports' / VERSION / 'progress-politics/manifest.json'
    manifest = json.loads(manifest_path.read_text(encoding='utf-8'))
    manifest['versionWrapper'] = {'path': Path(__file__).relative_to(REPO).as_posix(), 'sha256': generator.sha(__file__)}
    if '--skip-previews' not in cli:
        # The standard four views show fronts. Also expose the newly added backs.
        for obj in bpy.context.scene.objects:
            if obj.type == 'MESH' and obj.name != 'PreviewTable':
                obj.rotation_euler = (0, math.pi, 0)
                obj.location.z += .0003
        scene = bpy.context.scene
        camera = scene.camera
        camera.location = (0, 0, 1)
        camera.rotation_euler = (Vector((0, 0, 0)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        back_preview = root / 'previews' / VERSION / 'progress-politics/back.png'
        scene.render.filepath = str(back_preview)
        bpy.ops.render.render(write_still=True)
        manifest['previewViews'].append('back')
        manifest['previewHashes']['back.png'] = generator.sha(back_preview)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    main()
