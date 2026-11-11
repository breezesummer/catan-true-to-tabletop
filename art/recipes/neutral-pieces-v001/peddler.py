"""货郎的头部、衣袍和双手共用厘米制坐标，最终烘为一份棋盘材质。"""
from math import cos, sin, tau, pi, exp
from mathutils import Euler

def interpolate(stations, subdivisions):
    """在相邻截面之间作三次插值，消除关节处的硬折线。"""
    for i in range(len(stations)-1):
        a,b,c,d=[stations[max(0,min(len(stations)-1,k))] for k in (i-1,i,i+1,i+2)]
        for j in range(subdivisions):
            t=j/subdivisions
            yield tuple(.5*(2*y+(-x+z)*t+(2*x-5*y+4*z-w)*t*t+
                            (-x+3*y-3*z+w)*t**3) for x,y,z,w in zip(a,b,c,d))
    yield stations[-1]

def garment(stations, colour, depth):
    sections=[]
    for x,y,z,w,h,weight in interpolate(stations,4):
        section=[]
        for j in range(64):
            a=tau*j/64
            pleat=.74*cos(7*a+2.9*z)+.32*cos(11*a-3.7*z+.5)+.22*sin(3*a-z)
            fold=weight*depth*pleat
            lx=x+(w+fold)*cos(a)
            overlap=0
            if colour=='green' and z>.92 and sin(a)<0:
                t=(z-.92)/.51
                edge=.224-.29*t
                overlap=.012*exp(-((lx-edge)/.029)**2)*(-sin(a))**3
            section.append((lx,y+(h+fold)*sin(a)-overlap,
                            z+weight*.023*sin(2*a-.8)+weight*.012*cos(5*a+z)))
        sections.append(section)
    rings(sections,colour)

def folded_sleeve(stations):
    samples=list(interpolate(stations,5));sections=[]
    for j,(x,y,z,w,h) in enumerate(samples):
        prev=V(samples[max(0,j-1)][:3]);nxt=V(samples[min(j+1,len(samples)-1)][:3])
        axis=(nxt-prev).normalized()
        u=axis.cross(V((0,1,0))).normalized();v=axis.cross(u)
        t=j/(len(samples)-1);row=[]
        for k in range(40):
            a=tau*k/40
            gathers=.011*cos(5*a+3*t)+.006*sin(8*a-9*t)
            pinch=.014*cos(19*t+1.8*cos(a)) * exp(-((t-.56)/.17)**2)
            row.append(V((x,y,z))+u*(w+gathers+pinch)*cos(a)+v*(h+gathers)*sin(a)
                       +V((0,0,-.024*(1-cos(a))*.5*sin(pi*t))))
        sections.append(row)
    rings(sections,'blue')
    # 织边贴着袖口的最后几排截面。
    rings(sections[-4:],'vest')

def cloth_shoe(x,y,yaw):
    rows=[]
    for z,w,length,shift in [(0,.069,.137,0),(.016,.074,.142,-.005),(.030,.072,.141,-.003),
                             (.047,.070,.131,.004),(.065,.066,.103,.018),
                             (.090,.057,.074,.035),(.105,.052,.062,.04)]:
        section=[]
        for k in range(36):
            a=tau*k/36
            dx=w*cos(a)*(1-.14*sin(a));dy=length*sin(a)+shift
            section.append((x+dx*cos(yaw)-dy*sin(yaw),y+dx*sin(yaw)+dy*cos(yaw),
                            z+.013*max(0,-sin(a))**4))
        rows.append(section)
    rings(rows[:3],'leather');rings(rows[2:],'vest')

face_uv={}
face_polygons=set()
robe_rest={}
POT_X, POT_Y = .53, -.565

def lean(point):
    pivot=V((-.045,.025,.84))
    weight=max(0,min(1,(point.z-.77)/.27))
    rotation=Euler((.42*weight,.49*weight,-.07*weight)).to_matrix()
    return pivot+rotation @ (point-pivot)

def pose_range(start,transform):
    for i in range(start,len(vertices)):
        p=V(vertices[i])/.01-offset
        vertices[i]=tuple((transform(p)+offset)*.01)

def sculpt_head():
    # 正面通过局部位移连成一个面；眼眶、唇峰与鼻翼无拼接缝。
    stations=[(0,.035,.065),(.07,.066,.085),(.16,.088,.098),(.28,.103,.110),
              (.43,.112,.119),(.58,.119,.120),(.74,.116,.119),(.88,.100,.102),(1,.058,.061)]
    def profile(t):
        for (a,w,h),(b,v,k) in zip(stations,stations[1:]):
            if t<=b:
                q=(t-a)/(b-a);return w+(v-w)*q,h+(k-h)*q
        return stations[-1][1:]
    start=len(vertices);first=len(faces);rows=[]
    for j in range(49):
        t=j/48;w,h=profile(t);row=[]
        for k in range(96):
            a=tau*k/96;lx=w*cos(a);front=max(0,-sin(a))
            def bump(x0,t0,rx,rt): return exp(-((lx-x0)/rx)**2-((t-t0)/rt)**2)
            relief=.043*bump(0,.50,.019,.13)+.038*bump(0,.391,.027,.043)
            relief+=.012*bump(0,.26,.046,.05)+.013*bump(0,.105,.053,.058)
            for side in (-1,1):
                relief+=.016*bump(side*.046,.62,.035,.032)-.016*bump(side*.046,.565,.029,.042)
                relief+=.017*bump(side*.067,.46,.039,.081)+.014*bump(side*.025,.381,.016,.023)
            ly=h*sin(a)-front**2*relief
            # 面向来客，头部微偏，后脑到颈部自然后收。
            yaw=-.17
            expression=.009*bump(.048,.62,.039,.037)+.007*bump(.047,.24,.028,.040)
            row.append((.005+lx*cos(yaw)-ly*sin(yaw),-.132+lx*sin(yaw)+ly*cos(yaw),
                        1.48+.292*t-.023*ly+expression))
            face_uv[start+j*96+k]=(.5+lx/.263,.015+.963*t)
        rows.append(row)
    rings(rows,'skin')
    for j in range(48):
        for k in range(96):
            if sin(tau*(k+.5)/96)<-.15:
                face_polygons.add(first+1+j*96+k)
    # 露出耳廓，发髻扎于脑后，额前包一条窄旧布巾。
    for side in (-1,1):
        orb((.005+side*.114,-.118,1.625),(.016,.025,.034),'skin')
    loft([(.010,-.090,1.672,.119,.118),(.011,-.086,1.711,.119,.121),
          (.009,-.077,1.751,.098,.104),(.002,-.070,1.775,.065,.071)],'hair',48)
    # 发包随头骨向后卷，发束沿盘绕方向收紧，避免孤立的球形发髻。
    loft([(-.006,-.025,1.748,.055,.055),(-.011,-.008,1.764,.065,.065),
          (-.018,.007,1.784,.058,.071),(-.020,.014,1.805,.046,.056),
          (-.017,.014,1.817,.024,.032)],'hair',40)
    for j in range(8):
        a=j*tau/8
        pts=[]
        for k in range(14):
            t=k/13; r=.052*(1-.57*t); ang=a+1.45*t
            pts.append((-.014+r*cos(ang),.004+1.13*r*sin(ang),1.763+.052*t))
        pipe(pts,[.0032]*len(pts),'dark',6)
    pipe([(-.069,-.007,1.774),(-.011,-.058,1.765),(.048,-.012,1.773),
          (.001,.054,1.782),(-.070,-.007,1.774)],[.005]*5,'leather',8)
    for j in range(3):
        rows=[]
        for s in (0,1):
            row=[]
            for k in range(64):
                a=tau*k/64
                row.append((.01+.121*cos(a),-.085+.124*sin(a),
                            1.688+j*.008+s*.009+.018*cos(a-.5)+.006*sin(3*a)))
            rows.append(row)
        rings(rows,'vest')
    pipe([(.111,-.021,1.70),(.127,.015,1.66),(.121,.049,1.60),(.124,.044,1.55)],
         [.016,.015,.013,.006],'vest',10)

def ribbon(points,col):
    """薄布领按左右边界铺在胸前。"""
    sections=[]
    for x,y,z,w in interpolate(points,4):
        sections.append([(x-w,y,z-.004),(x+w,y-.002,z+.004),
                         (x+w,y+.007,z+.004),(x-w,y+.007,z-.004)])
    rings(sections,col)

def peddler():
    global offset
    offset=V((0,.17,0))
    for s in (-1,1):
        x=-.047+s*.153;foot_y=-.16 if s==1 else .126
        cloth_shoe(x,foot_y,s*.19)
        garment([(x,foot_y+.04,.075,.051,.058,.3),(x,foot_y+.03,.19,.058,.067,.8),
                 (x,foot_y+.022,.27,.077,.073,1),(x,foot_y-.035,.42,.089,.087,.7),
                 (s*.11-.038,.033,.66,.119,.117,.8)],'pants',.012)
    # 拉长袍身，一侧承重，另一侧前跨，衣摆从腰带垂下而非水平一圈。
    torso=[(-.055,.035,.32,.228,.147,1),(-.051,.043,.37,.229,.150,.94),
             (-.052,.039,.55,.217,.147,.95),(-.054,.026,.74,.196,.142,.78),
             (-.046,.011,.92,.152,.108,.30),(-.037,-.003,.962,.143,.102,.22),
             (-.018,-.052,1.105,.179,.129,.58),(.004,-.082,1.28,.212,.138,.65),
             (.013,-.093,1.36,.217,.124,.36),(.010,-.107,1.405,.148,.104,.24),
             (.005,-.11,1.436,.076,.076,.15)]
    torso_start=len(vertices)
    garment(torso,'green',.017)
    robe_rest.update({i:vertices[i] for i in range(torso_start,len(vertices))})
    garment([(-.044,.010,.895,.160,.117,.18),(-.043,.008,.923,.158,.116,.5),
             (-.039,.008,.956,.154,.111,.28)],'leather',.004)
    ribbon([(-.137,-.080,.938,.022),(-.168,-.103,.886,.025),(-.167,-.121,.791,.030),
            (-.183,-.130,.668,.027)],'leather')
    bar((.0,-.107,1.38),(.006,-.127,1.54),.065,'skin')
    pose_range(torso_start,lean)
    neck=V((.006,-.127,1.49))
    head_start=len(vertices)
    sculpt_head()
    look=Euler((-.27,.07,.56)).to_matrix()
    pose_range(head_start,lambda p: lean(neck)+look @ (p-neck))

    # 肩窝随躯干前倾，空闲的手肘贴近腰侧。
    left=lean(V((-.176,-.085,1.345)));right=lean(V((.178,-.089,1.342)))
    folded_sleeve([(*lean(V((-.130,-.088,1.348))),.075,.082),(*left,.084,.088),
                   (-.092,-.247,1.16,.078,.084),(-.195,-.158,1.025,.066,.076),
                   (-.276,-.134,.99,.061,.068),(-.421,-.153,1.036,.051,.056),
                   (-.518,-.18,1.064,.044,.049)])
    offset+=V((-.178,.054,.024))
    orb((-.356,-.245,1.043),(.045,.046,.054),'skin')
    for j in range(4):
        z=1.013+j*.018
        pipe([(-.366,-.264,z),(-.389,-.278,z+.001),(-.402,-.27,z+.008),
              (-.405,-.254,z+.014)],[.012,.013,.011,.008],'skin',10)
    pipe([(-.331,-.261,1.069),(-.355,-.287,1.068),(-.38,-.28,1.053)],
         [.017,.014,.010],'skin')
    drum=V((-.581,-.235,.798))
    bar((-.332,-.26,1.057),drum,.015,'wood',16)
    for dy,r,c in [(0,.125,'red'),(-.021,.118,'amber'),(-.027,.109,'linen')]:
        pipe([drum+V((0,dy+.018,0)),drum+V((0,dy-.004,0))],[r,r],c,40)
    for k in range(12):
        a=tau*k/12
        orb(drum+V((.112*cos(a),-.034,.112*sin(a))),(.008,.005,.008),'gold')
    for s in (-1,1):
        p=drum+V((s*.165,-.017,-.055))
        pipe([drum+V((s*.12,0,0)),p+V((-s*.026,.01,.005)),p],[.005]*3,'linen',6)
        orb(p,(.022,.019,.022),'red')

    offset=V((0,.17,0))
    # 壶柄位于近侧轮廓上，四指绕过上梁，拇指从另一侧压住。
    folded_sleeve([(*lean(V((.136,-.091,1.350))),.075,.082),(*right,.084,.095),
                   (.434,-.364,1.170,.079,.081),(.507,-.463,1.078,.066,.071),
                   (.602,-.598,1.067,.062,.068),(.66,-.679,1.048,.051,.056)])
    offset=V((0,0,0))
    pipe([(.652,-.499,1.05),(.685,-.519,1.034),(.711,-.545,1.013)],
         [.045,.040,.034],'skin',16)
    orb((.71,-.551,1.006),(.041,.041,.025),'skin')
    for j in range(4):
        x=.677+j*.018
        pipe([(x,-.558,1.008),(x+.004,-.576,1.001),(x+.004,-.583,.983),
              (x+.002,-.573,.971),(x,-.562,.970)],[.011,.011,.010,.009,.007],'skin',10)
    pipe([(.685,-.528,1.012),(.669,-.538,.998),(.672,-.554,.984)],
         [.014,.013,.009],'skin',12)

def offered_jug():
    x,y=POT_X,POT_Y
    loft([(x,y,.681,.104,.10),(x,y,.73,.141,.13),(x,y,.84,.16,.15),
          (x,y,.91,.12,.11),(x,y,.96,.077,.07),(x,y,1.00,.082,.077)],'pot',32)
    handle=[(x+.10,y+.010,.929),(x+.120,y+.006,.961),(x+.146,y,.981),
            (x+.182,y+.003,.987),(x+.211,y+.013,.977),(x+.226,y+.022,.943),
            (x+.218,y+.023,.901),(x+.187,y+.018,.87),(x+.13,y+.01,.844)]
    curve=list(interpolate(handle,3))
    pipe(curve,[.021]*len(curve),'pot',12)
    for z,r in ((.93,.108),(.947,.091),(1.008,.083)):
        points=[(x+r*cos(k*tau/40),y+r*.92*sin(k*tau/40),z) for k in range(41)]
        pipe(points,[.005]*41,'brim',7)
    loft([(x,y,1.001,.062,.057),(x,y,1.003,.062,.057)],'dark',32)

def stall_textiles():
    # 一幅靛蓝织物从桌面顺前缘垂下，织边和流苏跟随折痕。
    def surface(u,v):
        x=-.78+.56*u
        if v<.62:
            return (x,-.07-v*.91,.695+.009*cos(u*9+v))
        drop=(v-.62)/.38
        return (x,-.638-.028*sin(pi*drop)-.026*cos(4*pi*u)*drop,
                .693-.30*drop+.036*cos(4*pi*u+.3)*drop)
    for i in range(24):
        for j in range(20):
            u=i/24;v=j/20
            q=[surface(u,v),surface((i+1)/24,v),surface((i+1)/24,(j+1)/20),surface(u,(j+1)/20)]
            start=len(vertices)
            vertices.extend(tuple((V(p)+offset)*.01) for p in q)
            face(list(reversed(range(start,start+4))),'amber' if j in (16,18) or i in (1,22) else 'blue')
    for i in range(19):
        p=V(surface((i+.5)/19,1))
        pipe([p,p+V((.006,-.004,-.037)),p+V((.009,0,-.058))],[.007,.006,.003],'linen',6)
    for x in (-.70,.70):
        for y in (-.438,-.085):
            for z in (.48,.51):
                pipe([(x-.052,y-.05,z),(x+.053,y-.05,z),(x+.053,y+.05,z),
                      (x-.052,y+.05,z),(x-.052,y-.05,z)],[.009]*5,'brim',8)
    for x in (-.56,-.25):
        for j in range(2):
            y=-.29+j*.09
            points=[(x+.13*cos(k*tau/32),y,.80+.13*sin(k*tau/32)) for k in range(33)]
            pipe(points,[.008]*33,'brim',6)
        points=[]
        for j in range(81):
            t=j/80; a=t*tau*2.7
            points.append((x+(.013+.103*t)*cos(a),-.641,.80+(.013+.103*t)*sin(a)))
        pipe(points,[.006]*len(points),'linen' if x<-.4 else 'brim',6)

def bake_peddler(obj):
    # 将棉麻、木纹、草编、皮革与接触阴影烘到同一张图，游戏只提交一个材质。
    mesh=obj.data
    mesh.materials.clear()
    rest=mesh.attributes.new('衣袍原位','FLOAT_VECTOR','POINT')
    for i,p in robe_rest.items(): rest.data[i].vector=p
    target=bpy.data.images.new('货郎摊彩绘',width=2048,height=2048,alpha=False)
    target.colorspace_settings.name='sRGB'
    for key,rgb in PALETTE.items():
        mat=bpy.data.materials.new('货郎烘焙_'+key)
        mat.use_nodes=True
        mesh.materials.append(mat)
        nodes=mat.node_tree.nodes;links=mat.node_tree.links
        nodes.clear()
        out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission')
        links.new(emit.outputs[0],out.inputs['Surface'])
        coord=nodes.new('ShaderNodeTexCoord')
        scale=nodes.new('ShaderNodeVectorMath');scale.operation='MULTIPLY'
        factors=(1300,9500,13000) if key in ('wood','board') else (7000,8500,5700)
        scale.inputs[1].default_value=factors
        links.new(coord.outputs['Object'],scale.inputs[0])
        grain=nodes.new('ShaderNodeTexNoise');grain.inputs['Scale'].default_value=1.2
        grain.inputs['Detail'].default_value=2.8
        links.new(scale.outputs[0],grain.inputs['Vector'])
        ramp=nodes.new('ShaderNodeValToRGB')
        linear=[((int(rgb[i:i+2],16)/255+.055)/1.055)**2.4 for i in (0,2,4)]
        strength=.23 if key in ('wood','board','linen','blue','leather','earth','straw','brim') else .14
        for i,stop in enumerate(ramp.color_ramp.elements):
            stop.position=.22+i*.55
            stop.color=tuple(min(1.,v*(1+(-1 if i==0 else 1)*strength)) for v in linear)+(1,)
        links.new(grain.outputs['Fac'],ramp.inputs[0])
        ao=nodes.new('ShaderNodeAmbientOcclusion');ao.inputs['Distance'].default_value=.0023;ao.samples=16
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.65
        links.new(ramp.outputs[0],mix.inputs[1]);links.new(ao.outputs['Color'],mix.inputs[2])
        if key=='green':
            weave=bpy.data.images.load(str(SOURCE/'货郎麻袍底色.png'),check_existing=True)
            photo=nodes.new('ShaderNodeTexImage');photo.image=weave;photo.extension='EXTEND'
            rest_node=nodes.new('ShaderNodeAttribute');rest_node.attribute_name='衣袍原位'
            pos=nodes.new('ShaderNodeSeparateXYZ');links.new(rest_node.outputs['Vector'],pos.inputs[0])
            uv=nodes.new('ShaderNodeCombineXYZ')
            for channel,axis,slope,bias in [('X','X',-220,.52),('Y','Z',185,-1.66)]:
                affine=nodes.new('ShaderNodeMath');affine.operation='MULTIPLY_ADD'
                affine.inputs[1].default_value=slope;affine.inputs[2].default_value=bias
                links.new(pos.outputs[axis],affine.inputs[0]);links.new(affine.outputs[0],uv.inputs[channel])
            links.new(uv.outputs[0],photo.inputs['Vector'])
            normal=nodes.new('ShaderNodeSeparateXYZ')
            links.new(nodes.new('ShaderNodeNewGeometry').outputs['Normal'],normal.inputs[0])
            front=nodes.new('ShaderNodeMapRange')
            front.inputs['From Min'].default_value=-.57;front.inputs['From Max'].default_value=-.12
            front.inputs['To Min'].default_value=1;front.inputs['To Max'].default_value=0
            links.new(normal.outputs['Y'],front.inputs['Value'])
            height=nodes.new('ShaderNodeMapRange')
            height.inputs['From Min'].default_value=.0089;height.inputs['From Max'].default_value=.0094
            links.new(pos.outputs['Z'],height.inputs['Value'])
            mask=nodes.new('ShaderNodeMath');mask.operation='MULTIPLY'
            links.new(front.outputs[0],mask.inputs[0]);links.new(height.outputs[0],mask.inputs[1])
            overlay=nodes.new('ShaderNodeMixRGB')
            links.new(mask.outputs[0],overlay.inputs[0]);links.new(ramp.outputs[0],overlay.inputs[1])
            links.new(photo.outputs[0],overlay.inputs[2]);links.new(overlay.outputs[0],mix.inputs[1])
        links.new(mix.outputs[0],emit.inputs['Color'])
        tile=nodes.new('ShaderNodeTexImage');tile.image=target;nodes.active=tile
    face_mat=bpy.data.materials.new('货郎面部底色');face_mat.use_nodes=True
    mesh.materials.append(face_mat)
    nodes=face_mat.node_tree.nodes;nodes.clear();links=face_mat.node_tree.links
    out=nodes.new('ShaderNodeOutputMaterial');emit=nodes.new('ShaderNodeEmission')
    face_image=bpy.data.images.load(str(SOURCE/'货郎面部底色.png'),check_existing=True)
    source=nodes.new('ShaderNodeTexImage');source.image=face_image;source.extension='EXTEND'
    coords=nodes.new('ShaderNodeUVMap');coords.uv_map='面部投影'
    links.new(coords.outputs[0],source.inputs[0]);links.new(source.outputs[0],emit.inputs[0])
    links.new(emit.outputs[0],out.inputs['Surface'])
    tile=nodes.new('ShaderNodeTexImage');tile.image=target;nodes.active=tile
    for poly,key in zip(mesh.polygons,colors):
        poly.material_index=list(PALETTE).index(key)
    facial=mesh.uv_layers.new(name='面部投影')
    for index in face_polygons:
        poly=mesh.polygons[index];poly.material_index=len(PALETTE)
        for loop in poly.loop_indices:
            facial.data[loop].uv=face_uv[mesh.loops[loop].vertex_index]
    mesh.uv_layers.active_index=0;mesh.uv_layers[0].active_render=True
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(72),island_margin=.0025,area_weight=.8)
    bpy.ops.object.mode_set(mode='OBJECT')
    # 头部固定分配一块独立图集区域，避免五官被自动拆为小碎岛。
    atlas=mesh.uv_layers[0]
    for uv in atlas.data: uv.uv.y*=.73
    for index in face_polygons:
        for loop in mesh.polygons[index].loop_indices:
            u,v=face_uv[mesh.loops[loop].vertex_index]
            atlas.data[loop].uv=(.025+.245*u,.748+.245*v)
    scene.render.engine='CYCLES';scene.cycles.samples=8;scene.render.bake.margin=8
    bpy.ops.object.bake(type='EMIT')
    target.filepath_raw=str(DEST/'peddler-surface.png');target.file_format='PNG';target.save();target.pack()
    mesh.uv_layers.remove(mesh.uv_layers['面部投影'])
    mesh.attributes.remove(mesh.attributes['衣袍原位'])
    paint=bpy.data.materials.new('PeddlerSurface');paint.use_nodes=True
    texture=paint.node_tree.nodes.new('ShaderNodeTexImage');texture.image=target
    paint.node_tree.links.new(texture.outputs['Color'],paint.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])
    mesh.materials.clear();mesh.materials.append(paint)
