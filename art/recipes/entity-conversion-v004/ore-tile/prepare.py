"""Make a hex tile face from the existing corrected physical ore CARD illustration."""
import hashlib
import io
import json
from pathlib import Path
from PIL import Image, __version__ as pillow_version

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
VERSION = 'entity-conversion-v004'


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def write(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != data:
        raise FileExistsError('Frozen output differs; create a new version: ' + str(path))
    if not path.exists():
        path.write_bytes(data)


def main():
    original = 'assets/references/inbox/资源牌/铁矿石.jpg'
    inherited = 'art/source/entity-conversion-v001/resource-cards/textures/resource-ore-front.png'
    previous_recipe = REPO / 'art/recipes/entity-conversion-v001/resource-cards/recipe.json'
    old = json.loads(previous_recipe.read_text(encoding='utf-8-sig'))
    reference = next(r for r in old['references'] if r['originalPath'] == original)
    if sha(REPO / original) != reference['sha256']:
        raise ValueError('Original ore photograph changed')
    previous_manifest = json.loads((REPO / 'art/exports/entity-conversion-v001/resource-cards/manifest.json').read_text(encoding='utf-8-sig'))
    locked = next(t for t in previous_manifest['textureDependencies'] if t['path'].endswith('resource-ore-front.png'))
    if sha(REPO / inherited) != locked['sha256']:
        raise ValueError('Corrected ore face changed')
    crop = [112, 392, 912, 1192]
    face = Image.open(REPO / inherited).convert('RGB').crop(crop).resize((1024, 1024), Image.Resampling.BICUBIC)
    target = REPO / 'art/source' / VERSION / 'ore-tile/textures/tile-ore-top.png'
    encoded = io.BytesIO(); face.save(encoded, format='PNG'); write(target, encoded.getvalue())
    recipe = {
        'schemaVersion': 1, 'typeId': 'ore-tile', 'label': '铁矿资源卡插画衍生六边形厚板', 'seed': 20261011,
        'scaleStatus': 'estimated; 68.58921198 x 79.2 x 8 mm, same placement surface as other tiles',
        'references': [{**reference, 'role': 'physical ore RESOURCE CARD illustration; not a photographed mountain hex tile'}],
        'textureJobs': [],
        'inheritedTexture': {'source': inherited, 'sha256': sha(REPO / inherited),
                             'recipe': previous_recipe.relative_to(REPO).as_posix(), 'recipeSha256': sha(previous_recipe)},
        'derivedTexture': {'output': target.relative_to(REPO).as_posix(), 'sha256': sha(target),
                           'cropPixels': crop, 'size': [1024, 1024], 'method': 'crop original-photo corrected artwork and bicubic resample; hex boundary clips UV'},
        'limitations': ['用户要求棋盘铁矿改用实体卡片风格；当前没有六边形矿山地块原照，明确使用矿石资源卡插画衍生。',
                        '保留原照片像素、曝光、模糊与色偏；不使用 AI 重绘，不把资源卡照片声称为真实矿山地块照片。',
                        '中央矿石和矿山插画裁切为正方形后映射到六边形；完整资源手牌仍使用旧版未裁切卡面。',
                        '厚度、尺寸、未知背面仍为工作估算；实际闭合网格为厚板，不重建三维山体。'],
        'assets': [{'id': 'tile-ore', 'label': '铁矿卡片插画厚板', 'kind': 'tile',
                    'dimensionsMm': [68.58921198, 79.2, 8], 'frontTexture': target.relative_to(REPO).as_posix(),
                    'backTexture': None, 'frontUv': [[.9330127,.75],[.5,1],[.0669873,.75],[.0669873,.25],[.5,0],[.9330127,.25]]}]
    }
    write(HERE / 'recipe.json', (json.dumps(recipe, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    report = {'version': VERSION, 'typeId': 'ore-tile', 'originalPath': original, 'originalSha256': sha(REPO / original),
              'input': recipe['inheritedTexture'], 'operation': recipe['derivedTexture'],
              'scriptSha256': sha(__file__), 'pillowVersion': pillow_version}
    write(HERE / 'texture-derivation.json', (json.dumps(report, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('CATAN_ORE_TILE_TEXTURE_READY')


if __name__ == '__main__':
    main()
