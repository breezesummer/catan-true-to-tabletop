"""经 Blender MCP 执行。先在临时目录生成三座城市，全部成功后再覆盖 Unity 美术文件。"""
import json, shutil, tempfile
from pathlib import Path
recipe=Path(__file__).resolve().parent
common=recipe.parent/'expansion-models-v001'
def run(path):
    exec(compile(path.read_text(encoding='utf-8-sig'),str(path),'exec'),globals())
save_palette=False
scene_name='大都会 · 金帐市集、重檐议政殿、浑仪天文台'
run(common/'build.py')
run(common/'bake.py')
for file in ('details.py','market.py','palace.py','observatory.py'):run(recipe/file)
# 材质保留地图的暖石色，三系点色分别取金黄篷布、靛青檐瓦与铜绿。
tones={'stone':(143,121,95),'trim':(202,181,144),'shadow':(55,48,41),'wood':(92,55,35),
       'plank':(150,99,57),'brass':(219,185,114),'steel':(128,142,145),'iron':(65,69,64),
       'linen':(243,218,156),'player':(240,230,205),'leather':(131,57,37),
       'green':(74,139,126),'blue':(76,92,108),'yellow':(209,143,54)}
for i,key in enumerate(names):
    if key not in tones:continue
    ys=slice(i//4*256,(i//4+1)*256);xs=slice(i%4*256,(i%4+1)*256)
    grain=pix[ys,xs,0]/max(.01,np.mean(pix[ys,xs,0]))
    grain=grain+.022*np.sin(x*67+y*31)+.018*np.sin(y*101+x*14)
    srgb=np.clip(grain[:,:,None]*np.array(tones[key])[None,None,:]/255,.01,1)
    pix[ys,xs,:3]=srgb**1.42
tex.pixels.foreach_set(pix.ravel())
destination=ART
stage=Path(tempfile.mkdtemp(prefix='metro-sculpt-',dir=ROOT/'.local'))
ART=stage
info=[]
def contact(obj,rx,ry):
    mat=bpy.data.materials.new('Natural_Contact');mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial')
    mat.node_tree.links.new(nodes.new('ShaderNodeBsdfTransparent').outputs[0],out.inputs[0])
    obj.data.materials.append(mat)
    mesh=obj.data;bm=bmesh.new();bm.from_mesh(mesh)
    colour=bm.loops.layers.float_color.new('NaturalTone');uv=bm.loops.layers.uv.active
    rings=[]
    for scale,alpha in ((.64,.32),(.88,.18),(1,0)):
        loop=[bm.verts.new((rx*scale*cos(tau*i/32),ry*scale*sin(tau*i/32),.000025)) for i in range(32)]
        if rings:
            prev,old=rings[-1]
            faces=[(bm.faces.new((prev[i],loop[i],loop[(i+1)%32],prev[(i+1)%32])),(old,alpha,alpha,old)) for i in range(32)]
        else:faces=[(bm.faces.new(loop),[alpha]*32)]
        rings.append((loop,alpha))
        for f,values in faces:
            f.material_index=3
            for point,value in zip(f.loops,values):
                point[colour]=(.028,.020,.015,value);point[uv].uv=(1,.38)
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if f.material_index==3])
    bm.to_mesh(mesh);bm.free();mesh.update()

for kind,construct,scale,shadow in (
        ('metro-trade',market,.01375,(.0122,.0120)),
        ('metro-politics',palace,.0133,(.0121,.0107)),
        ('metro-science',observatory,.0127,(.0126,.0121))):
    construct()
    mesh=bpy.data.meshes.new(kind);mesh.from_pydata(V,[],F);mesh.update()
    for m in mats:mesh.materials.append(m)
    uv=mesh.uv_layers.new(name='UVMap')
    for poly,mi,coords,smooth in zip(mesh.polygons,M,UV,SM):
        poly.material_index=mi;poly.use_smooth=smooth
        for j,co in zip(poly.loop_indices,coords):uv.data[j].uv=co
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(kind,mesh);scene.collection.objects.link(obj)
    for ob in scene.objects:ob.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    edge=obj.modifiers.new('梁石收棱','BEVEL');edge.width=.005;edge.segments=2
    bpy.ops.object.modifier_apply(modifier=edge.name)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bottom=min(p.co.z for p in bm.verts)
    for p in bm.verts:p.co=(p.co-Vector((0,0,bottom)))*scale
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00000022)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_area()<3.5e-14],context='FACES')
    bm.to_mesh(mesh);bm.free();mesh.update()
    bake_surface(obj,kind,2048,.0025)
    contact(obj,*shadow)
    bpy.ops.export_scene.fbx(filepath=str(stage/(kind+'.fbx')),use_selection=True,object_types={'MESH'},
        axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,path_mode='STRIP')
    for m in mesh.materials:
        for node in m.node_tree.nodes:
            if node.type=='TEX_IMAGE' and not node.image.packed_file:node.image.pack()
    for buf in (V,F,M,UV,SM):buf.clear()
    info.append(dict(kind=kind,triangles=len(mesh.polygons),size=list(obj.dimensions)))
    models[kind]=obj;obj.hide_render=True;obj.select_set(False)

for i,ob in enumerate(models.values()):
    ob.location=((i-1)*.044,0,0);ob.hide_render=False
    tint=next(n for n in ob.data.materials[1].node_tree.nodes if n.type=='MIX_RGB')
    tint.inputs[2].default_value=(.51,.054,.026,1)
scene.world=bpy.data.worlds.new('工坊天光');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.43
for loc,power,size,name in [((-.05,-.078,.12),.30,.08,'暖窗'),((.06,.05,.08),.16,.05,'轮廓光')]:
    l=bpy.data.lights.new(name,'AREA');l.energy=power;l.size=size
    ob=bpy.data.objects.new(name,l);scene.collection.objects.link(ob);ob.location=loc
    ob.rotation_euler=(-ob.location).to_track_quat('-Z','Y').to_euler()
cam=bpy.data.objects.new('三城合照',bpy.data.cameras.new('三城合照'));scene.collection.objects.link(cam)
cam.data.type='ORTHO';cam.data.ortho_scale=.139;cam.data.clip_start=.0001
cam.location=(0,-.10,.112);cam.rotation_euler=(Vector((0,0,.019))-cam.location).to_track_quat('-Z','Y').to_euler()
scene.camera=cam;scene.render.film_transparent=True;scene.cycles.samples=24;scene.cycles.use_denoising=True
scene.render.resolution_percentage=100;scene.render.resolution_x=1440;scene.render.resolution_y=650
scene.render.filepath=str(stage/'三城造型.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(stage/'三座大都会.blend'),{scene},fake_user=True,compress=True)
for f in stage.iterdir():
    dest=recipe if f.name in ('三城造型.png','三座大都会.blend') else destination
    shutil.copy2(f,dest/f.name)
manifest=common/'几何.json';changes={i['kind']:i for i in info}
previous=json.loads(manifest.read_text('utf8'));manifest.write_text(json.dumps([changes.get(i['kind'],i) for i in previous],indent=2),encoding='utf8')
(recipe/'几何.json').write_text(json.dumps(info,indent=2),encoding='utf8')
if stage.resolve().parent==(ROOT/'.local').resolve() and stage.name.startswith('metro-sculpt-'):shutil.rmtree(stage)
