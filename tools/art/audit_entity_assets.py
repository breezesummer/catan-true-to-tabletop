"""Cross-check delivered art against the locked original inventory and rebuild evidence."""
import hashlib
import json
import xml.etree.ElementTree as ET
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
VERSION = 'entity-conversion-v001'

def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))

def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def main():
    catalog = read(REPO/'art/recipes'/VERSION/'catalog.json')
    originals = read(REPO/'art/recipes'/VERSION/'reference-manifest.json')['assets']
    locked = {a['originalPath']:a for a in originals}
    assert len(locked)==98
    for name, record in locked.items():
        assert sha(REPO/name)==record['sha256'], 'Original changed: '+name
    records, unique_ids, used_sources, expected_assets = [], set(), set(), {}
    for task in catalog['conversion_types']:
        key=task['id']; recipe_dir=REPO/'art/recipes'/VERSION/key
        recipe=read(recipe_dir/'recipe.json')
        directory=REPO/'art/exports'/VERSION/key
        manifest=read(directory/'manifest.json')
        verification=read(directory/'verification.json')
        clean_path=directory/'clean-rebuild-verification.json'
        clean=read(clean_path) if clean_path.exists() else verification
        assert verification['status']=='passed'
        assert clean['status']=='passed' and clean['cleanRebuild']['sameGeometryMaterialsTexturesReferences']
        assert sha(recipe_dir/'recipe.json')==manifest['recipeSha256']
        assert sha(REPO/manifest['scriptPath'])==manifest['scriptSha256']
        assert (recipe_dir/'README.md').exists()
        refs={a['originalPath']:a for a in recipe['references']}
        assert set(task['source_files']).issubset(refs), 'Missing assigned reference '+key
        used_sources.update(refs)
        for name, record in refs.items():
            assert name in locked and record['sha256']==locked[name]['sha256']
        assert [a['id'] for a in recipe['assets']]==[a['id'] for a in manifest['assets']]
        for asset in manifest['assets']:
            assert asset['id'] not in unique_ids, 'Duplicate asset ID'
            unique_ids.add(asset['id'])
            expected_assets[asset['id']]=asset
            assert sha(directory/(asset['id']+'.fbx'))==asset['fbxSha256']
            assert sha(REPO/'art/source'/VERSION/key/(asset['id']+'.blend'))==asset['blendSha256']
        for builder in manifest['customBuilders']:
            assert sha(REPO/builder['path'])==builder['sha256']
        for texture in manifest['textureDependencies']:
            assert sha(directory/texture['path'])==texture['sha256']
        views=REPO/'art/previews'/VERSION/key
        assert len(manifest['previewHashes'])==4
        for name, checksum in manifest['previewHashes'].items():
            assert sha(views/name)==checksum
        corrections=read(recipe_dir/'texture-correction.json') if (recipe_dir/'texture-correction.json').exists() else {'operations':[]}
        assert len(corrections['operations'])==len(recipe.get('textureJobs',[]))
        assert corrections['recipeSha256']==sha(recipe_dir/'recipe.json')
        assert corrections['scriptSha256']==sha(REPO/'tools/art/rectify_entity_photos.py')
        for operation in corrections['operations']:
            assert sha(REPO/operation['source'])==operation['sourceSha256']
            assert sha(REPO/operation['output'])==operation['sha256']
        records.append({'typeId':key,'assets':len(manifest['assets']),'references':len(refs),
                        'textures':len(manifest['textureDependencies']),'cleanRebuild':True,
                        'triangles':sum(a['triangles'] for a in manifest['assets'])})
    for sample in catalog['completed_samples']:
        used_sources.update(sample['source_files'])
    assert used_sources==set(locked), 'Source inventory coverage differs'
    unity=read(REPO/'docs/verification'/VERSION/'unity-import.json')
    assert {a['id'] for a in unity['assets'] if a['verified']}==unique_ids
    assert len(unity['assets'])==len(unique_ids)
    for imported in unity['assets']:
        expected=expected_assets[imported['id']]
        assert imported['triangles']==expected['triangles'], 'Unity triangle count differs '+imported['id']
        bounds=expected['expectedUnityBounds']
        assert all(abs(actual-(maximum-minimum))<.000015 for actual,minimum,maximum in zip(
            imported['dimensionsMetres'],bounds['min'],bounds['max'])), 'Unity size differs '+imported['id']
    player=read(REPO/'docs/verification'/VERSION/'player-verification.json')
    assert player['status']=='passed' and player['packagedPrefabs']==len(unique_ids) and player['types']==len(records)
    assert player['rotateFlip'] and player['wallAssembly']
    for asset_id in unique_ids:
        assert 'assets/'+asset_id+'/top' in player['capturedViews']
        assert (REPO/'docs/verification'/VERSION/'assets'/asset_id/'unity-top.png').exists()
    for record in records:
        for view in ('game-camera','top','side','crowded-vertex'):
            assert record['typeId']+'/'+view in player['capturedViews']
            assert (REPO/'docs/verification'/VERSION/record['typeId']/('unity-'+view+'.png')).exists()
    assembly=read(REPO/'docs/verification'/VERSION/'assembly-clearance.json')
    assert assembly['status']=='passed' and sha(REPO/assembly['wallSource'])==assembly['wallSha256']
    for insert in assembly['inserts']:
        assert sha(REPO/insert['source'])==insert['sha256'] and insert['minimumRadialClearanceMm']>0
    regression=REPO/'docs/verification'/VERSION/'rules-regression.trx'
    counters=ET.parse(regression).find('.//{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}Counters').attrib
    assert counters['passed']==counters['total']=='496' and counters['failed']=='0' and counters['notExecuted']=='0'
    report={'status':'passed','unchangedOriginalPhotos':len(locked),'convertedTypes':len(records),
            'convertedAssets':len(unique_ids),'types':records,'unityVersion':unity['unityVersion'],
            'playerCapturedViews':len(player['capturedViews']),'rulesChanged':False,
            'rulesRegression':{'passed':int(counters['passed']),'failed':int(counters['failed']),'trxSha256':sha(regression)},
            'assemblyClearanceMm':min(i['minimumRadialClearanceMm'] for i in assembly['inserts']),
            'inspectionPlayerSha256':sha(REPO/'.local/art-pipeline/Builds/Windows/CatanEntityAssets-v001.exe'),
            'scriptsSha256':{path:sha(REPO/path) for path in (
                'tools/art/entity_pipeline.py','tools/art/verify_entity_pipeline.py','tools/art/rectify_entity_photos.py',
                'Unity/Catan/Assets/Editor/EntityAssetBuild.cs','Unity/Catan/Assets/Scripts/EntityAssetGallery.cs',
                'Unity/Catan/Assets/Scripts/EntityAssetPlayerVerification.cs')},
            'limits':['estimated sizes and hidden surfaces','user visual approval pending',
                      'no full gameplay replacement','no long-session performance validation']}
    target=REPO/'docs/verification'/VERSION/'pipeline-audit.json'
    target.write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('CATAN_ENTITY_PIPELINE_AUDIT_OK',len(records),len(unique_ids),len(locked))

if __name__=='__main__':
    main()
