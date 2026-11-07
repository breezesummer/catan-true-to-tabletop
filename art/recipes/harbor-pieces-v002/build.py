"""在 Blender MCP 中运行，沿用扩展棋子的 UV、烘焙与米制约定。"""
from pathlib import Path
import tempfile,shutil,json
recipe=Path(__file__).resolve().parent
common=recipe.parent/'expansion-models-v001'
def run(p):
    exec(compile(p.read_text('utf-8-sig'),str(p),'exec'),globals())
save_palette=False
scene_name='海湾棋子 · 商船、海盗快船与城墙'
run(common/'build.py');run(common/'bake.py')
run(recipe.parent/'metropolis-v002/details.py')
run(recipe/'ships.py');run(recipe/'wall.py')
destination=ART
stage=Path(tempfile.mkdtemp(prefix='harbor-sculpt-',dir=ROOT/'.local'));ART=stage
initial=pix.copy()
def palette(tag):
    data=initial.copy()
    names_rgb={'plank':(167,120,77),'wood':(112,76,46),'leather':(63,51,36),
               'linen':(224,213,179),'player':(246,244,230)}
    if tag=='pirate':names_rgb.update(plank=(118,94,65),wood=(74,58,46),shadow=(38,45,48),iron=(66,74,73),linen=(226,217,188))
    if tag=='wall':names_rgb={'horse':(158,133,99)}
    v,u=np.mgrid[0:1:256j,0:1:256j]
    noise=np.random.default_rng(771)
    for key,rgb in names_rgb.items():
        i=names.index(key);grain=1+noise.normal(0,.023,u.shape)
        if key in ('plank','wood','leather'):
            phase=v*11+.22*np.sin(u*8+v*12)+.09*np.sin(u*25)
            for cx,cy in ((.26,.27),(.76,.66)):
                rr=np.sqrt(((u-cx)*.65)**2+((v-cy)*2.7)**2)
                phase+=.59*np.exp(-18*rr)*np.cos(rr*42)
                grain-=.22*np.exp(-68*rr)
            grain+=.082*np.sin(phase*tau)-.21*np.maximum(0,np.cos(phase*tau))**12
            grain+=.035*np.sin(v*651+.8*np.sin(u*23))+.035*np.cos(v*19+u*3)
        elif key=='horse':
            grain+=.062*np.sin(u*18+np.sin(v*10))+.044*np.cos(14*v-3*u)+.025*np.cos(u*47+v*33)
            for j in range(145):
                cx,cy=noise.random(2);r=noise.uniform(.0015,.006);rr=(u-cx)**2+((v-cy)*1.3)**2
                grain+=np.exp(-rr/(r*r))*noise.uniform(-.32,.22)
        else:
            grain+=.020*(np.sin(v*640)+np.cos(u*673))+.022*np.cos(v*18+u*7)
        if key=='player' and tag=='ship':
            for seam in (.28,.52,.77):
                grain-=.064*np.exp(-((u-seam)/.006)**2)
                grain+=.032*np.exp(-((u-seam-.007)/.004)**2)
        srgb=np.clip(np.array(rgb)[None,None,:]*grain[:,:,None]/255,.01,1)
        data[i//4*256:(i//4+1)*256,i%4*256:(i%4+1)*256,:3]=srgb**1.30
    if tag=='pirate':
        # 直接在连续帆布 UV 上作旧象牙色的骷髅与交叉骨，双面随帆腹弯曲。
        i=names.index('shadow');im=data[i//4*256:(i//4+1)*256,i%4*256:(i%4+1)*256,:3]
        uu=(u-.016)/.967;vv=(v-.016)/.967
        def ellipse(x,y,rx,ry):
            return ((uu-x)/rx)**2+((vv-y)/ry)**2<1
        def stroke(x,y,a,b,w):
            dx=a-x;dy=b-y;q=np.clip(((uu-x)*dx+(vv-y)*dy)/(dx*dx+dy*dy),0,1)
            return (uu-x-q*dx)**2+(vv-y-q*dy)**2<w*w
        bones=np.zeros(u.shape,dtype=bool)
        for a,b in (((.29,.60),(.72,.79)),((.30,.79),(.71,.60))):
            bones|=stroke(*a,*b,.018)
            for x,y in (a,b):
                bones|=ellipse(x-.013,y+.010,.021,.019)|ellipse(x+.013,y-.010,.021,.019)
        skull=ellipse(.50,.366,.18,.170)|ellipse(.50,.461,.156,.098)
        skull|=(abs(uu-.50)<.094)&(vv>.48)&(vv<.595-.020*abs((uu-.5)/.094))
        holes=ellipse(.426,.396,.052,.046)|ellipse(.574,.396,.052,.046)
        holes|=(vv>.446)&(vv<.511)&(abs(uu-.50)<(vv-.44)*.42)
        holes|=(vv>.546)&((abs(uu-.468)<.006)|(abs(uu-.532)<.006))
        label=(bones|skull)&~holes
        flag=im.copy();fabric=.90+.035*np.sin(u*38+v*13)+noise.normal(0,.017,u.shape)
        flag[label]=np.clip(np.array([.79,.73,.60])*fabric[label,None],0,1)
        i=names.index('blue');data[i//4*256:(i//4+1)*256,i%4*256:(i%4+1)*256,:3]=flag
    tex.pixels.foreach_set(data.ravel())


def footing(obj,origin,scale):
    # 外墙四面的砂土逐渐隐进地形。门前的路不铺半透明地毯。
    soil=bpy.data.materials.new('Natural_Contact');soil.use_nodes=True
    nodes=soil.node_tree.nodes;nodes.clear();out=nodes.new('ShaderNodeOutputMaterial')
    soil.node_tree.links.new(nodes.new('ShaderNodeBsdfTransparent').outputs[0],out.inputs[0]);obj.data.materials.append(soil)
    bm=bmesh.new();bm.from_mesh(obj.data);tone=bm.loops.layers.float_color.new('NaturalTone');uv=bm.loops.layers.uv.active
    for side in range(4):
        spans=[(-.94,-.26),(.26,.94)] if side==0 else [(-.94,.94)]
        for lo,hi in spans:
            rows=[]
            for i in range(19):
                a=lo+(hi-lo)*i/18;wave=.018*sin(11*a+.8*side)+.013*cos(19*a-.4*side)
                rows.append([])
                for r in (.874,.938+wave,.994+wave*1.6):
                    x,y=((a,-r),(r,a),(-a,r),(-r,-a))[side]
                    rows[-1].append(bm.verts.new((Vector((x,y,.002))-origin)*scale))
            for i in range(18):
                for j in (0,1):
                    f=bm.faces.new((rows[i][j],rows[i+1][j],rows[i+1][j+1],rows[i][j+1]));f.material_index=3
                    for l,alpha in zip(f.loops,((.65,.65,.33,.33) if j==0 else (.33,.33,0,0))):
                        l[tone]=(.25,.17,.10,alpha);l[uv].uv=(.5,.5)
    bmesh.ops.triangulate(bm,faces=[f for f in bm.faces if f.material_index==3]);bm.to_mesh(obj.data);bm.free();obj.data.update()

report=[]
for tag in ('ship','pirate','wall'):
    palette(tag);rng.seed(1183)
    if tag=='wall':
        gatewall()
        # 只以原墙确定中心和缩放。门前新增的路石不改变门洞与道路的配准。
        raw=np.asarray(V);lo=raw.min(axis=0);hi=raw.max(axis=0)
        scale=.027/max(hi[0]-lo[0],hi[1]-lo[1]);origin=Vector(((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]))
        courtyard()
    elif tag=='pirate':pirate()
    else:merchant()
    mesh=bpy.data.meshes.new('Harbor_'+tag);mesh.from_pydata(V,[],F);mesh.update()
    for mat in mats:mesh.materials.append(mat)
    uv=mesh.uv_layers.new(name='UVMap')
    for p,mi,coords,smooth in zip(mesh.polygons,M,UV,SM):
        p.material_index=mi;p.use_smooth=smooth
        for i,co in zip(p.loop_indices,coords):uv.data[i].uv=co
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    obj=bpy.data.objects.new(tag,mesh);scene.collection.objects.link(obj)
    for other in scene.objects:other.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    if tag!='wall':
        bevel=obj.modifiers.new('船缘倒角','BEVEL');bevel.width=.003;bevel.segments=1
        bpy.ops.object.modifier_apply(modifier=bevel.name)
    bm=bmesh.new();bm.from_mesh(mesh)
    if tag!='wall':
        low=min(v.co.y for v in bm.verts);high=max(v.co.y for v in bm.verts)
        scale=(.0295 if tag=='ship' else .0325)/(high-low)
        origin=Vector((0,(low+high)/2,min(v.co.z for v in bm.verts)))
    for v in bm.verts:v.co=(v.co-origin)*scale
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0000002)
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_area()<3.5e-14],context='FACES');bm.to_mesh(mesh);bm.free();mesh.update()
    bake_surface(obj,tag,2048,.0025)
    if tag=='wall':footing(obj,origin,scale)
    bpy.ops.export_scene.fbx(filepath=str(stage/(tag+'.fbx')),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,path_mode='STRIP')
    for m in mesh.materials:
        for n in m.node_tree.nodes:
            if n.type=='TEX_IMAGE' and not n.image.packed_file:n.image.pack()
    report.append(dict(kind=tag,triangles=len(mesh.polygons),size=list(obj.dimensions),baseY=min(v.co.z for v in mesh.vertices)))
    models[tag]=obj;obj.hide_render=True;obj.select_set(False)
    for buf in (V,F,M,UV,SM):buf.clear()
scene.world=bpy.data.worlds.new('海港天光');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs[1].default_value=.70
cam=bpy.data.objects.new('模型相机',bpy.data.cameras.new('模型相机'));scene.collection.objects.link(cam)
cam.data.type='ORTHO';cam.data.clip_start=.0001;scene.camera=cam
l=bpy.data.lights.new('侧光','AREA');l.energy=.21;l.size=.065
lamp=bpy.data.objects.new('侧光',l);scene.collection.objects.link(lamp);lamp.location=(-.05,-.075,.105)
lamp.rotation_euler=(-lamp.location).to_track_quat('-Z','Y').to_euler()
scene.cycles.samples=16;scene.cycles.use_denoising=True;scene.render.film_transparent=True
scene.render.resolution_percentage=100;scene.render.resolution_x=384;scene.render.resolution_y=384
colours={'red':(.82,.23,.145),'blue':(.27,.48,.77),'orange':(.98,.54,.18),'beige':(1,.98,.89)}
for tag in ('ship','wall'):
    obj=models[tag];obj.hide_render=False
    target=Vector((0,0,obj.dimensions.z*.45));cam.location=target+Vector((.042,-.105,.095 if tag=='ship' else .17))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=max(obj.dimensions)*1.42
    mix=next(n for n in obj.data.materials[1].node_tree.nodes if n.type=='MIX_RGB')
    for key,values in colours.items():
        mix.inputs[2].default_value=tuple(((v+.055)/1.055)**2.4 for v in values)+(1,)
        scene.render.filepath=str(stage/f'thumb-{tag}-{key}.png');bpy.ops.render.render(write_still=True)
    mix.inputs[2].default_value=(.059,.196,.554,1)
    obj.hide_render=True
for i,obj in enumerate(models.values()):obj.location=((i-1)*.042,0,0);obj.hide_render=False
target=Vector((0,0,.009));cam.location=target+Vector((0,-.11,.12))
cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=.13
scene.render.resolution_x=1350;scene.render.resolution_y=535
scene.render.filepath=str(stage/'模型合照.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(stage/'海湾棋子.blend'),{scene},fake_user=True,compress=True)
for f in stage.iterdir():
    dst=recipe if f.name in ('海湾棋子.blend','模型合照.png') else RES if f.name.startswith('thumb-') else destination
    shutil.copy2(f,dst/f.name)
manifest=common/'几何.json';prior=json.loads(manifest.read_text('utf8'))
by_kind={i['kind']:i for i in prior}
by_kind.update({i['kind']:i for i in report})
manifest.write_text(json.dumps(list(by_kind.values()),indent=2),encoding='utf8')
(recipe/'几何.json').write_text(json.dumps(report,indent=2),encoding='utf8')
if stage.resolve().parent==(ROOT/'.local').resolve() and stage.name.startswith('harbor-sculpt-'):shutil.rmtree(stage)
