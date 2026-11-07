"""在 Blender MCP 新场景执行；整组完成后再覆盖 Unity 资源。"""
from pathlib import Path
import bpy, math, json, shutil, tempfile, traceback
from mathutils import Vector,Matrix
recipe=Path(__file__).resolve().parent
for file in ('sculpt.py','armour.py','warhorse.py','soldier.py'):
    exec(compile((recipe/file).read_text('utf-8-sig'),str(recipe/file),'exec'))
stage=Path(tempfile.mkdtemp(prefix='骑士导出-',dir=ROOT/'.local'))
status=ROOT/'.local/knight-reference-status.txt'
old_art=ART
ART=stage
# 共用扩展棋子的实材质烘焙流程，独立 UV 保留环境遮蔽。
exec(compile((ROOT/'art/recipes/expansion-models-v001/bake.py').read_text('utf8'),'bake.py','exec'))
def contact_patch(obj,turn,scale,mounted):
    # 与城市、道路使用同一棋盘空间的接触阴影。脚底实、边缘透，不参与拾取。
    mat=bpy.data.materials.new('Natural_Contact');mat.use_nodes=True
    nodes=mat.node_tree.nodes;nodes.clear()
    output=nodes.new('ShaderNodeOutputMaterial');transparent=nodes.new('ShaderNodeBsdfTransparent')
    mat.node_tree.links.new(transparent.outputs[0],output.inputs['Surface'])
    obj.data.materials.append(mat)
    bm=bmesh.new();bm.from_mesh(obj.data)
    uv=bm.loops.layers.uv.active
    tint=bm.loops.layers.float_color.new('NaturalTone')
    patches=[(x,y,w,d,.53) for x,y,w,d in footprints]
    patches.append((.02,.035,.41 if mounted else .39,.77 if mounted else .255,.23))
    for x,y,w,d,opacity in patches:
        center=bm.verts.new(turn@Vector((x*scale,y*scale,.000025)))
        prev=None
        for fraction,alpha in ((.35,opacity),(.65,opacity*.72),(1,0)):
            row=[bm.verts.new(turn@Vector(((x+w*fraction*cos(tau*i/20))*scale,(y+d*fraction*sin(tau*i/20))*scale,.000025))) for i in range(20)]
            for i in range(20):
                j=(i+1)%20
                face=bm.faces.new([center,row[i],row[j]] if prev is None else [prev[i],row[i],row[j],prev[j]])
                face.material_index=3
                for loop in face.loops:
                    strength=alpha if loop.vert in (row[i],row[j]) else prior_alpha if prev else opacity
                    loop[tint]=(.02,.016,.01,strength);loop[uv].uv=(1,.055)
            prev=row;prior_alpha=alpha
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if f.material_index==3])
    bm.to_mesh(obj.data);bm.free();obj.data.update()

report=[]
scene.render.engine='CYCLES'
for level in range(1,4):
    for active in (False,True):
        tag=f'knight-{level}-'+('active' if active else 'rest')
        status.write_text('雕塑 '+tag,encoding='utf8')
        soldier(level,active)
        # 核心占点固定在脚下。装备沿角色方向伸出，六姿态保持同一人物尺度。
        turn=Matrix.Rotation(math.radians(-43 if level==3 else 5 if level==1 else 9),3,'Z')
        s=(.0108 if level<3 else .0106)
        floor=min(v[2] for v in verts)
        xyz=[]
        for vertex_pos in verts:
            x,y,z=vertex_pos
            xyz.append(tuple(turn@Vector((x*s,y*s,(z-floor)*s))))
        mesh=bpy.data.meshes.new('Expansion_'+tag);mesh.from_pydata(xyz,[],faces);mesh.update()
        for m in mats:mesh.materials.append(m)
        uv=mesh.uv_layers.new(name='UVMap')
        for poly,mi,texcoords,smooth in zip(mesh.polygons,materials,coords,shading):
            poly.material_index=mi;poly.use_smooth=smooth
            for i,co in zip(poly.loop_indices,texcoords):uv.data[i].uv=co
        bm=bmesh.new();bm.from_mesh(mesh)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00000012)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bmesh.ops.triangulate(bm,faces=list(bm.faces))
        bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_area()<1.1e-14],context='FACES')
        bm.to_mesh(mesh);bm.free();mesh.update()
        obj=bpy.data.objects.new(tag,mesh);scene.collection.objects.link(obj)
        for ob in scene.objects:ob.select_set(False)
        obj.select_set(True);bpy.context.view_layer.objects.active=obj
        status.write_text('烘焙 '+tag,encoding='utf8')
        bake_surface(obj,tag,resolution=2048,margin=.0018)
        contact_patch(obj,turn,s,level==3)
        footprints.clear()
        bpy.ops.export_scene.fbx(filepath=str(stage/(tag+'.fbx')),use_selection=True,object_types={'MESH'},
            axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,path_mode='STRIP')
        for mat in mesh.materials:
            for node in mat.node_tree.nodes:
                if node.type=='TEX_IMAGE' and not node.image.packed_file:node.image.pack()
        surface=mesh.materials[2].node_tree.nodes.get('Principled BSDF')
        surface.inputs['Metallic'].default_value=.67
        surface.inputs['Roughness'].default_value=.34
        report.append(dict(kind=tag,triangles=len(mesh.polygons),size=list(obj.dimensions)))
        models[tag]=obj;obj.hide_render=True;obj.select_set(False)
        for buffer in (verts,faces,materials,coords,shading):buffer.clear()

# 透明库存图使用与游戏同一网格。额外设置一盏暖主光和两盏柔光，检查金属曲率。
world=bpy.data.worlds.new('中性天光');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.64,.68,.72,1)
world.node_tree.nodes['Background'].inputs[1].default_value=.30
cam=bpy.data.objects.new('骑士相机',bpy.data.cameras.new('骑士相机'));scene.collection.objects.link(cam)
scene.camera=cam;cam.data.type='ORTHO';cam.data.clip_start=.0001
def light(name,coord,power,size,col):
    l=bpy.data.lights.new(name,'AREA');l.energy=power;l.shape='DISK';l.size=size;l.color=col
    o=bpy.data.objects.new(name,l);scene.collection.objects.link(o);o.location=coord
    o.rotation_euler=(Vector((0,0,.012))-o.location).to_track_quat('-Z','Y').to_euler()
light('暖窗',(-.045,-.058,.085),.13,.034,(1,.86,.69))
light('冷补光',(.055,-.045,.048),.042,.055,(.77,.85,1))
light('后缘光',(.035,.05,.075),.13,.045,(1,.90,.73))
scene.cycles.samples=20;scene.cycles.use_denoising=True;scene.render.film_transparent=True
scene.render.resolution_percentage=100;scene.render.resolution_x=384;scene.render.resolution_y=384
colours={'red':(.80,.205,.12),'blue':(.23,.44,.76),'orange':(.95,.50,.13),'beige':(.98,.94,.83)}
def tint(obj,c):
    mix=next(n for n in obj.data.materials[1].node_tree.nodes if n.type=='MIX_RGB')
    mix.inputs[2].default_value=tuple(((v+.055)/1.055)**2.4 for v in colours[c])+(1,)
def frame(obj,angle,padding):
    corners=[Vector(v) for v in obj.bound_box];ctr=sum(corners,Vector())/8
    direction=Vector((0,-math.cos(math.radians(angle)),math.sin(math.radians(angle))))
    cam.location=ctr+direction*.15;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
    rot=cam.rotation_euler.to_matrix().transposed()
    screen=[rot@(p-ctr) for p in corners]
    cam.data.ortho_scale=max(max(p[i] for p in screen)-min(p[i] for p in screen) for i in (0,1))*padding
for tag,obj in models.items():
    obj.hide_render=False
    if tag.endswith('rest'):
        frame(obj,37,1.09)
        for c in colours:
            tint(obj,c);scene.render.filepath=str(stage/f'thumb-{tag}-{c}.png')
            bpy.ops.render.render(write_still=True)
    else:
        scene.render.resolution_x=800;scene.render.resolution_y=800
        frame(obj,26,1.15);tint(obj,'red')
        scene.render.filepath=str(stage/f'{tag}-原模.png');bpy.ops.render.render(write_still=True)
        scene.render.resolution_x=384;scene.render.resolution_y=384
    obj.hide_render=True
for obj in models.values():tint(obj,'red')
for lvl in range(1,4):
    for active in (False,True):
        obj=models[f'knight-{lvl}-'+('active' if active else 'rest')]
        obj.location=((lvl-2)*.046,(-.029 if active else .029),0)
        obj.hide_render=False
middle=Vector((0,0,.013));direction=Vector((0,-.80,.60))
cam.location=middle+direction*.18;cam.rotation_euler=(-direction).to_track_quat('-Z','Y').to_euler()
cam.data.ortho_scale=.150;scene.render.resolution_x=1440;scene.render.resolution_y=900
bpy.data.libraries.write(str(stage/'骑士雕塑.blend'),{scene},fake_user=True,compress=True)
manifest=ROOT/'art/recipes/expansion-models-v001/几何.json'
prior=json.loads(manifest.read_text('utf8'))
for f in stage.iterdir():
    dest=RES if f.name.startswith('thumb-') else OUT if f.suffix=='.blend' or '原模' in f.stem else old_art
    shutil.copy2(f,dest/f.name)
manifest.write_text(json.dumps([i for i in prior if not i['kind'].startswith('knight-')]+report,indent=2),encoding='utf8')
(OUT/'几何.json').write_text(json.dumps(report,indent=2),encoding='utf8')
if stage.resolve().parent==(ROOT/'.local').resolve() and stage.name.startswith('骑士导出-'):shutil.rmtree(stage)
status.write_text('完成',encoding='utf8')
