"""Read-only inventory and checksum audit; writes only the versioned report."""
import hashlib
import json
from collections import Counter
from pathlib import Path

REPO = Path(__file__).resolve().parents[4]
REPORT = Path(__file__).resolve().parent / 'photo-coverage-audit.json'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def main():
    inbox = REPO / 'assets/references/inbox'
    photos = {path.relative_to(REPO).as_posix(): path for path in inbox.rglob('*')
              if path.is_file() and path.suffix.lower() in ('.jpg', '.jpeg', '.png', '.webp')}
    photo_hashes = {name: sha(path) for name, path in photos.items()}
    manifests = [(path, read(path)) for version in ('photo-samples-v001', 'entity-conversion-v001',
                                                    'entity-conversion-v002', 'entity-conversion-v003')
                 for path in sorted((REPO / 'art/exports' / version).rglob('manifest.json'))]
    referenced = {}
    mismatches, missing_files, changed_outputs = [], [], []
    batches, all_ids = [], []
    for manifest_path, manifest in manifests:
        version = manifest.get('version', 'photo-samples-v001')
        type_id = manifest.get('typeId', 'photo-samples')
        source_directory = REPO / 'art/source' / version
        if version != 'photo-samples-v001':
            source_directory /= type_id
        for reference in manifest.get('references', []):
            name = reference['originalPath']
            referenced.setdefault(name, set()).add(version + '/' + type_id)
            if name not in photo_hashes or photo_hashes[name] != reference['sha256']:
                mismatches.append(name)
        for asset in manifest['assets']:
            all_ids.append(asset['id'])
            for path, key in ((manifest_path.parent / (asset['id'] + '.fbx'), 'fbxSha256'),
                              (source_directory / (asset['id'] + '.blend'), 'blendSha256')):
                if not path.exists():
                    missing_files.append(path.relative_to(REPO).as_posix())
                elif asset.get(key) and sha(path) != asset[key]:
                    changed_outputs.append(path.relative_to(REPO).as_posix())
        for dependency in manifest.get('textureDependencies', []):
            path = manifest_path.parent / dependency['path']
            if not path.exists():
                missing_files.append(path.relative_to(REPO).as_posix())
            elif sha(path) != dependency['sha256']:
                changed_outputs.append(path.relative_to(REPO).as_posix())
        verification_path = manifest_path.parent / 'verification.json'
        batches.append({'version': version, 'typeId': type_id, 'assets': len(manifest['assets']),
                        'references': len(manifest.get('references', [])),
                        'textures': len(manifest.get('textureDependencies', [])),
                        'manifest': manifest_path.relative_to(REPO).as_posix(),
                        'priorVerification': read(verification_path).get('status') if verification_path.exists() else None})
    before_known = {item['path'] for item in read(REPO / 'art/recipes/entity-conversion-v001/catalog.json')['images']}
    component_catalog = read(REPO / 'assets/references/catalogs/player-components-v001.json')['assets']
    before_known.update(item['classifiedPath'] for item in component_catalog)
    backup_mismatches = [item['originalPath'] for item in component_catalog
                         if sha(REPO / item['originalPath']) != item['sha256'] or
                         photo_hashes[item['classifiedPath']] != item['sha256']]
    missing_coverage = sorted(set(photos) - set(referenced))
    report = {
        'status': 'passed' if not (mismatches or missing_files or changed_outputs or backup_mismatches or missing_coverage) else 'failed',
        'auditDate': '2026-10-10', 'originalInboxPhotographs': len(photos),
        'originalBackupPhotographsNotCountedTwice': len(component_catalog),
        'categories': dict(sorted(Counter(path.relative_to(inbox).parent.as_posix() for path in photos.values()).items())),
        'priorCataloguedPhotographs': len(before_known),
        'priorOmittedPhotographs': sorted(set(photos) - before_known),
        'referencedPhotographsAfterV003': len(set(referenced) & set(photos)),
        'unconvertedPhotographs': missing_coverage,
        'referenceHashMismatches': sorted(set(mismatches)), 'backupHashMismatches': backup_mismatches,
        'missingArtifactFiles': missing_files, 'changedArtifactHashes': changed_outputs,
        'retainedExportedFbxVersions': len(all_ids), 'uniqueAssetIds': len(set(all_ids)),
        'v003ReplacesExistingPoliticalCards': 9,
        'batches': batches,
        'photoCoverage': [{'originalPath': name, 'sha256': photo_hashes[name],
                           'exportBatches': sorted(referenced.get(name, set()))} for name in sorted(photos)],
        'interpretation': [
            'Photograph coverage is not one model per photograph: paired fronts/backs and combined-piece photographs are shared.',
            'Seven photographed terrain types are printed textures on closed, thick hexagonal meshes; they are not sculpted terrain.',
            'The red city+wall and metropolis+wall photos are references for independently exported components, not dedicated combined FBX assets.',
            'Source/export/texture files and hashes were checked now; historic Blender/Unity verification was inspected, not rerun by this inventory.',
            'Unity import and gallery validation do not establish current in-game use; current display wiring is audited independently.',
            'Physical measurements, hidden surfaces, final appearance and M8 acceptance remain pending.'
        ]
    }
    REPORT.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print('CATAN_PHOTO_COVERAGE_AUDIT', report['status'], len(photos), len(referenced), len(all_ids), len(set(all_ids)))
    if report['status'] != 'passed':
        raise SystemExit(1)


if __name__ == '__main__':
    main()
