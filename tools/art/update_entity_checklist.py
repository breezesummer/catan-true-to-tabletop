"""Update the human checklist from per-type delivered evidence, never intent."""
import argparse
import json
import hashlib
from datetime import datetime, timezone, timedelta
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
VERSION = 'entity-conversion-v001'
POLITICS_VERSION = 'entity-conversion-v003'
ORE_VERSION = 'entity-conversion-v006'
INTEGRATION_EVIDENCE = REPO / 'docs/verification/entity-integration-v003'
ORE_EVIDENCE = REPO / 'docs/verification/mountain-tile-v006'
LABELS = {
    'tiles': '地图板块（其余六类）', 'resource-cards': '资源卡（其余四类）',
    'commodity-cards': '商品卡', 'progress-trade': '贸易进步卡（黄色）',
    'progress-science': '科学进步卡（绿色）', 'progress-politics': '政治进步卡（蓝色）',
    'city-improvements': '城市设施与升级提示板', 'defender-card': '卡坦保卫者奖励卡',
    'barbarian-track': '蛮族入侵轨道板', 'ships': '玩家船', 'city': '城市',
    'city-wall': '城墙', 'metropolis': '大都会', 'knights': '骑士',
    'robber': '强盗', 'pirate': '海盗', 'barbarian-ship': '蛮族入侵船', 'merchant': '商人',
}
DETAILS = {
    'tiles': '草地、海洋、金矿、沙漠、小麦、丘陵；六边形纸板网格＋校正顶面',
    'resource-cards': '羊毛、砖头、小麦、矿石；四张双面卡＋原照片资源背面',
    'commodity-cards': '布匹、纸张、银币；背面缺参考，保留中性底色',
    'progress-trade': '六种正面＋黄色共同背面',
    'progress-science': '十种正面＋绿色共同背面',
    'progress-politics': '九种正面；蓝色共同背面已提供，新版状态见 v003',
    'city-improvements': '三轨道×五等级×正反面，另两张提示板照片',
    'defender-card': '一张正面；背面缺参考',
    'barbarian-track': '文件名“海盗入侵卡”；显式登记为蛮族轨道，背面未知',
    'ships': '红、蓝、橘、米色；船体、桅杆、弧帆、后舱',
    'city': '红色城市，底座、屋群与屋顶',
    'city-wall': '由城市＋城墙、大都会＋城墙组合照片估算独立城墙，隐藏面未知',
    'metropolis': '红色大都会；三轨道身份标记尚无照片',
    'knights': '三级×有无头盔，共六种外观；外观状态不代替规则状态',
    'robber': '两张照片是同一黑色雕塑的不同视角',
    'pirate': '海洋板块上的黑色海盗船',
    'barbarian-ship': '文件名“海盗（入侵卡上的单位）”；显式映射为蛮族船',
    'merchant': '中性色商人雕塑，单视角近似',
}


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig')) if path.exists() else {}


def delivered_type(version, key):
    export = REPO / 'art/exports' / version / key
    manifest = read(export / 'manifest.json')
    verification = read(export / 'verification.json')
    rebuild = read(export / 'clean-rebuild-verification.json')
    recipe = REPO / 'art/recipes' / version / key / 'recipe.json'
    rebuilt = (verification.get('cleanRebuild') or rebuild.get('cleanRebuild') or {}).get('sameGeometryMaterialsTexturesReferences', False)
    done = bool(manifest.get('assets')) and recipe.exists() and (recipe.parent / 'README.md').exists()
    done = done and hashlib.sha256(recipe.read_bytes()).hexdigest() == manifest.get('recipeSha256')
    done = done and verification.get('status') == 'passed' and rebuild.get('status') == 'passed' and rebuilt
    done = done and all((REPO / 'art/previews' / version / key / (view + '.png')).exists()
                        for view in ('top', 'side', 'game-camera', 'crowded-vertex'))
    return manifest, bool(done)


def coverage_evidence():
    inbox = REPO / 'assets/references/inbox'
    photos = {path.relative_to(REPO).as_posix(): path for path in inbox.rglob('*')
              if path.is_file() and path.suffix.lower() in ('.jpg', '.jpeg', '.png', '.webp')}
    references, ids = set(), []
    for version in (REPO / 'art/exports').iterdir():
        if not version.is_dir() or not version.name.startswith(('entity-conversion-v', 'photo-samples-v')):
            continue
        for path in version.rglob('manifest.json'):
            manifest = read(path)
            references.update(reference['originalPath'] for reference in manifest.get('references', []))
            ids.extend(asset['id'] for asset in manifest.get('assets', []))
    audit = read(REPO / 'art/recipes' / POLITICS_VERSION / 'progress-politics/photo-coverage-audit.json')
    audited = {entry['originalPath']: entry['sha256'] for entry in audit.get('photoCoverage', [])}
    current = set(audited) == set(photos) and all(hashlib.sha256(path.read_bytes()).hexdigest() == audited.get(name)
                                                  for name, path in photos.items())
    return {'photographs': len(photos), 'covered': len(set(photos) & references),
            'uniqueIds': len(set(ids)), 'retainedFbx': len(ids),
            'auditPassed': audit.get('status') == 'passed' and current}


def integration_passed():
    runtime = read(INTEGRATION_EVIDENCE / 'runtime-integration.json')
    smoke = read(INTEGRATION_EVIDENCE / 'm6-smoke.json')
    return runtime.get('status') == 'passed' and smoke.get('status') == 'passed'


def ore_evidence():
    manifest, converted = delivered_type(ORE_VERSION, 'ore-tile')
    ids = {asset['id'] for asset in manifest.get('assets', [])}
    imported = {asset['id'] for asset in read(ORE_EVIDENCE / 'unity-import.json').get('assets', []) if asset.get('verified')}
    runtime = read(ORE_EVIDENCE / 'runtime-integration.json')
    required_views = {mode + '/' + view for mode in ('base', 'seafarers', 'cities-knights', 'combined')
                      for view in ('top', 'game-camera')}
    runtime_passed = (runtime.get('status') == 'passed' and runtime.get('oreUsesMountainTileArt') is True
                      and runtime.get('oreCardUnchanged') is True
                      and required_views.issubset(runtime.get('views', []))
                      and all((ORE_EVIDENCE / (view + '.png')).exists() for view in required_views))
    imported_passed = ids == {'tile-ore'} and ids.issubset(imported)
    return {'converted': converted, 'imported': imported_passed,
            'runtimePassed': runtime_passed, 'passed': converted and imported_passed and runtime_passed}


def ore_section():
    evidence = ore_evidence()
    return ['', '## 高山地图板块 v006（2026-10-15，当前矿石地形）', '',
            '用户已提供真实高山六边形板块照片，替换 v004 矿石资源卡插画派生地形。'
            '原照经六角几何校正为 1024×1024 顶图，映射到估算 8 mm 厚板；'
            '保留山峰插画和黄色边框，完整矿石手牌仍使用原资源牌照片。', '',
            '| 转化与干净重建 | Unity 导入 | 四类正式显示入口 |', '| --- | --- | --- |',
            f"| {'已验证' if evidence['converted'] else '待验证'} | {'已验证' if evidence['imported'] else '待验证'} | "
            f"{'已验证' if evidence['passed'] else '待验证'} |", '',
            '[配方与复现](../art/recipes/entity-conversion-v006/ore-tile/README.md)；'
            '[回读](../art/exports/entity-conversion-v006/ore-tile/verification.json)；'
            '[干净重建](../art/exports/entity-conversion-v006/ore-tile/clean-rebuild-verification.json)；'
            '[Unity 导入](verification/mountain-tile-v006/unity-import.json)；'
            '[正式显示](verification/mountain-tile-v006/runtime-integration.json)；'
            '[画面核对](verification/mountain-tile-v006/visual-review.json)。', '',
            '运行通过要求 `oreUsesMountainTileArt=true`、`oreCardUnchanged=true`，且四类各含俯视与游戏相机证据。'
            '本轮验证范围以独立摘要为准；最终外观与 M8 退出条件仍待确认。'
            '[本轮说明](ART_MOUNTAIN_TILE_V006.md)。', '']


def integration_section(coverage):
    passed = integration_passed()
    status = '正式运行验证通过' if passed else '接入代码已完成；正式运行待验证'
    lines = ['', '## 全部现有照片的正式游戏接入 v003', '',
             '**' + status + '**。正式运行状态仅在 '
             '[显示入口记录](verification/entity-integration-v003/runtime-integration.json)与'
             '[M6 独立进程记录](verification/entity-integration-v003/m6-smoke.json)两者 `status=passed` 时更新。'
             '图库导入、照片覆盖和最终用户外观确认分别记录。', '',
             '| 验收层次 | 当前结果 |', '| --- | --- |',
             f"| 原照转化来源 | {coverage['covered']}/{coverage['photographs']} 张不重复原照进入导出来源；{'当前原照哈希审计通过' if coverage['auditPassed'] else '当前原照哈希审计待验证'} |",
             f"| 资产数量 | {coverage['uniqueIds']} 个唯一资产 ID，{coverage['retainedFbx']} 个保留版本 FBX；v003 九件政治卡替代旧版，不能重复计为新类型 |",
             '| Unity 导入与图库 | v001、v002 既有记录分批保留；v003 新政治卡导入独立列证，不将导入或图库视为整局验收 |',
             '| 正式游戏运行 | ' + status + '；范围为当前 M6 中基础版、航海家、城市与骑士和已支持组合 |',
             '| 最终用户外观 / M8 | 待确认；尺寸、隐藏面、色彩、节点操作、长期性能和发布条件仍未全部验收 |', '',
             '棋盘已接入八种有地块照片的板块；高山原照 v006 替换原 v004 资源卡插画派生矿石地形，证据见下方。'
             '资源卡显示于本人手牌面板；城市与骑士及组合增加商品、进步卡、城市设施和公开奖励卡面。'
             '悬停放大、点击固定查看、已有背面翻面；私有卡面限定当前 PlayerView，换座或隐私幕清空。'
             '船、强盗、海盗、商人和蛮族轨道/船使用实体参考模型。八种照片板块是有厚度的印刷板块网格，不代表立体景观。', '',
             '查看与重建：`tools/development/Play-M6.ps1`；'
             '`tools/development/Build-EntityIntegration.ps1`；`tools/validation/Test-EntityIntegration.ps1`。'
             '当前 M6 是本轮正式入口，旧独立阶段程序和图库记录按原批次保留。'
             '[接入说明与验证范围](ART_INTEGRATION_V003.md)。', '']
    return lines


def politics_section():
    manifest, done = delivered_type(POLITICS_VERSION, 'progress-politics')
    ids = {asset['id'] for asset in manifest.get('assets', [])}
    imported = {asset['id'] for asset in read(INTEGRATION_EVIDENCE / 'unity-import-v003.json').get('assets', [])
                if asset.get('verified')}
    return ['', '## 蓝色政治进步卡背面补转 v003', '',
            '遗漏的 `assets/references/inbox/发展卡/蓝色发展卡/卡背.jpg` 已在'
            '[新增来源登记](../assets/references/catalogs/blue-politics-back-v001.json)记录，原照片字节保留。'
            '九件卡的正面沿用 v001 冻结校正图，背面新增原像素几何校正；旧版产物保留。', '',
            f"| 转化 | 件数 | 证据 | Unity 导入 |", '| --- | --- | --- | --- |',
            f"| [{'x' if done else ' '}] | {len(ids)} | [配方](../art/recipes/{POLITICS_VERSION}/progress-politics/README.md)；"
            f"[回读](../art/exports/{POLITICS_VERSION}/progress-politics/verification.json)；"
            f"[干净重建](../art/exports/{POLITICS_VERSION}/progress-politics/clean-rebuild-verification.json) | "
            f"{'已验证' if ids and ids.issubset(imported) else '待验证'} |", '',
            '商品卡和保卫者卡背面仍缺参考，使用中性底色；蓝色政治卡共同背面已经补齐，不再列为缺参考。', '']


def player_components_section():
    version = 'entity-conversion-v002'
    evidence = REPO/'docs/verification'/version
    imported = {a['id'] for a in read(evidence/'unity-import.json').get('assets', []) if a.get('verified')}
    runtime = read(evidence/'player-components-verification.json')
    lines = ['', '## 三色玩家组件追加批次 v002', '',
             '2026-10-10 分类的橙色、米色、蓝色照片共 36 张，原文件名备份与 SHA-256 见'
             '[归档记录](../assets/references/catalogs/player-components-v001.json)。每色 6 种骑士外观、6 种建筑及城墙组合。'
             '旧版 v001 产物保留；本批次另存 v002，照片只作三维建模参考。', '',
             '| 进度 | 类型 | 资产数 | 证据 | Unity |', '| --- | --- | --- | --- | --- |']
    for key, label in [('player-knights', '三色骑士'), ('player-buildings', '三色建筑与城墙组合')]:
        folder = REPO/'art/exports'/version/key
        manifest = read(folder/'manifest.json')
        verification = read(folder/'verification.json')
        rebuild = read(folder/'clean-rebuild-verification.json')
        recipe = REPO/'art/recipes'/version/key/'recipe.json'
        ids = {a['id'] for a in manifest.get('assets', [])}
        done = (len(ids) == 18 and verification.get('status') == 'passed' and rebuild.get('status') == 'passed'
                and (rebuild.get('cleanRebuild') or {}).get('sameGeometryMaterialsTexturesReferences')
                and recipe.exists() and hashlib.sha256(recipe.read_bytes()).hexdigest() == manifest.get('recipeSha256')
                and (recipe.parent/'README.md').exists()
                and all((REPO/'art/previews'/version/key/(v+'.png')).exists()
                        for v in ('top', 'side', 'game-camera', 'crowded-vertex')))
        proof = f'[配方](../art/recipes/{version}/{key}/README.md)；[回读](../art/exports/{version}/{key}/verification.json)'
        lines.append(f"| [{'x' if done else ' '}] | {label} `{key}` | {len(ids)} | {proof} | {'已验证' if ids and ids.issubset(imported) else '待验证'} |")
    lines += ['', 'v002 公开视图渲染入口：' + ('已有验证通过记录' if runtime.get('status') == 'passed' else '接入验证中') +
              '。村庄、城市、城墙、大都会和骑士按玩家席位选择资产；规则与保存格式不变。'
              '本轮全资源正式运行状态见 v003；最终外观、实测尺寸及长局性能仍待验。[追加批次说明](ART_CONVERSION_V002.md)。', '']
    return lines


def update():
    catalog = read(REPO / 'art/recipes/entity-conversion-v001/catalog.json')
    coverage = coverage_evidence()
    politics, politics_done = delivered_type(POLITICS_VERSION, 'progress-politics')
    politics_imported = {asset['id'] for asset in read(INTEGRATION_EVIDENCE / 'unity-import-v003.json').get('assets', []) if asset.get('verified')}
    unity = read(REPO / 'docs/verification/entity-conversion-v001/unity-import.json')
    unity_ids = {x['id'] for x in unity.get('assets', []) if x.get('verified')}
    lines = ['# 游戏实体美术资源转化清单', '',
             '更新日期：' + datetime.now(timezone(timedelta(hours=8))).strftime('%Y-%m-%d %H:%M（北京时间）'), '',
             f"范围：基础版、航海家、城市与骑士及已支持的组合。现有 {coverage['photographs']} 张不重复实体照片，"
             f"其中 {coverage['covered']} 张进入版本化导出来源（原件备份不重复计数）；原件只读保留。"
             '每一类型派发独立子任务，源文件、配方、贴图、导出、预览和验证齐全后更新本清单。', '',
             '用户于 2026-10-09 选择**原照片校正贴图、尽量保留原版插画与文字**。'
             '新增平面资产只做几何校正，保留原照片像素、照明和模糊；不以照片中的规则文字改变官方规则。'
             '既有森林、木头卡、村庄、道路样板按用户说明保留，前两项仍是旧版 AI 清理候选。', '',
             '勾选表示照片转化交付通过 Blender 回读、干净目录重建及四视图输出；Unity 导入另列。'
             '正式游戏接入代码与运行验证单独记录在下方 v003，不能把图库导入视为整局验收。'
             '全部尺寸仍是估算；用户外观确认、实测比例、隐藏面补充与长期性能待验。'
             '本清单不表示 M8 已满足退出条件。', '', '## 已完成的实验样板', '',
             '| 进度 | 类型 | 已有产物 | 验证 / 待确认 |', '| --- | --- | --- | --- |',
             '| [x] | 森林板块 | 印刷顶面＋六边形网格 | 既有样板工程验证；AI 清理候选、用户外观待确认 |',
             '| [x] | 木头资源卡 | 正反贴图＋双面卡网格 | 既有样板工程验证；AI 清理候选、用户外观待确认 |',
             '| [x] | 村庄 | 参数化模型 | 既有样板工程验证；单照近似、未知隐藏面 |',
             '| [x] | 道路 | 参数化模型 | 既有样板工程验证；单照近似、未知底面 |', '',
             '产物与入口：[第一批样板说明](PHOTO_SAMPLES_V001.md)。', '',
             '## 本轮分类型流水线', '',
             '| 进度 | 类型 / 子任务 ID | 内容与变体 | 照片数 | 产物和证据 | Unity 导入 |',
             '| --- | --- | --- | --- | --- | --- |']
    completed, imported, count = 0, 0, 0
    states = []
    for entry in catalog.get('conversion_types', []):
        key = entry['id']
        current_version = POLITICS_VERSION if key == 'progress-politics' and politics_done else VERSION
        export = REPO / 'art/exports' / current_version / key
        manifest, verification = read(export/'manifest.json'), read(export/'verification.json')
        rebuild = read(export/'clean-rebuild-verification.json')
        recipe_path = REPO/'art/recipes'/current_version/key/'recipe.json'
        recipe_current = recipe_path.exists() and manifest.get('recipeSha256') == hashlib.sha256(recipe_path.read_bytes()).hexdigest()
        rebuilt = (verification.get('cleanRebuild') or rebuild.get('cleanRebuild') or {}).get('sameGeometryMaterialsTexturesReferences', False)
        previews = REPO / 'art/previews' / current_version / key
        done = (recipe_path.parent/'README.md').exists() and recipe_current and rebuilt and verification.get('status') == 'passed' and manifest.get('assets') and all(
            (previews/(v+'.png')).exists() for v in ('top','side','game-camera','crowded-vertex'))
        ids = [a['id'] for a in manifest.get('assets', [])]
        current_imports = politics_imported if current_version == POLITICS_VERSION else unity_ids
        unity_done = bool(ids) and all(a in current_imports for a in ids)
        if done:
            completed += 1
            count += len(ids)
        if unity_done:
            imported += 1
        state = '已转化' if done else ('已生成，验证中' if ids else ('执行中' if (REPO/'art/recipes'/current_version/key/'recipe.json').exists() else '待派发'))
        evidence = f'[配方与说明](../art/recipes/{current_version}/{key}/README.md)；[回读](../art/exports/{current_version}/{key}/verification.json)' if done else state
        detail = '九种冻结正面＋原照片蓝色共同背面（v003）' if current_version == POLITICS_VERSION else DETAILS[key]
        photos = len(manifest.get('references', [])) if current_version == POLITICS_VERSION else len(entry['source_files'])
        lines.append(f"| [{'x' if done else ' '}] | {LABELS[key]} `{key}` | {detail} | {photos} | {evidence} | {'已验证' if unity_done else '待验证'} |")
        states.append({'id': key, 'status': state, 'converted': bool(done), 'unityImported': unity_done, 'assetCount': len(ids)})
    lines += ['', f'当前类型进度：**{completed}/18 类已转化，{count} 件资产；{imported}/18 类 Unity 导入验证通过**。'
              '政治卡使用 v003 替代版；已有四类实验样板与三色 v002 另列，数量按唯一资产 ID 去重。', '',
              '## 每类转化的交付要求（复用模板）', '',
              '- [ ] 登记所有来源、SHA-256、可见特征、未知表面与估算尺寸。',
              '- [ ] 平面照片记录四角/六角和校正尺寸；棋子记录参数化脚本与固定种子。',
              '- [ ] 保存 `.blend`、显式 `.fbx`、PNG 依赖、材质和导出清单。',
              '- [ ] 回读检查实际网格、底心轴心、尺寸、UV、闭合性、非退化三角形与来源完整性。',
              '- [ ] 输出俯视、侧视、游戏相机、拥挤节点四种预览；人工看图并记录限制。',
              '- [ ] 从干净目录重建并比较几何、材质、贴图；Unity 校验尺寸、材质与预制体。',
              '- [ ] 更新本清单对应类型，用户确认独立记录；不把位图当作立体网格。', '',
              '## 缺少参考、后续需要补齐', '',
              '| 范围 | 待补项目 | 当前处理 |', '| --- | --- | --- |',
              '| 共通 | 实体版本、全桌布局、带尺尺寸、棋子顶侧背底面 | 尺寸标估算；单照隐藏形状标近似 |',
              '| 玩家变体与实例 | 道路完整配色、多视角与实测颜色；整局实例数量和摆放 | 三色村庄、城市、城墙、大都会与骑士追加批次见下方 v002；照片白平衡与最终外观仍待确认 |',
              '| 基础版 | 数字盘正面/字母；海框；两枚产出骰；基础发展卡五类型及背面；最长道路/最大军队卡；建造费用参考卡 | 高山实物照片已补转 v006；港口及数字背面见 v005；其余维持规则显示 |',
              '| 航海家 | 卡坦标记/海外胜利筹码；布匹筹码；中立村庄；海盗要塞；战船标记；五张奇迹卡与等级标记；迷雾背面 | 未提供照片，不伪造来源 |',
              '| 城市与骑士 | 商品/保卫者卡背面；事件骰；三种大都会身份标记；独立城墙多视图 | 蓝色进步卡背面已提供并补转 v003；其余缺背面使用明确中性底色 |', '',
              '## 路径与运行', '',
              '- 来源索引：[目录清点与任务定义](../art/recipes/entity-conversion-v001/catalog.json)。',
              '- 本轮结果：[交付与验证说明](ART_CONVERSION_V001.md)；[跨类型审计](verification/entity-conversion-v001/pipeline-audit.json)。',
              '- 当前全量清点：[135 张原照来源与资产审计](../art/recipes/entity-conversion-v003/progress-politics/photo-coverage-audit.json)。',
              '- 通用生成与验证：`tools/art/Build-EntityAssets.ps1 -Type <类型ID>`。',
              '- 校正：`tools/art/rectify_entity_photos.py --recipe <recipe.json>`。',
              '- 版本根目录：`art/{recipes,source,exports,previews}/entity-conversion-v001/<类型ID>/`。',
              '- Unity 导入：`tools/development/Build-EntityAssets.ps1`；查看：`tools/development/Play-EntityAssets.ps1`。',
              '- 独立 Windows 检查：`tools/validation/Test-EntityAssets.ps1`；逐资产加载、材质检查与俯视截图，每类代表件另输出四视图及背面。',
              '- Blender 的 `crowded-vertex` 是类型总览候选；实际相邻板块/道路/建筑环境由 Unity 查看程序另行提供，不能把陈列图当作节点拾取验收。',
              '- 每类完成后运行 `tools/art/update_entity_checklist.py`；进度由实际证据生成，可安全重复执行。', '',
              '类型表的照片数按任务引用计数，同一组合照片可被城墙与大都会共同引用。'
              'v001 原件目录为历史 98 张，v002 追加 36 张，v003 补登记此前遗漏的蓝色共同背面 1 张；当前总数读取实际 inbox 与全量审计，不重复计备份。', '']
    lines += player_components_section()
    lines += politics_section()
    lines += integration_section(coverage)
    lines += ore_section()
    (REPO/'docs/ART_CONVERSION_CHECKLIST.md').write_text('\n'.join(lines), encoding='utf-8')
    (REPO/'art/recipes'/VERSION/'progress.json').write_text(json.dumps({'types':states,'completed':completed,'assetCount':count}, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    print(f'CATAN_ENTITY_CHECKLIST_UPDATED {completed}/18 {count} assets {imported} Unity types')


if __name__ == '__main__':
    update()
