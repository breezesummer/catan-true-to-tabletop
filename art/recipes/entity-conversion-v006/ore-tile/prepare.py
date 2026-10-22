"""将用户提供的高山板块原像素校正到棋盘六边形；原件和旧版产物保留。"""
import hashlib
import io
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps, __version__ as pillow_version

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
VERSION = 'entity-conversion-v006'
ORIGINAL = 'assets/references/archives/mountain-tile-v006/高山.jpg'
SOURCE_SHA = '3fb55da76875624c901e791892a44e122bde8ee35b343e73f9ac455bf27b084d'
# 原照片顶面内沿，按右、右上、左上、左、左下、右下记录；避开桌面和纸板投影。
CORNERS = [[1454, 668], [1152, 209], [637, 209], [364, 674], [684, 1182], [1267, 1132]]
UV = [[.9330127, .75], [.5, 1], [.0669873, .75], [.0669873, .25], [.5, 0], [.9330127, .25]]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def frozen(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes() != data:
        raise FileExistsError('已冻结产物有差异，请创建新版本：' + str(path))
    if not path.exists():
        path.write_bytes(data)


def png(path, picture):
    encoded = io.BytesIO()
    picture.save(encoded, format='PNG')
    frozen(path, encoded.getvalue())


def main():
    archived = REPO / ORIGINAL
    if not archived.exists():
        inbox = REPO / 'assets/references/inbox/地图板块/高山.jpg'
        if sha(inbox) != SOURCE_SHA:
            raise ValueError('高山原照与用户附件校验值不同')
        frozen(archived, inbox.read_bytes())
    if sha(archived) != SOURCE_SHA:
        raise ValueError('高山归档原件发生变化')
    original = ImageOps.exif_transpose(Image.open(archived)).convert('RGB')
    width = height = 1024
    destination = np.asarray([[u * width, (1 - v) * height] for u, v in UV])
    source_center = np.mean(CORNERS, axis=0)
    corrected = Image.new('RGB', (width, height), (193, 171, 126))
    for index in range(6):
        dest = [[width / 2, height / 2], destination[index].tolist(), destination[(index + 1) % 6].tolist()]
        source = [source_center.tolist(), CORNERS[index], CORNERS[(index + 1) % 6]]
        matrix = np.linalg.solve(np.asarray([[x, y, 1] for x, y in dest]), np.asarray(source))
        patch = original.transform((width, height), Image.Transform.AFFINE,
                                   [*matrix[:, 0], *matrix[:, 1]], Image.Resampling.BICUBIC)
        mask = Image.new('L', (width, height), 0)
        ImageDraw.Draw(mask).polygon([tuple(point) for point in dest], fill=255)
        corrected.paste(patch, (0, 0), mask)
    texture = f'art/source/{VERSION}/ore-tile/textures/tile-ore-top.png'
    png(REPO / texture, corrected)
    inspection = original.copy()
    outline = ImageDraw.Draw(inspection)
    outline.line([tuple(point) for point in CORNERS + CORNERS[:1]], fill=(255, 40, 40), width=4)
    for index, (x, y) in enumerate(CORNERS):
        outline.ellipse((x - 7, y - 7, x + 7, y + 7), fill=(255, 40, 40))
        outline.text((x + 10, y - 20), str(index), fill=(255, 40, 40), stroke_width=1)
    inspection.thumbnail((1024, 1024))
    png(HERE / 'source-inspection.png', inspection)
    recipe = {
        'schemaVersion': 1, 'typeId': 'ore-tile', 'label': '高山原照片六边形板块（产矿石）', 'seed': 20261015,
        'scaleStatus': 'estimated; 68.58921198 x 79.2 x 8 mm; same board placement surface; not physically measured',
        'references': [{'originalPath': ORIGINAL, 'sha256': SOURCE_SHA,
                        'role': '用户提供的实体高山地图板块正面；产矿石；与资源牌照片分别登记'}],
        'textureJobs': [{'source': ORIGINAL, 'output': texture, 'hex': CORNERS, 'size': [width, height],
                         'orientation': '右/右上/左上/左/左下/右下映射为棋盘右上/上/左上/左下/下/右下；与既有板块一致'}],
        'limitations': ['仅校正原照片像素，保留插画、黄色边框、失焦与光照；没有 AI 重绘、锐化或颜色替换。',
                        '六个顶面内沿角点分片 affine 校正，未做镜头标定；少量边缘裁切和分片插值属于近似。',
                        '输出为闭合六边形厚板网格，不把照片中的山峰伪称为三维山体。',
                        '实物尺寸、厚度及背面未知，沿用棋盘估算尺寸和中性底面；外观仍待用户确认。'],
        'assets': [{'id': 'tile-ore', 'label': '高山地图板块（产矿石）', 'kind': 'tile',
                    'dimensionsMm': [68.58921198, 79.2, 8], 'frontTexture': texture,
                    'backTexture': None, 'frontUv': UV}]
    }
    frozen(HERE / 'recipe.json', (json.dumps(recipe, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    report = {'version': VERSION, 'typeId': 'ore-tile', 'sourceSha256': SOURCE_SHA,
              'source': ORIGINAL, 'sourceSize': list(original.size), 'operation': recipe['textureJobs'][0],
              'outputSha256': sha(REPO / texture), 'method': '原像素六三角 affine 校正及 bicubic 重采样',
              'scriptSha256': sha(__file__), 'pillowVersion': pillow_version, 'numpyVersion': np.__version__}
    frozen(HERE / 'texture-correction.json', (json.dumps(report, ensure_ascii=False, indent=2) + '\n').encode('utf-8'))
    print('CATAN_MOUNTAIN_TILE_TEXTURE_READY')


if __name__ == '__main__':
    main()
