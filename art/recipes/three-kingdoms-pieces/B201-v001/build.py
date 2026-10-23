"""B201 验证样板；必须由 Blender MCP 执行。单位先按厘米构建，再转换为米。"""
import bpy, math, random, json, bmesh
from pathlib import Path
from mathutils import Vector
ROOT = Path('D:/Project/catan-true-to-tabletop')
OUT = ROOT/'art/exports/three-kingdoms-pieces/B201-v001'
PRE = ROOT/'art/previews/three-kingdoms-pieces/B201-v001'
SRC = ROOT/'art/source/three-kingdoms-pieces/B201-v001'
random.seed(201)
scene = bpy.data.scenes.new('B201_验证样板')
bpy.context.window.scene = scene
for old in list(bpy.data.scenes):
    if old != scene and old.name.startswith('B201_验证样板'):
        for obj in list(old.objects):bpy.data.objects.remove(obj,do_unlink=True)
        bpy.data.scenes.remove(old)
scene.name='B201_验证样板'
scene.unit_settings.system = 'METRIC'
assets = []
atlas = bpy.data.images.load(str(OUT/'material-atlas.png'), check_existing=True)
materials={}
for key,rough in [('灰泥',.92),('旧木',.76),('灰瓦',.86),('石基',.9)]:
    m=bpy.data.materials.new('B201_'+key); m.use_nodes=True
    n=m.node_tree.nodes; p=n.get('Principled BSDF'); p.inputs['Roughness'].default_value=rough
    tex=n.new('ShaderNodeTexImage'); tex.image=atlas; tex.interpolation='Closest'
    if key in ['灰瓦','旧木']:
        tint=n.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1
        value=.26 if key=='灰瓦' else .67;tint.inputs[2].default_value=(value,value,value,1)
        m.node_tree.links.new(tex.outputs['Color'],tint.inputs[1]);m.node_tree.links.new(tint.outputs[0],p.inputs['Base Color'])
    else:m.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color'])
    materials[key]=m
metal=bpy.data.materials.new('B201_旧铜');metal.diffuse_color=(.16,.10,.045,1);metal.use_nodes=True
metal.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.16,.10,.045,1)
metal.node_tree.nodes.get('Principled BSDF').inputs['Metallic'].default_value=.65
metal.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.55

def active(o):
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o

def finish(o,name,mat,bevel=0):
    o.name=name;assets.append(o);o.data.materials.append(materials[mat] if isinstance(mat,str) else mat)
    if bevel >= .01:
        active(o);mod=o.modifiers.new('边缘倒角','BEVEL');mod.width=bevel;mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def box(name,loc,dim,mat='旧木',bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);o=bpy.context.object;o.dimensions=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    return finish(o,name,mat,bevel)

def mesh(name,verts,faces,mat):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    o=bpy.data.objects.new(name,me);scene.collection.objects.link(o)
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(me);bm.free()
    return finish(o,name,mat)

def beam(name,a,b,width,depth=None,mat='旧木',bevel=.008):
    a,b=Vector(a),Vector(b);o=box(name,(a+b)/2,(width,depth or width,(b-a).length),mat,bevel)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

# 封闭的连续墙壳，窗洞用布尔建立真实内外壁与窗洞侧壁。
walls=box('灰泥_连续墙壳',(0,0,1.14),(3,2.5,2.28),'灰泥',0)
def cut(target,loc,dim):
    bpy.ops.mesh.primitive_cube_add(size=1,location=loc);c=bpy.context.object;c.dimensions=dim
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    active(target);m=target.modifiers.new('真实开口','BOOLEAN');m.operation='DIFFERENCE';m.object=c
    bpy.ops.object.modifier_apply(modifier=m.name);bpy.data.objects.remove(c,do_unlink=True)
cut(walls,(0,0,1.32),(2.68,2.18,2.4))
for x in [-.96,.96]:cut(walls,(x,-1.25,1.2),(.38,.5,.48))
for x in [-1.5,1.5]:cut(walls,(x,0,1.17),(.5,.55,.52))
cut(walls,(0,-1.25,.83),(.78,.5,1.42))
active(walls);mod=walls.modifiers.new('洞口小倒角','BEVEL');mod.width=.008;mod.segments=1
bpy.ops.object.modifier_apply(modifier=mod.name)
box('室内地板',(0,0,.10),(2.68,2.18,.10),'旧木',0)
box('室内顶板',(0,0,2.20),(2.68,2.18,.08),'旧木',0)
beam('室内横梁',(-1.34,0,2.07),(1.34,0,2.07),.10)

# 山墙为有厚度的封闭五棱体，与墙壳上下相接。
for s in [-1,1]:
    a,b=s*1.34,s*1.50
    profile=[(-1.25,2.28),(1.25,2.28),(1.25,2.31),(1,2.42),(.65,2.62),(.3,2.87),(0,3.10),(-.3,2.87),(-.65,2.62),(-1,2.42),(-1.25,2.31)]
    vs=[(x,y,z) for x in [a,b] for y,z in profile];n=len(profile)
    fs=[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    mesh('山墙_'+str(s),vs,fs,'灰泥')

# 低石基：接缝落在连续墙壳前，块体之间留缝，不用重叠面。
for row in range(2):
    z=.105+row*.21
    for side in [-1,1]:
        for i in range(6):
            x=-1.25+i*.5
            if side==-1 and abs(x)<.5:continue
            box('石基_前后',(x,side*1.285,z),(.484,.17,.197),'石基',.017 if side==-1 and row==1 else 0)
        for i in range(4):
            box('石基_两侧',(side*1.535,-.9+i*.6,z),(.17,.584,.197),'石基',.017 if row==1 else 0)
box('门槛',(0,-1.30,.075),(.78,.30,.15),'石基',.009)

for x in [-1.46,1.46]:
    for y in [-1.21,1.21]:box('角柱',(x,y,1.31),(.17,.17,1.82))
for x in [-.47,.47]:box('门柱',(x,-1.29,1.30),(.13,.15,1.83),bevel=0)
for y in [-1.25,1.25]:box('檐下横梁',(0,y,2.21),(3.16,.17,.17))
for x in [-1.51,1.51]:
    box('山墙横梁',(x,0,2.19),(.17,2.55,.15))
    beam('山墙中柱',(x,0,2.27),(x,0,3.12),.115)
for i in range(8):
    box('双扇门板',(-.344+i*.098,-1.24,.84),(.094,.08,1.35),'旧木',.004)
box('门楣',(0,-1.345,1.59),(1.04,.13,.13),bevel=0)
for x in [-.20,.20]:
    for z in [.34,1.34]:box('门横带',(x,-1.303,z),(.32,.045,.065),'旧木',.004)
    bpy.ops.mesh.primitive_torus_add(major_segments=8,minor_segments=4,location=(x,-1.345,.88),major_radius=.052,minor_radius=.009,rotation=(math.pi/2,0,0))
    finish(bpy.context.object,'铜门环',metal)

def window(name,center,side=False):
    c=Vector(center);w=.55 if side else .38;h=.52 if side else .48
    def place(label,u,v,dx,dz):
        loc=c+Vector((0,u,v) if side else (u,0,v))
        return box(name+label,loc,(.095,dx,dz) if side else (dx,.095,dz),'旧木',.006)
    for z in [-h/2-.035,h/2+.035]:place('_框',0,z,w+.15,.07)
    for u in [-w/2-.025,w/2+.025]:place('_框',u,0,.05,h)
    for i in range(4):place('_栅',-w*.32+i*w*.215,0,.022,h)
for x in [-.96,.96]:
    window('前窗',(x,-1.285,1.2))
    box('前窗雨檐',(x,-1.34,1.51),(.57,.23,.055),'灰瓦',.009)
for s in [-1,1]:
    window('侧窗',(s*1.52,0,1.17),True)
    box('阁楼闭合木窗',(s*1.526,0,2.60),(.06,.29,.34),'旧木',.007)
    for z in [2.41,2.79]:box('阁窗框',(s*1.566,0,z),(.06,.4,.055),'旧木',.005)

# 双坡曲面：瓦垄沿坡向连续、横向规则四边面；实体背板封住所有瓦缝。
ys=[0,.30,.65,1.,1.35,1.63]
zs=[3.17,2.94,2.69,2.49,2.34,2.27]
for s in [-1,1]:
    verts=[(x,s*y,z+dz) for dz in [0,-.07] for x in [-1.77,1.77] for y,z in zip(ys,zs)]
    faces=[]
    for j in range(5):faces.extend([(j,j+1,j+7,j+6),(j+12,j+18,j+19,j+13),(j,j+12,j+13,j+1),(j+6,j+7,j+19,j+18)])
    faces += [(0,6,18,12),(5,17,23,11)]
    mesh('连续瓦底_'+str(s),verts,faces,'灰瓦')
    for i in range(17):
        x=-1.67+i*.209
        # 五点拱形截面加底部两点，封闭瓦垄。
        cross=[(-.054,0),(-.046,.04),(0,.066),(.046,.04),(.054,0)]
        vs=[];fs=[]
        for j in range(5):
            offset=len(vs)
            for t in [.012,.988]:
                y=ys[j]+(ys[j+1]-ys[j])*t;z=zs[j]+(zs[j+1]-zs[j])*t
                vs.extend((x+dx,s*y,z+dz+.012) for dx,dz in cross)
            for k in range(5):fs.append(tuple(offset+n for n in (k,(k+1)%5,5+(k+1)%5,5+k)))
            fs.extend([tuple(offset+n for n in range(4,-1,-1)),tuple(offset+n for n in range(5,10))])
        tile=mesh('筒瓦垄',vs,fs,'灰瓦')
        for p in tile.data.polygons:p.use_smooth=p.index%7<4
    for x in [-1.79,1.79]:
        for j in range(5):beam('封檐边梁',(x,s*ys[j],zs[j]-.08),(x,s*ys[j+1],zs[j+1]-.08),.105,.13,bevel=.006)
    for i in range(9):box('檐下椽头',(-1.64+i*.41,s*1.48,2.20),(.085,.33,.12),'旧木',.006)
box('屋脊',(0,0,3.23),(3.59,.19,.16),'灰瓦',.025)
for x in [-1.79,1.79]:box('山墙檐口合缝木',(x,0,3.075),(.14,.14,.18),'旧木',0)
for x in [-1.73,1.73]:
    s=1 if x>0 else -1
    beam('脊端翘头',(x,0,3.24),(x+s*.11,0,3.31),.18,.17,'灰瓦',.015)
    beam('脊端上翘',(x+s*.11,0,3.31),(x+s*.13,0,3.48),.16,.17,'灰瓦',.015)

# 合并双坡瓦底的公共端面，消除内部重合面并保持流形。
roofbases=[o for o in assets if o.name.startswith('连续瓦底')]
active(roofbases[0]);roofbases[1].select_set(True)
assets.remove(roofbases[1]);bpy.ops.object.join()
bm=bmesh.new();bm.from_mesh(roofbases[0].data);seen={};remove=[]
for f in bm.faces:
    key=tuple(sorted(tuple(round(c,6) for c in v.co) for v in f.verts))
    if key in seen:remove.extend([f,seen[key]])
    else:seen[key]=f
bmesh.ops.delete(bm,geom=remove,context='FACES_ONLY')
bmesh.ops.remove_doubles(bm,verts=bm.verts,dist=.000001)
bmesh.ops.recalc_face_normals(bm,faces=bm.faces);bm.to_mesh(roofbases[0].data);bm.free()

# 图集映射按实际厘米尺度统一；每面选取不同小区域，避免整张纹理重复。
quadrants={'灰泥':(0,.5),'旧木':(.5,.5),'灰瓦':(0,0),'石基':(.5,0)}
for o in assets:
    if o.type!='MESH':continue
    me=o.data
    if not me.uv_layers:me.uv_layers.new(name='UVMap')
    mat=me.materials[0].name.replace('B201_','').split('.')[0]
    q=quadrants.get(mat,(.5,0));density=.105
    # 1 厘米约132纹素；全墙跨度约400纹素。
    for poly in me.polygons:
        coords=[me.vertices[me.loops[li].vertex_index].co for li in poly.loop_indices]
        normal=poly.normal;drop=max(range(3),key=lambda a:abs(normal[a]));axes=[a for a in range(3) if a!=drop]
        if mat=='旧木':axes.sort(key=lambda a:max(v[a] for v in coords)-min(v[a] for v in coords))
        lo=[min(v[a] for v in coords) for a in axes];hi=[max(v[a] for v in coords) for a in axes]
        span=[(hi[i]-lo[i])*density for i in range(2)]
        ox=q[0]+.018+random.random()*max(0,.464-span[0]);oy=q[1]+.018+random.random()*max(0,.464-span[1])
        for li,v in zip(poly.loop_indices,coords):me.uv_layers.active.data[li].uv=(ox+(v[axes[0]]-lo[0])*density,oy+(v[axes[1]]-lo[1])*density)
    for v in me.vertices:v.co*=.01
    o.location*=.01
    me.update()

# 场景与相机独立于交付网格。
world=bpy.data.worlds.new('B201_棚拍世界');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.16,.12,.19,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.35
def point(o,p):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,color in [('主光',(.025,-.045,.07),.55,.05,(1,.87,.71)),('补光',(-.045,-.01,.045),.25,.04,(.75,.83,1)),('轮廓光',(.02,.045,.06),.45,.03,(1,.91,.79))]:
    d=bpy.data.lights.new(name,'AREA');d.energy=power;d.shape='DISK';d.size=size;d.color=color
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;point(o,(0,0,.016))
d=bpy.data.cameras.new('B201_相机');cam=bpy.data.objects.new('B201_相机',d);scene.collection.objects.link(cam);scene.camera=cam;d.type='ORTHO';d.ortho_scale=.058;d.clip_start=.001;d.clip_end=10
cam.location=(.06,-.085,.065);point(cam,(0,0,.017))
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=1100;scene.render.resolution_y=1100;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
scene.view_settings.look='AgX - Medium High Contrast'
scene.view_settings.exposure=-2
scene.render.filepath=str(PRE/'hero.png')
for o in assets:o.select_set(True)
for o in scene.objects:
    if o not in assets:o.select_set(False)
bpy.context.view_layer.objects.active=assets[0]
report={}
for o in assets:
    o.data.calc_loop_triangles();report[o.name]=len(o.data.loop_triangles)
(OUT/'triangles-initial.json').write_text(json.dumps({'三角面合计':sum(report.values()),'组件':report},ensure_ascii=False,indent=2),encoding='utf-8',newline='\n')
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'B201.blend'))
print(json.dumps({'三角面合计':sum(report.values()),'对象数':len(assets)},ensure_ascii=False))
