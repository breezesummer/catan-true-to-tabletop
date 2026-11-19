"""通过 Blender MCP 生成木材港森林岸的立体沙坡、浅滩与岩体。"""
import bpy, math, random
from pathlib import Path
from math import sin, pi, exp
root = Path('D:/Project/catan-true-to-tabletop')
bpy.ops.wm.read_factory_settings(use_empty=True)
rng = random.Random(217)
def mesh(name, points, faces, colors):
    data = bpy.data.meshes.new(name)
    data.from_pydata(points, [], faces)
    data.update()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    attr = data.color_attributes.new(name='Pigment', type='FLOAT_COLOR', domain='POINT')
    for item, color in zip(attr.data, colors):
        item.color = color
    return obj
# 一条边长为一个单位。两侧沙嘴向海延伸，中央退成浅弯。
points, faces, colors = [], [], []
# 向栈桥侧绕过顶点，圆滑连接两条相邻六边形边。
for i in range(102):
    x = -.2625 + i / 80
    taper = min(1, max(0, (x+.2625)/.12), max(0, (1-x)/.10))
    # 平滑的两边交线；在角点向海多铺一段沙肩。
    base = .866*(x-math.sqrt(x*x+.036**2))
    width = taper*(.060+.074*exp(-((x-.12)/.13)**2)+.059*exp(-((x-.80)/.13)**2)+.009*sin(30*x))
    shelf = taper*(.068+.103*exp(-((x-.43)/.26)**2))
    blend = taper*(.052+.012*sin(31*x)+.008*sin(61*x))
    rows = [(-blend,.136,(.92,.76,.48,0)),(-.32*blend,.132,(.92,.76,.49,.70)),
            (.06*width,.124,(.91,.75,.48,1)),(.46*width,.080,(.86,.65,.39,1)),
            (width,.014,(.68,.54,.36,1)),(width+.19*shelf,.009,(.42,.60,.50,.84)),
            (width+.59*shelf,.006,(.25,.53,.48,.51)),(width+shelf,.004,(.14,.43,.40,0))]
    count=len(rows)
    for j,(offset,z,color) in enumerate(rows):
        points.append((x,base+offset,z))
        colors.append(color)
        k=len(points)-1
        if i and j:
            faces.extend([(k-count-1,k-1,k),(k-count-1,k,k-count)])
mesh('SandShelf',points,faces,colors)

def reef(n,x,y,s):
    # 偏移的棱脊和不等宽岩面产生劈裂岩块，水下保留完整底座。
    ring=[(-1,-.35),(-.42,-.83),(.72,-.65),(1,.10),(.39,.76),(-.68,.59)]
    pts=[(x+s*u,y+s*v,-.024) for u,v in ring]
    pts += [(x+s*(.72*u-.15),y+s*(.72*v+.06),s*(.37+.25*v+.13*u)) for u,v in ring]
    pts += [(x-.46*s,y+.06*s,.95*s),(x+.39*s,y+.20*s,.66*s)]
    facets=[(6,7,12),(7,8,13,12),(8,9,13),(9,10,13),(10,11,12,13),(11,6,12)]
    # 湿线沿岩面逐角起伏，同一切面上的干湿两段共用法向。
    for i in range(6):
        foot, shoulder = pts[i], pts[i+6]
        height = min(shoulder[2]*.82, .009+s*.13+.002*sin(i*1.8+n))
        fraction = (height-foot[2])/(shoulder[2]-foot[2])
        pts.append(tuple(a+(b-a)*fraction for a,b in zip(foot, shoulder)))
    for i in range(6):
        j=(i+1)%6
        facets.extend([(i,j,14+j,14+i),(14+i,14+j,6+j,6+i)])
    verts,faces,colors=[],[],[]
    stone=random.Random(217+n)
    for f in facets:
        brightness=stone.uniform(.46,.66) if min(f)>=6 else stone.uniform(.40,.50)
        faces.append(tuple(range(len(verts),len(verts)+len(f))))
        for idx in f:
            verts.append(pts[idx])
            wet = .51 if idx<6 or idx>=14 else 1
            colors.append((brightness*.89*wet,brightness*.90*wet,brightness*.80*wet,1))
    mesh('Reef'+str(n),verts,faces,colors)
    # 同一来浪方向下，小礁背水面留白，迎浪面分出短沫和较长的浪梢。
    pts,faces,colors=[],[],[]
    wave=random.Random(918+n)
    angle=wave.uniform(-.22,.72)
    for patch in range(2+n%3):
        span=wave.uniform(.16,.36) if patch%2 else wave.uniform(.48,.94)
        offset=wave.uniform(.97,1.14)
        strength=wave.uniform(.42,.79)
        width=wave.uniform(.08,.22)
        for i in range(13):
            t=i/12
            a=angle+span*t
            crest=sin(pi*t)**.7
            r=offset+.10*sin(5*a+n)+.05*sin(13*a)
            for j,(d,alpha) in enumerate([(-width*.42,.10),(0,strength),(width,0)]):
                radius=r+d*crest
                pts.append((x+math.cos(a)*s*radius,y+sin(a)*s*.78*radius,.012))
                colors.append((.84,.91,.83,alpha*crest))
                k=len(pts)-1
                if i and j: faces.extend([(k-4,k-1,k),(k-4,k,k-3)])
        angle+=span+wave.uniform(.19,.81)
    mesh('Foam'+str(n),pts,faces,colors)

for n,(x,y,s) in enumerate([(-.048,.055,.045),(.011,.119,.032),(.126,.169,.039),
                             (.19,.181,.020),(.73,.128,.032),(.79,.147,.048),(.84,.137,.019)]):
    reef(n,x,y,s)
for name,pos in [('Origin',(0,0,0)),('End',(1,0,0)),('Sea',(0,1,0))]:
    obj=bpy.data.objects.new(name,None)
    bpy.context.collection.objects.link(obj)
    obj.location=pos
folder=root/'Unity/Catan/Assets/Art/PaintedMap/Coast'
# 以同一个模型的接陆线为轴，分别塑造窄岸、偏心沙嘴与绕角沙肩。
# 礁石和碎浪跟随同一横断面移动，岩体本身保留原比例。
models=[o for o in bpy.context.scene.objects if o.type=='MESH']
original={o.name:[v.co.copy() for v in o.data.vertices] for o in models}
rocks={o.name[4:]:sum(v.x for v in original[o.name])/len(original[o.name])
       for o in models if o.name.startswith('Reef')}
def stretch(x,variant):
    if variant=='CoveSpit':
        return .67+1.22*exp(-((x-.62)/.34)**2)+.32*exp(-((x-.09)/.23)**2)
    if variant=='CoveShoulder':
        return .72+1.15*exp(-((x-.06)/.32)**2)+.38*exp(-((x-.82)/.22)**2)
    return .40+.48*exp(-((x-.38)/.22)**2)
for variant in ['ForestCove','CoveSpit','CoveShoulder','CoveNarrow']:
    for obj in models:
        coords=original[obj.name]
        if obj.name!='SandShelf':
            center=rocks[obj.name[4:]]
            rock=original['Reef'+obj.name[4:]]
            center_y=sum(v.y for v in rock)/len(rock)
        for vertex,p in zip(obj.data.vertices,coords):
            vertex.co=p
            if variant=='ForestCove': continue
            x=p.x if obj.name=='SandShelf' else center
            base=.866*(x-math.sqrt(x*x+.036**2))
            offset=p.y-base if obj.name=='SandShelf' else center_y-base
            if offset>0: vertex.co.y+=offset*(stretch(x,variant)-1)
        obj.data.update()
    bpy.ops.export_scene.fbx(filepath=str(folder/(variant+'.fbx')),add_leaf_bones=False)
result={'models':4}