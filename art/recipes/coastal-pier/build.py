"""在 Blender 中生成共用栈桥、嵌牌和六种装卸货物。构件尺寸以毫米计。"""
import bpy
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
DEST = ROOT / "Unity/Catan/Assets/Resources/PaintedMap/Coast/pier.fbx"
rng = random.Random(429)
scene = bpy.context.scene
objects = []
verts, faces, colors, uvs = [], [], [], []

def face(points, color, uv=None):
    start = len(verts)
    verts.extend(tuple(v * .001 for v in p) for p in points)
    faces.append(tuple(range(start, start + len(points))))
    colors.extend([color] * len(points))
    uvs.extend(uv or [(p[0] * .036 + p[2] * .017, p[1] * .061 + p[2] * .08) for p in points])

def finish(name):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    tint = mesh.color_attributes.new(name="Pigment", type="BYTE_COLOR", domain="CORNER")
    uv = mesh.uv_layers.new(name="Grain")
    for loop in mesh.loops:
        i = loop.vertex_index
        c = colors[i]
        tint.data[loop.index].color_srgb = tuple(int(c[j:j+2], 16)/255 for j in (0,2,4))+(1,)
        uv.data[loop.index].uv = uvs[i]
    if name=="PierDeck":
        contact_uv=mesh.uv_layers.new(name="货堆接触阴影")
        for loop in mesh.loops:
            x,y,_=verts[loop.vertex_index]
            contact_uv.data[loop.index].uv=(x/.034+.5,y/.034+.5)
        mesh.uv_layers.active_index=0
    obj = bpy.data.objects.new(name, mesh)
    scene.collection.objects.link(obj)
    objects.append(obj)
    for buf in (verts, faces, colors, uvs):
        buf.clear()

def rings(layers, color, caps=True):
    for a,b in zip(layers, layers[1:]):
        for i in range(len(a)):
            j = (i+1)%len(a)
            face([a[i],a[j],b[j],b[i]],color)
    if caps:
        face(list(reversed(layers[0])),color)
        face(layers[-1],color)

def box(x,y,z,w,d,h,c,bevel=.13,yaw=0):
    def layer(inset, height):
        a,b=w/2-inset,d/2-inset
        k=min(.24,a*.2,b*.2)
        outline=[(-a+k,-b),(a-k,-b),(a,-b+k),(a,b-k),(a-k,b),(-a+k,b),(-a,b-k),(-a,-b+k)]
        co,si=math.cos(yaw),math.sin(yaw)
        return [(x+u*co-v*si,y+u*si+v*co,height) for u,v in outline]
    e=min(bevel,h*.22,w*.18,d*.18)
    rings([layer(e,z),layer(0,z+e),layer(0,z+h-e),layer(e,z+h)],c)

def spindle(base, axis, profile, color, sides=12, phase=0):
    q=Vector((0,0,1)).rotation_difference(Vector(axis).normalized())
    layers=[]
    for along,r in profile:
        layers.append([Vector(base)+q@Vector((r*math.cos(i*math.tau/sides+phase),
                    r*math.sin(i*math.tau/sides+phase),along)) for i in range(sides)])
    rings(layers,color)

def stick(a,b,r,c,n=9):
    direction=Vector(b)-Vector(a)
    spindle(a,direction,[(0,r),(direction.length,r)],c,n)

def hoop(x,y,z,r,t,color):
    spindle((x,y,z),(0,0,1),[(0,r),(.08,r+.06),(t-.06,r+.06),(t,r)],color,16)

woods=['a7744c','b98252','bd8d62','ad7953','c29467','ac7854']
timber='694c38'
# 桥梁和坡道承重梁。岸端高度与地块表面一致。
for x in (-2.7,2.7):
    box(x,5.6,-1.25,1.0,11.5,1.52,timber)
for x in (-10.5,0,10.5):
    box(x,-7.95,-1.18,1.25,15.4,1.85,timber)
for j in range(11):
    box(0,-14.98+j*1.40,.53,24.8,1.37,.69,rng.choice(woods))
for j in range(8):
    y=.42+j*1.48
    box(0,y,.51-.069*j,6.1,1.46,.69,rng.choice(woods))
# 桥桩、横向系梁和绳栏；水线以下仍有桩身。
for x in (-11.25,11.25):
    for y in ((-14.45,-1.1) if x<0 else (-14.45,)):
        spindle((x,y,-5.2),(0,0,1),[(0,.82),(6.4,1.0),(8.0,1.03),(8.14,.90)],'78543d')
        hoop(x,y,2.15,1.04,.36,'cdac75')
for y in (-14.45,-1.1):
    box(0,y,-1.1,23.3,1.35,1.52,'79533d')
for x in (-3.15,3.15):
    for y in (1.1,6.0,10.7):
        top=2.85-.065*y
        spindle((x,y,-4.9),(0,0,1),[(0,.43),(top+4.75,.52),(top+4.9,.45)],'916345',10)
        hoop(x,y,top-.49,.52,.26,'d9bb81')
    for y0,y1 in ((1.1,6),(6,10.7)):
        path=[(x,y0+(y1-y0)*i/4,2.64-.065*(y0+(y1-y0)*i/4)-.40*math.sin(math.pi*i/4)) for i in range(5)]
        for a,b in zip(path,path[1:]):
            stick(a,b,.13,'d0ae74',7)
# 近岸角桩上的木盖与铜箍。贸易信息由同款悬浮玉签显示。
for y in (.19,1.18):
    box(8.65,y,.53,7.08,.97,.69,'926e4b')
spindle((8.65,-1.85,-5.2),(0,0,1),[(0,1.25),(6.4,1.47),(8.47,1.39),(8.62,1.28)],'785338',16)
for z in (1.36,2.65):
    hoop(8.65,-1.85,z,1.43,.22,'866840')
# 橡木板两端的钉帽与桩顶端纹都直接落在实物表面。
for j in range(11):
    for x in (-10.1,10.1):
        spindle((x,-14.98+j*1.40,1.22),(0,0,1),[(0,.19),(.045,.16)],'53483c',8)
for x in (-11.25,11.25):
    for y in ((-14.45,-1.1) if x<0 else (-14.45,)):
        for idx,(r,c) in enumerate(((.88,'b48959'),(.66,'8f6b4b'),(.54,'b18a5c'),(.32,'957454'),(.19,'ba9162'))):
            spindle((x,y,2.94+idx*.009),(0,0,1),[(0,r),(.009,r)],c,12)
finish("PierDeck")
# 六种货物只共享平台和材质图集，各有独立的真实轮廓。
import sys
from importlib import reload
recipe=Path(__file__).resolve().parent
sys.path.insert(0,str(recipe))
try:
    import sculpt, cargo, contact
    reload(sculpt);reload(cargo);reload(contact)
    measures={}
    # 横木与麦束的长边各取平台的一轴；体积较小的砖、矿、毛卷占满空出的货位。
    placements={'wood':(1.43,1.49),'brick':(.06,1.66),'ore':(.17,1.43),
                'wheat':(-.05,1.46),'wool':(.13,1.61),'generic':(.10,1.52)}
    for kind,make in cargo.KINDS.items():
        for proxy in (False,True):
            model=sculpt.Sculpt(proxy)
            yaw,scale=placements[kind]
            model.reset_transform((-3.05,-8.08,1.23),scale,yaw)
            make(model)
            obj=model.finish(('Pick_' if proxy else 'Cargo_')+kind)
            objects.append(obj)
            obj.data.calc_loop_triangles()
            measures[obj.name]=len(obj.data.loop_triangles)
    contact.bake(objects,cargo.KINDS,DEST.parent/'cargo-contact.png')
finally:
    sys.path.remove(str(recipe))

bpy.ops.object.select_all(action="DESELECT")
for obj in objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(DEST),use_selection=True,axis_forward='-Z',axis_up='Y',
    bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',colors_type='SRGB',use_triangles=True,
    bake_anim=False,object_types={'MESH'})

import json
(recipe/'货物网格.json').write_text(json.dumps(measures,ensure_ascii=False,indent=2),encoding='utf8')
