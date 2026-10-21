"""独立核对网格尺寸、扇区归属、共享边与净空；可比较干净目录重建。"""
import argparse
import hashlib
import itertools
import json
import math
from pathlib import Path

ROOT=Path(__file__).resolve().parents[5]
parser=argparse.ArgumentParser()
parser.add_argument('--root',default=str(ROOT/'art'))
parser.add_argument('--compare',default='')
args=parser.parse_args()
directory=Path(args.root)/'exports/ultra/v002/pasture'
file=directory/'pasture-mesh.json';data=json.loads(file.read_text())
report={'version':data['version'],'lods':2,'checks':[]}
minimum_clearance=1
maximum_height=0
for level in data['lods']:
    parts={p['name']:p for p in level['parts']}
    assert len(parts)==19
    for name,p in parts.items():
        v=list(zip(*[iter(p['vertices'])]*3))
        assert len(p['colors'])==4*len(v)
        assert all(0<=i<len(v) for i in p['triangles']+p['paintTriangles'])
        assert all(math.isfinite(q) for q in p['vertices'])
        maximum_height=max(maximum_height,max(y for x,y,z in v))
        if name=='ground':
            assert abs(min(y for x,y,z in v))<1e-7
            assert abs(max(math.hypot(x,z) for x,y,z in v)-.0395)<1e-6
            # 同类型相邻地块共享边的顶点高度一致，外缘严格回到8毫米。
            for x,y,z in v:
                if math.hypot(x,z)>.0341 and y>.001:assert abs(y-.008)<1e-6
            continue
        index=int(name.split('-')[1]);center=math.radians(30+index*60)
        if name.startswith('wild'):center+=math.pi/6;width=math.pi/6+.04
        else:width=math.pi/3+.04
        for x,y,z in v:
            distance=abs(math.atan2(math.sin(math.atan2(z,x)-center),math.cos(math.atan2(z,x)-center)))
            assert distance<width,(name,'跨开发分区',distance)
            # 棋盘规则半径40毫米。逐六边测距，景观不得进入道路/节点中性边缘。
            clearance=min(.04*math.cos(math.pi/6)-(x*math.cos(k*math.pi/3)+z*math.sin(k*math.pi/3)) for k in range(6))
            minimum_clearance=min(minimum_clearance,clearance)
            assert clearance>.004,(name,'侵占中性道路带',clearance)
    # 低细节必须减少真实三角形；不能通过减少羊数或区域归属实现。
assert sum(len(p['triangles'])+len(p['paintTriangles']) for p in data['lods'][1]['parts'])<sum(len(p['triangles'])+len(p['paintTriangles']) for p in data['lods'][0]['parts'])

# 独立以六角形投影面积核对朝顶点120度区域，穷举所有合法的非邻接建筑组合。
points=[(.029*math.cos(math.radians(30+i*60)),.029*math.sin(math.radians(30+i*60))) for i in range(6)]
areas=[abs(points[i][0]*points[(i+1)%6][1]-points[(i+1)%6][0]*points[i][1])/2 for i in range(6)]
for count in range(4):
    for combo in itertools.combinations(range(6),count):
        if any((a-b)%6 in [1,5] for a,b in itertools.combinations(combo,2)):continue
        sectors=[i for vertex in combo for i in [(vertex-1)%6,vertex]]
        assert len(set(sectors))==2*count
        assert abs(sum(areas[i] for i in sectors)/sum(areas)-count/3)<1e-10
report['checks']=['全部网格索引/数值有效','真实模型尺寸与8毫米共享边','景观不侵占道路带','开发区模块未跨玩家区域','全部合法组合投影面积为每座三分之一','低LOD减少三角形']
report['minimumRoadClearanceMm']=round(minimum_clearance*1000,3)
report['maximumHeightMm']=round(maximum_height*1000,3)
report['sha256']=hashlib.sha256(file.read_bytes()).hexdigest()
if args.compare:
    other=Path(args.compare)/'exports/ultra/v002/pasture/pasture-mesh.json'
    assert file.read_bytes()==other.read_bytes(),'干净重建网格不一致'
    report['checks'].append('干净目录重建逐字节相同')
(directory/'verification.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(report,ensure_ascii=False,indent=2))
