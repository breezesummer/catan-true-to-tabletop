"""归档八张原照并以原像素生成港口/圆片贴图；不重绘照片内容。"""
from pathlib import Path
from collections import defaultdict
import hashlib
import importlib.util
import io
import json
import math
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageOps, __version__ as pillow_version

REPO = Path(__file__).resolve().parents[4]
HERE = Path(__file__).resolve().parent
VERSION = 'entity-conversion-v005'
TYPE = 'tabletop-markers'
SOURCE = REPO / 'art/source' / VERSION / TYPE / 'textures'
ARCHIVE = REPO / 'assets/references/archives/tabletop-markers-v005'
INBOX = REPO / 'assets/references/inbox/未分类'
PORTS = [('port-generic','3：1港口.jpg','3:1 通用港口'),
         ('port-wood','木头港口.jpg','2:1 木头港口'),
         ('port-brick','砖港口.jpg','2:1 砖港口'),
         ('port-wool','羊毛港口.jpg','2:1 羊毛港口'),
         ('port-wheat','小麦港口.jpg','2:1 小麦港口'),
         ('port-ore','矿石港口.jpg','2:1 矿石港口')]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def immutable_write(path, data):
    path=Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    if path.exists() and path.read_bytes()!=data:
        raise FileExistsError('版本化产物已经存在且不同，请使用新版本: '+str(path))
    if not path.exists():
        path.write_bytes(data)


def json_write(path,value):
    immutable_write(path,(json.dumps(value,ensure_ascii=False,indent=2)+'\n').encode('utf-8'))


def png_write(path,image):
    stream=io.BytesIO()
    image.save(stream,format='PNG')
    immutable_write(path,stream.getvalue())


def pathstr(path):
    return Path(path).relative_to(REPO).as_posix()


def rdp(points,epsilon):
    if len(points)<3:
        return points
    a,b=np.array(points[0],float),np.array(points[-1],float)
    p=np.asarray(points,float)
    delta=b-a;relative=p-a
    distance=np.linalg.norm(relative,axis=1) if np.linalg.norm(delta)<1e-12 else np.abs(delta[0]*relative[:,1]-delta[1]*relative[:,0])/np.linalg.norm(delta)
    index=int(np.argmax(distance))
    if distance[index]>epsilon:
        return rdp(points[:index+1],epsilon)[:-1]+rdp(points[index:],epsilon)
    return [points[0],points[-1]]


def polygon_area(points):
    return sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(points,points[1:]+points[:1]))/2


def port_outline(photo):
    """色差仅用于读取外轮廓，绝不改动输出图像中的 RGB 像素。"""
    small=photo.resize((600,450),Image.Resampling.LANCZOS)
    a=np.asarray(small).astype(int)
    mask=((a.max(axis=2)-a.min(axis=2)>24)&(a[:,:,0]<210)).astype('uint8')*255
    mask=np.asarray(Image.fromarray(mask).filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.MinFilter(9)))>0
    # 沿二值像素格边界行走；内部插画的灰白孔洞被外轮廓自动排除。
    edges=defaultdict(list)
    for y,x in zip(*np.where(mask)):
        if y==0 or not mask[y-1,x]: edges[(x,y)].append((x+1,y))
        if x==mask.shape[1]-1 or not mask[y,x+1]: edges[(x+1,y)].append((x+1,y+1))
        if y==mask.shape[0]-1 or not mask[y+1,x]: edges[(x+1,y+1)].append((x,y+1))
        if x==0 or not mask[y,x-1]: edges[(x,y+1)].append((x,y))
    contours=[]
    while edges:
        start=next(iter(edges));point=start;contour=[point]
        for _ in range(10000):
            if point not in edges: break
            next_point=edges[point].pop()
            if not edges[point]: del edges[point]
            point=next_point
            if point==start: break
            contour.append(point)
        if len(contour)>10: contours.append(contour)
    contour=max(contours,key=lambda c:abs(polygon_area(c)))
    if abs(polygon_area(contour))<20000:
        raise ValueError('未找到完整港口纸板轮廓')
    far=int(np.argmax(np.linalg.norm(np.asarray(contour)-contour[0],axis=1)))
    contour=rdp(contour[:far+1],.85)[:-1]+rdp(contour[far:]+contour[:1],.85)[:-1]
    scale=photo.width/600
    points=np.asarray(contour,float)*scale
    # 沙岸底边在画面内轻微倾斜。由中间 60% 的底边格点估算其直线并扶正。
    ys,xs=np.where(mask)
    xmin,xmax=xs.min(),xs.max()
    baseline=[]
    for x in range(int(xmin+(xmax-xmin)*.2),int(xmin+(xmax-xmin)*.8)):
        column=np.where(mask[:,x])[0]
        if len(column): baseline.append((x,column.max()+1))
    slope,intercept=np.polyfit(np.asarray(baseline)[:,0],np.asarray(baseline)[:,1],1)
    angle=math.atan(float(slope))
    rotation=np.asarray([[math.cos(angle),math.sin(angle)],[-math.sin(angle),math.cos(angle)]])
    aligned=points@rotation.T
    low,high=aligned.min(axis=0),aligned.max(axis=0)
    quad=np.asarray([[low[0],low[1]],[high[0],low[1]],[high[0],high[1]],[low[0],high[1]]])@rotation
    normalized=(aligned-low)/(high-low)
    return points.tolist(),quad.tolist(),normalized.tolist(),float(math.degrees(angle))


def main():
    spec=importlib.util.spec_from_file_location('original_pixel_homography',REPO/'tools/art/rectify_entity_photos.py')
    helper=importlib.util.module_from_spec(spec);spec.loader.exec_module(helper)
    references=[];assets=[];operations=[];catalog=[]
    records=[(asset_id,name,label,'ports',[44,34,1.5]) for asset_id,name,label in PORTS]
    records += [('number-token-back','点数背面.jpg','点数牌共同船图案背面','tokens',[21,21,.45]),
                ('catan-victory-marker','卡坦分标记.jpg','卡坦胜利分标记','tokens',[18,18,1.5])]
    for asset_id,name,label,group,dimensions in records:
        original=INBOX/name;archive=ARCHIVE/group/name
        if original.exists():
            immutable_write(archive,original.read_bytes())
            if sha(original)!=sha(archive): raise AssertionError('归档字节不同')
        elif not archive.exists():
            raise FileNotFoundError('原件和归档均不存在: '+str(original))
        photo=ImageOps.exif_transpose(Image.open(archive)).convert('RGB')
        if group=='ports':
            source_outline,quad,outline,baseline_angle=port_outline(photo)
            size=[1024,792];rotation=0
            extraction={'method':'RGB 色差阈值仅提取轮廓，9 px 闭运算，外边界 RDP 0.85 px 简化；工作图 600×450',
                        'sourceOutlinePixels':source_outline,'shoreBaselineDegrees':baseline_angle}
            texture_name=asset_id+'-front.png'
        else:
            # 四角为原照片中圆片可见椭圆的外接矩形；以圆形厚片做工作尺度校正。
            bounds=[1490,1140,2810,2460] if asset_id=='number-token-back' else [1787,1451,2731,2421]
            x0,y0,x1,y1=bounds
            quad=[[x0,y0],[x1,y0],[x1,y1],[x0,y1]]
            source_outline=[]
            outline=[[.5+.5*math.cos(i*2*math.pi/64),.5+.5*math.sin(i*2*math.pi/64)] for i in range(64)]
            size=[1024,1024];rotation=-26 if asset_id=='number-token-back' else 0
            extraction={'method':'逐张目检可见圆片边界，以外接椭圆矩形近似；64 段圆轮廓',
                        'visibleEllipseBoundsPixels':bounds,'printUprightingDegreesClockwise':-rotation}
            texture_name=asset_id+'.png'
        width,height=size
        coefficients=helper.perspective_coefficients([(0,0),(width,0),(width,height),(0,height)],quad)
        corrected=photo.transform(tuple(size),Image.Transform.PERSPECTIVE,coefficients,Image.Resampling.BICUBIC)
        if rotation: corrected=corrected.rotate(rotation,Image.Resampling.BICUBIC,expand=False)
        texture=SOURCE/texture_name
        png_write(texture,corrected)
        inspect=photo.resize((1024,768),Image.Resampling.LANCZOS)
        draw=ImageDraw.Draw(inspect);scale=1024/photo.width
        if source_outline:
            points=[(x*scale,y*scale) for x,y in source_outline]
            draw.line(points+points[:1],fill=(255,40,20),width=2)
        else:
            draw.ellipse((quad[0][0]*scale,quad[0][1]*scale,quad[2][0]*scale,quad[2][1]*scale),outline=(255,40,20),width=2)
        points=[(x*scale,y*scale) for x,y in quad]
        draw.line(points+points[:1],fill=(0,180,255),width=2)
        png_write(HERE/(asset_id+'-source-inspection.png'),inspect)
        reference={'id':'physical-'+asset_id+'-001','originalPath':pathstr(archive),'inboxPath':pathstr(original),
                   'sha256':sha(archive),'role':label+'原照片；非规则文字来源','pixelSize':list(photo.size)}
        references.append(reference)
        catalog.append({**reference,'classifiedPath':pathstr(archive),'kind':'photograph','edition':None,
                        'measuredDimensionsMm':None,'estimatedDimensionsMm':dimensions,'processedAssetIds':[asset_id],
                        'mustPreserve':['原件字节','印刷插画与文字','原照照明/失焦/色偏'],
                        'unseenSurfaces':['另一面','纸芯与侧边实测细节'],'status':'已归档；原件原位保留'})
        operation={'source':pathstr(archive),'inboxPath':pathstr(original),'sourceSha256':sha(archive),
                   'output':pathstr(texture),'sha256':sha(texture),'method':'原照片 RGB 像素四点几何重投影与双三次采样；无 AI 重绘',
                   'quad':quad,'size':size,'rotation':rotation,'extraction':extraction,'outlineUvImage':outline,
                   'limitations':'未做镜头标定；照片色偏、照明、模糊与纸边损伤保留；几何尺寸及相机俯角仍为估算'}
        operations.append(operation)
        assets.append({'id':asset_id,'label':label,'kind':'custom','dimensionsMm':dimensions,
                       'customBuilder':pathstr(HERE/'builder.py'),'frontTexture':pathstr(texture),'backTexture':None,
                       'outlineUvImage':outline,'sourceReferenceId':reference['id'],
                       'faceRole':'号码牌共同背面样板；未见数字正面' if asset_id=='number-token-back' else '可见印刷正面',
                       'orientation':'Blender +Z 顶面/+Y 柄；Unity +Y 顶面/-Z 柄，港口沙岸向 +Z'})
    recipe={'schemaVersion':1,'typeId':TYPE,'label':'六种实体港口、点数共同背面与卡坦分标记',
            'seed':20261014,'scaleStatus':'全部尺寸为估算，未取得尺量或用户尺度验收',
            'references':references,'textureJobs':operations,'assets':assets,
            'limitations':['六港口为保留原像素插画的异形闭合厚纸板，并非港口立体景观。',
                           '照片无尺，港口 44×34×1.5 mm、点数圆片 21×21×0.45 mm、分标记 18×18×1.5 mm 全部估算。',
                           '背面与纸芯细节未知；未见面使用中性材料，不捏造另一面图案。',
                           '港口轮廓来自色差外边界，底边校平后按估算长宽归一；并非精确光学标定。',
                           '点数背面只供现有规则数字牌共用，不从照片推断数字或改变规则。',
                           '四视图是 Blender 类型陈列；实际相邻节点、Unity 拾取与最终用户外观另验。']}
    json_write(HERE/'recipe.json',recipe)
    json_write(HERE/'texture-correction.json',{'version':VERSION,'typeId':TYPE,'operations':operations,
               'pythonVersion':sys.version.split()[0],'pillowVersion':pillow_version,'numpyVersion':np.__version__,
               'scriptSha256':sha(__file__),'helperScriptSha256':sha(REPO/'tools/art/rectify_entity_photos.py'),
               'recipeSha256':sha(HERE/'recipe.json')})
    json_write(REPO/'assets/references/catalogs/tabletop-markers-v005.json',{
        'schemaVersion':1,'catalogVersion':'tabletop-markers-v005','classifiedDate':'2026-10-14',
        'originalsPreserved':True,'originalsRemainAtInboxPaths':True,'uniquePhotographCount':8,
        'archiveIsByteIdentical':True,'assets':catalog})
    json_write(ARCHIVE/'classification.json',{'schemaVersion':1,'batch':'tabletop-markers-v005',
        'sourceDirectory':'assets/references/inbox/未分类','status':'本批 8 张全部归档并登记，原件原位保留',
        'catalog':'assets/references/catalogs/tabletop-markers-v005.json','assets':references})
    print('CATAN_TABLETOP_MARKERS_PREPARED photographs=8 assets=8')


if __name__=='__main__': main()
