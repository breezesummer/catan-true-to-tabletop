"""草原重制：真实曲面草叶、连绵草坡、羊毛及分层土壤；继承稳定模块接口。"""
import importlib.util
import gzip
import math
import random
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[5]
spec = importlib.util.spec_from_file_location('pasture_base', ROOT/'art/recipes/ultra/v002/pasture/build.py')
b = importlib.util.module_from_spec(spec)
spec.loader.exec_module(b)

def boundary(a):
    return b.R*math.cos(math.pi/6)/math.cos((a-math.pi/6)%(math.pi/3)-math.pi/6)

def height(x,y):
    a=math.atan2(y,x); r=math.hypot(x,y)
    fade=min(1,max(0,(boundary(a)-r)/.006))
    fade=fade*fade*(3-2*fade)
    hills=0
    for hx,hy,amp,sx,sy in [(-.016,.014,.008,.014,.010),(.016,.010,.006,.013,.011),(-.008,-.015,.006,.016,.009),(.019,-.018,.0035,.009,.008)]:
        hills+=amp*math.exp(-((x-hx)/sx)**2-((y-hy)/sy)**2)
    return .008+fade*(.0008+hills)

def grass(self,start,end,count,seed,short=False):
    rng=random.Random(seed)
    high=count>200
    count=3500 if high else 360
    for i in range(count):
        a=rng.uniform(start+.004,end-.004)
        r=math.sqrt(rng.random())*(boundary(a)-.00045)
        x,y=b.polar(r,a)
        if short and .010<r<.025 and abs(a-(start+end)/2)<.32:continue
        z=height(x,y)
        patch=.5+.5*math.sin(x*340+y*180)*math.cos(y*260)
        hue=rng.random()
        base=(.09+.10*hue,.24+.20*hue+.045*patch,.025+.035*hue,1)
        tip=(.27+.20*hue,.49+.22*hue,.055+.09*hue,1)
        for k in range(3 if high else 2):
            direction=-.6+.7*math.sin(x*140+y*90)+rng.uniform(-.65,.65)
            dx,dy=math.cos(direction),math.sin(direction)
            w=rng.uniform(.00009,.00015)
            h=rng.uniform(.0014,.0023)*(1 if high else .65)
            bend=rng.uniform(.00085,.00165)
            ids=[]
            steps=4 if high else 2
            for j in range(steps+1):
                t=j/steps; width=w*(1-t)**.7
                px=x+dx*bend*t*t;py=y+dy*bend*t*t
                for s in [-1,1]:
                    ids.append(self.vert((px-dy*width*s,py+dx*width*s,height(px,py)+h*(t-.18*t*t)),tuple(base[q]*(1-t)+tip[q]*t for q in range(4))))
            for j in range(steps):self.face(ids[j*2:j*2+2]+[ids[j*2+3],ids[j*2+2]])

def sheep(self,x,y,detail):
    rng=random.Random(round((x+y)*1e7))
    offset=len(self.v);z=height(x,y)
    wool=(.88,.85,.73,1);skin=(.70,.66,.55,1)
    high=detail>=10
    for dx in [-.00072,.00072]:
        for dy in [-.0012,.0012]:
            self.ellipsoid((x+dx,y+dy,z+.00095),(.00023,.00024,.0009),skin,8)
            self.ellipsoid((x+dx,y+dy+.00008,z+.00022),(.00026,.00034,.00023),(.24,.21,.16,1),8)
    self.ellipsoid((x,y,z+.0023),(.00125,.00205,.00125),wool,16 if high else 10)
    # 羊毛卷沿椭球表面均匀分布，保留背脊、腹部和肩胛轮廓。
    n=160 if high else 28
    for i in range(n):
        zz=1-2*(i+.5)/n;a=i*2.399963;rr=math.sqrt(1-zz*zz)
        wx=.0012*rr*math.cos(a);wy=.00198*rr*math.sin(a);wz=.0012*zz
        s=rng.uniform(.00025,.00036)
        c=tuple(v*rng.uniform(.93,1.02) for v in wool[:3])+(1,)
        self.ellipsoid((x+wx,y+wy,z+.0023+wz),(s*.75,s*1.65,s*.65),c,8 if high else 6)
    self.ellipsoid((x,y+.00185,z+.0018),(.00056,.00072,.00076),wool,12)
    self.ellipsoid((x,y+.00245,z+.00105),(.00043,.00076,.00048),skin,12)
    for s in [-1,1]:
        self.ellipsoid((x+s*.00059,y+.00208,z+.00155),(.00043,.00023,.00013),skin,10)
        self.ellipsoid((x+s*.00036,y+.00255,z+.00135),(.00008,.00009,.00008),(.06,.045,.025,1),8)
    self.ellipsoid((x,y-.00205,z+.00215),(.00032,.00055,.00040),wool,10)
    # 每只羊朝向独立，脚部依真实坡面逐点落地。
    angle=rng.uniform(0,math.tau)
    for i in range(offset,len(self.v)):
        px,py,pz=self.v[i];dx,dy=px-x,py-y
        nx=x+dx*math.cos(angle)-dy*math.sin(angle);ny=y+dx*math.sin(angle)+dy*math.cos(angle)
        self.v[i]=(nx,ny,pz+height(nx,ny)-z)

old_ground=b.ground
def ground(lod):
    m=b.Mesh('ground')
    n=192 if lod==0 else 72;rings=40 if lod==0 else 16
    center=m.vert((0,0,height(0,0)),(.17,.38,.045,1))
    for j in range(1,rings+1):
        for i in range(n):
            a=i*math.tau/n;x,y=b.polar(boundary(a)*j/rings,a)
            m.vert((x,y,height(x,y)),(.15+.025*math.sin(x*230),.34+.04*math.cos(y*150),.035,1))
    for i in range(n):m.face([center,1+i,1+(i+1)%n])
    for j in range(rings-1):
        for i in range(n):
            q=1+j*n+i;v=1+j*n+(i+1)%n;m.face([q,q+n,v+n,v])
    rng=random.Random(543)
    count=240 if lod==0 else 72
    layers=7
    ids=[]
    for j in range(layers):
        t=j/(layers-1)
        for i in range(count):
            a=i*math.tau/count;r=boundary(a)
            rough=(math.sin(i*1.73+j*2.1)+math.sin(i*.61-j))*.00028*math.sin(math.pi*t)
            x,y=b.polar(r+rough,a)
            band=.035*math.sin(j*2.3)
            c=(.29+.095*t+band+rng.uniform(-.06,.06),.17+.08*t+band,.075+.035*t,1)
            ids.append(m.vert((x,y,.008*t),c))
    for j in range(layers-1):
        for i in range(count):
            q=j*count+i;v=j*count+(i+1)%count
            m.face([ids[q],ids[v],ids[v+count],ids[q+count]])
    bottom=m.vert((0,0,0),b.WOOD)
    for i in range(count):m.face([bottom,ids[(i+1)%count],ids[i]])
    # 土壤颗粒和细根只位于侧面，不在草原上放石头。
    for i in range(850 if lod==0 else 100):
        a=rng.uniform(0,math.tau);r=boundary(a);x,y=b.polar(r,a);z=rng.uniform(.0006,.0074)
        s=rng.uniform(.00012,.00035)
        m.ellipsoid((x,y,z),(s,s,s*.65),(.36,.25,.13,1),6)
    for i in range(210 if lod==0 else 35):
        a=rng.uniform(0,math.tau);r=boundary(a)+.00003;x,y=b.polar(r,a)
        m.beam((x,y,.0078),(x+.00016*math.sin(a),y-.00016*math.cos(a),rng.uniform(.004,.0068)),.000045,(.49,.35,.17,1))
    return m

b.height=height;b.Mesh.grass=grass;b.Mesh.sheep=sheep;b.ground=ground

# 使用同一模块导出逻辑，版本路径另存，旧样板保留。
source=Path(b.__file__).read_text(encoding='utf-8')
main_source=source[source.index('def main():'):source.index("if __name__=='__main__'")]
main_source=main_source.replace('v002','v003').replace("if i%2==0:x,y=polar(.018,a+.45);m.sheep(x,y,10 if lod==0 else 6)","x,y=polar(.018 if i%2==0 else .024,a+.45);m.sheep(x,y,10 if lod==0 else 6)")
exec(main_source,b.__dict__)

if __name__=='__main__':
    b.main()
    import argparse
    ap=argparse.ArgumentParser();ap.add_argument('--output',default=str(ROOT/'art'))
    args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    exp=Path(args.output)/'exports/ultra/v003/pasture'
    (exp/'pasture-mesh.bytes').write_bytes(gzip.compress((exp/'pasture-mesh.json').read_bytes(),mtime=0))
