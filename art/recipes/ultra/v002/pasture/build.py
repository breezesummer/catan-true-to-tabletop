"""U2 草原静态样板：米制真实网格，Blender 源文件、FBX 与 Unity 显式网格数据同源。"""
import argparse
import hashlib
import json
import math
import random
import sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[5]
TAU = math.tau
R = .0395
INNER = .029
GREEN = (.24, .53, .075, 1)
WOOD = (.31, .16, .065, 1)
CREAM = (.92, .90, .76, 1)
PLAYER = (1, 1, 1, 1)

def height(x, y):
    # 连续草坡，边缘精确回到棋子基准面；中央没有点数留白。
    t = max(0, 1 - (math.hypot(x, y) / .0335) ** 4)
    return .008 + t * (.0024 + .0028 * math.sin(x * 145 + y * 65) ** 2)

def polar(r, a):
    return r * math.cos(a), r * math.sin(a)

class Mesh:
    def __init__(self, name):
        self.name, self.v, self.c, self.f, self.paint = name, [], [], [], []
    def vert(self, p, color):
        self.v.append(tuple(p)); self.c.append(color); return len(self.v) - 1
    def face(self, indices, paint=False):
        for i in range(1, len(indices) - 1):
            self.f.append((indices[0], indices[i], indices[i+1])); self.paint.append(paint)
    def box(self, center, size, color=WOOD, paint=False, angle=0):
        ids=[]
        for x,y,z in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]:
            x,y=x*size[0]/2,y*size[1]/2
            ids.append(self.vert((center[0]+x*math.cos(angle)-y*math.sin(angle),center[1]+x*math.sin(angle)+y*math.cos(angle),center[2]+z*size[2]/2),color))
        for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:self.face([ids[i] for i in f],paint)
    def ellipsoid(self, center, scale, color, detail=8):
        rows=detail//2; start=len(self.v)
        for j in range(rows+1):
            phi=math.pi*j/rows
            for i in range(detail):
                a=TAU*i/detail
                self.vert((center[0]+scale[0]*math.sin(phi)*math.cos(a),center[1]+scale[1]*math.sin(phi)*math.sin(a),center[2]+scale[2]*math.cos(phi)),color)
        for j in range(rows):
            for i in range(detail):
                a=start+j*detail+i; b=start+j*detail+(i+1)%detail
                self.face([a,a+detail,b+detail,b])
    def beam(self, a, b, width, color=WOOD):
        a,b=Vector(a),Vector(b); axis=(b-a).normalized()
        u=axis.cross(Vector((0,0,1)))
        if u.length<.1:u=Vector((1,0,0))
        u.normalize();v=axis.cross(u).normalized();ids=[]
        for p in [a,b]:
            for s,t in [(-1,-1),(1,-1),(1,1),(-1,1)]:ids.append(self.vert(p+(u*s+v*t)*width/2,color))
        for f in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:self.face([ids[i] for i in f])
    def sheep(self,x,y,detail):
        z=height(x,y)
        for dx in [-.00085,.00085]:
            for dy in [-.00125,.00125]:self.box((x+dx,y+dy,z+.0008),(.0004,.00045,.0016),(.36,.29,.17,1))
        self.ellipsoid((x,y,z+.0021),(.00155,.00225,.0015),CREAM,detail)
        if detail>=10:
            for dx,dy in [(-.0008,-.0007),(.0007,-.0007),(-.0007,.0007),(.0007,.0007),(0,0)]:
                self.ellipsoid((x+dx,y+dy,z+.0031),(.0008,.00095,.0006),CREAM,6)
        self.ellipsoid((x,y+.002,z+.0015),(.0007,.001,.0008),(.65,.58,.40,1),detail)
        for dx in [-.0008,.0008]:self.ellipsoid((x+dx,y+.002,z+.0019),(.0006,.0003,.0002),CREAM,6)
    def grass(self,start,end,count,seed,short=False):
        rng=random.Random(seed)
        for i in range(count):
            a=rng.uniform(start+.02,end-.02);r=math.sqrt(rng.uniform(.000008,.00080));x,y=polar(r,a)
            if short and r>.010 and abs(a-(start+end)/2)<.35:continue
            z=height(x,y); c=(rng.uniform(.19,.36),rng.uniform(.43,.67),rng.uniform(.035,.11),1)
            for k in range(2):
                t=rng.uniform(0,TAU); w=.00024;h=rng.uniform(.0006,.0015)
                blade=[(x+math.cos(t)*w,y+math.sin(t)*w,z),(x-math.cos(t)*w,y-math.sin(t)*w,z),(x+.0003,y+.00015,z+h)]
                if any((math.atan2(py,px)-start)%TAU>end-start for px,py,pz in blade):continue
                p,q,tip=[self.vert(point,c) for point in blade]
                self.face([p,q,tip])
    def export(self):
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.v,[],self.f);mesh.update()
        obj=bpy.data.objects.new(self.name,mesh);bpy.context.collection.objects.link(obj)
        mesh.materials.append(NATURAL);mesh.materials.append(TINT)
        colors=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='POINT')
        for i,c in enumerate(self.c):colors.data[i].color=c
        for i,p in enumerate(mesh.polygons):p.material_index=int(self.paint[i]);p.use_smooth=True
        # Blender Z-up -> Unity Y-up；交换轴后显式反转绕序，不依赖 FBX 导入器的隐含轴处理。
        return {'name':self.name,'vertices':[round(q,7) for x,y,z in self.v for q in (x,z,y)],
                'colors':[round(q,4) for c in self.c for q in c],
                'triangles':[q for f,p in zip(self.f,self.paint) if not p for q in (f[0],f[2],f[1])],
                'paintTriangles':[q for f,p in zip(self.f,self.paint) if p for q in (f[0],f[2],f[1])]}

def ground(lod):
    m=Mesh('ground');n=72 if lod==0 else 36;rings=12 if lod==0 else 6
    center=m.vert((0,0,height(0,0)),GREEN)
    for j in range(1,rings+1):
        for i in range(n):
            a=TAU*i/n;rad=R*math.cos(math.pi/6)/math.cos((a% (math.pi/3))-math.pi/6)
            # 六角顶点30°，与规则坐标一致。
            a+=math.pi/6;x,y=polar(rad*j/rings,a);f=j/rings
            c=(.19+.045*math.sin(x*230)+.05*f,.44+.08*math.cos(y*150),.045+.045*f,1)
            m.vert((x,y,height(x,y)),c)
    for i in range(n):m.face([center,1+i,1+(i+1)%n])
    for j in range(rings-1):
        for i in range(n):
            a=1+j*n+i;b=1+j*n+(i+1)%n;m.face([a,a+n,b+n,b])
    # 不含景观的中性低底座与闭合底面。
    bottom=m.vert((0,0,0),WOOD)
    for i in range(n):
        a=1+(rings-1)*n+i;b=1+(rings-1)*n+(i+1)%n
        va,vb=m.v[a],m.v[b];c=m.vert((va[0],va[1],0),WOOD);d=m.vert((vb[0],vb[1],0),WOOD)
        aa=m.vert(va,(.34,.27,.13,1));bb=m.vert(vb,(.34,.27,.13,1));m.face([aa,c,d,bb]);m.face([bottom,d,c])
    return m

def ranch(vertex,lod,upgrade=False):
    m=Mesh(('city-' if upgrade else 'ranch-')+str(vertex));a=math.radians(30+60*vertex)
    def pos(r,t=0):return polar(r,a+t)
    if upgrade:
        x,y=pos(.018,-.45);m.box((x,y,height(x,y)+.0012),(.002,.003,.0024),(.65,.45,.13,1),angle=a)
        x,y=pos(.023,.27);m.box((x,y,height(x,y)+.0016),(.0015,.0015,.0032),WOOD)
        m.box((x,y,height(x,y)+.0038),(.003,.0002,.0018),PLAYER,True,angle=a)
        return m
    m.grass(a-math.pi/3,a+math.pi/3,640 if lod==0 else 100,140+vertex,True)
    # 设施收在内部120度区域，围栏柔化扇区边界，留出通向相邻建筑的门。
    points=[pos(.011,-.70),pos(.019,-.72),pos(.025,-.48),pos(.026,-.1),pos(.025,.40),pos(.018,.70),pos(.010,.62)]
    for x,y in points:m.box((x,y,height(x,y)+.0017),(.00065,.00065,.0034))
    for i in range(len(points)-1):
        if i==3:continue
        for z in [.0012,.0025]:
            x,y=points[i];u,v=points[i+1];m.beam((x,y,height(x,y)+z),(u,v,height(u,v)+z),.00045)
    x,y=pos(.020,.20);base=height(x,y)
    for dx in [-.003,.003]:
        for dy in [-.002,.002]:m.box((x+dx,y+dy,base+.002),(.00055,.00055,.004))
    # 薄顶棚含弧度，所属玩家只影响布面，不给地表整体染色。
    ids=[]
    for j in range(3):
        for k in range(5):ids.append(m.vert((x-.0034+k*.0017,y-.0024+j*.0024,base+.0042+(.0006 if k==2 else 0)),PLAYER))
    for j in range(2):
        for k in range(4):q=j*5+k;m.face([ids[q],ids[q+1],ids[q+6],ids[q+5]],True)
    x,y=pos(.014,-.40);z=height(x,y)
    m.box((x,y,z+.0006),(.004,.0015,.0012));m.box((x,y,z+.00125),(.0033,.001,.00015),(.17,.42,.42,1))
    for r,t in [(.015,.13),(.021,-.35),(.011,-.15)]:x,y=pos(r,t);m.sheep(x,y,10 if lod==0 else 6)
    return m

def material(name,tint=False):
    mat=bpy.data.materials.new(name);mat.use_nodes=True;n=mat.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Roughness'].default_value=.78
    if tint:p.inputs['Base Color'].default_value=(.56,.055,.027,1)
    else:
        attr=n.new('ShaderNodeVertexColor');attr.layer_name='Color';mat.node_tree.links.new(attr.outputs['Color'],p.inputs['Base Color'])
    return mat

def main():
    global NATURAL,TINT
    ap=argparse.ArgumentParser();ap.add_argument('--output',default=str(ROOT/'art'));args=ap.parse_args(sys.argv[sys.argv.index('--')+1:])
    out=Path(args.output);exp=out/'exports/ultra/v002/pasture';source=out/'source/ultra/v002/pasture'
    exp.mkdir(parents=True,exist_ok=True);source.mkdir(parents=True,exist_ok=True)
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    NATURAL=material('草原自然色');TINT=material('玩家棚布')
    data={'version':'u2-pasture-v002','lods':[]};stats=[]
    for lod in range(2):
        parts=[ground(lod)]
        for i in range(6):
            m=Mesh('wild-'+str(i));a=math.radians(30+60*i)
            m.grass(a,a+math.pi/3,640 if lod==0 else 80,80+i)
            if i%2==0:x,y=polar(.018,a+.45);m.sheep(x,y,10 if lod==0 else 6)
            parts.append(m)
        parts.extend(ranch(i,lod) for i in range(6));parts.extend(ranch(i,lod,True) for i in range(6))
        payload=[p.export() for p in parts];data['lods'].append({'parts':payload})
        for obj in list(bpy.context.scene.objects):
            if not obj.name.startswith('LOD'):obj.name='LOD'+str(lod)+'_'+obj.name
        stats.append({'lod':lod,'pristineTriangles':sum(len(p.f) for p in parts[:7]),'allVariantsTriangles':sum(len(p.f) for p in parts)})
    target=exp/'pasture-mesh.json';target.write_text(json.dumps(data,separators=(',',':')),encoding='utf-8')
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.export_scene.fbx(filepath=str(exp/'pasture.fbx'),use_selection=True,object_types={'MESH'},apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False)
    # 打开源文件时只显示高LOD原始态；其余互斥模块保留，避免源场景叠放全部变体。
    for obj in bpy.context.scene.objects:
        visible=obj.name.startswith('LOD0_ground') or obj.name.startswith('LOD0_wild-')
        obj.hide_render=not visible;obj.hide_set(not visible)
    bpy.ops.wm.save_as_mainfile(filepath=str(source/'pasture.blend'))
    manifest={'version':data['version'],'unit':'米','radius':R,'interiorRadius':INNER,'surfaceHeight':.008,'seed':'80..85 / 140..145',
              'reference':'assets/prototypes/ultra/v006/terrain-states/grassland-*-v006.png','meshSha256':hashlib.sha256(target.read_bytes()).hexdigest(),'stats':stats,
              'note':'静态样板；低/高LOD共享同一布局和羊的数量。FBX用于交换，Unity显式网格保留颜色和轴向。'}
    manifest['references']=[{'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in sorted((ROOT/'assets/prototypes/ultra/v006/terrain-states').glob('grassland-*-v006.png'))]
    (exp/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
    print('CATAN_U2_MESH_OK '+str(stats))

if __name__=='__main__':main()
