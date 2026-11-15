"""用 Blender MCP 导出港屋和可绕旗杆转动的吊旗。尺寸单位为毫米。"""
import bpy
import math
import random
from mathutils import Vector
from pathlib import Path

output = Path(__file__).resolve().parents[3] / "Unity/Catan/Assets/Resources/PaintedMap/Coast/harbor-house.fbx"
objects = []
vertices, faces, pigments, coords = [], [], [], []

def face(points, color, uv=None):
    start = len(vertices)
    vertices.extend(tuple(v*.001 for v in p) for p in points)
    faces.append(tuple(range(start, len(vertices))))
    pigments.extend([tuple(int(color[i:i+2],16)/255 for i in (0,2,4))+(1,)]*len(points))
    coords.extend(uv or [(x*.06+z*.08,y*.07+z*.05) for x,y,z in points])

def finish(name):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(vertices,[],faces)
    mesh.update()
    uv=mesh.uv_layers.new(name="UVMap")
    color=mesh.color_attributes.new(name="Pigment",type="BYTE_COLOR",domain="CORNER")
    for loop in mesh.loops:
        uv.data[loop.index].uv=coords[loop.vertex_index]
        color.data[loop.index].color_srgb=pigments[loop.vertex_index]
    obj=bpy.data.objects.new(name,mesh)
    bpy.context.scene.collection.objects.link(obj)
    objects.append(obj)
    for values in (vertices,faces,pigments,coords): values.clear()

def prism(bottom,top,color):
    face(bottom[::-1], color)
    face(top,color)
    for i in range(len(top)):
        j=(i+1)%len(top)
        face([bottom[i],bottom[j],top[j],top[i]],color)

def box(x,y,z,w,d,h,color):
    ring=[(x+a*w/2,y+b*d/2,z) for a,b in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    prism(ring,[(a,b,c+h) for a,b,c in ring],color)

def rod(a,b,r,color,n=8):
    direction=Vector(b)-Vector(a)
    rot=Vector((0,0,1)).rotation_difference(direction.normalized())
    ring=[Vector(a)+rot@Vector((r*math.cos(i*math.tau/n),r*math.sin(i*math.tau/n),0)) for i in range(n)]
    prism(ring,[p+direction for p in ring],color)

# 整条桥面跨在内收的纵梁上，让桩间能看穿水面。岸端最后四板缓坡接地。
rng = random.Random(216)
sea, shore = -21.5, 11.5
pitch = (shore-sea)/25
for i in range(25):
    y = sea+(i+.5)*pitch
    top = 1.22-max(0,y-7)*.20
    box(0,y,top-.66,11.3+rng.uniform(-.14,.14),pitch-.13,.66,
        rng.choice(["bd9167","b58a60","b98d62","c39a71","ae825c"]))
for x in (-4.75,4.75):
    box(x,-5.9,-.45,.72,30.8,1.03,"785739")
for y in (-20.4,-10.9,-1.4,8.2):
    for x in (-5.05,5.05):
        top=2.65 if y<0 else 2.14
        rod((x,y,-6.0),(x,y,top),.61,"916a49",10)
        rod((x,y,top),(x,y,top+.06),.54,"b08961",10)
        rod((x,y,top-.43),(x,y,top-.23),.63,"b69a69",10)
    box(0,y,-.78,11.0,.88,.84,"785638")
    for side in (-1,1):
        rod((side*5.04,y,-1.38),(side*3.65,y,.13),.21,"805f40")
# 板端钉头与顺板纤维贴在表面，远景由整块板的色差接替细纹。
for i in range(25):
    y=sea+(i+.5)*pitch
    top=1.22-max(0,y-7)*.20
    for x in (-4.0,4.0):
        rod((x,y,top),(x,y,top+.025),.10,"71543e",7)
    for j in range(3):
        x=rng.uniform(-4.7,1.5)
        length=rng.uniform(1.1,3.5)
        line=y+rng.uniform(-.42,.42)
        face([(x,line,top+.012),(x+length*.45,line-.035,top+.012),
              (x+length,line+.025,top+.012),(x+length*.50,line+.045,top+.012)],"9f7958")
finish("CoastalWalkway")

# 桥面偏岸一侧留出装卸通道，屋脊下为灰泥、深色梁柱和陶瓦。
x,y=0,4.0
# 四道围墙分段砌，深色窗后壁与洞口之间留出窗台和侧壁。
for side in (-1,1):
    wall_x=side*3.85
    for offset in (-2.55,2.55):
        box(wall_x,y+offset,1.22,.34,1.90,5.8,"dec097")
    box(wall_x,y,1.22,.34,3.2,1.68,"cfac83")
    box(wall_x,y,5.74,.34,3.2,1.28,"ddbb8e")
    box(side*3.32,y,2.89,.15,3.28,2.91,"444d49")
    box(side*3.68,y,2.80,1.02,3.58,.22,"a37d57")
    box(side*3.69,y,5.70,.94,3.58,.34,"805e44")
    for edge in (-1.58,1.58):
        box(side*3.67,y+edge,2.91,.70,.20,2.86,"825f45")
    box(side*3.43,y,2.91,.15,.19,2.80,"a27c54")
    box(side*3.44,y,4.32,.15,3.02,.14,"96714e")
for side in (-1,1):
    front=y+3.35*side
    for offset in (-2.53,2.53):
        box(offset,front,1.22,2.94,.35,5.8,"d9b78f")
    box(0,front,5.58,2.12,.35,1.44,"d6b187")
    box(0,front-side*.33,1.23,2.24,.16,4.48,"574332")
    for leaf in range(5):
        box(-.84+leaf*.42,front-side*.22,1.34,.39,.12,4.12,"846044")
    face([(-4,front,7),(4,front,7),(0,front,10.2)][::1 if side<0 else -1],"ca9d73")
    box(0,front,6.76,8.25,.48,.40,"77553d")
    rod((0,front+side*.16,6.98),(0,front+side*.16,10.12),.16,"806044")
for dx in (-3.90,3.90):
    for dy in (-3.42,3.42):
        box(dx,y+dy,1.22,.52,.52,5.87,"73523b")
    box(dx,y,1.33,.50,7.20,.31,"866449")
    box(dx,y,6.72,.68,7.55,.44,"6e523d")
# 连续檐板承托搭接瓦片；瓦沟沿落水方向走，每排露出暗色的瓦口。
for side in (-1,1):
    def roof_point(run,along,lift):
        return (side*run,y+along,10.72-.76*run+lift)
    outline=[roof_point(r,t,0) for r,t in [(0,-4.65),(5.22,-4.65),(5.22,4.65),(0,4.65)]]
    if side<0: outline.reverse()
    prism([(a,b,c-.35) for a,b,c in outline],outline,"745037")
    for row in range(5):
        inner=row*1.03
        outer=inner+1.10
        for col in range(10):
            start=-4.65+col*.93
            color=rng.choice(["aa6548","ae684b","a55c41","b16c4f","a96245"])
            for band in range(4):
                l,r=band/4,(band+1)/4
                corners=[roof_point(run,start+v*.93,
                             .12+.21*math.sin(math.pi*v)+(.13 if run==outer else 0))
                         for run,v in [(inner,l),(outer,l),(outer,r),(inner,r)]]
                face(corners if side>0 else corners[::-1],color)
                lip=[corners[1],corners[2],(corners[2][0],corners[2][1],corners[2][2]-.18),
                     (corners[1][0],corners[1][1],corners[1][2]-.18)]
                face(lip[::-1] if side>0 else lip,"88513b")
    for along in (-4.65,4.65):
        rod(roof_point(0,along,.05),roof_point(5.22,along,.05),.18,"85523b")
rod((0,y-4.78,10.91),(0,y+4.78,10.91),.32,"995d42")
finish("HarborHouse")

rod((0,0,-5),(0,0,19.9),.49,"987348")
rod((-6.65,0,17.1),(6.65,0,17.1),.36,"93673e")
for side in (-1,1):
    rod((0,0,19.4),(side*6.0,0,17.3),.13,"c49a64")
    rod((side*5.95,-1.0,16.45),(side*5.95,0,17.35),.17,"cab088")
# 下沿由细横撑收住，两端套绳；保留上、下缘之间的自然松量。
rod((-6.12,-3.18,5.02),(6.12,-3.18,5.02),.14,"ab8762")
rod((0,0,4.90),(0,-3.18,5.02),.17,"997a56")
for side in (-1,1):
    for j in range(3):
        a=side*(5.64+.12*j)
        rod((a,-3.31,4.86),(a+.05,-3.23,5.31),.11,"b69c75")
    rod((side*5.92,-3.16,5.12),(side*6.08,-.06,16.85),.075,"b49c77")
finish("HarborMast")

def cloth(u,v):
    # 四角固定。两侧斜褶向内逐渐消散，下沿接近水平。
    span=math.sin(math.pi*u)
    height=math.sin(math.pi*v)
    edge=min(u,1-u)
    crease=math.exp(-((edge-.39*min(v,1-v))/.108)**2)
    depth=-3.18+2.18*v-span*height*.90+.62*crease*height
    depth+=.29*span*height*math.sin(math.tau*(u+.30*v))
    return ((u-.5)*(11.9-.33*height),depth,5.10+11.40*v-span*(.10+.40*v))
for j in range(12):
    for i in range(32):
        uv=[(i/32,j/12),((i+1)/32,j/12),((i+1)/32,(j+1)/12),(i/32,(j+1)/12)]
        face([cloth(u,v) for u,v in uv],"ffffff",uv)
finish("HarborFlag")
bpy.ops.object.select_all(action="DESELECT")
for obj in objects: obj.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.export_scene.fbx(filepath=str(output),use_selection=True,axis_forward="-Z",axis_up="Y",
    bake_space_transform=True,apply_scale_options="FBX_SCALE_ALL",colors_type="SRGB",use_triangles=True,bake_anim=False)
