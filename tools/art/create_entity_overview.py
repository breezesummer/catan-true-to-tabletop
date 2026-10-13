"""Compose existing Unity captures for human review; does not alter source art."""
import json
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont, ImageOps
from update_entity_checklist import LABELS

REPO = Path(__file__).resolve().parents[2]
EVIDENCE = REPO/'docs/verification/entity-conversion-v001'

def main():
    catalog=json.loads((REPO/'art/recipes/entity-conversion-v001/catalog.json').read_text(encoding='utf-8'))
    report=json.loads((EVIDENCE/'player-verification.json').read_text(encoding='utf-8'))
    assert report['status']=='passed'
    font=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',22)
    small=ImageFont.truetype('C:/Windows/Fonts/msyh.ttc',18)
    tasks=catalog['conversion_types']; columns=4; cell=(450,335); margin=24; title=90
    canvas=Image.new('RGB',(columns*cell[0]+margin*2,((len(tasks)+columns-1)//columns)*cell[1]+title+margin),(235,239,241))
    draw=ImageDraw.Draw(canvas)
    draw.text((margin,18),f"实体资源 v001 · {report['types']} 类 / {report['packagedPrefabs']} 件",font=font,fill=(25,44,55))
    draw.text((margin,53),'Unity 独立 Windows 查看程序截图；每类代表一件，船使用横侧视角',font=small,fill=(65,82,91))
    for index,task in enumerate(tasks):
        key=task['id'];view='side' if key in ('ships','pirate','barbarian-ship') else 'game-camera'
        source=Image.open(EVIDENCE/key/('unity-'+view+'.png')).convert('RGB')
        x=margin+(index%columns)*cell[0]; y=title+(index//columns)*cell[1]
        picture=ImageOps.contain(source,(cell[0]-12,cell[1]-58))
        canvas.paste(picture,(x+(cell[0]-picture.width)//2,y))
        label=LABELS[key]
        draw.text((x+7,y+cell[1]-54),label,font=small,fill=(25,44,55))
        draw.text((x+7,y+cell[1]-29),key,font=small,fill=(75,94,104))
    target=EVIDENCE/'overview.png';canvas.save(target)
    print('CATAN_ENTITY_OVERVIEW',target)

if __name__=='__main__': main()
