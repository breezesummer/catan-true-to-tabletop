def ship():
    # 由船头至船尾逐站放样，每道板带都有真实的外缘和压舷条。
    stations=[(-1.29,.014,.51),(-1.05,.26,.40),(-.70,.43,.34),(-.15,.49,.33),(.49,.46,.35),(.91,.35,.42),(1.10,.27,.50)]
    for sign in (-1,1):
        for j in range(5):
            a=j/5;b=(j+1)/5
            for (y0,w0,h0),(y1,w1,h1) in zip(stations,stations[1:]):
                def pt(y,w,h,t):return (sign*w*(.44+.56*t**.65),y,h*t)
                q=[pt(y0,w0,h0,a),pt(y1,w1,h1,a),pt(y1,w1,h1,b),pt(y0,w0,h0,b)]
                loft([q,[(x-sign*.010,y,z-.003) for x,y,z in q]],'player' if j==4 else 'wood' if j in (0,3) else 'plank')
                rod(pt(y0,w0,h0,b),pt(y1,w1,h1,b),.012 if j<4 else .026,'wood',n=6)
        for y,w,h in stations[1:-1]:rod((sign*w*.83,y,.13),(sign*w,y,h+.035),.026,'wood')
    def width(y):
        for (a,w,_),(b,v,_) in zip(stations,stations[1:]):
            if a<=y<=b:return w+(v-w)*(y-a)/(b-a)
        return .012
    for i in range(21):
        a=-1.24+i*.109;b=a+.106
        wa=width(a)*.91;wb=width(b)*.91
        sheet([(-wa,a,.235),(wa,a,.235),(wb,b,.235),(-wb,b,.235)],'plank',.025)
    face([(-.27,1.10,.5),(.27,1.10,.5),(.12,1.1,0),(-.12,1.1,0)],'wood')
    box(0,.81,.26,.54,.47,.42,'wood');box(0,.81,.67,.64,.56,.064,'plank')
    roof(0,.81,.75,.67,.55,.17,False,'wood')
    rod((0,.51,.943),(0,1.10,.943),.032,'player')
    for x in (-.16,.16):
        box(x,.564,.40,.105,.015,.14,'shadow');box(x,.549,.40,.013,.018,.14,'brass')
    for x in (-.29,.29):
        for y in (.58,.81,1.04):box(x,y,.68,.045,.045,.17,'player')
    for x,y,w in ((-.19,-.05,.23),(.11,-.01,.21),(-.17,.21,.19),(.16,.24,.18),(.03,-.92,.15)):crate(x,y,.26,w)
    for x,y in ((-.30,-.46),(.28,-.44),(.25,.41)):barrel(x,y,.26,.084)
    rod((0,-.54,.22),(0,-.54,1.60),.043,'wood',.029)
    rod((-.48,-.54,1.39),(.48,-.54,1.39),.026,'plank')
    def canvas(u,t):return ((u-.5)*(.95-.12*t),-.50-.29*t-.13*sin(t*pi)-.045*cos((u-.5)*pi),1.38-.56*t+.055*t*(2*u-1)**2)
    for i in range(6):
        for j in range(5):
            a=i/6;b=(i+1)/6;c=j/5;d=(j+1)/5
            q=[canvas(a,c),canvas(b,c),canvas(b,d),canvas(a,d)]
            loft([q,[(x,y+.009,z) for x,y,z in q]],'player' if i in (2,3) else 'linen')
    for x in (-.43,.43):
        rod((x,-.54,1.39),(x*.76,.31,.38),.009,'linen',n=6)
        rod((0,-.54,1.54),(x*.50,-1.00,.37),.008,'linen',n=6)
    rod((.25,.34,.26),(.25,.34,.97),.025,'wood')
    rod((.25,.34,.93),(.28,-.22,.81),.03,'plank')
    rod((.28,-.22,.80),(.28,-.22,.55),.008,'linen',n=6);crate(.28,-.22,.41,.16)
    for sign in (-1,1):rod((0,-1.27,.50),(sign*.24,-1.05,.42),.021,'player')
def plate(a,b,r0,r1,k='steel'):
    rod(a,b,r0,k,r1,12)
    a,b=Vector(a),Vector(b)
    rod(a.lerp(b,.84),a.lerp(b,.91),r1*1.05,'iron',n=12)
def sword(hand,direction):
    a=Vector(hand);forward=Vector(direction).normalized()
    across=Vector((-forward.y,forward.x,0)).normalized()
    normal=forward.cross(across).normalized()
    guard=a+forward*.14
    rod(a-forward*.14,guard,.032,'leather')
    rod(guard-across*.105,guard+across*.105,.028,'brass')
    # 单刃刀有弧收的刀尖和宽刀背，出鞘时俯看仍能见刃面。
    blade=[guard-across*.035,guard+across*.050,
           guard+forward*.47+across*.103,guard+forward*.69+across*.112,
           guard+forward*.57-across*.010,guard+forward*.23-across*.035]
    ridge=guard+forward*.31+normal*.025
    for i,point in enumerate(blade):
        face([point,blade[(i+1)%len(blade)],ridge],'steel' if i in (1,2,3) else 'iron')
    face([v-normal*.009 for v in reversed(blade)],'steel')
    for i,point in enumerate(blade):
        end=blade[(i+1)%len(blade)]
        face([point,end,end-normal*.009,point-normal*.009],'steel')
    bulb(a-forward*.144,(.044,.038,.038),'brass',10)
def horse():
    bulb((0,.07,.83),(.27,.58,.32),'horse',18)
    bulb((0,.45,.79),(.285,.29,.34),'horse')
    rod((0,-.26,.91),(0,-.55,1.38),.196,'horse',.135,16)
    # 马面、额甲、鼻带与尖耳形成独立于骑手的第二个轮廓。
    rod((0,-.54,1.35),(0,-.86,1.14),.142,'horse',.095,14)
    bulb((0,-.57,1.36),(.135,.17,.12),'steel')
    for side in (-1,1):
        rod((side*.080,-.48,1.39),(side*.108,-.47,1.61),.055,'horse',.008)
        bulb((side*.125,-.62,1.32),(.025,.036,.024),'shadow',8)
        rod((side*.11,-.75,1.17),(side*.22,-.16,1.24),.012,'leather',n=6)
    for side in (-1,1):
        rod((side*.094,-.84,1.14),(side*.13,-.59,1.36),.019,'leather')
        bulb((side*.144,-.63,1.31),(.028,.027,.028),'brass',8)
    rod((-.09,-.85,1.16),(.09,-.85,1.16),.019,'leather')
    for i in range(6):bulb((0,-.48+.047*i,1.37-.063*i),(.067,.11,.085),'shadow',10)
    for side in (-1,1):
        for front in (True,False):
            y=-.27 if front else .40;shift=side*(.065 if front else -.06)
            a=(side*.165,y,.81);b=(side*.195,y+.04+shift,.44);c=(side*.21,y-.075+shift,.135)
            plate(a,b,.102,.067,'horse');plate(b,c,.065,.04,'horse')
            bulb((c[0],c[1]-.027,.084),(.070,.095,.064),'shadow')
        q=[(side*.19,-.29,1.03),(side*.21,.45,1.04),(side*.278,.48,.62),(side*.282,-.22,.57)]
        loft([q,[(u-side*.016,v,w) for u,v,w in q]],'player')
        rod(q[2],q[3],.018,'linen',n=6)
    rod((0,.58,.95),(.045,.83,.46),.079,'shadow',.049)
    bulb((0,.02,1.10),(.21,.32,.085),'leather')
exec(compile((OUT/'knights.py').read_text(encoding='utf-8-sig'), 'knights.py', 'exec'))
def export(kind,target,height=False):
    if kind.startswith('knight'):
        angle=math.radians(32 if kind.startswith('knight-3') else 12)
        V[:]=[(x*cos(angle)-y*sin(angle),x*sin(angle)+y*cos(angle),z) for x,y,z in V]
    mesh=bpy.data.meshes.new('Expansion_'+kind);mesh.from_pydata(V,[],F);mesh.update()
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
    if kind!='wall':
        bevel=obj.modifiers.new('手工圆棱','BEVEL');bevel.width=.007;bevel.segments=1
        bpy.ops.object.modifier_apply(modifier=bevel.name)
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.triangulate(bm,faces=list(bm.faces))
    pts=[v.co for v in bm.verts]
    lo=Vector([min(p[i] for p in pts) for i in range(3)]);hi=Vector([max(p[i] for p in pts) for i in range(3)])
    scale=target/((hi-lo).z if height else max((hi-lo).x,(hi-lo).y));origin=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
    if height:
        scale=target/(2.62 if kind.startswith('knight-3') else 1.64)
        origin.x=0;origin.y=0
    for v in bm.verts:v.co=(v.co-origin)*scale
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.calc_area()<1e-14],context='FACES')
    bm.to_mesh(mesh);bm.free();mesh.update()
    bake_surface(obj,kind)
    target_file=ART/(kind+'.fbx')
    staging=target_file.with_suffix('.stage.fbx')
    bpy.ops.export_scene.fbx(filepath=str(staging),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',bake_space_transform=True,add_leaf_bones=False,path_mode='STRIP')
    staging.replace(target_file)
    obj.hide_render=True;obj.select_set(False);models[kind]=obj
    for a in (V,F,UV,M,SM):a.clear()
if not knights_only:
    gatewall();export('wall',.027)
    ship();export('ship',.0295)
    for key,build in [('trade',market),('politics',palace),('science',science)]:
        build();export('metro-'+key,.0235)
for level,height in enumerate((.017,.018,.024),1):
    for active in (False,True):
        knight(level,active);export('knight-'+str(level)+('-active' if active else '-rest'),height,True)
report=[dict(kind=k,triangles=len(o.data.polygons),size=list(o.dimensions)) for k,o in models.items()]
if knights_only:
    previous=json.loads((OUT/'几何.json').read_text(encoding='utf8'))
    report=[item for item in previous if not item['kind'].startswith('knight-')]+report
(OUT/'几何.json').write_text(json.dumps(report,indent=2),encoding='utf8')
scene.render.engine='CYCLES';scene.cycles.samples=8;scene.cycles.use_denoising=True
prefs=bpy.context.preferences.addons['cycles'].preferences
prefs.compute_device_type='HIP';prefs.get_devices()
for device in prefs.devices:device.use=device.type=='HIP'
scene.cycles.device='GPU'
scene.render.film_transparent=True;scene.render.resolution_percentage=100
scene.render.resolution_x=384;scene.render.resolution_y=384
world=bpy.data.worlds.new('柔和天光');scene.world=world;world.use_nodes=True
world.node_tree.nodes['Background'].inputs[0].default_value=(.72,.78,.88,1);world.node_tree.nodes['Background'].inputs[1].default_value=.65
cam=bpy.data.objects.new('原型相机',bpy.data.cameras.new('原型相机'));scene.collection.objects.link(cam);scene.camera=cam;cam.data.type='ORTHO';cam.data.clip_start=.0001
light=bpy.data.objects.new('窗光',bpy.data.lights.new('窗光','AREA'));scene.collection.objects.link(light);light.location=(-.055,-.065,.10);light.data.energy=.10;light.data.shape='DISK';light.data.size=.08;light.rotation_euler=(-light.location).to_track_quat('-Z','Y').to_euler()
bpy.data.libraries.write(str(OUT/('三国骑士.blend' if knights_only else '扩展棋子.blend')),{scene},fake_user=True,compress=True)
result={'models':report,'scene':scene.name}

