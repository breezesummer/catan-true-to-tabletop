"""原照几何校正；保留印刷像素，不去反光、不重绘。先核对参考清单 SHA256。"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps

ROOT = Path(__file__).resolve().parents[3]
RECIPE = Path(__file__).parent


def coefficients(destination, source):
    rows, values = [], []
    for (x, y), (u, v) in zip(destination, source):
        rows.extend([[x, y, 1, 0, 0, 0, -u*x, -u*y],
                     [0, 0, 0, x, y, 1, -v*x, -v*y]])
        values.extend([u, v])
    return np.linalg.solve(np.asarray(rows, float), np.asarray(values, float)).tolist()


def prepare(output=None):
    output = Path(output) if output else ROOT / 'art/source/harbor-fishing-v002/textures'
    output.mkdir(parents=True, exist_ok=True)
    records = []
    for reference in json.loads((RECIPE / '参考清单.json').read_text())['照片']:
        source = ROOT / reference['路径']
        if hashlib.sha256(source.read_bytes()).hexdigest() != reference['SHA256']:
            raise ValueError('参考原图内容不符：' + source.name)
        image = ImageOps.exif_transpose(Image.open(source)).convert('RGB')
        corners = reference['角点像素']
        name = reference['id']
        if name.startswith('ground-'):
            # 两个四边形分别投影，精确保留六角凹口；不把木桌带入网格。
            size = (1024, 768)
            destination = [(0, 0), (512, 307.2), (1024, 0),
                           (1024, 460.8), (512, 768), (0, 460.8)]
            patches = [(0, 1, 4, 5), (1, 2, 3, 4)]
        elif name == 'lake':
            size = (1024, 1024)
            destination = [(512, 0), (1024, 256), (1024, 768),
                           (512, 1024), (0, 768), (0, 256)]
            # 六个三角形与既有实体地块校正方式一致。
            destination.append((512, 512))
            corners = corners + [np.mean(corners, axis=0).tolist()]
            patches = [(6, i, (i+1) % 6) for i in range(6)]
        else:
            size = (512, 512)
            destination = [(0, 0), (512, 0), (512, 512), (0, 512)]
            patches = [(0, 1, 2, 3)]
        corrected = Image.new('RGB', size, (64, 108, 145))
        for patch in patches:
            target = [destination[i] for i in patch]
            origin = [corners[i] for i in patch]
            if len(patch) == 4:
                mode = Image.Transform.PERSPECTIVE
                transform = coefficients(target, origin)
            else:
                matrix = np.linalg.solve(np.asarray([[x, y, 1] for x, y in target]), np.asarray(origin))
                mode = Image.Transform.AFFINE
                transform = [*matrix[:, 0], *matrix[:, 1]]
            patch_image = image.transform(size, mode, transform, Image.Resampling.BICUBIC)
            mask = Image.new('L', size, 0)
            ImageDraw.Draw(mask).polygon(target, fill=255)
            corrected.paste(patch_image, (0, 0), mask)
        path = output / (name + '-albedo.png')
        corrected.save(path)
        records.append({'id': name, '源SHA256': reference['SHA256'],
                        '贴图SHA256': hashlib.sha256(path.read_bytes()).hexdigest(),
                        '分辨率': size, '处理': '记录角点的原像素几何投影；反光、颗粒与印刷文字保留'})
    (output.parent / '贴图检查.json').write_text(json.dumps(records, ensure_ascii=False, indent=2)+'\n')
    return records


if __name__ == '__main__':
    import sys
    print(json.dumps(prepare(sys.argv[1] if len(sys.argv) > 1 else None), ensure_ascii=False, indent=2))
