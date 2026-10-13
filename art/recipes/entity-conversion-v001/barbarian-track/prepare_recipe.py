"""Freeze metadata for the original-photo barbarian invasion track.

The source is an irregular panel, not a rectangular card. The four straight
body edges determine the projective frame. Its canvas is expanded to contain
the manually traced real outline, without clipping the two round protrusions.
This script writes metadata and an inspection overlay, never painted artwork.
"""
import hashlib
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageOps

REPO = Path(__file__).resolve().parents[4]
TYPE = 'barbarian-track'
ORIGINAL = 'assets/references/inbox/单位/海盗入侵卡.jpg'
FRONT = 'art/source/entity-conversion-v001/barbarian-track/textures/barbarian-track-front.png'

# Original 3072 x 4096 coordinates: straight body-edge extension intersections.
# These are a projective frame, not the irregular panel's physical corners.
BODY_QUAD = [[520, 638], [2270, 696], [2680, 3570], [687, 3602]]

# Actual visible paper edge, read clockwise on a 1368 x 1824 inspection display.
# The same exact scale applies to x and y; coordinates map to original pixels.
OUTLINE_DISPLAY = [
    [315,284], [502,290], [896,302], [1100,314], [1124,316],
    [1150,325], [1178,342], [1198,363], [1217,392], [1230,424],
    [1240,450], [1247,482], [1246,501], [1242,525], [1232,550],
    [1216,573], [1196,591], [1168,603], [1132,612], [1101,615],
    [1084,621], [1070,635], [1064,648], [1062,663], [1063,683],
    [1088,867], [1114,1052], [1140,1237], [1167,1422], [1180,1496],
    [1185,1520], [1185,1543], [1180,1563], [1171,1576], [1158,1586],
    [1136,1593], [1110,1595], [815,1602], [505,1607], [206,1608],
    [179,1606], [151,1599], [127,1587], [105,1570], [87,1550],
    [70,1525], [56,1498], [48,1470], [43,1440], [42,1413],
    [45,1389], [54,1367], [66,1344], [82,1323], [101,1304],
    [124,1287], [147,1272], [173,1258], [200,1245], [224,1234],
    [240,1227], [255,1219], [266,1208], [275,1194], [281,1177],
    [282,1155], [270,937], [258,718], [245,499], [235,342],
    [235,331], [239,318], [245,307], [254,298], [266,291],
    [282,286],
]


def homography(source, target):
    rows, values = [], []
    for (x, y), (u, v) in zip(source, target):
        rows.extend([[x,y,1,0,0,0,-u*x,-u*y], [0,0,0,x,y,1,-v*x,-v*y]])
        values.extend([u,v])
    return np.append(np.linalg.solve(np.asarray(rows), np.asarray(values)), 1).reshape(3,3)


def project(matrix, points):
    result = np.c_[points, np.ones(len(points))] @ matrix.T
    return result[:, :2] / result[:, 2:]


def author():
    original = REPO / ORIGINAL
    source_outline = np.asarray(OUTLINE_DISPLAY, dtype=float) * (3072/1368)
    matrix = homography(np.asarray(BODY_QUAD, float), [[0,0],[1,0],[1,1],[0,1]])
    body_outline = project(matrix, source_outline)
    minimum, maximum = body_outline.min(axis=0), body_outline.max(axis=0)
    outline = (body_outline-minimum)/(maximum-minimum)
    expanded = [[minimum[0],minimum[1]], [maximum[0],minimum[1]],
                [maximum[0],maximum[1]], [minimum[0],maximum[1]]]
    quad = project(np.linalg.inv(matrix), expanded)
    ratio = float((maximum[0]-minimum[0])*.61/(maximum[1]-minimum[1]))
    recipe = {
        'schemaVersion': 1, 'typeId': TYPE, 'label': '蛮族入侵异形轨道板',
        'seed': 20261009,
        'scaleStatus': 'estimated; 190 x 200 x 1.5 mm; photo-derived silhouette ratio about 0.95; not physically measured or user-approved',
        'semanticMapping': {'originalFilename': '海盗入侵卡.jpg', 'assetId': 'barbarian-track',
                            'rulesRole': 'barbarian invasion track; original filename preserved; photo content does not alter official 2025 rules'},
        'limitations': [
            '原件3072x4096保持不变；PIL四点投影只重采样原照片像素，保留插画、反光、模糊和磨损，无AI重绘、OCR、补画或颜色替换。',
            '真实轮廓是两角外突圆弧与两处内凹的异形薄板；手工轮廓为照片近似，不能当作实测刀模。',
            '主体直边延长线定义透视框，校正画布扩展到包含全部轮廓；textureJobs.quad是扩展虚拟外接框，sourceOutline是真实可见外边。',
            '校正PNG矩形内仍有桌面像素，实际凹多边形厚壳通过匹配UV只显示轮廓内像素；外沿可能保留少量照片阴影或桌面。',
            '纵向印刷保留：7个浅色轨道圈与左下红色登陆圈；右上起点为远景船，另6浅色圈为线描船。',
            '未提供背面；backTexture为null，未知底面和纸边使用明确中性材质，未借用其他卡背。',
            '190x200x1.5mm仅工作估算；主体框宽高比0.61用近圆形印刷辅助观察，实体比例、厚度、纸面材质与版本未知。',
            '这是静态轨道视觉资产，轨道圈不是本子任务制作的交互控件；规则语义为蛮族入侵，不按海盗文件名或旧版照片改变规则。',
        ],
        'references': [{'originalPath': ORIGINAL, 'sha256': hashlib.sha256(original.read_bytes()).hexdigest(),
                        'role': 'visible front of barbarian invasion track; filename says pirate invasion and is retained unchanged'}],
        'textureJobs': [{'source': ORIGINAL, 'output': FRONT,
                         'quad': np.round(quad, 8).tolist(), 'size': [1946,2048], 'rotation': 0}],
        'photoGeometry': {'sourceSize': [3072,4096], 'inspectionDisplaySize': [1368,1824],
                          'bodyQuad': BODY_QUAD, 'quadMeaning': 'expanded projective enclosing canvas TL/TR/BR/BL, not four physical paper corners',
                          'sourceOutline': np.round(source_outline, 8).tolist(),
                          'outlineCoordinateOrigin': 'PNG top left; normalized image u,v',
                          'bodyFrameAspectRatioEstimate': .61, 'estimatedCorrectedOutlineAspectRatio': ratio},
        'assets': [{'id': 'barbarian-track', 'label': '蛮族入侵轨道板（原文件海盗入侵卡）',
                    'kind': 'custom', 'role': 'panel', 'rulesRole': 'barbarian-invasion-track',
                    'dimensionsMm': [190,200,1.5], 'frontTexture': FRONT, 'backTexture': None,
                    'customBuilder': 'art/recipes/entity-conversion-v001/barbarian-track/build_track.py',
                    'panelOutline': np.round(outline, 9).tolist()}],
    }
    folder = Path(__file__).parent
    (folder/'recipe.json').write_text(json.dumps(recipe, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    image = ImageOps.exif_transpose(Image.open(original)).convert('RGB')
    image = image.resize((1368,1824))
    draw = ImageDraw.Draw(image)
    draw.line(OUTLINE_DISPLAY + [OUTLINE_DISPLAY[0]], fill=(255,30,40), width=2)
    for x,y in OUTLINE_DISPLAY:
        draw.ellipse((x-2,y-2,x+2,y+2), fill=(255,255,0))
    draw.line([(x/(3072/1368),y/(3072/1368)) for x,y in BODY_QUAD+BODY_QUAD[:1]], fill=(20,255,20), width=2)
    image.save(folder/'source-outline-inspection.png')
    print('CATAN_TRACK_RECIPE_AUTHORED', len(outline), 'outline points', 'ratio', ratio)


if __name__ == '__main__':
    author()
