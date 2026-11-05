"""经 Blender MCP 执行：自然棋子共享几何、四色材质及透明缩略图。"""
import bpy, bmesh, math, random, json, os
from pathlib import Path
from mathutils import Vector

ROOT=Path('D:/Project/catan-true-to-tabletop')
ART=ROOT/'Unity/Catan/Assets/Art/NaturalPieces/v001'
RES=ROOT/'Unity/Catan/Assets/Resources/NaturalPieces/v001'
OUT=ROOT/'art/recipes/natural-pieces-v002'
random.seed(211105)
scene=bpy.data.scenes.new('自然棋子_v002')
bpy.context.window.scene=scene
for existing in bpy.data.scenes:
    for obj in existing.objects:obj.select_set(False,view_layer=existing.view_layers[0])
scene.unit_settings.system='METRIC'
scene.render.engine='CYCLES'
scene.cycles.samples=32
scene.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='HIP';prefs.get_devices()
for device in prefs.devices:device.use=device.type=='HIP'
scene.cycles.device='GPU'
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.film_transparent=True
scene.view_settings.view_transform='AgX'
scene.world=bpy.data.worlds.new('自然棋子柔光')
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.64,.72,.82,1)
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.65
atlas=bpy.data.images.load(str(ART/'material-atlas.png'),check_existing=True)
COLORS={'red':(.80,.225,.135,1),'blue':(.27,.48,.77,1),'orange':(1,.62,.20,1),'beige':(1,.96,.79,1)}
def material(name,color):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.9
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=atlas
    mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=color
    m.node_tree.links.new(tex.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
    m.diffuse_color=color
    return m
neutral=material('Natural_Masonry',(1,1,1,1))
roofmat=material('Natural_Player',COLORS['red'])
dark=material('Natural_Recess',(.18,.16,.13,1))
mats=[neutral,roofmat,dark]
V=[];F=[];MI=[];UV=[];VC=[];FOOT=[]
TONE=1.0
def poly(points,mat=0,region='stone'):
    n=len(points);start=len(V);V.extend(points);F.append(tuple(range(start,start+n)));MI.append(mat)
    ox,oy={'roof':(0,.5),'wall':(.5,.5),'stone':(0,0),'wood':(.5,0)}[region]
    a=random.uniform(.035,.18);b=random.uniform(.035,.18);span=.25
    # 每面都留在所属象限内部，低 mip 也不串到邻材质。
    UV.append([(ox+a+span*(i in (1,2)),oy+b+span*(i>=2)) for i in range(n)])
    tint=TONE*(random.uniform(.92,1.04) if region!='wood' else random.uniform(.88,1.01))
    VC.append((tint,tint,tint,1))
def box(x,y,z,w,d,h,mat=0,region='stone'):
    p=[(x+sx*w/2,y+sy*d/2,z+sz*h) for sz in (0,1) for sy in (-1,1) for sx in (-1,1)]
    for ids in [(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)]:poly([p[i] for i in ids],mat,region)
def prism(points,thick=.025,mat=1,region='roof'):
    upper=[(x,y,z+thick) for x,y,z in points]
    poly(list(reversed(points)),mat,region);poly(upper,mat,region)
    for i in range(len(points)):
        j=(i+1)%len(points);poly([points[i],points[j],upper[j],upper[i]],mat,region)
def beam(a,b,width,depth=None,mat=0,region='wood'):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
    if u.length<.01:u=Vector((1,0,0))
    u.normalize();u*=width/2;v=axis.cross(u).normalized()*(depth or width)/2
    p=[tuple(c+su*u+sv*v) for c in (a,b) for su,sv in [(-1,-1),(1,-1),(1,1),(-1,1)]]
    for ids in [(3,2,1,0),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:poly([p[i] for i in ids],mat,region)
def roof(x,y,z,w,d,rise,hip=False):
    global TONE
    # 独立有厚度的坡面与瓦片，层叠关系在高俯视角仍可读。
    if hip:
        apex=Vector((x,y,z+rise));corners=[Vector((x-w/2,y-d/2,z)),Vector((x+w/2,y-d/2,z)),Vector((x+w/2,y+d/2,z)),Vector((x-w/2,y+d/2,z))]
        for s in range(4):
            a,b=corners[s],corners[(s+1)%4]
            prism([tuple(a),tuple(b),tuple(apex)],.030,1,'roof')
            rows=max(3,round(rise/.14)+1)
            for row in range(rows):
                t0=row/rows;t1=min(1,(row+1)/rows)
                aa=a.lerp(apex,t0);bb=b.lerp(apex,t0);cc=a.lerp(apex,t1);dd=b.lerp(apex,t1)
                count=max(1,round((bb-aa).length/.18))
                for j in range(count):
                    TONE=.80+.22*t0+random.uniform(-.045,.045)
                    l=j/count+.006/count;r=(j+1)/count-.006/count
                    prism([tuple(aa.lerp(bb,l)),tuple(aa.lerp(bb,r)),tuple(cc.lerp(dd,r)),tuple(cc.lerp(dd,l))],.042+random.random()*.006)
            TONE=1.12
            beam(tuple(a+Vector((0,0,.045))),tuple(apex+Vector((0,0,.045))),.052,mat=1,region='roof')
    else:
        for side in (-1,1):
            a=Vector((x+side*w/2,y-d/2,z));b=Vector((x+side*w/2,y+d/2,z));c=Vector((x,y-d/2,z+rise));e=Vector((x,y+d/2,z+rise))
            prism([tuple(a),tuple(b),tuple(e),tuple(c)],.030,1,'roof')
            rows=max(3,round(w/.25));cols=max(3,round(d/.18))
            for row in range(rows):
                t0=row/rows;t1=min(1,(row+1)/rows)
                for j in range(cols):
                    TONE=.80+.22*t0+random.uniform(-.045,.045)
                    q0=j/cols+.006/cols;q1=(j+1)/cols-.006/cols
                    prism([tuple(a.lerp(c,t0).lerp(b.lerp(e,t0),q0)),tuple(a.lerp(c,t0).lerp(b.lerp(e,t0),q1)),tuple(a.lerp(c,t1).lerp(b.lerp(e,t1),q1)),tuple(a.lerp(c,t1).lerp(b.lerp(e,t1),q0))],.042+random.random()*.006)
        TONE=1.12
        beam((x,y-d/2-.025,z+rise+.05),(x,y+d/2+.025,z+rise+.05),.075,mat=1,region='roof')
        for yy in (y-d/2+.035,y+d/2-.035):
            poly([(x-w/2+.06,yy,z),(x+w/2-.06,yy,z),(x,yy,z+rise-.045)],0,'wall')
            for s in (-1,1):beam((x+s*(w/2-.03),yy,z),(x,yy,z+rise),.06)
    TONE=.84
    for yy in (y-d/2,y+d/2):beam((x-w/2,yy,z),(x+w/2,yy,z),.055,mat=0,region='wood')
    for xx in (x-w/2,x+w/2):beam((xx,y-d/2,z),(xx,y+d/2,z),.055,mat=0,region='wood')
    TONE=1
def arch(x,y,z,w,h,door=False):
    radius=w/2;spring=z+h-radius
    pts=[(x-w/2,y,z),(x+w/2,y,z)]
    for i in range(9):
        ang=i*math.pi/8;pts.append((x+radius*math.cos(ang),y,spring+radius*math.sin(ang)))
    # 先暗洞，再门板；石门套产生真实凹陷阴影。
    poly(pts,0 if door else 2,'wood')
    t=.045 if door else .035
    for side in (-1,1):box(x+side*(radius+t*.6),y-.008,z,t,.065,h-radius,0,'stone')
    for i in range(8):
        a=i*math.pi/8+.018;b=(i+1)*math.pi/8-.018
        q=[(x+rr*math.cos(ang),y-.035,spring+rr*math.sin(ang)) for rr,ang in [(radius,a),(radius,b),(radius+t,b),(radius+t,a)]]
        poly(q,0,'stone')
        poly([(px,py+.06,pz) for px,py,pz in reversed(q)],0,'stone')
        for k in range(4):
            j=(k+1)%4;poly([q[k],q[j],(q[j][0],q[j][1]+.06,q[j][2]),(q[k][0],q[k][1]+.06,q[k][2])],0,'stone')
    box(x,y-.025,z-.035,w+.13,.11,.055)
    if door:
        box(x,y-.013,z,.015,.015,h-radius*.2,2,'wood')
        for zz in (z+h*.25,z+h*.64):box(x,y-.022,zz,w*.85,.025,.027,2,'wood')
def stones(x,y,z,w,d,h):
    rows=max(1,round(h/.13))
    box(x,y,z,w,d,h,0,'stone')
    for row in range(rows):
        for side in (-1,1):
            n=max(2,round(w/.20));step=w/n
            for j in range(n):box(x-w/2+step*(j+.5),y+side*d/2,z+row*h/rows+.008,step-.014,.035,h/rows-.018)
            n=max(2,round(d/.20));step=d/n
            for j in range(n):box(x+side*w/2,y-d/2+step*(j+.5),z+row*h/rows+.008,.035,step-.014,h/rows-.018)
def house(x,y,w,d,h,rise,hip=False,castle=False,base=.09):
    if base<.1:FOOT.append((x,y,w+.18,d+.18))
    if base<.1:box(x,y,0,w+.07,d+.07,.10)
    stones(x,y,base,w,d,.17)
    box(x,y,base+.17,w-.018,d-.018,h-.17,0,'stone' if castle else 'wall')
    for sx in (-1,1):
        for sy in (-1,1):
            if castle:
                for zz in range(max(1,round((h-.17)/.14))):box(x+sx*(w/2-.035),y+sy*(d/2-.035),base+.18+zz*.14,.11,.11,.11)
            else:box(x+sx*(w/2-.025),y+sy*(d/2-.025),base+.17,.045,.045,h-.17,0,'wood')
    box(x,y,base+h-.07,w+.035,d+.035,.06,0,'stone' if castle else 'wood')
    roof(x,y,base+h,w+.18,d+.18,rise,hip)
def chimney(x,y,z):
    box(x,y,z,.15,.15,.30);box(x,y,z+.28,.19,.19,.055);box(x,y,z+.335,.10,.10,.002,2,'wood')
def foundation(x,y,w,d):
    # 每栋房屋自带贴墙石基，不额外铺设与建筑分离的方块。
    pass
def bake_materials(obj,kind):
    # 在 Blender 内烘焙局部遮蔽和瓦石深浅；运行时仍是一网格、三材质。
    for other in scene.objects:
        if other!=obj:other.hide_render=True
    obj.hide_render=False
    obj.data.uv_layers.new(name='BakedUV')
    obj.data.uv_layers.active_index=1
    obj.data.uv_layers[1].active_render=True
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(70),island_margin=.006,area_weight=.8)
    bpy.ops.object.mode_set(mode='OBJECT')
    size=2048 if kind=='city' else 1024
    baked=bpy.data.images.new('自然材质_'+kind,width=size,height=size,alpha=False)
    baked.colorspace_settings.name='sRGB'
    newmats=[]
    for index,source in enumerate(mats):
        mat=source.copy();mat.name=['Natural_Masonry','Natural_Player','Natural_Recess'][index]+'_'+kind
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission')
        uv=nodes.new('ShaderNodeUVMap');uv.uv_map='UVMap'
        tex=nodes.new('ShaderNodeTexImage');tex.image=atlas;links.new(uv.outputs[0],tex.inputs[0])
        col=nodes.new('ShaderNodeVertexColor');col.layer_name='NaturalTone'
        tone=nodes.new('ShaderNodeMixRGB');tone.blend_type='MULTIPLY';tone.inputs[0].default_value=1
        links.new(tex.outputs['Color'],tone.inputs[1]);links.new(col.outputs['Color'],tone.inputs[2])
        if index==2:
            recess=nodes.new('ShaderNodeMixRGB');recess.blend_type='MULTIPLY';recess.inputs[0].default_value=1
            recess.inputs[2].default_value=(.060,.045,.030,1);links.new(tone.outputs[0],recess.inputs[1]);tone=recess
        ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.0018;ao.samples=16
        soft=nodes.new('ShaderNodeMixRGB');soft.inputs[0].default_value=.65;soft.inputs[1].default_value=(1,1,1,1)
        links.new(ao.outputs['AO'],soft.inputs[2])
        shade=nodes.new('ShaderNodeMixRGB');shade.blend_type='MULTIPLY';shade.inputs[0].default_value=1
        links.new(tone.outputs[0],shade.inputs[1]);links.new(soft.outputs[0],shade.inputs[2]);links.new(shade.outputs[0],emit.inputs[0]);links.new(emit.outputs[0],out.inputs['Surface'])
        target=nodes.new('ShaderNodeTexImage');target.image=baked;nodes.active=target
        obj.data.materials[index]=mat;newmats.append(mat)
    scene.render.bake.margin=12;scene.render.bake.use_clear=True;scene.cycles.samples=16
    bpy.ops.object.bake(type='EMIT')
    baked.filepath_raw=str(ART/(kind+'-surface.png'));baked.file_format='PNG';baked.save()
    for index,mat in enumerate(newmats):
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
        out=nodes.new('ShaderNodeOutputMaterial');shader=nodes.new('ShaderNodeBsdfPrincipled');shader.inputs['Roughness'].default_value=.9
        tex=nodes.new('ShaderNodeTexImage');tex.image=baked
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1
        mix.inputs[2].default_value=(1,1,1,1)
        links.new(tex.outputs['Color'],mix.inputs[1]);links.new(mix.outputs[0],shader.inputs['Base Color']);links.new(shader.outputs[0],out.inputs['Surface'])
    obj.data.uv_layers.remove(obj.data.uv_layers['UVMap'])
    obj.data.uv_layers[0].name='UVMap'
    for p in obj.data.polygons:
        if p.material_index==2:p.material_index=0
    obj.data.materials.pop(index=2)
    obj.hide_render=True
def ground_contact(obj,scale,center,width):
    # 柔影和主体合并在一个网格内，门窗暗部已烘入石木材质，仍只用三个材质槽。
    mat=bpy.data.materials.new('Natural_Contact');mat.use_nodes=True
    nodes=mat.node_tree.nodes;links=mat.node_tree.links;nodes.clear()
    out=nodes.new('ShaderNodeOutputMaterial');mix=nodes.new('ShaderNodeMixShader')
    transparent=nodes.new('ShaderNodeBsdfTransparent');black=nodes.new('ShaderNodeEmission');black.inputs[0].default_value=(.025,.019,.012,1)
    col=nodes.new('ShaderNodeVertexColor');col.layer_name='NaturalTone'
    links.new(col.outputs['Alpha'],mix.inputs[0]);links.new(transparent.outputs[0],mix.inputs[1]);links.new(black.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs['Surface'])
    obj.data.materials.append(mat)
    bm=bmesh.new();bm.from_mesh(obj.data)
    colour=bm.loops.layers.float_color.get('NaturalTone');uv=bm.loops.layers.uv.active
    for x,y,w,d in FOOT:
        loops=[]
        for factor,alpha in ((.65,.22),(.84,.13),(1,0)):
            ring=[]
            for px,py in ((-.7,-1),(.7,-1),(1,-.7),(1,.7),(.7,1),(-.7,1),(-1,.7),(-1,-.7)):
                p=(Vector((x+px*w*.5*factor,y+py*d*.5*factor,.001))-center)*scale
                ring.append(bm.verts.new(p))
            loops.append((ring,alpha))
        faces=[(bm.faces.new(loops[0][0]),[.22]*8)]
        for k in range(2):
            inner,ia=loops[k];outer,oa=loops[k+1]
            for j in range(8):
                n=(j+1)%8;faces.append((bm.faces.new([inner[j],outer[j],outer[n],inner[n]]),[ia,oa,oa,ia]))
        for face,alphas in faces:
            face.material_index=2
            for loop,alpha in zip(face.loops,alphas):loop[colour]=(1,1,1,alpha);loop[uv].uv=(.5,.5)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    pts=[v.co for v in obj.data.vertices];lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)))
    factor=width/max(hi.x-lo.x,hi.y-lo.y);middle=Vector(((hi.x+lo.x)/2,(hi.y+lo.y)/2,lo.z))
    for v in obj.data.vertices:v.co=(v.co-middle)*factor
    obj.data.update()
def finish(kind,width):
    global V,F,MI,UV,VC,FOOT
    mesh=bpy.data.meshes.new('Natural_'+kind);mesh.from_pydata(V,[],F);mesh.materials.clear()
    for m in mats:mesh.materials.append(m)
    uv=mesh.uv_layers.new(name='UVMap')
    colors=mesh.color_attributes.new(name='NaturalTone',type='FLOAT_COLOR',domain='CORNER')
    for p,mi,coords,col in zip(mesh.polygons,MI,UV,VC):
        p.material_index=mi
        for li,co in zip(p.loop_indices,coords):
            uv.data[li].uv=co;colors.data[li].color=col
    mesh.update()
    bm=bmesh.new();bm.from_mesh(mesh)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(kind,mesh);scene.collection.objects.link(obj)
    bpy.context.view_layer.objects.active=obj;obj.select_set(True)
    bevel=obj.modifiers.new('柔化石瓦边缘','BEVEL');bevel.width=.006;bevel.segments=1;bevel.affect='EDGES'
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    # 按实际游戏占地归一，底面中心为原点。
    pts=[v.co for v in obj.data.vertices];lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)))
    scale=width/max(hi.x-lo.x,hi.y-lo.y);center=Vector(((hi.x+lo.x)/2,(hi.y+lo.y)/2,lo.z))
    for v in obj.data.vertices:v.co=(v.co-center)*scale
    obj.data.update()
    tri=obj.modifiers.new('导出三角面','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=tri.name)
    bm=bmesh.new();bm.from_mesh(obj.data)
    flat=[f for f in bm.faces if f.calc_area()<1e-14]
    if flat:bmesh.ops.delete(bm,geom=flat,context='FACES')
    bm.to_mesh(obj.data);bm.free();obj.data.update()
    bake_materials(obj,kind)
    ground_contact(obj,scale,center,width)
    bpy.ops.export_scene.fbx(filepath=str(ART/(kind+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,bake_space_transform=True,add_leaf_bones=False,use_mesh_modifiers=True,path_mode='STRIP')
    obj.select_set(False);V=[];F=[];MI=[];UV=[];VC=[];FOOT=[]
    return obj

house(-.28,.20,.87,.95,.73,.38)
house(.37,-.33,.53,.65,.47,.29)
arch(-.28,-.285,.27,.24,.43,True)
arch(.37,-.665,.20,.19,.31,True)
for xx in (-.52,-.04):arch(xx,-.285,.63,.12,.18)
chimney(-.51,.40,1.04)
foundation(-.05,.02,1.55,1.51)
for i in range(3):box(-.28,-.40-i*.095,.02+i*.015,.30,.10,.06-i*.015)
village=finish('village',.018)

# 中央主塔用完整高墙统领，左右副塔下降一层，前方只保留一个低门厅。
house(0,.24,.84,.80,1.85,.46,True,True)
house(-.60,.19,.36,.46,1.22,.26,True,True)
house(.60,.23,.36,.43,1.03,.24,True,True)
house(0,-.47,.65,.55,.64,.27,False,True)
# 低矮连廊连接塔基，平台与矮垛代替抢眼的小屋顶。
for xx in (-.47,.47):
    stones(xx,-.30,.09,.31,.61,.45)
    box(xx,-.30,.54,.35,.65,.07)
    for yy in (-.55,-.32,-.09):box(xx+(-.10 if xx<0 else .10),yy,.61,.10,.12,.11)
arch(0,-.756,.16,.32,.48,True)
for xx in (-.22,.22):arch(xx,-.169,1.47,.15,.35)
arch(0,-.169,.98,.17,.31)
arch(-.60,-.047,.85,.14,.28);arch(.60,.006,.70,.13,.26)
box(0,.24,1.33,.90,.86,.065)
box(0,.24,1.88,.90,.86,.055)
for i in range(3):box(0,-.80-i*.10,.015,.42+i*.02,.105,.105-i*.03)
city=finish('city',.0205)

# 道路长轴为 Blender Y，导入后沿 Unity Z 与现有边旋转一致。
FOOT.append((0,0,.52,2.45))
box(0,0,0,.43,2.45,.065,0,'stone')
for side in (-1,1):
    for j in range(11):box(side*(.181+random.uniform(-.007,.007)),-1.225+(j+.5)*2.45/11,.035,.086,2.45/11-.012,.08+random.uniform(-.012,.012),1,'roof')
for j in range(14):
    for side in (-1,1):box(side*.073+random.uniform(-.008,.008),-1.225+(j+.5)*2.45/14,.045,.136,2.45/14-.008,.048+random.uniform(-.009,.009))
road=finish('road',.027)
models=[village,city,road]
report=[]
for obj in models:
    obj.hide_render=True
    report.append({'kind':obj.name,'triangles':len(obj.data.polygons),'vertices':len(obj.data.vertices),'materials':len(obj.data.materials),'bounds_m':[round(x,6) for x in obj.dimensions]})

camera_data=bpy.data.cameras.new('验收相机');camera=bpy.data.objects.new('验收相机',camera_data);scene.collection.objects.link(camera);scene.camera=camera
camera_data.type='ORTHO';camera_data.lens=50;camera_data.clip_start=.0001;camera_data.clip_end=10
ld=bpy.data.lights.new('柔光','AREA');light=bpy.data.objects.new('柔光',ld);scene.collection.objects.link(light);light.location=(-.06,-.08,.13);ld.energy=.08;ld.shape='DISK';ld.size=.10
light.rotation_euler=(Vector((0,0,.005))-light.location).to_track_quat('-Z','Y').to_euler()
(OUT/'geometry.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
bpy.data.libraries.write(str(OUT/'natural-pieces.blend'),{scene},fake_user=True,compress=True)
result={'models':report,'scene':scene.name,'source':str(OUT/'natural-pieces.blend')}
