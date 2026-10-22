"""Author the fixed recipe from manually inspected original-photo corners.

Corner readings used a 1279 x 1706 inspection copy for every portrait source;
the three larger originals are mapped back to original pixel coordinates here.
This writes metadata only. Pixel correction is performed by the shared
rectify_entity_photos.py; no generated or reconstructed image content is used.
"""
import hashlib
import json
from pathlib import Path
from PIL import Image, ImageOps

REPO = Path(__file__).resolve().parents[4]
TYPE = 'city-improvements'
FOLDER = REPO / 'assets/references/inbox/城市设施'
SOURCE = 'art/source/entity-conversion-v001/' + TYPE + '/textures/'

# TL, TR, BR, BL; straight outer-edge intersections, before rounded corners.
# Order: science 1..5, politics 1..5, trade 1..5; back, front for each.
QUADS = [
    [[531,666],[982,657],[996,1108],[534,1115]],
    [[425,584],[939,566],[968,1079],[442,1098]],
    [[457,564],[936,557],[948,1037],[457,1038]],
    [[393,633],[884,616],[905,1116],[402,1130]],
    [[317,611],[833,596],[852,1105],[326,1128]],
    [[410,606],[892,587],[917,1071],[416,1088]],
    [[527,632],[1069,625],[1076,1173],[524,1172]],
    [[374,513],[891,475],[938,990],[410,1033]],
    [[446,559],[961,558],[968,1080],[439,1075]],
    [[360,623],[904,597],[941,1150],[383,1180]],
    [[458,679],[917,650],[952,1108],[481,1140]],
    [[424,687],[853,667],[875,1098],[442,1115]],
    [[424,668],[947,669],[955,1193],[418,1197]],
    [[321,544],[847,510],[881,1042],[343,1075]],
    [[330,555],[810,575],[791,1062],[296,1043]],
    [[410,622],[932,608],[958,1134],[421,1156]],
    [[412,568],[877,565],[883,1030],[403,1032]],
    [[348,578],[835,578],[838,1068],[335,1067]],
    [[424,599],[915,595],[919,1085],[418,1086]],
    [[403,673],[921,662],[940,1186],[407,1196]],
    [[416,629],[864,629],[870,1079],[411,1078]],
    [[379,566],[816,566],[817,1002],[373,1000]],
    [[529,657],[985,675],[970,1137],[507,1114]],
    [[459,522],[927,510],[943,989],[467,998]],
    [[494,691],[985,681],[1003,1176],[495,1187]],
    [[484,550],[982,554],[986,1055],[479,1052]],
    [[416,657],[900,664],[900,1149],[405,1145]],
    [[417,679],[903,663],[923,1159],[423,1170]],
    [[428,477],[900,476],[912,941],[436,949]],
    [[359,589],[839,571],[858,1055],[370,1070]],
]

GROUPS = [
    ('science', '绿色', ['修道院','图书馆','引水渠','剧院','大学'],
     ['修道院','圖書館','引水渠','劇場','大學']),
    ('politics', '蓝色', ['市政厅','教堂','要塞','大教堂','卡坦议会'],
     ['市政廳','教堂','要塞','大教堂','卡坦議會']),
    ('trade', '黄色', ['市场','行会','同业公会','银行','商业中心'],
     ['市場','行會','同業公會','銀行','商業中心']),
]


def author():
    references, jobs, assets = [], [], []
    corner_index = 0
    for group, color, file_titles, print_titles in GROUPS:
        for level, (file_title, print_title) in enumerate(zip(file_titles, print_titles), 1):
            asset_id = f'improvement-{group}-{level}'
            for suffix, side in [('反面','back'), ('正面','front')]:
                name = f'{color}{level}级{file_title}{suffix}.jpg'
                original = FOLDER / name
                width, height = ImageOps.exif_transpose(Image.open(original)).size
                quad = [[round(x*width/1279, 3), round(y*height/1706, 3)] for x,y in QUADS[corner_index]]
                corner_index += 1
                path = original.relative_to(REPO).as_posix()
                references.append({'originalPath': path,
                                   'sha256': hashlib.sha256(original.read_bytes()).hexdigest(),
                                   'role': f'{group} level {level} {side}; original print and photo pixels'})
                jobs.append({'source': path, 'output': SOURCE+asset_id+'-'+side+'.png',
                             'quad': quad, 'size': [1024,1024], 'rotation': 0})
            assets.append({'id': asset_id, 'label': f'{color}{level}级{print_title}',
                           'kind':'panel', 'dimensionsMm':[40,40,1],
                           'frontTexture': SOURCE+asset_id+'-front.png',
                           'backTexture': SOURCE+asset_id+'-back.png'})
    for board_id, filename, label, quad in [
        ('guidance-upgrade','城市设施升级卡.jpg','城市设施升级组合提示板',
         [[59,288],[1608,318],[1654,1003],[25,1012]]),
        ('guidance-max','城市设施满级卡.jpg','城市设施满级组合提示板',
         [[56,299],[1646,312],[1694,1027],[19,1039]]),
    ]:
        original = FOLDER/filename
        path = original.relative_to(REPO).as_posix()
        references.append({'originalPath':path,'sha256':hashlib.sha256(original.read_bytes()).hexdigest(),
                           'role':'assembled three-colour photo board; visible side only'})
        jobs.append({'source':path,'output':SOURCE+board_id+'-front.png',
                     'quad':quad,'size':[2304,1024],'rotation':0})
        assets.append({'id':board_id,'label':label,'kind':'panel','dimensionsMm':[180,80,1],
                       'frontTexture':SOURCE+board_id+'-front.png'})
    recipe = {
        'schemaVersion':1,'typeId':TYPE,'label':'城市设施十五种双面翻板与两种组合提示板',
        'seed':20261009,
        'scaleStatus':'estimated; level panels 40 x 40 x 1 mm; photo guidance boards 180 x 80 x 1 mm; not physically measured or user-approved',
        'limitations':[
            '人工读取原照片四边交点，PIL投影校正原像素；保留原插画、繁体印刷、数字、曝光、模糊和反光，不使用AI重绘、OCR替字、锐化或颜色替换。',
            '十五等级翻板为近正方形双面薄panel；四角圆角在工作网格中尚未建模，外角可能残留极少桌面像素。',
            '两张组合提示板贴图只裁取可见组件整体边界；照片内部孔洞、组件间隙和已摆放翻板仍属于原照片像素，不是可操作城市建设控件。',
            '组合提示板未见底面，使用明确中性材质；其整板拼装厚度、铰链结构、卡槽、真实尺寸与颜色标准均未知。',
            '照片中的设施题名与旧版规则提示仅作视觉来源；不改变已锁定官方2025规则。',
            '1024与2304像素导出包含原照片采样放大，不增加真实细节；高分辨率原照片与所有32个源文件保持不变。'
        ],
        'references':references,'textureJobs':jobs,'assets':assets,
    }
    (Path(__file__).parent/'recipe.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print('CITY_IMPROVEMENTS_RECIPE', len(references), len(jobs), len(assets))


if __name__ == '__main__':
    author()
