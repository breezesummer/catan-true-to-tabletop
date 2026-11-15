"""通过 Blender MCP 制作跨两格的南岸浅湾，并导出草岸、岩岸局部模型。"""
import bpy, math, random
from pathlib import Path
from math import sin, cos, exp, pi
bpy.ops.wm.read_factory_settings(use_empty=True)
folder=Path('D:/Project/catan-true-to-tabletop/Unity/Catan/Assets/Resources/PaintedMap/Coast')
rng=random.Random(809)
def surface(name,points,faces,colors):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(points,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.collection.objects.link(obj)
    attr=mesh.color_attributes.new(name='Pigment',type='FLOAT_COLOR',domain='POINT')
    for item,col in zip(attr.data,colors): item.color=col
def rock(x,y,r):
    points=[]; faces=[]; colors=[]
    n=6
    ring=[(cos(i*2*pi/n)*rng.uniform(.82,1.18),sin(i*2*pi/n)*rng.uniform(.78,1.05)) for i in range(n)]
    ridge=[(x+r*(u*.68-.14),y+r*v*.59,r*(.46+.27*v+.14*u)) for u,v in ring]
    peak=(x-r*.28,y+r*.09,r*.95)
    for i in range(n):
        j=(i+1)%n
        foot=lambda k:(x+r*ring[k][0],y+r*.74*ring[k][1],-.018)
        for face,tint in [([foot(i),foot(j),ridge[j],ridge[i]],rng.uniform(.30,.46)),([ridge[i],ridge[j],peak],rng.uniform(.51,.74))]:
            faces.append(tuple(range(len(points),len(points)+len(face))))
            for p in face:
                points.append(p)
                shade=tint*(.70 if p[2]<0 else 1)
                colors.append((shade,shade*.91,shade*.71,1))
    surface('岸岩',points,faces,colors)
def strip(name,samples):
    pts=[]; faces=[]; colors=[]
    for i,(x,rows) in enumerate(samples):
        count=len(rows)
        for j,(y,z,col) in enumerate(rows):
            pts.append((x,y,z));colors.append(col)
            k=len(pts)-1
            if i and j: faces.extend([(k-count-1,k-count,k),(k-count-1,k,k-1)])
    surface(name,pts,faces,colors)
def save(name):
    for key,pos in [('Origin',(0,0,0)),('End',(1,0,0)),('Sea',(0,1,0))]:
        obj=bpy.data.objects.new(key,None);bpy.context.collection.objects.link(obj);obj.location=pos
    bpy.ops.export_scene.fbx(filepath=str(folder/(name+'.fbx')),add_leaf_bones=False)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete()
# 边长归一化，两格南侧共用一条浅滩外缘，接陆线沿原六边形内移渐隐。
def bay():
    h=math.sqrt(3)/2
    for i in range(151):
        x=(i/150*4-2)*h
        edge=-1+.5*abs(abs(x)-h)/h
        fade=max(0,sin(pi*i/150))**.42
        shore=edge-.055*fade
        # 南岸中央浅水外缘跨过原有棋格接角。
        outside=-.5-.84*fade-.07*cos(x*3.5)*fade
        meadow=exp(-((x-.76)/.57)**4)
        outcrop=exp(-((x+1.13)/.37)**4)
        tongue=fade*(.04+.15*meadow+.095*outcrop)
        tint=(.90-.24*meadow-.22*outcrop,.74-.045*meadow-.21*outcrop,.45-.12*meadow-.035*outcrop)
        end=shore-tongue
        yield x,[(edge+.16*fade,.145,(*tint,0)),(edge+.038*fade,.133,(*tint,.96)),
                 (shore-.51*tongue,.077,(*tint,1)),(end,.013,(.71-.12*meadow,.58,.39,1)),
                 (end*.86+outside*.14,.010,(.49,.67,.54,.86)),(end*.56+outside*.44,.008,(.37,.66,.57,.76)),
                 (end*.20+outside*.80,.006,(.23,.56,.52,.43)),(outside,.004,(.16,.46,.44,0))]
strip('跨格浅湾',bay())
for x,y,r in [(-1.27,-.91,.15),(-1.12,-1.02,.19),(-.96,-1.10,.125),(-.84,-1.15,.071),(-1.34,-1.01,.066),(-.71,-1.19,.036),(1.18,-1.03,.056)]:
    rock(x,y,r)
save('SouthernBay')
for meadow,name in [(True,'GrassyBank'),(False,'RockyBank')]:
    def rows():
        for i in range(51):
            x=.16+.64*i/50
            t=sin(pi*i/50)**1.4
            width=t*(.15+.035*sin(9*x))
            c=(.66,.71,.31) if meadow else (.68,.54,.41)
            yield x,[(-.09*t,.145,(*c,0)),(-.02*t,.137,(*c,.91*t)),(.49*width,.082,(*c,t)),(width,.019,(*c,.9*t)),(width+.025*t,.008,(*c,0))]
    strip(name,rows())
    if not meadow:
        for x,y,r in [(.34,.116,.089),(.45,.151,.108),(.57,.124,.057)]: rock(x,y,r)
    save(name)
result={'models':3}
