"""由 Blender MCP 执行。以人物身高为造型单位，输出时统一换算为米。"""
import bpy
import math
import struct
import tempfile
import zlib
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = Path(__file__).resolve().parent
DEST = ROOT / 'Unity/Catan/Assets/Resources/NeutralPieces/v001'
PALETTE = {
    'skin':'cb946d','shadow':'986044','hair':'49382c','dark':'332e29',
    'linen':'ded2ae','green':'616d4e','blue':'505e75','vest':'484647',
    'pants':'696257','leather':'70513d','amber':'ae794a','wood':'976e49',
    'board':'b18a60','steel':'c4cdd1','edge':'eef1ed','gold':'c8ac6e',
    'straw':'cdad73','brim':'b18b55','pot':'809784','rock':'787970',
    'earth':'776b53','grass':'89905d','red':'a95442','eye':'211e1a'
}
scene = bpy.data.scenes.new('商路中立棋子')
bpy.context.window.scene = scene
mesh_objects = []
vertices, faces, shades, colors = [], [], [], []
offset = Vector((0,0,0))
V = Vector

def palette_image():
    # 小色块内部保留浅淡的明暗渐变，避免大面积纯平涂。
    size=128
    rows=[]
    for y in range(size):
        row=bytearray([0])
        for x in range(size):
            index=(7-y//16)*8+x//16
            key=list(PALETTE.values())[min(index,len(PALETTE)-1)]
            tint=.95+.07*(15-y%16)/15+.025*math.sin(x%16*.21)
            row.extend(min(255,round(int(key[i:i+2],16)*tint)) for i in (0,2,4))
        rows.append(row)
    def chunk(tag,data):
        return struct.pack('>I',len(data))+tag+data+struct.pack('>I',zlib.crc32(tag+data))
    path=DEST/'neutral-palette.png'
    path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>2I5B',size,size,8,2,0,0,0))+chunk(b'IDAT',zlib.compress(b''.join(rows)))+chunk(b'IEND',b''))
    return path

material=bpy.data.materials.get('NeutralPalette') or bpy.data.materials.new('NeutralPalette')
material.use_nodes=True
image=bpy.data.images.load(str(palette_image()),check_existing=True)
texture=next((node for node in material.node_tree.nodes if node.type=='TEX_IMAGE'),None) or material.node_tree.nodes.new('ShaderNodeTexImage')
texture.image=image
material.node_tree.links.new(texture.outputs['Color'],material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
image.pack()

def face(ids,color,smooth=True):
    faces.append(ids); colors.append(color); shades.append(smooth)

def rings(rows,color,smooth=True):
    start=len(vertices); n=len(rows[0])
    for row in rows:
        vertices.extend(tuple((V(p)+offset)*.010) for p in row)
    face(list(reversed(range(start,start+n))),color,False)
    for j in range(len(rows)-1):
        for i in range(n):
            a=start+j*n+i; b=start+j*n+(i+1)%n
            face([a,b,b+n,a+n],color,smooth)
    end=start+(len(rows)-1)*n
    face(list(range(end,end+n)),color,False)

def loft(stations,color,n=16):
    rows=[]
    for x,y,z,rx,ry in stations:
        rows.append([(x+rx*math.cos(i*math.tau/n),y+ry*math.sin(i*math.tau/n),z) for i in range(n)])
    rings(rows,color)

def orb(p,r,color):
    x,y,z=p; a,b,c=r
    loft([(x,y,z+c*math.sin(t),a*math.cos(t),b*math.cos(t)) for t in (-1.52,-1.15,-.72,-.36,0,.36,.72,1.15,1.52)],color)

def pipe(points,radii,color,n=12):
    pts=list(map(V,points)); rows=[]
    for i,(p,r) in enumerate(zip(pts,radii)):
        axis=(pts[min(i+1,len(pts)-1)]-pts[max(i-1,0)]).normalized()
        ref=V((0,1,0)) if abs(axis.y)<.9 else V((1,0,0))
        u=axis.cross(ref).normalized(); v=axis.cross(u)
        rows.append([p+r*(u*math.cos(j*math.tau/n)+v*math.sin(j*math.tau/n)) for j in range(n)])
    rings(rows,color)

def bar(a,b,r,color,n=12):
    pipe([a,b],[r,r],color,n)

def plank(x,y,z,w,d,h,color):
    rows=[]
    for level,shrink in [(z,.025),(z+.025,0),(z+h-.025,0),(z+h,.025)]:
        a=w/2-shrink; b=d/2-shrink; cut=min(.035,a/2,b/2)
        rows.append([(x+px,y+py,level) for px,py in [(-a+cut,-b),(a-cut,-b),(a,-b+cut),(a,b-cut),(a-cut,b),(-a+cut,b),(-a,b-cut),(-a,-b+cut)]])
    rings(rows,color,False)

def finish(name):
    mesh=bpy.data.meshes.new(name); mesh.from_pydata(vertices,[],faces);mesh.update()
    uv=mesh.uv_layers.new(name='颜料')
    for poly,key,smooth in zip(mesh.polygons,colors,shades):
        poly.use_smooth=smooth
        idx=list(PALETTE).index(key)
        for j,loop in enumerate(poly.loop_indices):
            t=j*math.tau/len(poly.loop_indices)
            uv.data[loop].uv=((idx%8+.5+.25*math.cos(t))/8,(idx//8+.5+.25*math.sin(t))/8)
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj);mesh.materials.append(material)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if name=='peddler-stall':
        bake_peddler(obj)
    if name!='robber-band' or not globals().get('merchant_only',False):
        with tempfile.TemporaryDirectory(prefix='neutral-export-',dir=ROOT/'.local') as stage:
            staged=Path(stage)/(name+'.fbx')
            bpy.ops.export_scene.fbx(filepath=str(staged),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,add_leaf_bones=False)
            staged.replace(DEST/staged.name)
    mesh_objects.append(obj)
    for arr in (vertices,faces,shades,colors): arr.clear()

exec(compile((SOURCE/'peddler.py').read_text('utf-8-sig'),str(SOURCE/'peddler.py'),'exec'))
if not globals().get('merchant_only',False):
    exec(compile((SOURCE/'robber.py').read_text('utf-8-sig'),str(SOURCE/'robber.py'),'exec'))
    robber()
if not globals().get('robber_only',False):
    peddler()
    offset=V((0,0,0))
    for x in (-.70,.70):
        for y in (-.44,-.08): plank(x,y,0,.10,.10,.70,'wood')
    for i in range(3): plank(0,-.55+.188*i,.60,1.73,.19,.083,'board')
    for x,col in [(-.56,'blue'),(-.25,'linen')]:
        pipe([(x,-.63,.80),(x,-.585,.80),(x,-.14,.80)],[.121,.13,.125],col,18)
        for rad in (.075,.035):
            pipe([(x,-.635,.80),(x,-.63,.80)],[rad,rad],col if rad==.035 else 'leather',16)
    offered_jug()
    plank(.51,-.155,.688,.198,.26,.058,'amber')
    for i in range(6): bar((.44,-.235+i*.03,.750),(.57,-.235+i*.03,.750),.009,'wood',8)
    stall_textiles()
    finish('peddler-stall')
if len(mesh_objects)==2:
    mesh_objects[0].location.x=-.016
    mesh_objects[1].location.x=.016
    bpy.data.libraries.write(str(SOURCE/'商路棋子.blend'), {scene}, compress=True)
elif globals().get('merchant_only',False):
    bpy.data.libraries.write(str(SOURCE/'货郎递壶.blend'), {scene}, compress=True)
result={}
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    result[obj.name]={'triangles':len(obj.data.loop_triangles),'metres':list(obj.dimensions)}
