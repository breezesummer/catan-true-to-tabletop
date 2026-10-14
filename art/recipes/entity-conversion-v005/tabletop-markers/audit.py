"""补核估算尺寸、原照与归档副本；人工看图后可显式登记目检范围。"""
import argparse
import hashlib
import json
from pathlib import Path

REPO=Path(__file__).resolve().parents[4]
HERE=Path(__file__).resolve().parent


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def write_json(path,value):
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')


def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--output-root',type=Path,default=REPO/'art')
    parser.add_argument('--record-visual-review',action='store_true',help='仅在实际人工看过本批原照、校正图与四视图后使用')
    args=parser.parse_args()
    export=args.output_root.resolve()/'exports/entity-conversion-v005/tabletop-markers'
    recipe=json.loads((HERE/'recipe.json').read_text(encoding='utf-8'))
    verification=json.loads((export/'verification.json').read_text(encoding='utf-8'))
    manifest=json.loads((export/'manifest.json').read_text(encoding='utf-8'))
    if verification['status']!='passed': raise AssertionError('须先完成实际 FBX 回读')
    report={'status':'passed','scope':'八张原照/分类副本/原像素贴图来源、工作估算尺寸及已回读闭合厚板',
            'uniquePhotographs':len(set(x['sha256'] for x in recipe['references'])),'assets':[]}
    expected={x['id']:x for x in recipe['assets']}
    if set(expected)!=set(x['id'] for x in verification['assets']): raise AssertionError('资产 ID 不一致')
    for record in verification['assets']:
        asset=expected[record['id']]
        error=max(abs(y*1000-z) for y,z in zip(record['blenderDimensionsMetres'],asset['dimensionsMm']))
        if error>=.01 or not record['closedMeshes']: raise AssertionError('厚板尺寸或闭合性不一致')
        report['assets'].append({'id':record['id'],'dimensionsMm':asset['dimensionsMm'],
                                'dimensionStatus':'估算，未实测','maximumDimensionErrorMm':error,
                                'closedMeshReadback':True,'sourceTextureSha256':sha(REPO/asset['frontTexture'])})
    report['inboxOriginalsPresent']=True
    for reference in recipe['references']:
        if sha(REPO/reference['originalPath'])!=reference['sha256']: raise AssertionError('归档字节改变')
        original=REPO/reference['inboxPath']
        if original.exists():
            if sha(original)!=reference['sha256']: raise AssertionError('原位原件字节改变')
        else:
            report['inboxOriginalsPresent']=False
    if report['uniquePhotographs']!=8 or len(report['assets'])!=8: raise AssertionError('本批应为八原照八资产')
    report['originalsAndArchivedCopiesByteIdentical']=True
    report['notCovered']=['Unity 游戏画面与拾取','最终用户外观确认','实物尺寸及未知面']
    write_json(export/'source-provenance-audit.json',report)
    if args.record_visual_review:
        if manifest['previewViews']!=['top','side','game-camera','crowded-vertex']:
            raise AssertionError('登记目检要求正式四视图')
        write_json(export/'visual-review.json',{'status':'passed','scope':'原照与校正像素、Blender 四视图人工目检',
            'reviewed':['八张原照联系表','六港口轮廓分割标注联系表','八张校正图联系表',
                        'top.png','side.png','game-camera.png','crowded-vertex.png'],
            'previewHashes':manifest['previewHashes'],
            'findings':['六港口保留中央柄、两侧圆耳与沙岸异形轮廓，文字和资源图标完整',
                        '船背图案已扶正，两圆片无镜像或明显裁断',
                        '斜视可见实际厚边，侧视分层显示全部八件',
                        '照片色偏、模糊、照明和纸边损伤保留；不视为原版色值已还原'],
            'limitations':['crowded-vertex 为类型陈列，未验实际节点拾取','未知另一面使用中性材质',
                           '所有尺寸仍为估算','最终用户外观与 Unity 游戏画面另验']})
    print('CATAN_MARKERS_PROVENANCE_DIMENSIONS_CHECKS_PASSED')


if __name__=='__main__': main()
