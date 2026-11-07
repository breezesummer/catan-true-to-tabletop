"""由 Blender MCP 执行。以人物身高为造型单位，输出时统一换算为米。"""
import bpy
import math
import struct
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

def head(role,z):
    orb((0,0,z),(.163,.146,.205),'skin')
    orb((0,-.137,z-.014),(.039,.057,.069),'skin')
    for side in (-1,1):
        x=side*.068
        orb((x,-.135,z+.034),(.023,.011,.014),'eye')
        bar((x-side*.031,-.135,z+.054),(x+side*.035,-.125,z+.068),.013,'hair')
        orb((side*.155,0,z),(.028,.043,.059),'skin')
    smile=role=='peddler'
    pipe([(-.052,-.136,z-.087+( .015 if smile else 0)),(0,-.143,z-.093),(.052,-.136,z-.087+(.015 if smile else 0))],[.007,.011,.007],'shadow',8)
    if role=='bow':
        loft([(0,.035,z-.145,.192,.147),(0,.04,z+.04,.19,.163),(0,.055,z+.16,.157,.142),(0,.09,z+.237,.052,.054)],'green')
        # 前缘向后退，露出眼鼻。
        for side in (-1,1):
            pipe([(side*.148,-.04,z+.105),(side*.151,-.073,z),(side*.143,-.05,z-.15)],[.041,.035,.037],'green')
    else:
        loft([(0,.014,z+.095,.161,.142),(0,.021,z+.157,.154,.133),(-.027,.025,z+.217,.093,.085),(-.042,.031,z+.231,.025,.024)],'hair')
        for i in range(10):
            angle=i*math.tau/10
            x=.135*math.cos(angle); y=.12*math.sin(angle)
            pipe([(x*.74,y*.74,z+.178),(x,y,z+.139),(x+.014,y+.01,z+.09)],[.034,.034,.007],'hair',8)
        if role!='peddler':
            for side in (-1,1):
                pipe([(side*.135,-.056,z+.012),(side*.112,-.092,z-.105),(side*.05,-.092,z-.16)],[.03,.036,.023],'hair')
        else:
            loft([(0,0,z+.09,.326,.30),(0,0,z+.112,.34,.31),(0,0,z+.145,.224,.215),(0,0,z+.29,.128,.133),(0,0,z+.31,.093,.101)],'straw',24)
            for i in range(12):
                a=i*math.tau/12
                pipe([(.128*math.cos(a),.133*math.sin(a),z+.286),(.215*math.cos(a),.21*math.sin(a),z+.153),(.327*math.cos(a),.301*math.sin(a),z+.114)],[.004]*3,'brim',6)
            loft([(0,0,z+.149,.219,.21),(0,0,z+.17,.205,.20)],'brim')
            for side in (-1,1):
                bar((0,-.145,z-.056),(side*.058,-.128,z-.067),.014,'hair')

def figure(role,origin):
    global offset
    offset=V(origin)
    jacket={'bow':'green','blade':'vest','club':'blue','peddler':'blue'}[role]
    crouch=.11 if role=='club' else 0
    for s in (-1,1):
        hip=(s*.125,0,.80-crouch); knee=(s*.22,-.07,.47-crouch); foot=(s*.28,.04,.13)
        pipe([hip,knee,foot],[.126,.112,.085],'pants')
        pipe([foot,(s*.27,.04,.25),(s*.255,.012,.32)],[.10,.10,.096],'leather')
        orb((foot[0],-.047,.09),(.11,.164,.089),'leather')
    loft([(0,0,.65-crouch,.239,.164),(0,0,.86-crouch,.215,.145),(0,0,1.12-crouch,.254,.167),(0,0,1.27-crouch,.274,.156),(0,0,1.33-crouch,.145,.107)],jacket)
    loft([(0,0,.80-crouch,.237,.16),(0,0,.884-crouch,.226,.157)],'leather')
    # 胸前交领、腰带和皮袋保持在贴身轮廓内。
    for s in (-1,1):
        pipe([(s*.112,-.091,1.317-crouch),(s*.081,-.149,1.235-crouch),(0,-.171,1.14-crouch)],[.023,.026,.017],'linen' if role=='peddler' else 'leather',8)
    pipe([(.14,-.13,1.283-crouch),(.074,-.171,1.11-crouch),(-.14,-.145,.86-crouch)],[.026,.031,.028],'leather',8)
    orb((-.19,-.111,.789-crouch),(.084,.055,.10),'leather')
    plank(0,-.157,.805-crouch,.081,.057,.068,'gold')
    bar((0,0,1.25-crouch),(0,0,1.43-crouch),.092,'skin')
    head(role,1.52-crouch)
    if role=='bow': hands=[(-.55,-.29,1.21),(.07,-.196,1.35)]
    elif role=='blade': hands=[(-.35,-.25,1.04),(.53,-.16,1.35)]
    elif role=='club': hands=[(-.13,-.31,.82),(.18,-.33,1.015)]
    else: hands=[(-.38,-.15,1.36),(.54,-.22,1.18)]
    for s,hand in zip((-1,1),hands):
        shoulder=V((s*.224,0,1.23-crouch)); wrist=V(hand)
        elbow=(shoulder+wrist)*.5+V((s*.085,.027,-.14))
        pipe([shoulder,shoulder.lerp(elbow,.45),elbow],[.122,.125,.096],'linen' if role=='peddler' else jacket)
        pipe([elbow,elbow.lerp(wrist,.47),wrist],[.091,.089,.064],'linen' if role=='peddler' else 'skin')
        orb(wrist,(.080,.063,.074),'skin')
        orb(wrist+V((-s*.046,-.038,.018)),(.029,.031,.052),'skin')
        if (role=='blade' or role=='peddler') and s==1:
            for f in range(4):
                finger=wrist+V((.044,-.01+(f-1.5)*.027,.012))
                pipe([finger,finger+V((.084-abs(f-1)*.013,-.009,.02+(f-1.5)*.010))],[.015,.010],'skin',8)
    if role=='blade':
        h=V(hands[0]); tip=h+V((.57,0,.54))
        bar(h-V((.08,0,.08)),h+V((.08,0,.08)),.035,'leather')
        bar(h+V((.005,0,.155)),h+V((.155,0,.005)),.027,'amber')
        blade=[h+V((.08,0,.11)),h+V((.16,0,.075)),tip-V((.05,0,.14)),tip,tip-V((.13,0,.13))]
        rings([blade,[p+V((0,.025,0)) for p in blade]],'steel',False)
    elif role=='bow':
        h=V(hands[0]); ends=[h+V((.115,0,-.45)),h+V((.115,0,.45))]
        pipe([ends[0],h+V((-.03,0,-.23)),h,h+V((-.03,0,.23)),ends[1]],[.014,.025,.029,.025,.014],'wood')
        draw=V(hands[1]); pipe([ends[0],draw,ends[1]],[.007]*3,'linen',6)
        target=h+V((-.17,0,0)); bar(draw,target,.012,'wood')
        pipe([target,target+V((-.08,0,-.01))],[.032,.002],'steel',8)
    elif role=='club':
        a,b=map(V,hands); direction=(b-a).normalized()
        pipe([a-direction*.12,a,b,b+direction*.33,b+direction*.37],[.048,.05,.065,.094,.063],'wood')
    else:
        h=V(hands[0]); drum=h+V((0,0,.24))
        bar(h,drum,.024,'wood')
        orb(drum,(.121,.042,.121),'red')
        orb(drum+V((0,-.031,0)),(.105,.019,.105),'linen')
        orb(drum+V((0,-.05,0)),(.032,.011,.032),'red')
        for s in (-1,1):
            p=drum+V((s*.15,0,-.072)); bar(drum+V((s*.10,0,0)),p,.009,'wood'); orb(p,(.025,)*3,'red')

def base(rx,ry):
    loft([(0,0,0,rx*.97,ry*.97),(0,0,.045,rx,ry),(0,0,.075,rx*.97,ry*.97)],'earth',40)
    loft([(0,0,.067,rx*.97,ry*.97),(0,0,.083,rx*.95,ry*.95)],'earth',40)
    for i in range(11):
        a=i*math.tau/11+.15; x=.83*rx*math.cos(a); y=.81*ry*math.sin(a)
        if i%3==0:
            orb((x,y,.087),(.082,.054,.040),'rock')
        else:
            for j in (-1,0,1):
                pipe([(x,y,.081),(x+j*.025,y,.108),(x+j*.061,y+.018,.139+.02*(j==0))],[.020,.015,.002],'grass',6)

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
    bpy.ops.export_scene.fbx(filepath=str(DEST/(name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',bake_anim=False,add_leaf_bones=False)
    mesh_objects.append(obj)
    for arr in (vertices,faces,shades,colors): arr.clear()

base(1.18,.81)
offset=V((0,.22,.075));orb((0,0,.105),(.41,.27,.145),'rock')
figure('blade',(0,.27,.285));figure('bow',(-.56,-.17,.083));figure('club',(.55,-.22,.083))
finish('robber-band')
offset=V((0,0,0));base(1.03,.76)
figure('peddler',(0,.30,.083))
offset=V((0,0,.08))
for x in (-.70,.70):
    for y in (-.44,-.08): plank(x,y,0,.10,.10,.62,'wood')
for i in range(3): plank(0,-.55+.188*i,.60,1.73,.19,.083,'board')
for x,col in [(-.56,'blue'),(-.25,'linen')]:
    pipe([(x,-.63,.80),(x,-.585,.80),(x,-.14,.80)],[.121,.13,.125],col,18)
    for rad in (.075,.035):
        pipe([(x,-.635,.80),(x,-.63,.80)],[rad,rad],col if rad==.035 else 'leather',16)
loft([(.18,-.39,.68,.104,.10),(.18,-.39,.73,.141,.13),(.18,-.39,.84,.16,.15),(.18,-.39,.91,.12,.11),(.18,-.39,.96,.077,.07),(.18,-.39,1.00,.082,.077)],'pot')
pipe([(.28,-.39,.92),(.37,-.39,.94),(.39,-.39,.85),(.315,-.39,.81)],[.024]*4,'pot')
plank(.60,-.40,.688,.198,.26,.058,'amber')
for i in range(6): bar((.53,-.48+i*.03,.750),(.66,-.48+i*.03,.750),.009,'wood',8)
finish('peddler-stall')
# 源场景内两件分开放置；游戏 FBX 各自采用底部中心原点。
mesh_objects[0].location.x=-.016;mesh_objects[1].location.x=.016
bpy.data.libraries.write(str(SOURCE/'商路棋子.blend'), {scene}, compress=True)
result={}
for obj in mesh_objects:
    obj.data.calc_loop_triangles()
    result[obj.name]={'triangles':len(obj.data.loop_triangles),'metres':list(obj.dimensions)}
