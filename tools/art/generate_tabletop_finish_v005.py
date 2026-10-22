"""固定种子生成克制的烟熏橡木色彩及微表面法线；不覆盖旧版本。"""
from pathlib import Path
import argparse
import hashlib
import json
import struct
import zlib
import numpy as np


def png(path, pixels):
    height, width, channels = pixels.shape
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))
    rows = b''.join(b'\x00' + row.tobytes() for row in pixels)
    path.write_bytes(b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 2, 0, 0, 0))
                     + chunk(b'IDAT', zlib.compress(rows, 7)) + chunk(b'IEND', b''))


def noise(u, v, px, py, seed):
    rng = np.random.default_rng(seed)
    grid = rng.random((py, px), dtype=np.float32)
    x, y = (u % 1) * px, (v % 1) * py
    ix, iy = x.astype(np.int32), y.astype(np.int32)
    fx, fy = x - ix, y - iy
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a = grid[iy % py, ix % px] * (1 - fx) + grid[iy % py, (ix + 1) % px] * fx
    b = grid[(iy + 1) % py, ix % px] * (1 - fx) + grid[(iy + 1) % py, (ix + 1) % px] * fx
    return a * (1 - fy) + b * fy


def generate(destination, size):
    destination.mkdir(parents=True, exist_ok=True)
    u = np.arange(size, dtype=np.float32)[None, :] / size
    albedo = np.empty((size, size, 3), dtype=np.uint8)
    heights = np.empty((size, size), dtype=np.float32)
    for first in range(0, size, 128):
        last = min(size, first + 128)
        v = np.arange(first, last, dtype=np.float32)[:, None] / size
        # 多尺度非周期线条噪声，无正弦年轮、等距拼板或高反差细线。
        broad = noise(u, v, 3, 5, 20261013) - .5
        warp = (noise(u, v, 4, 13, 33) - .5) * .018
        grain = noise(u, v + warp, 11, 290, 34) - .5
        pores = noise(u, v + warp, 74, 840, 35) - .5
        fine = noise(u, v, 490, 980, 36) - .5
        variation = broad * .030 + grain * .019 + pores * .010 + fine * .004
        for channel, base in enumerate((.335, .266, .202)):
            albedo[first:last, :, channel] = np.clip((base + variation * (1, .88, .73)[channel]) * 255, 0, 255)
        heights[first:last] = grain * .065 + pores * .026 + fine * .012
    dx = (np.roll(heights, -1, 1) - np.roll(heights, 1, 1)) * 1.7
    dy = (np.roll(heights, -1, 0) - np.roll(heights, 1, 0)) * 1.7
    norm = np.sqrt(dx * dx + dy * dy + 1)
    normals = np.stack((-dx / norm * .5 + .5, -dy / norm * .5 + .5, 1 / norm * .5 + .5), axis=2)
    png(destination / 'smoked-oak-albedo.png', albedo)
    png(destination / 'smoked-oak-normal.png', np.clip(normals * 255, 0, 255).astype(np.uint8))
    manifest = {
        '版本': 'tabletop-finish-v005', '种子': [20261013, 33, 34, 35, 36], '尺寸': [size, size],
        '纹理覆盖米数': .75,
        '类型': '确定性生成的色彩和法线位图；三维桌面及倒角板块由 Unity 网格提供',
        '来源': '本脚本的多尺度平滑噪声；无外部图像、无图像模型、保留 v003/v004 源资源',
        '工具': {'numpy': np.__version__},
        '文件': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(destination.glob('*.png'))},
        '说明': '这是烟熏橡木材质候选；未代表实测尺寸或用户最终美术验收。'
    }
    (destination / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(manifest, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--size', type=int, default=2048)
    args = parser.parse_args()
    generate(args.output, args.size)
