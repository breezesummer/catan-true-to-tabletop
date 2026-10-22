"""Register this 18-reference batch, materials and immutable v001 ancestry."""
from pathlib import Path
import hashlib
import json

DIRECTORY=Path(__file__).resolve().parent
REPO=DIRECTORY.parents[3]
BASE=DIRECTORY.relative_to(REPO).as_posix()
COLOURS=[('orange','橙色',[.95,.23,.018]),('beige','米色',[.82,.70,.43]),('blue','蓝色',[.008,.024,.12])]
KINDS=[('village','村庄',[12,12,16],'village'),('city','城市',[18,18,20],'city'),
       ('metropolis','大都会',[18,18,24],'metropolis'),('city-wall','城墙',[24,24,5],'wall'),
       ('city-with-wall','城市+城墙',[24,24,20],'combination'),
       ('metropolis-with-wall','大都会+城墙',[24,24,24],'combination')]
references=[]
assets=[]
for colour, chinese, linear in COLOURS:
    for kind,label,dimensions,builder in KINDS:
        path='assets/references/inbox/建筑/'+chinese+label+'.jpg'
        digest=hashlib.sha256((REPO/path).read_bytes()).hexdigest()
        references.append({'originalPath':path,'sha256':digest,'role':colour+'-'+kind+'-shape-and-plastic-colour'})
        args={'color':linear,'playerColor':colour}
        if kind=='city-wall':
            args['innerDiameterMm']=18.6
        if builder=='combination':
            args['bodyKind']='city' if kind=='city-with-wall' else 'metropolis'
        assets.append({'id':kind+'-'+colour,'label':chinese+('独立城墙' if kind=='city-wall' else label),
                       'kind':'custom','dimensionsMm':dimensions,'customBuilder':BASE+'/'+builder+'_builder.py',
                       'builderArgs':args,'referencePaths':[path]})
recipe={
    'schemaVersion':1,'typeId':'player-buildings','label':'三色玩家建筑（18张实体照片参考）',
    'seed':20261010,'scaleStatus':'estimate-not-measured','references':references,'assets':assets,
    'limitations':[
        '18张原照片已通过逐色6图检视表实际查看；没有尺子，真实尺寸、底面和多数背面未知。',
        '村庄12×12×16、城市18×18×20、大都会18×18×24、城墙24×24×5mm均为沿用开发基线的估算。',
        '城市、大都会和城墙复用并复制v001参数化几何到新版本；三色照片显示相同实体模具轮廓，不是扫描或摄影测量。',
        '村庄沿用照片样板的成组屋顶、烟囱、台阶和浮雕布局，新照片可见圆润相邻底座，采用封闭圆底近似。',
        '独立城墙与两种建筑+城墙组合均为实际3D网格；组合包含两件底心对齐网格，未用整块不透光圆柱填充内孔。',
        '城墙名义内径18.6mm、八圆塔及32墙垛为估算；背部和内侧被遮挡细节仍按重复布局近似。',
        '色值为观察原照得到的橙色、暖米色及深蓝色线性RGB候选，受照片曝光/白平衡影响，不是实物测色。',
        '同材质部件为相交封闭壳，未执行实体布尔并集；可用于游戏渲染，不声明可直接3D打印。',
        'Unity正式灯光、拥挤节点拾取与用户外观确认由主任务独立验证。'
    ]}
(DIRECTORY/'recipe.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
ancestry=[]
for kind,name in [('city','city'),('metropolis','metropolis'),('city-wall','wall')]:
    original=REPO/'art/recipes/entity-conversion-v001'/kind/'builder.py'
    copy=DIRECTORY/(name+'_builder.py')
    ancestry.append({'originalPath':original.relative_to(REPO).as_posix(),
                     'originalSha256':hashlib.sha256(original.read_bytes()).hexdigest(),
                     'copiedPath':copy.relative_to(REPO).as_posix(),
                     'copiedSha256':hashlib.sha256(copy.read_bytes()).hexdigest(),
                     'bytesIdentical':original.read_bytes()==copy.read_bytes()})
(DIRECTORY/'builder-source-provenance.json').write_text(json.dumps({'copiedBuilders':ancestry,
    'villageAncestry':'tools/art/generate_photo_samples.py make_village/gable; adapted rounded footings and deterministic UV',
    'villageAncestrySha256':hashlib.sha256((REPO/'tools/art/generate_photo_samples.py').read_bytes()).hexdigest()},
    ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
