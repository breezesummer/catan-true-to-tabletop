"""Check v002 source preservation, delivered meshes and actual Unity evidence."""
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
VERSION = 'entity-conversion-v002'


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main():
    originals = read(REPO/'assets/references/catalogs/player-components-v001.json')['assets']
    assert len(originals) == 36
    sources = {a['classifiedPath']: a['sha256'] for a in originals}
    for original in originals:
        for field in ('originalPath', 'classifiedPath'):
            assert sha(REPO/original[field]) == original['sha256'], 'Original bytes changed'
    expected, used_sources, types = {}, set(), []
    for key in ('player-buildings', 'player-knights'):
        folder = REPO/'art/exports'/VERSION/key
        manifest = read(folder/'manifest.json')
        assert len(manifest['assets']) == 18
        assert sha(REPO/manifest['recipePath']) == manifest['recipeSha256']
        assert sha(REPO/manifest['scriptPath']) == manifest['scriptSha256']
        assert read(folder/'verification.json')['status'] == 'passed'
        rebuild = read(folder/'clean-rebuild-verification.json')
        assert rebuild['status'] == 'passed' and rebuild['cleanRebuild']['sameGeometryMaterialsTexturesReferences']
        for reference in manifest['references']:
            assert sources[reference['originalPath']] == reference['sha256']
            used_sources.add(reference['originalPath'])
        for builder in manifest['customBuilders']:
            assert sha(REPO/builder['path']) == builder['sha256']
        for asset in manifest['assets']:
            assert asset['id'] not in expected
            expected[asset['id']] = asset
            assert sha(folder/(asset['id']+'.fbx')) == asset['fbxSha256']
            assert sha(REPO/'art/source'/VERSION/key/(asset['id']+'.blend')) == asset['blendSha256']
        assert len(manifest['previewHashes']) == 4
        for name, digest in manifest['previewHashes'].items():
            assert sha(REPO/'art/previews'/VERSION/key/name) == digest
        types.append({'typeId': key, 'assets': 18, 'references': len(manifest['references']),
                      'triangles': sum(a['triangles'] for a in manifest['assets'])})
    assert used_sources == set(sources)
    evidence = REPO/'docs/verification'/VERSION
    unity = read(evidence/'unity-import.json')
    assert unity['unityVersion'] == '6000.3.25f1'
    assert {a['id'] for a in unity['assets'] if a['verified']} == set(expected)
    for imported in unity['assets']:
        asset = expected[imported['id']]
        assert imported['triangles'] == asset['triangles']
        bounds = asset['expectedUnityBounds']
        assert all(abs(actual-(maximum-minimum)) < .000015 for actual, minimum, maximum in
                   zip(imported['dimensionsMetres'], bounds['min'], bounds['max']))
    player = read(evidence/'player-verification.json')
    assert player['status'] == 'passed' and player['packagedPrefabs'] == 36 and player['rotateFlip']
    for asset_id in expected:
        for view in ('game-camera', 'top', 'side', 'crowded-vertex'):
            assert 'assets/'+asset_id+'/'+view in player['capturedViews']
            assert (evidence/'assets'/asset_id/('unity-'+view+'.png')).exists()
    projected = read(evidence/'player-components-verification.json')
    assert projected['status'] == 'passed' and projected['checkedInstances'] == 104
    assert projected['previewColoursPreserved']
    smoke = read(evidence/'m6-smoke.json')
    assert smoke['status'] == 'passed' and smoke['independentProcesses'] == 2
    assert smoke['saveRestore'] and smoke['continuedAi']
    for instance in projected['instances']:
        assert all(instance[key] for key in ('uniqueRoot', 'metreScale', 'noColliders', 'sourceMeshesMatched'))
        if instance['kind'] == 'knight':
            assert instance['explicitStateMarker']
            assert instance['explicitLevelMarker']
    counters = ET.parse(evidence/'rules-regression.trx').find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
    assert counters['total'] == counters['passed'] == '496' and counters['failed'] == counters['notExecuted'] == '0'
    report = {'status': 'passed', 'version': VERSION, 'originalsUnchanged': 36,
              'originalBackupHashesVerified': 36, 'assets': 36, 'types': types,
              'unityVersion': unity['unityVersion'], 'standaloneViews': 144,
              'projectedInstances': 104, 'rulesRegressionPassed': 496,
              'm6GameSaveRestoreProcesses': 2}
    (evidence/'pipeline-audit.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('CATAN_PLAYER_COMPONENT_AUDIT_OK assets=36 originals=36 views=144 projected=104')


if __name__ == '__main__':
    main()
