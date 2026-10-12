"""生成原生 8K 橡木材质；固定种子，不放大旧位图，不覆盖原始资产。"""
from pathlib import Path
import argparse
import hashlib
import json
import numpy as np
from PIL import Image


def generate(destination: Path, size: int):
    destination.mkdir(parents=True, exist_ok=True)
    rng = np.random.default_rng(20261012)
    u = np.arange(size, dtype=np.float32)[None, :] / size
    image = np.empty((size, size, 3), dtype=np.uint8)
    plank_tones = rng.uniform(-0.028, 0.028, 9)
    # 按行块计算，避免 8K 多通道临时浮点数组占用数 GB。
    for first in range(0, size, 128):
        last = min(size, first + 128)
        v = np.arange(first, last, dtype=np.float32)[:, None] / size
        plank = np.minimum(8, (v * 9).astype(np.int32))
        across = v * 9 - plank
        bend = .004 * np.sin(u * np.pi * 4 + plank * 1.1) + .002 * np.sin(u * np.pi * 10 + plank)
        grain = across + bend * 9
        rings = np.sin(grain * 92 + np.sin(u * 13 + plank) * 2)
        fine = np.sin(grain * 810 + np.sin(u * 39 + plank) * 3)
        pores = np.maximum(0, np.sin(grain * 1900 + np.sin(u * 27) * 5)) ** 20
        rays = np.maximum(0, np.sin(u * 1450 + np.sin(v * 160) * 2)) ** 22
        seam = np.exp(-np.minimum(across, 1 - across) * 1600)
        # 在每个输出像素采样独立微观导管，不依赖低分辨率原图。
        speckle = rng.normal(0, .006, (last - first, size)).astype(np.float32)
        tone = plank_tones[plank] + .029 * rings + .012 * fine - .027 * pores + .007 * rays - .11 * seam + speckle
        broad = .014 * np.sin(u * 5 + plank) + .01 * np.cos(v * 31)
        for channel, base in enumerate((.54, .365, .205)):
            image[first:last, :, channel] = np.clip((base + (tone + broad) * (1, .84, .60)[channel]) * 255, 0, 255)
    output = destination / 'oak-albedo-8k.png'
    Image.fromarray(image).save(output, compress_level=6)
    manifest = {
        '版本': 'tabletop-oak-v004', '种子': 20261012, '尺寸': [size, size],
        '类型': '确定性程序生成的位图材质；三维桌面另由 Unity 网格提供',
        '来源': '本脚本逐像素计算木纹、导管、拼板接缝和微观细节；未缩放旧贴图',
        'sha256': hashlib.sha256(output.read_bytes()).hexdigest(),
        '工具': {'numpy': np.__version__, 'Pillow': Image.__version__},
        '说明': '橡木外观为程序化候选，不代表实体照片或用户已确认的尺寸。'
    }
    (destination / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(json.dumps(manifest, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--size', type=int, default=8192)
    args = parser.parse_args()
    generate(args.output, args.size)
