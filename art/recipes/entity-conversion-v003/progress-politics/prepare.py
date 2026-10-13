"""Preserve v001 fronts and rectify the newly supplied blue card back only."""
import hashlib
import importlib.util
import io
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps, __version__ as pillow_version
import numpy as np

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
VERSION = 'entity-conversion-v003'
SOURCE = REPO / 'art/source' / VERSION / 'progress-politics/textures'
BACK_SOURCE = 'assets/references/inbox/发展卡/蓝色发展卡/卡背.jpg'
QUAD = [[700, 558], [2535, 548], [2600, 3392], [738, 3440]]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def immutable_write(path, data):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != data:
        raise FileExistsError('Versioned output differs; create a new version: ' + str(path))
    if not path.exists():
        path.write_bytes(data)


def json_write(path, value):
    immutable_write(path, (json.dumps(value, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))


def main():
    old_recipe = REPO / 'art/recipes/entity-conversion-v001/progress-politics/recipe.json'
    recipe = json.loads(old_recipe.read_text(encoding='utf-8-sig'))
    original = REPO / BACK_SOURCE
    original_sha = sha(original)
    photo = ImageOps.exif_transpose(Image.open(original)).convert('RGB')
    spec = importlib.util.spec_from_file_location('entity_photo_math', REPO / 'tools/art/rectify_entity_photos.py')
    helper = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(helper)
    width, height = 1024, 1536
    coefficients = helper.perspective_coefficients([(0, 0), (width, 0), (width, height), (0, height)], QUAD)
    corrected = photo.transform((width, height), Image.Transform.PERSPECTIVE, coefficients, Image.Resampling.BICUBIC)
    output = SOURCE / 'progress-politics-back-original.png'
    encoded = io.BytesIO()
    corrected.save(encoded, format='PNG')
    immutable_write(output, encoded.getvalue())
    inherited = []
    for asset in recipe['assets']:
        previous = REPO / asset['frontTexture']
        target = SOURCE / previous.name
        immutable_write(target, previous.read_bytes())
        inherited.append({'source': previous.relative_to(REPO).as_posix(), 'sourceSha256': sha(previous),
                          'output': target.relative_to(REPO).as_posix(), 'sha256': sha(target),
                          'method': 'unchanged byte-for-byte copy of frozen v001 corrected front'})
        asset['frontTexture'] = target.relative_to(REPO).as_posix()
        asset['backTexture'] = output.relative_to(REPO).as_posix()
    recipe['label'] = '蓝色政治进步卡九种正面与新增原照片共同背面'
    recipe['seed'] = 20261010
    recipe['references'].append({'originalPath': BACK_SOURCE, 'sha256': original_sha,
                                 'role': 'newly supplied blue politics common back; original photograph',
                                 'pixelSize': list(photo.size)})
    recipe['textureJobs'] = [{'source': BACK_SOURCE, 'output': output.relative_to(REPO).as_posix(),
                              'quad': QUAD, 'size': [width, height], 'rotation': 0}]
    recipe['inheritedTextures'] = inherited
    recipe['supersedes'] = {'recipePath': old_recipe.relative_to(REPO).as_posix(), 'sha256': sha(old_recipe),
                            'change': 'common political card back added; all nine frozen fronts unchanged'}
    recipe['limitations'] = [
        '九张正面沿用 v001 原照片几何校正产物，逐字节复制；新版只追加蓝色共同背面。',
        '新增背面仅按记录四角进行原像素透视校正；保留照片色偏、模糊、照明与保护套反光，不做 AI 重绘、去反光、锐化或颜色替换。',
        '角点取卡片直边延长线交点，圆角外仍含少量照片背景；闭合薄盒体没有精确复制圆角。',
        '54 × 80 × 0.3 mm 仍是估算；正背面配准、实测尺寸、原版色值与最终外观尚待用户确认。',
        '照片中的印刷文字只作美术参考，不修改现有 2025 规则逻辑。'
    ]
    recipe_path = HERE / 'recipe.json'
    json_write(recipe_path, recipe)
    json_write(HERE / 'texture-correction.json', {
        'typeId': 'progress-politics', 'version': VERSION,
        'operations': [{'source': BACK_SOURCE, 'sourceSha256': original_sha,
                        'output': output.relative_to(REPO).as_posix(), 'sha256': sha(output),
                        'method': 'original-pixel projective homography from recorded four corners',
                        'corners': QUAD, 'size': [width, height], 'rotation': 0,
                        'limitations': 'photo blur, lighting, glare and printed artwork retained'}],
        'inheritedFronts': inherited, 'pythonVersion': sys.version.split()[0],
        'pillowVersion': pillow_version, 'numpyVersion': np.__version__,
        'scriptSha256': sha(__file__), 'helperScriptSha256': sha(REPO / 'tools/art/rectify_entity_photos.py'),
        'recipeSha256': sha(recipe_path)
    })
    inspection = photo.copy()
    inspection.thumbnail((1024, 1366))
    scale = inspection.width / photo.width
    draw = ImageDraw.Draw(inspection)
    points = [(x * scale, y * scale) for x, y in QUAD]
    draw.line(points + points[:1], fill=(255, 50, 20), width=3)
    for index, (x, y) in enumerate(points):
        draw.ellipse((x-4, y-4, x+4, y+4), fill=(255, 50, 20))
        draw.text((x+5, y+5), str(index+1), fill=(255, 50, 20))
    encoded = io.BytesIO()
    inspection.save(encoded, format='PNG')
    immutable_write(HERE / 'source-outline-inspection.png', encoded.getvalue())
    json_write(REPO / 'assets/references/catalogs/blue-politics-back-v001.json', {
        'schemaVersion': 1, 'catalogVersion': 'blue-politics-back-v001',
        'classifiedDate': '2026-10-10', 'originalsPreserved': True,
        'assets': [{'id': 'physical-progress-politics-back-001', 'originalPath': BACK_SOURCE,
                    'sha256': original_sha, 'kind': 'photograph', 'edition': None,
                    'referenceRole': 'politics-common-back', 'measuredDimensionsMm': None,
                    'pixelSize': list(photo.size), 'mustPreserve': ['original photograph bytes', 'printed artwork'],
                    'notes': 'Omitted by the frozen v001 catalog; added without modifying earlier catalog or outputs.'}]
    })
    print('CATAN_POLITICS_V003_PREPARED fronts=9 back=1')


if __name__ == '__main__':
    main()
