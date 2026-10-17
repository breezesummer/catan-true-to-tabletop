"""把公开地图定义与已保留的实体纹理排版为菜单示意图，不读取任何对局存档。"""
import argparse
import hashlib
import json
import math
from functools import lru_cache
from pathlib import Path

import PIL
from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageEnhance

REPO = Path(__file__).resolve().parents[3]
RES = REPO / 'Unity/Catan/Assets/Resources'
HERE = Path(__file__).resolve().parent
RULES = ['base', 'seafarers', 'cities-knights', 'combined']
NAMES = ['基础版', '航海家', '城市与骑士', '双扩展组合']
W, H = 1024, 640
SOURCES = {}
FONT_PATH = RES / 'Fonts/NotoSerifCJKsc-Regular.otf'


def source(path):
    path = Path(path)
    key = path.relative_to(REPO).as_posix()
    if key not in SOURCES:
        SOURCES[key] = hashlib.sha256(path.read_bytes()).hexdigest()
    return path


@lru_cache(maxsize=64)
def font(size):
    return ImageFont.truetype(str(source(FONT_PATH)), size)


def text(draw, xy, value, size, color, anchor=None):
    draw.text(xy, value, font=font(size), fill=color, anchor=anchor, stroke_width=0)


def hexagon(x, y, r):
    return [(x + math.sin(k * math.pi / 3) * r, y - math.cos(k * math.pi / 3) * r) for k in range(6)]


@lru_cache(maxsize=4)
def background(rule):
    image = Image.new('RGB', (W, H))
    pix = image.load()
    shades = [(20, 61, 64), (17, 61, 78), (31, 53, 64), (20, 55, 68)]
    base = shades[rule]
    for y in range(H):
        for x in range(W):
            light = 10 * math.exp(-((x - 420) ** 2 + (y - 260) ** 2) / 180000)
            grain = ((x * 17 + y * 13) % 11 - 5) * .14
            pix[x, y] = tuple(max(0, min(255, int(c + light + grain))) for c in base)
    d = ImageDraw.Draw(image)
    # 类航海图的海面线条，保持地块视觉优先。
    for row in range(12):
        for col in range(20):
            x, y = col * 61 + (row % 2) * 23, row * 51 + 22
            d.arc((x, y, x + 19, y + 5), 0, 150, fill=(46, 89, 94), width=1)
    d.rounded_rectangle((12, 12, W - 13, H - 13), radius=21, outline=(130, 141, 117), width=1)
    d.line((34, H - 91, W - 34, H - 91), fill=(85, 120, 114), width=1)
    # 罗盘只作背景装饰。
    x, y = 930, 89
    d.ellipse((x - 23, y - 23, x + 23, y + 23), outline=(90, 128, 122))
    d.polygon([(x, y - 34), (x + 6, y), (x, y + 29), (x - 6, y)], fill=(138, 161, 142))
    d.line((x - 32, y, x + 32, y), fill=(93, 129, 123))
    return image.convert('RGBA')


TILE_PATHS = {
    'wood': 'PhotoSamples/v001/forest-top.png',
    'brick': 'EntityAssets/v001/tiles/tile-hills-top.png',
    'wool': 'EntityAssets/v001/tiles/tile-pasture-top.png',
    'wheat': 'EntityAssets/v001/tiles/tile-fields-top.png',
    'ore': 'EntityAssets/v006/ore-tile/tile-ore-top.png',
    'gold': 'EntityAssets/v001/tiles/tile-gold-top.png',
    'desert': 'EntityAssets/v001/tiles/tile-desert-top.png',
    'sea': 'EntityAssets/v001/tiles/tile-sea-top.png',
}
TILES = {}


def tile_texture(resource, width, height):
    if resource not in TILES:
        tile = Image.open(source(RES / TILE_PATHS[resource])).convert('RGB')
        # 已有源纹理使用完整正六边形外接正方形；外侧背景不进入最终蒙版。
        margin = round(tile.width * .06699)
        tile = tile.crop((margin, 0, tile.width - margin, tile.height))
        tile = ImageEnhance.Color(tile).enhance(1.14)
        tile = ImageEnhance.Brightness(tile).enhance(1.15)
        TILES[resource] = tile
    return TILES[resource].resize((width, height), Image.Resampling.LANCZOS)


def draw_tile(image, tile, x, y, radius):
    resource = tile['resource']
    d = ImageDraw.Draw(image)
    points = hexagon(x, y, radius - 1)
    # 桌面组件厚度和浅色倒角。
    d.polygon([(px, py + 6) for px, py in points], fill=(19, 36, 35))
    if resource == 'fog':
        d.polygon(points, fill=(167, 190, 188), outline=(209, 219, 202), width=2)
        mist = Image.new('RGBA', image.size)
        md = ImageDraw.Draw(mist)
        for ox, oy, rr in [(-.2, .0, .43), (.2, -.1, .48), (.0, .25, .41)]:
            md.ellipse((x + (ox - rr) * radius, y + (oy - rr) * radius, x + (ox + rr) * radius, y + (oy + rr) * radius), fill=(229, 235, 220, 190))
        image.alpha_composite(mist.filter(ImageFilter.GaussianBlur(3)))
        text(ImageDraw.Draw(image), (x, y - 2), '?', max(16, int(radius * .55)), (71, 101, 105), 'mm')
        return
    # 图像只用于二维地图图表，不作为三维网格资产。
    left, top = int(x - radius * .866), int(y - radius)
    width, height = max(1, int(radius * 1.732)), max(1, int(radius * 2))
    texture = tile_texture(resource, width, height)
    mask = Image.new('L', (width, height))
    ImageDraw.Draw(mask).polygon([(px - left, py - top) for px, py in points], fill=255)
    image.paste(texture, (left, top), mask)
    d = ImageDraw.Draw(image)
    d.line(points + [points[0]], fill=(185, 181, 145) if resource != 'sea' else (70, 128, 133), width=2)
    if resource == 'sea':
        # 海域保留原有海浪纹理，略罩青色使陆地更易辨认。
        wash = Image.new('RGBA', image.size)
        ImageDraw.Draw(wash).polygon(points, fill=(22, 87, 106, 90))
        image.alpha_composite(wash)
    elif tile.get('number'):
        r = max(10, radius * .22)
        cy = y + radius * .16
        d.ellipse((x - r, cy - r + 2, x + r, cy + r + 2), fill=(75, 65, 43))
        d.ellipse((x - r, cy - r, x + r, cy + r), fill=(244, 229, 191), outline=(148, 129, 86), width=1)
        col = (161, 53, 37) if tile['number'] in (6, 8) else (50, 50, 40)
        text(d, (x, cy - 1), str(tile['number']), max(12, int(r * 1.35)), col, 'mm')


def paste_piece(image, kind, center, size, color='red'):
    piece = Image.open(source(RES / f'OperationUI/v002/pieces/{kind}-{color}.png')).convert('RGBA')
    box = piece.getbbox()
    piece = piece.crop(box)
    piece.thumbnail((size, size), Image.Resampling.LANCZOS)
    x, y = int(center[0] - piece.width / 2), int(center[1] - piece.height / 2)
    shadow = Image.new('RGBA', image.size)
    sd = ImageDraw.Draw(shadow)
    sd.ellipse((x + 3, y + piece.height - 14, x + piece.width + 8, y + piece.height + 5), fill=(1, 10, 10, 130))
    image.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(6)))
    image.alpha_composite(piece, (x, y))


def render_map(entry):
    rule, players = entry['rule'], entry['players']
    image = background(rule).copy()
    tiles = entry['tiles']
    positions = [(math.sqrt(3) * (t['q'] + t['r'] / 2), 1.5 * t['r']) for t in tiles]
    lowx, highx = min(x for x, y in positions) - .9, max(x for x, y in positions) + .9
    lowy, highy = min(y for x, y in positions) - 1, max(y for x, y in positions) + 1
    radius = min(822 / (highx - lowx), 445 / (highy - lowy))
    ox, oy = 503 - (highx + lowx) * radius / 2, 306 - (highy + lowy) * radius / 2
    for tile, (x, y) in zip(tiles, positions):
        draw_tile(image, tile, ox + x * radius, oy + y * radius, radius)
    # 特殊公开地点采用固定符号，不展示任何玩家的真实布置。
    d = ImageDraw.Draw(image)
    for key, col in [('villages', (239, 209, 125)), ('fortresses', (209, 78, 56)), ('wonders', (111, 204, 191))]:
        for vertex in entry.get(key, []):
            x, y = ox + vertex['x'] * math.sqrt(3) / 2 * radius, oy + vertex['y'] / 2 * radius
            d.rounded_rectangle((x - 7, y - 7, x + 7, y + 7), radius=2, fill=col, outline=(245, 238, 208), width=2)
    d.rounded_rectangle((36, 31, 199, 69), radius=13, fill=(231, 221, 182))
    text(d, (117, 49), f'{players} 人 · 示意', 22, (36, 66, 60), 'mm')
    name = entry['name'].replace(' · 城市与骑士', '')
    text(d, (38, 565), name, 28, (246, 237, 208))
    caption = '云雾中保留未知' if entry['id'] == 'fog-islands' else '开局随机 · 此图为范例' if entry['randomExample'] else '公开地图 · 地形布局'
    text(d, (39, 607), caption, 16, (179, 203, 193))
    if rule == 2:
        paste_piece(image, 'city', (910, 579), 77, 'beige')
        paste_piece(image, 'knight-3', (974, 580), 75)
    elif rule == 3:
        paste_piece(image, 'ship', (904, 581), 76, 'beige')
        paste_piece(image, 'knight-3', (972, 581), 74)
    else:
        text(d, (980, 597), NAMES[rule], 22, (213, 218, 186), 'rm')
    return image.convert('RGB')


def render_rule(rule, maps):
    chosen = next(e for e in maps if e['rule'] == rule and e['players'] == 4 and e['id'] in ('standard', 'heading-for-new-shores'))
    image = render_map(chosen).convert('RGBA')
    # 玩法封面将棋盘缩成背景，前景采用项目原有实体棋子渲染图。
    overlay = Image.new('RGBA', image.size)
    od = ImageDraw.Draw(overlay)
    od.rounded_rectangle((43, 353, 981, 542), radius=29, fill=(12, 38, 39, 145))
    image.alpha_composite(overlay)
    kinds = [('settlement', 'road', 'city'), ('ship', 'settlement', 'ship'), ('wall', 'city', 'knight-3'), ('ship', 'city', 'knight-3')][rule]
    colors = [('red', 'beige', 'blue'), ('red', 'beige', 'blue'), ('beige', 'red', 'blue'), ('red', 'beige', 'blue')][rule]
    for i, (kind, color) in enumerate(zip(kinds, colors)):
        paste_piece(image, kind, (265 + i * 250, 425), 185, color)
    d = ImageDraw.Draw(image)
    d.rectangle((30, 552, 994, 626), fill=(20, 57, 60))
    text(d, (39, 562), NAMES[rule], 31, (248, 238, 207))
    subtitles = ['村庄与道路 · 丰收与交易', '扬帆起航 · 发现新海岸', '坚固城墙 · 骑士与城邦', '船帆与骑士 · 同一片山海']
    text(d, (983, 591), subtitles[rule], 23, (197, 216, 197), 'rm')
    d.rounded_rectangle((31, 25, 232, 75), radius=14, fill=(224, 214, 174))
    text(d, (130, 49), '玩法内容示意', 23, (37, 66, 59), 'mm')
    return image.convert('RGB')


def main():
    parser = argparse.ArgumentParser(description='生成公开地图与玩法封面。')
    parser.add_argument('--output', type=Path, default=REPO / 'art/exports/menu-previews-v001')
    parser.add_argument('--install', action='store_true', help='同时写入 Unity Resources 的版本化目标目录。')
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    maps = json.loads(source(HERE / 'public-layouts.json').read_text(encoding='utf-8'))['maps']
    for recipe in ('render_previews.py', 'ExportPreviews.cs', 'ExportPreviews.csproj'):
        source(HERE / recipe)
    manifest = {'schemaVersion': 1, 'version': 'menu-previews-v001', 'tool': f'Python + Pillow {PIL.__version__}', 'privacy': '纯公开静态定义；迷雾格保持未知；与真实对局随机种子无关。', 'images': []}
    images = []
    for entry in sorted(maps, key=lambda x: (x['rule'], x['id'], x['players'])):
        key = f"{RULES[entry['rule']]}-{entry['id']}-{entry['players']}"
        image = render_map(entry)
        item = {'key': key, 'kind': 'map', 'rule': entry['rule'], 'scenarioId': entry['id'], 'playerCount': entry['players'], 'sourceFile': entry['sourceFile'], 'sourcePage': entry['sourcePage'], 'tileCount': len(entry['tiles']), 'fogTileCount': sum(t['resource'] == 'fog' for t in entry['tiles'])}
        images.append((key, image, item))
    for rule in range(4):
        images.append(('rule-' + RULES[rule], render_rule(rule, maps), {'key': 'rule-' + RULES[rule], 'kind': 'rule', 'rule': rule}))
    install = RES / 'MenuPreviews/v001'
    if args.install:
        install.mkdir(parents=True, exist_ok=True)
    for key, image, item in images:
        target = args.output / (key + '.png')
        image.save(target, optimize=True)
        item.update(width=W, height=H, sha256=hashlib.sha256(target.read_bytes()).hexdigest())
        manifest['images'].append(item)
        if args.install:
            (install / target.name).write_bytes(target.read_bytes())
    cols, cw, ch = 4, 307, 212
    contact = Image.new('RGB', (cols * cw, math.ceil(len(images) / cols) * ch), (234, 235, 220))
    cd = ImageDraw.Draw(contact)
    for i, (key, image, item) in enumerate(images):
        x, y = i % cols * cw, i // cols * ch
        contact.paste(image.resize((299, 187), Image.Resampling.LANCZOS), (x + 4, y + 4))
        label = NAMES[item['rule']] + (' · ' + str(item['playerCount']) + ' 人' if item['kind'] == 'map' else ' · 封面')
        text(cd, (x + 7, y + 192), label, 13, (32, 62, 57))
    contact.save(args.output / 'contact-sheet.jpg', quality=93)
    manifest['sources'] = SOURCES
    (args.output / 'manifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding='utf-8')
    print(f'已生成 {len(images)} 张预览，覆盖 {len(maps)} 个地图选择；总览：{args.output / "contact-sheet.jpg"}')


if __name__ == '__main__':
    main()
