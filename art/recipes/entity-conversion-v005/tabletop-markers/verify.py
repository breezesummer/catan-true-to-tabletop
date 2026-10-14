"""独立回读 .blend/.fbx，并检查八原件与归档副本一致。"""
import hashlib
import importlib.util
import json
from pathlib import Path
REPO=Path(__file__).resolve().parents[4]
spec=importlib.util.spec_from_file_location('tabletop_markers_verifier',REPO/'tools/art/verify_entity_pipeline.py')
verifier=importlib.util.module_from_spec(spec);spec.loader.exec_module(verifier)
verifier.VERSION='entity-conversion-v005'
recipe=json.loads((Path(__file__).parent/'recipe.json').read_text(encoding='utf-8'))
for ref in recipe['references']:
    for key in ('originalPath','inboxPath'):
        path=REPO/ref[key]
        # 干净 Git 克隆中可没有未分类副本；归档原件是权威可复现来源。
        if key=='inboxPath' and not path.exists(): continue
        if hashlib.sha256(path.read_bytes()).hexdigest()!=ref['sha256']:
            raise AssertionError('原件或分类归档字节发生变化: '+str(path))
verifier.main()
