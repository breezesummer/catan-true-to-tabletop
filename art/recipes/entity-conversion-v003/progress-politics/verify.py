"""Reimport v003 assets and verify every shared back plus inherited fronts."""
import importlib.util
import json
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
VERSION = 'entity-conversion-v003'


def main():
    spec = importlib.util.spec_from_file_location('entity_verify_v003', REPO / 'tools/art/verify_entity_pipeline.py')
    verifier = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(verifier)
    verifier.VERSION = VERSION
    verifier.main()
    cli = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    root = Path(cli[cli.index('--output-root') + 1]).resolve() if '--output-root' in cli else REPO / 'art'
    directory = root / 'exports' / VERSION / 'progress-politics'
    manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8'))
    wrapper = manifest['versionWrapper']
    verifier.require(verifier.sha(REPO / wrapper['path']) == wrapper['sha256'], 'Version wrapper changed')
    recipe = json.loads((REPO / manifest['recipePath']).read_text(encoding='utf-8'))
    back_name = 'progress-politics-back-original.png'
    verifier.require(len(manifest['assets']) == 9, 'Expected nine political progress cards')
    for asset in manifest['assets']:
        backs = [m for m in asset['materials'] if m['name'].endswith('_PhotoBack')]
        verifier.require(asset['backStatus'] == 'photo-backed' and len(backs) == 1 and backs[0]['texture'] == back_name,
                         'Missing original-photo back on ' + asset['id'])
    for record in recipe['inheritedTextures']:
        verifier.require(verifier.sha(REPO / record['source']) == record['sourceSha256'] == record['sha256'] == verifier.sha(REPO / record['output']),
                         'Frozen front changed')
    report_path = directory / 'verification.json'
    report = json.loads(report_path.read_text(encoding='utf-8'))
    report['commonPhotoBackBoundAssets'] = 9
    report['unchangedInheritedFronts'] = 9
    report['versionWrapperUnchanged'] = True
    report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('CATAN_POLITICS_V003_BACKS_VERIFIED 9')


if __name__ == '__main__':
    main()
