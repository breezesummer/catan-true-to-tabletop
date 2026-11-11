"""从菜单原画提取地块与棋子，依公开布局生成透明预览。"""
import json, math
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter
here=Path(__file__).resolve().parent
root=here.parents[2]
out=root/'art/exports/地图预览'
parts=here/'组件'
parts.mkdir(exist_ok=True)
res=root/'Unity/Catan/Assets/Resources'
src=Image.open(res/'PastoralMenu/v004/collector-table.png').convert('RGBA').resize((1672,941))
extra=Image.open(here/'补绘地块.png').convert('RGBA').resize((2048,683))
def cut(poly):
    mask=Image.new('L',src.size);ImageDraw.Draw(mask).polygon(poly,fill=255)
    obj=src.copy();obj.putalpha(mask);return obj.crop(mask.getbbox())
shapes={
 '红村庄':[(1442,554),(1462,540),(1478,542),(1490,553),(1481,558),(1481,578),(1462,585),(1444,577)],
 '蓝村庄':[(1336,524),(1356,505),(1371,512),(1385,525),(1376,531),(1376,547),(1355,552),(1341,544)],
 '帆船':[(641,434),(659,436),(658,388),(686,378),(687,366),(693,365),(695,379),(716,386),(719,428),(738,437),(723,451),(692,457),(661,451)],
 '港口':[(694,493),(710,481),(710,470),(717,468),(724,475),(738,473),(739,466),(748,465),(753,472),(772,464),(771,450),(788,434),(804,450),(803,467),(821,475),(816,494),(784,511),(729,521),(703,514)],
 '红道路':[(967,524),(1003,516),(1010,520),(1007,529),(971,539),(967,535)],
 '蓝道路':[(1303,514),(1325,523),(1336,525),(1334,536),(1321,534),(1302,526)]}
pieces={k:cut(v) for k,v in shapes.items()}
for name,obj in pieces.items():obj.save(parts/(name+'.png'))
# 将原照六个角校正到共同透视，山峰、麦穗与羊保留原像素。
bounds={
'wheat':[(1038,533),(1172,559),(1186,649),(1060,696),(912,641),(916,578)],
'wool':[(1322,527),(1455,568),(1478,644),(1331,698),(1195,653),(1179,565)],
'ore':[(1189,419),(1290,467),(1302,522),(1182,559),(1053,517),(1058,462)],
'wood':[(1076,349),(1173,373),(1185,423),(1060,456),(971,421),(987,381)],
'brick':[(934,429),(1044,456),(1043,515),(922,551),(823,515),(832,455)],
'sea':[(622,441),(653,451),(654,467),(623,478),(591,466),(591,453)]}
# 上盖被原点数遮挡的小片区域取自同一地块，数字另行排布。
clean=src.copy()
for x,y,w,h,dx,dy in [(1038,596,67,40,67,21),(1345,607,65,41,-70,26),(1103,410,54,34,38,-12),(936,472,62,36,-57,16)]:
    box=(x-w//2,y-h//2,x-w//2+w,y-h//2+h)
    patch=src.crop((box[0]+dx,box[1]+dy,box[2]+dx,box[3]+dy))
    mask=Image.new('L',(w,h));ImageDraw.Draw(mask).ellipse((1,1,w-2,h-2),fill=255)
    clean.paste(patch,box,mask.filter(ImageFilter.GaussianBlur(1)))
target=[(120,0),(239,42),(239,126),(120,167),(0,126),(0,42)]
def rectify(source,poly):
    result=Image.new('RGBA',(240,168))
    for i in range(6):
        dest=[(120,84),target[i],target[(i+1)%6]]
        orig=[np.mean(poly,axis=0),poly[i],poly[(i+1)%6]]
        matrix=np.linalg.solve(np.array([[x,y,1] for x,y in dest]),orig).T.flatten()
        face=source.transform(result.size,Image.Transform.AFFINE,matrix,Image.Resampling.BICUBIC)
        mask=Image.new('L',result.size);ImageDraw.Draw(mask).polygon(dest,fill=255)
        result.paste(face,(0,0),mask)
    return result
# 取边界内侧，剔除相邻格上的屋顶与道路。
for k in ['wood','wool','brick','ore']:
    p=np.array(bounds[k]);mid=p.mean(axis=0);bounds[k]=(mid+(p-mid)*.83).tolist()
tiles={k:rectify(clean,p) for k,p in bounds.items()}
for i,k in enumerate(['desert','gold','fog']):
    tiles[k]=rectify(extra,[(x+i*682,y) for x,y in [(342,135),(596,222),(645,402),(341,524),(42,401),(76,223)]])
for k,t in tiles.items():t.save(parts/(k+'.png'))
fontpath=str(res/'Fonts/NotoSerifCJKsc-Regular.otf')
maps=json.loads((here.parent/'menu-previews-v001/public-layouts.json').read_text('utf8'))['maps']
entries=[m for m in maps if m['rule']!=3]+[dict(m,rule=3) for m in maps if m['rule']==1]
rules=['基础版','航海家','城市与骑士','双扩展']
gallery=Image.new('RGB',(1200,2110),'#eee3cd')
for idx,m in enumerate(sorted(entries,key=lambda e:(e['rule'],e['id'],e['players']))):
    board=Image.new('RGBA',(1280,900))
    cells=sorted(m['tiles'],key=lambda t:(t['r'],t['q']))
    xy=[(t['q']+t['r']/2,t['r']*.525) for t in cells]
    xs,ys=zip(*xy)
    scale=min(1160/(max(xs)-min(xs)+1),750/(max(ys)-min(ys)+.7))
    centers=[(round(640+(x-(max(xs)+min(xs))/2)*scale),round(444+(y-(max(ys)+min(ys))/2)*scale)) for x,y in xy]
    w=round(scale)+2;h=round(scale*.7)+2
    def stamp(obj,x,y,width):
        tile=obj.resize((width,round(obj.height*width/obj.width)),Image.Resampling.LANCZOS)
        board.alpha_composite(tile,(round(x-tile.width/2),round(y-tile.height/2)))
    for (x,y) in centers:
        ImageDraw.Draw(board).polygon([(x+(a-120)*w/240,y+(b-84)*h/168+5) for a,b in target],fill='#866137')
    for t,(x,y) in zip(cells,centers):
        stamp(tiles[t['resource']],x,y,w)
        if t['number']:
            d=ImageDraw.Draw(board);rad=w*.129;cy=y+h*.14
            d.ellipse((x-rad,cy-rad*.72+2,x+rad,cy+rad*.72+3),fill='#79552e')
            d.ellipse((x-rad,cy-rad*.72,x+rad,cy+rad*.72),fill='#edcf98',outline='#bd9458')
            d.text((x,cy-1),str(t['number']),anchor='mm',font=ImageFont.truetype('C:/Windows/Fonts/georgiab.ttf',round(rad*1.1)),fill='#922e1b' if t['number'] in (6,8) else '#342519')
    sea=[i for i,t in enumerate(cells) if t['resource']=='sea']
    land=[i for i,t in enumerate(cells) if t['resource'] not in ('sea','fog')]
    for j,i in enumerate([land[len(land)//3],land[2*len(land)//3]]):
        x,y=centers[i];color=['红','蓝'][j]
        stamp(pieces[color+'道路'],x+w*.28,y+h*.34,round(w*.32))
        stamp(pieces[color+'村庄'],x+w*.46,y+h*.21,round(w*.24))
    if sea:
        coast=next((i for i in sea if any(math.dist(xy[i],xy[j])<1.03 for j in land)),sea[0]);px,py=centers[coast];stamp(pieces['港口'],px,py,round(w*.47))
        x,y=centers[sea[len(sea)//2]];stamp(pieces['帆船'],x,y-h*.09,round(w*.52))
    for key,symbol in [('villages','村'),('fortresses','堡'),('wonders','奇')]:
        for pt in m[key]:
            x=640+(pt['x']/2-(max(xs)+min(xs))/2)*scale
            y=444+(pt['y']*.175-(max(ys)+min(ys))/2)*scale
            d=ImageDraw.Draw(board);d.ellipse((x-9,y-9,x+9,y+9),fill='#51402b',outline='#deb775')
            d.text((x,y),symbol,anchor='mm',font=ImageFont.truetype(fontpath,12),fill='#f6dfaa')
    name=rules[m['rule']]+'-'+m['name']+'-'+str(m['players'])+'人'
    board.save(out/(name+'.png'))
    thumb=board.resize((296,188),Image.Resampling.LANCZOS);xx=idx%4*300;yy=idx//4*211
    gallery.paste(thumb,(xx,yy),thumb);ImageDraw.Draw(gallery).text((xx+150,yy+181),name,anchor='mt',font=ImageFont.truetype(fontpath,15),fill='#493623')
gallery.save(out/'全地图总览.jpg',quality=94)


