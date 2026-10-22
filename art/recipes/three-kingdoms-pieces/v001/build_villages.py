"""四色里邑：米制真实网格、共享图集、显式FBX、回读检查与棚拍。"""
import bpy, math, random, json, sys, argparse, hashlib, os
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]
p=argparse.ArgumentParser(); p.add_argument('--output', default=str(ROOT/'art')); p.add_argument('--skip-previews', action='store_true')
args=p.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
OUT=Path(args.output).resolve(); rel=Path('three-kingdoms-pieces/v001')
SRC=OUT/'source'/rel; EXP=OUT/'exports'/rel; PRE=OUT/'previews'/rel
for d in (SRC, EXP/'textures', PRE): d.mkdir(parents=True,exist_ok=True)
random.seed(20261022)
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene; scene.unit_settings.system='METRIC'; scene.unit_settings.scale_length=1

# 各图集格保存独立材质面，不含场景光影。瓦纹由UV方向决定。
palette=[(.22,.14,.085),(.57,.53,.43),(.75,.70,.57),(.22,.25,.26),(.38,.27,.16),(.13,.095,.062),(.43,.28,.17),(.33,.37,.20)]
size=512; img=bpy.data.images.new('村庄共享基础色',width=size,height=size,alpha=False)
pixels=[]
for y in range(size):
    for x in range(size):
        k=(y//128)*2+x//256; u=x%256; v=y%128
        noise=random.uniform(-.025,.025); factor=1
        if k in (0,4,5): factor=.92+.07*math.sin(v*.7+math.sin(u*.06)*2)+.035*math.sin(v*2)
        if k==1: factor=.74 if v%32<2 or (u+(32 if v//32%2 else 0))%64<2 else 1
        if k==2: factor=.96+.025*math.sin(u*.12)*math.sin(v*.16)
        if k==3:
            factor=.52+.28*math.sin(math.pi*(u%24)/24)
            if v%25<2: factor*=.63
        pixels.extend([max(0,min(1,c*factor+noise)) for c in palette[k]]+[1])
img.pixels.foreach_set(pixels); img.filepath_raw=str(EXP/'textures/village-atlas.png'); img.file_format='PNG'; img.save()
img.filepath='//'+os.path.relpath(EXP/'textures/village-atlas.png',SRC).replace('\\','/')
atlas=bpy.data.materials.new('Village_Atlas'); atlas.use_nodes=True
bs=atlas.node_tree.nodes.get('Principled BSDF'); bs.inputs['Roughness'].default_value=.8
tex=atlas.node_tree.nodes.new('ShaderNodeTexImage'); tex.image=img; atlas.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
seat=bpy.data.materials.new('Village_Seat'); seat.use_nodes=True; seat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value=.57
objects=[]
def mesh(name,verts,faces,k=0,colour=False,bevel=0):
    m=bpy.data.meshes.new(name); m.from_pydata([(x/1000,y/1000,z/1000) for x,y,z in verts],[],faces); m.update()
    o=bpy.data.objects.new(name,m); scene.collection.objects.link(o); objects.append(o); o.data.materials.append(seat if colour else atlas)
    uv=m.uv_layers.new(name='AtlasUV')
    for f in m.polygons:
        axis=max(range(3),key=lambda i:abs(f.normal[i])); axes=[i for i in range(3) if i!=axis]
        coords=[m.vertices[m.loops[j].vertex_index].co for j in f.loop_indices]
        lo=[min(c[a] for c in coords) for a in axes]; hi=[max(c[a] for c in coords) for a in axes]
        for j,c in zip(f.loop_indices,coords):
            a=[(c[ax]-lo[t])/max(hi[t]-lo[t],1e-9) for t,ax in enumerate(axes)]
            uv.data[j].uv=((k%2+.03+a[0]*.94)/2,(k//2+.03+a[1]*.94)/4)
    if bevel:
        bpy.context.view_layer.objects.active=o; o.select_set(True)
        mod=o.modifiers.new('微缩倒角','BEVEL'); mod.width=bevel/1000; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name); o.select_set(False)
    return o
def box(n,loc,dim,k=0,b=0,colour=False):
    x,y,z=loc; a,c,d=[s/2 for s in dim]
    vs=[(x+sx*a,y+sy*c,z+sz*d) for sx,sy,sz in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    return mesh(n,vs,[(3,2,1,0),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)],k,colour,b)
def lathe(n,x,y,rings,k=0,N=24,colour=False):
    vs=[(x+r*math.cos(i*math.tau/N),y+r*math.sin(i*math.tau/N),z) for z,r in rings for i in range(N)]
    fs=[tuple(reversed(range(N)))]
    for j in range(len(rings)-1):
        for i in range(N): a=j*N+i; b=j*N+(i+1)%N; fs.append((a,b,b+N,a+N))
    fs.append(tuple((len(rings)-1)*N+i for i in range(N)))
    return mesh(n,vs,fs,k,colour)
def beam(n,a,b,width,k=0):
    v=Vector(b)-Vector(a); o=box(n,(0,0,0),(width,width,v.length),k)
    q=v.to_track_quat('Z','Y'); c=(Vector(a)+Vector(b))/2000
    for pt in o.data.vertices: pt.co=q@pt.co+c
    return o

lathe('木底座',0,0,[(0,8.65),(.18,9),(.75,9),(1,8.8)],0,32)
lathe('归属色环',0,0,[(.35,9.015),(.72,9.015)],0,32,True)
lathe('院落地面',0,0,[(.95,8.65),(1.2,8.65)],1,32)

def house(n,x,y,w,d,wall,peak):
    bottom=1.2; eave=bottom+wall; ridge=bottom+peak
    box(n+'石基',(x,y,1.5),(w+.3,d+.3,.6),1,b=.08)
    box(n+'灰墙',(x,y,(1.7+eave)/2),(w,d,eave-1.7),2)
    # 山墙三角封闭；屋脊沿X，正门朝-Y。
    mesh(n+'山墙',[(x+xx,y+yy,z) for xx in (-w/2,w/2) for yy,z in [(-d/2,eave),(d/2,eave),(0,ridge-.2)]],[(2,1,0),(3,4,5),(0,1,4,3),(1,2,5,4),(2,0,3,5)],2)
    for xx in (-w/2,w/2):
        for yy in (-d/2,d/2): box(n+'角柱',(x+xx,y+yy,(1.6+eave)/2),(.25,.26,eave-1.6),0)
    for yy in (-d/2,d/2): box(n+'横梁',(x,y+yy,eave-.15),(w+.3,.25,.3),0)
    # 两个有厚度的曲线屋坡，4段折面避免纯三角屋顶。
    for side in (-1,1):
        span=d/2+.6
        profile=[(side*span*t,ridge-(ridge-eave+.28)*t+.48*t**4) for t in (0,.33,.67,1)]
        verts=[(x+xx,y+yy,z+zz) for zz in (0,-.25) for xx in (-w/2-.5,w/2+.5) for yy,z in profile]
        fs=[]
        for j in range(3): fs.extend([(j,j+1,5+j,4+j),(8+j,12+j,13+j,9+j)])
        fs.extend([(0,4,12,8),(3,11,15,7)])
        for j in range(3):fs.extend([(j,8+j,9+j,j+1),(4+j,5+j,13+j,12+j)])
        roof=mesh(n+'瓦坡',verts,fs,3)
        # 屋面UV连续，瓦垄由共享贴图表达，檐口及边瓦保留实体。
        uv=roof.data.uv_layers.active
        for f in roof.data.polygons:
            for j in f.loop_indices:
                c=roof.data.vertices[roof.data.loops[j].vertex_index].co*1000
                uv.data[j].uv=(.5+.015+(.47*(c.x-(x-w/2-.5))/(w+1)),.25+.0075+.235*abs(c.y-y)/span)
        for xx in (-w/2-.5,w/2+.5):
            for j in range(3):
                a,b=profile[j],profile[j+1];beam(n+'封檐边瓦',(x+xx,y+a[0],a[1]+.1),(x+xx,y+b[0],b[1]+.1),.25,3)
        # 檐口筒瓦头，10边小圆柱沿Y。
        for i in range(9):
            xx=x-(w+.6)/2+i*(w+.6)/8
            beam(n+'檐瓦',(xx,y+side*(span-.45),profile[-1][1]+.03),(xx,y+side*(span+.03),profile[-1][1]+.03),.27,3)
    beam(n+'正脊',(x-w/2-.55,y,ridge+.13),(x+w/2+.55,y,ridge+.13),.4,3)
    for side in (-1,1):beam(n+'脊端',(x+side*(w/2+.3),y,ridge+.13),(x+side*(w/2+.65),y,ridge+.52),.3,3)
    doorw=w*.28; dh=wall*.65; front=y-d/2-.04
    box(n+'门洞',(x+.35,front,1.7+dh/2),(doorw+.22,.12,dh+.15),5)
    for i in range(4):box(n+'门板',(x+.35-doorw/2+(i+.5)*doorw/4,front-.08,1.7+dh/2),(doorw/4-.025,.09,dh),4)
    for h in (.15,dh-.18):box(n+'门闩',(x+.35,front-.15,1.7+h),(doorw,.10,.14),0)
    box(n+'门楣',(x+.35,front-.06,1.7+dh+.15),(doorw+.5,.25,.25),0)
    box(n+'门阶',(x+.35,front-.5,1.4),(doorw+.5,.8,.4),1,b=.05)
    # 前窗与后窗使用暗色实心内板，四边都有收口。
    for yy in (y-d/2-.06,y+d/2+.06):
        xx=x-w*.29; z=1.7+wall*.55
        box(n+'暗窗',(xx,yy,z),(.95,.1,1.1),5)
        for ox in (-.5,0,.5):box(n+'窗棂',(xx+ox,yy*1.001,z),(.1,.19,1.2),0)
        for oz in (-.6,.6):box(n+'窗框',(xx,yy,z+oz),(1.1,.20,.12),0)
    for side in (-1,1):
        xx=x+side*(w/2+.06); z=1.7+wall*.55
        box(n+'侧窗',(xx,y,z),(.12,1.3,1.5),5)
        for oy in (-.67,0,.67):box(n+'侧窗棂',(xx,y+oy,z),(.2,.12,1.6),0)
        for oz in (-.8,.8):box(n+'侧窗框',(xx,y,z+oz),(.22,1.5,.12),0)
        beam(n+'山墙横梁',(xx,y-d/2,eave-.12),(xx,y+d/2,eave-.12),.2,0)
house('主屋',1.4,2.6,7.0,4.8,7.5,11.1)
house('附屋',-3.6,-2.1,4.4,3.8,4.8,7.2)

# 十段低矮实心院墙；前方开门，院墙无垛口。
for i in range(12):
    a=math.radians(-78+i*28); b=math.radians(-78+(i+1)*28)
    r=7.6; h=2.65
    vs=[(rr*math.cos(t),rr*math.sin(t),z) for z in (1.2,1.2+h) for rr in (r-.35,r+.35) for t in (a,b)]
    mesh('低院墙',vs,[(3,2,0,1),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3),(4,6,7,5)],1)
    beam('院墙压顶',((r)*math.cos(a),r*math.sin(a),3.92),(r*math.cos(b),r*math.sin(b),3.92),.78,1)
# 南侧门独立嵌在留空段，门板有厚度。
for x in (-1.32,1.32):box('院门柱',(x,-7.25,2.75),(.45,.55,3.1),0,b=.05)
box('院门梁',(0,-7.25,4.25),(3.2,.7,.4),0,b=.06)
for i in range(7):box('院门木板',(-1.08+i*.36,-7.25,2.65),(.33,.25,2.6),4)
for z in (1.8,3.5):box('院门横闩',(0,-7.42,z),(2.6,.15,.18),0)
lathe('旗杆',6,2.6,[(1.2,.18),(14.5,.18),(15,.04)],0,8)
beam('旗横杆',(5.65,2.6,13.7),(7.9,2.6,13.7),.20,0)
# 双面封闭旗面，有轻微折线与燕尾，旗不伸出底座。
flag=[(6.25,2.59,13.5),(6.95,2.77,13.5),(7.65,2.62,13.5),(6.25,2.59,10.4),(6.95,2.77,10.85),(7.65,2.62,10.4)]
mesh('玩家旗',flag+[(x,y+.09,z) for x,y,z in flag],[(0,3,4,1),(1,4,5,2),(6,7,10,9),(7,8,11,10),(0,1,7,6),(1,2,8,7),(3,9,10,4),(4,10,11,5),(0,6,9,3),(2,5,11,8)],colour=True)
for x,y,s in [(4.6,-2.5,1),(-6,-3.5,.75),(3.8,.0,.7)]:
    lathe('陶罐',x,y,[(1.2,.42*s),(1.45,.65*s),(2.2,.66*s),(2.5,.43*s),(2.65,.45*s),(2.65,.31*s),(2.3,.28*s)],6,10)

# 合并保留2材质；法线一致朝外，所有组件封闭。
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]; bpy.ops.object.join(); model=bpy.context.object; model.name='Village'
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='SELECT'); bpy.ops.mesh.normals_make_consistent(inside=False); bpy.ops.object.mode_set(mode='OBJECT')
# 当前棋盘边长40毫米，道路长26毫米，节点留空半径7毫米；留出0.5毫米以上余量。
for v in model.data.vertices:v.co*=.72
model.data.calc_loop_triangles(); triangles=len(model.data.loop_triangles)
# 小尺寸闭合构件保留完整拓扑，面数以导出回读实测为准。
COLOURS={'red':(.85,.24,.16),'blue':(.23,.56,.78),'orange':(.94,.74,.30),'beige':(.79,.79,.72)}
manifest={'日期':'2026-10-22','种子':20261022,'工具':bpy.app.version_string,'估算尺寸':True,'三角形':triangles,'材质数':len(model.data.materials),'assets':[]}
def linear(c):return c/12.92 if c<.04045 else ((c+.055)/1.055)**2.4
def set_seat(c):
    rgba=tuple(linear(v) for v in c)+(1,); seat.diffuse_color=rgba; seat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=rgba

for key,c in COLOURS.items():
    set_seat(c); model.name='tk-village-'+key
    bpy.ops.wm.save_as_mainfile(filepath=str(SRC/(model.name+'.blend')))
    bpy.ops.export_scene.fbx(filepath=str(EXP/(model.name+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,use_mesh_modifiers=True,add_leaf_bones=False,path_mode='STRIP')
    manifest['assets'].append({'id':model.name,'颜色':list(c),'尺寸米':list(model.dimensions),'底面':min(v.co.z for v in model.data.vertices),'枢轴':list(model.location),'UV':len(model.data.uv_layers)})

# 独立回读每个FBX，检验单位、完整性、UV和材质；只删除此后台场景的新导入件。
import bmesh
reports=[]
for asset in manifest['assets']:
    before=set(bpy.data.objects); bpy.ops.import_scene.fbx(filepath=str(EXP/(asset['id']+'.fbx')))
    imported=[o for o in bpy.data.objects if o not in before and o.type=='MESH']; assert len(imported)==1
    o=imported[0]; o.data.calc_loop_triangles(); bm=bmesh.new();bm.from_mesh(o.data)
    boundary=sum(1 for e in bm.edges if not e.is_manifold);bm.free()
    pts=[o.matrix_world@v.co for v in o.data.vertices]; dims=[max(v[i] for v in pts)-min(v[i] for v in pts) for i in range(3)]
    print('回读',len(o.data.loop_triangles),triangles,boundary,len(o.data.uv_layers))
    assert len(o.data.loop_triangles)==triangles and boundary==0 and len(o.data.uv_layers)==1
    assert max(abs(a-b) for a,b in zip(sorted(dims),sorted(asset['尺寸米'])))<.00001
    assert min(v.z for v in pts)>-.00001
    reports.append({'id':asset['id'],'三角形':triangles,'非流形边':boundary,'尺寸米':dims,'UV':True,'材质数':len(o.data.materials)})
    for obj in set(bpy.data.objects)-before:bpy.data.objects.remove(obj,do_unlink=True)
(EXP/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
(EXP/'fbx-verification.json').write_text(json.dumps(reports,ensure_ascii=False,indent=2),encoding='utf8')
if args.skip_previews:sys.exit(0)

# 以下为真实模型棚拍，场景不导出到游戏。
scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
scene.render.resolution_x=800;scene.render.resolution_y=800;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('暗紫棚拍');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.075,.045,.11,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.45
scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
camdata=bpy.data.cameras.new('正交相机');cam=bpy.data.objects.new('正交相机',camdata);scene.collection.objects.link(cam);scene.camera=cam;camdata.type='ORTHO';camdata.lens=50;camdata.clip_start=.0001;camdata.clip_end=10
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for n,loc,power,scale in [('主光',(.025,-.035,.05),.065,.035),('补光',(-.03,-.01,.025),.035,.03),('轮廓光',(.01,.035,.04),.07,.025)]:
    data=bpy.data.lights.new(n,'AREA');data.energy=power;data.shape='DISK';data.size=scale
    light=bpy.data.objects.new(n,data);scene.collection.objects.link(light);light.location=loc;aim(light,(0,0,.005))
def render(name,loc,target=(0,0,.007),scale=.025):
    cam.location=loc;aim(cam,target);camdata.ortho_scale=scale;scene.render.filepath=str(PRE/(name+'.png'));bpy.ops.render.render(write_still=True)
set_seat(COLOURS['red'])
for name,loc in [('正面',(0,-.06,.007)),('右侧',(.06,0,.007)),('背面',(0,.06,.007)),('顶面',(0,0,.06)),('参考角度',(.032,-.046,.036))]:
    render(name,loc,(0,0,0) if name=='顶面' else (0,0,.007))
gray=bpy.data.materials.new('灰模检查');gray.diffuse_color=(.5,.5,.5,1)
scene.view_layers[0].material_override=gray
render('灰模结构',(.032,-.046,.036))
scene.view_layers[0].material_override=None
scene.render.film_transparent=True
for key,c in COLOURS.items():set_seat(c);render('thumbnail-'+key,(.032,-.046,.036))
scene.render.film_transparent=False
# 四色合照共享几何；复制玩家材质，其余材质共享。
for i,(key,c) in enumerate(COLOURS.items()):
    o=model if i==0 else bpy.data.objects.new('展台-'+key,model.data.copy())
    if i:scene.collection.objects.link(o)
    o.location.x=(i-1.5)*.022
    sm=seat.copy(); rgba=tuple(linear(v) for v in c)+(1,);sm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=rgba
    for slot in o.material_slots:
        if slot.material.name.startswith('Village_Seat'):slot.material=sm
scene.render.resolution_x=1800;scene.render.resolution_y=700
render('四色村庄',(.032,-.09,.067),(0,0,.006),.096)
bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'四色村庄棚拍.blend'))
print('四色村庄生成与回读完成',triangles)
