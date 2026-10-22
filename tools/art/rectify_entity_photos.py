"""Deterministic geometric correction of original photo pixels, without redraw.

Each recipe textureJobs entry contains source/output, size and either quad
(TL, TR, BR, BL pixel coordinates) or hex (six corners, clockwise).
No OCR, generated text, inpainting, colour replacement or sharpening is used.
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps, __version__ as pillow_version

REPO = Path(__file__).resolve().parents[2]


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def perspective_coefficients(destination, source):
    rows, rhs = [], []
    for (x, y), (u, v) in zip(destination, source):
        rows.extend([[x, y, 1, 0, 0, 0, -u*x, -u*y],
                     [0, 0, 0, x, y, 1, -v*x, -v*y]])
        rhs.extend([u, v])
    return np.linalg.solve(np.asarray(rows, dtype=float), np.asarray(rhs, dtype=float)).tolist()


def rectify(recipe_path, overwrite=False):
    recipe_path = Path(recipe_path).resolve()
    recipe = json.loads(recipe_path.read_text(encoding='utf-8-sig'))
    reports = []
    for job in recipe.get('textureJobs', []):
        source = (REPO / job['source']).resolve()
        output = (REPO / job['output']).resolve()
        if not output.is_relative_to(REPO / 'art/source/entity-conversion-v001'):
            raise ValueError('Corrected textures must stay in the versioned source directory')
        source_sha = sha(source)
        expected = next((r['sha256'] for r in recipe['references'] if r['originalPath'] == job['source']), None)
        if expected != source_sha:
            raise ValueError('Original reference checksum mismatch: ' + job['source'])
        original = ImageOps.exif_transpose(Image.open(source)).convert('RGB')
        width, height = job.get('size', [1024, 1536])
        if 'quad' in job:
            destination = [(0, 0), (width, 0), (width, height), (0, height)]
            coefficients = perspective_coefficients(destination, job['quad'])
            corrected = original.transform((width, height), Image.Transform.PERSPECTIVE,
                                           coefficients, Image.Resampling.BICUBIC)
            method = 'original-pixel projective homography from recorded four corners'
        else:
            corners = job['hex']
            # Same pointy hex and 30-degree start as the Blender tile, in image coordinates.
            destination = [[.9330127*width, .25*height], [.5*width, 0],
                           [.0669873*width, .25*height], [.0669873*width, .75*height],
                           [.5*width, height], [.9330127*width, .75*height]]
            src_center = np.mean(corners, axis=0).tolist()
            dst_center = [width/2, height/2]
            corrected = Image.new('RGB', (width, height), (193, 171, 126))
            for index in range(6):
                dest = [dst_center, destination[index], destination[(index+1)%6]]
                src = [src_center, corners[index], corners[(index+1)%6]]
                matrix = np.linalg.solve(np.asarray([[x, y, 1] for x, y in dest]), np.asarray(src))
                coefficients = [*matrix[:, 0], *matrix[:, 1]]
                patch = original.transform((width, height), Image.Transform.AFFINE,
                                           coefficients, Image.Resampling.BICUBIC)
                mask = Image.new('L', (width, height), 0)
                ImageDraw.Draw(mask).polygon([tuple(p) for p in dest], fill=255)
                corrected.paste(patch, (0, 0), mask)
            method = 'original-pixel six-triangle affine hex correction from recorded corners'
        rotation = job.get('rotation', 0)
        if rotation:
            corrected = corrected.rotate(rotation, expand=True)
        output.parent.mkdir(parents=True, exist_ok=True)
        if output.exists() and not overwrite:
            import io
            encoded = io.BytesIO()
            corrected.save(encoded, format='PNG')
            if output.read_bytes() != encoded.getvalue():
                raise FileExistsError('Versioned texture differs; use a new version: ' + str(output))
        else:
            corrected.save(output, format='PNG')
        reports.append({'source': job['source'], 'sourceSha256': source_sha,
                        'output': job['output'], 'sha256': sha(output), 'method': method,
                        'corners': job.get('quad', job.get('hex')), 'size': list(corrected.size),
                        'rotation': rotation, 'limitations': 'photo blur, lighting, glare and print text retained'})
    report_path = recipe_path.parent / 'texture-correction.json'
    report_path.write_text(json.dumps({'typeId': recipe['typeId'], 'operations': reports,
                                      'pythonVersion': sys.version.split()[0], 'pillowVersion': pillow_version,
                                      'numpyVersion': np.__version__, 'scriptSha256': sha(Path(__file__)),
                                      'recipeSha256': sha(recipe_path)},
                                      ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print('CATAN_ENTITY_PHOTOS_CORRECTED', recipe['typeId'], len(reports))
    return reports


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--recipe', type=Path, required=True)
    parser.add_argument('--overwrite', action='store_true')
    arguments = parser.parse_args()
    rectify(arguments.recipe, arguments.overwrite)
