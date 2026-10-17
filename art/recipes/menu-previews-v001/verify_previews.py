"""检查菜单覆盖、公开数据边界、独立规则资料计数和最终图片，不依赖 Unity。"""
from collections import Counter
import argparse
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageStat

REPO = Path(__file__).resolve().parents[3]
RULES = ['base', 'seafarers', 'cities-knights', 'combined']
SCENARIOS = ['heading-for-new-shores', 'four-islands', 'fog-islands', 'through-the-desert', 'forgotten-tribe', 'cloth-for-catan', 'pirate-islands', 'wonders-of-catan', 'new-world']
COMBINED = ['heading-for-new-shores', 'through-the-desert']


def run(output):
    checks = 0
    def require(condition, reason):
        nonlocal checks
        checks += 1
        if not condition:
            raise AssertionError(reason)
    public = json.loads((REPO / 'art/recipes/menu-previews-v001/public-layouts.json').read_text(encoding='utf-8'))
    manifest = json.loads((output / 'manifest.json').read_text(encoding='utf-8'))
    expected = {(rule, id, people) for rule in range(4) for id in (SCENARIOS if rule == 1 else COMBINED if rule == 3 else ['standard']) for people in (3, 4)}
    actual = {(m['rule'], m['id'], m['players']) for m in public['maps']}
    require(len(public['maps']) == 26 and actual == expected, '地图定义必须覆盖全部 26 种选择且无重复。')
    expected_keys = {f'{RULES[r]}-{s}-{n}' for r, s, n in expected} | {'rule-' + r for r in RULES}
    require(len(manifest['images']) == 30 and {i['key'] for i in manifest['images']} == expected_keys, '图片清单必须覆盖 26 张地图和 4 张玩法。')
    require(len({i['sha256'] for i in manifest['images']}) == 30, '不同选择必须包含自己的图片内容或人数标识。')
    for m in public['maps']:
        require(set(m) == {'rule', 'id', 'players', 'name', 'sourceFile', 'sourcePage', 'randomExample', 'tiles', 'villages', 'fortresses', 'wonders'}, '地图包含未批准的额外字段。')
        require(all(set(t) == {'id', 'q', 'r', 'resource', 'number'} for t in m['tiles']), '地块包含未批准的隐藏数据。')
        require(all(t['resource'] in ['wood', 'brick', 'wool', 'wheat', 'ore', 'gold', 'desert', 'sea', 'fog'] for t in m['tiles']), '地形未正确识别。')
        positions = [(math.sqrt(3) * (t['q'] + t['r'] / 2), 1.5 * t['r']) for t in m['tiles']]
        lx, hx = min(x for x, y in positions) - .9, max(x for x, y in positions) + .9
        ly, hy = min(y for x, y in positions) - 1, max(y for x, y in positions) + 1
        radius = min(822 / (hx - lx), 445 / (hy - ly))
        ox, oy = 503 - (hx + lx) * radius / 2, 306 - (hy + ly) * radius / 2
        require(ox + lx * radius >= .18 * 1024 and ox + hx * radius <= .82 * 1024 and oy + ly * radius >= .11 * 640 and oy + hy * radius + 7 <= .85 * 640, '缩略图裁切会丢失地图边缘：' + m['id'])
        if m['id'] == 'fog-islands':
            fog = [t for t in m['tiles'] if t['resource'] == 'fog']
            require(len(fog) == 12 and all(t['number'] is None for t in fog), '迷雾应保留 12 个未知格且不得带点数。')
        if m['rule'] in (0, 2):
            require(len(m['tiles']) == 19 and Counter(t['resource'] for t in m['tiles']) == Counter(wood=4, wool=4, wheat=4, ore=3, brick=3, desert=1), '基础岛屿资源组成与独立基线不一致。')
    # 该来源计数表早于本轮预览功能，由官方 PDF 的组件表独立转录。
    reference = json.loads((REPO / 'docs/acceptance/m3-v001/scenario-source-counts.json').read_text(encoding='utf-8'))
    for case in reference['cases']:
        for players in case['players'] if isinstance(case['players'], list) else [case['players']]:
            m = next(m for m in public['maps'] if m['rule'] == 1 and m['id'] == case['id'] and m['players'] == players)
            counts = Counter(t['resource'] for t in m['tiles'])
            require([counts[r] for r in reference['resourceOrder']] == case['resources'], '地图地形数量与 PDF 独立记录不一致：' + case['id'])
            require(m['sourcePage'] == case['page'], '规则来源页码不一致：' + case['id'])
    for item in manifest['images']:
        path = output / (item['key'] + '.png')
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        require(digest == item['sha256'], '预览导出哈希不一致：' + item['key'])
        installed = REPO / 'Unity/Catan/Assets/Resources/MenuPreviews/v001' / path.name
        require(hashlib.sha256(installed.read_bytes()).hexdigest() == digest, 'Unity 资源不等于已验收导出：' + item['key'])
        with Image.open(path) as image:
            require(image.size == (1024, 640) and image.mode == 'RGB', '图片规格不正确：' + item['key'])
            require(min(ImageStat.Stat(image).stddev) > 25, '图片可能空白或丢失内容：' + item['key'])
    for path, digest in manifest['sources'].items():
        require(hashlib.sha256((REPO / path).read_bytes()).hexdigest() == digest, '配方来源发生变化，需重新生成：' + path)
    print(f'预览验证通过：{checks} 项检查；26 种地图选择；4 张玩法图；迷雾保持未知。')
    return {'status': 'passed', 'checks': checks, 'maps': 26, 'rules': 4, 'fogProtected': True, 'sourceCountsChecked': True, 'unityRuntimeChecked': False}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='验证所有地图与扩展预览。')
    parser.add_argument('--output', type=Path, default=REPO / 'art/exports/menu-previews-v001')
    args = parser.parse_args()
    result = run(args.output)
    (args.output / 'verification.json').write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding='utf-8')
