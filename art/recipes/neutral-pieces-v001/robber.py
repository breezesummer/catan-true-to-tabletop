"""山匪群像。人物在局部厘米坐标雕刻，三人共用一张彩绘图集。"""
import numpy as np

surface_uv={}
surface_patch={}


def project_front(first_face, region, project):
    for f in range(first_face,len(faces)):
        polygon=faces[f]
        if len(polygon)!=4: continue
        pts=[V(vertices[v])*100 for v in polygon]
        direction=(pts[1]-pts[0]).cross(pts[2]-pts[0]).normalized()
        if direction.y>-.20: continue
        surface_patch[f]=region
        for index,pt in zip(polygon,pts):
            surface_uv[index]=project(pt)


def limb(stations, color, folds=0, sides=22):
    """沿关节走向构造椭圆截面，肘膝内弯处收褶，袖口另包布边。"""
    samples=list(interpolate(stations,4))
    rows=[]
    for i,(x,y,z,w,d) in enumerate(samples):
        axis=(V(samples[min(i+1,len(samples)-1)][:3])-V(samples[max(i-1,0)][:3])).normalized()
        guide=V((0,1,0)) if abs(axis.y)<.9 else V((1,0,0))
        u=axis.cross(guide).normalized()
        v=axis.cross(u)
        t=i/(len(samples)-1)
        row=[]
        for k in range(sides):
            a=tau*k/sides
            ridge=folds*(.42*sin(5*a+3*t)+exp(-((t-.52)/.24)**2)*cos(23*t+2*cos(a)))
            row.append(V((x,y,z))+(w+ridge)*cos(a)*u+(d+ridge)*sin(a)*v)
        rows.append(row)
    rings(rows,color)
    return rows


def fingers(center,axis,open_hand=False):
    """掌背、四指的指根及两节指骨围着武器，拇指从另一侧压住握柄。"""
    center=V(center); axis=V(axis).normalized()
    front=V((0,-1,0))
    side=front.cross(axis).normalized()
    palm=center+.031*side+.018*(-front)
    orb(palm,(.051,.032,.063),'skin')
    for i in range(4):
        root=center+axis*((i-1.5)*.027)
        if open_hand:
            start=palm+side*((i-1.5)*.026)
            pts=[start,start+axis*.058,start+axis*(.115-.009*abs(i-1))+front*.008]
        else:
            pts=[root+side*.062,root+side*.051+front*.030,
                 root+side*.019+front*.047,root-side*.016+front*.035]
        pipe(pts,[.015,.014,.010] if open_hand else [.016,.015,.012,.009],'skin',9)
    pipe([palm-axis*.032,center-side*.034+axis*.038,
          center-side*.024+front*.043+axis*.025],[.023,.020,.013],'skin',10)


def bandit_head(role):
    wide={'blade':.119,'bow':.110,'club':.141}[role]
    height={'blade':.326,'bow':.297,'club':.303}[role]
    rows=[]
    for j in range(33):
        t=j/32
        width=wide*(.34+.67*sin(pi*(.02+.95*t))**.56)
        if role=='club': width+=.014*exp(-((t-.23)/.16)**2)
        row=[]
        for k in range(64):
            a=tau*k/64; x=width*cos(a); front=max(0,-sin(a))
            def bump(cx,ct,rx,rt):
                return exp(-((x-cx)/rx)**2-((t-ct)/rt)**2)
            relief=.047*bump(0,.49,.020,.16)+.043*bump(0,.39,.029,.047)
            relief+=.018*bump(0,.22,.045,.05)+.020*bump(0,.10,.060,.068)
            for s in (-1,1):
                relief+=.022*bump(s*.057,.62,.041,.036)-.017*bump(s*.050,.54,.031,.052)
                relief+=.025*bump(s*.074,.43,.039,.073)
            y=-.045+.113*sin(a)-front**2*relief
            row.append((x,y,1.45+height*t))
        rows.append(row)
    first=len(faces)
    rings(rows,'skin')
    project_front(first,'face',lambda p:(.5+.44*p.x/wide,.015+.97*(p.z-1.45)/height))
    for s in (-1,1):
        orb((s*wide,-.042,1.45+.46*height),(.021,.025,.039),'skin')
    if role=='bow':
        # 兜帽用开口的双层曲面，帽沿绕过脸颊，面前留空。
        for side in (-1,1):
            sections=[]
            for j in range(15):
                t=j/14; x=side*.146*(1-t**2)
                sections.append([(x,-.15+.047*t,1.48+.324*t),(x+side*.015,-.133,1.48+.322*t),
                                 (x+side*.028,.101,1.42+.36*t),(x*.9,.141,1.42+.34*t)])
            rings(sections,'green')
        loft([(0,.062,1.43,.133,.089),(0,.058,1.64,.147,.099),
              (0,.054,1.75,.111,.097),(0,.032,1.80,.023,.030)],'green',32)
        # 蒙面布跨过下颌，布边在鼻翼下方收拢。
        scarf=[]
        for j in range(6):
            t=j/5;row=[]
            for k in range(40):
                a=tau*k/40;front=max(0,-sin(a))
                row.append(((.109+.013*t)*cos(a),-.050+(.130+.012*t)*sin(a),
                            1.46+.105*t+.021*front*t+.004*cos(4*a+3*t)))
            scarf.append(row)
        rings(scarf,'vest')
    else:
        loft([(0,-.044,1.45+height*.69,wide*1.09,.130),(0,-.044,1.45+height*.84,wide*1.08,.139),
              (0,-.044,1.45+height,.101,.112),(.009,-.029,1.477+height,.045,.055)],'hair',32)
        for k in range(9):
            a=pi+(k+.35)*pi/9
            pts=[]
            for j in range(7):
                t=j/6; ang=a+.32*t
                pts.append(((wide*1.09-.047*t*t)*cos(ang),-.044+(.139-.046*t*t)*sin(ang),
                            1.45+height*(.80+.24*t)))
            pipe(pts,[.003]*6+[.001],'dark',6)
        for j in range(3):
            section=[]
            for rise in (0,.014):
                section.append([(1.09*wide*cos(a),-.044+.130*sin(a),1.45+height*.70+
                                j*.012+rise+.019*cos(a-.6)+.004*sin(3*a)) for a in [tau*i/32 for i in range(32)]])
            rings(section,'red' if role=='blade' else 'vest')
        if role=='blade':
            orb((.027,.084,1.45+height),(.057,.059,.049),'hair')
            ribbon([(.09,.069,1.69,.025),(.15,.102,1.59,.030),(.18,.074,1.47,.014)],'red')
        else:
            beard=[]
            for j in range(8):
                t=j/7;row=[]
                for k in range(28):
                    a=-pi+.14+(pi-.28)*k/27;front=-sin(a)
                    row.append((wide*(.59+.37*t)*cos(a),-.049+(.103+.020*t)*sin(a),
                                1.45+.103*t+.024*(1-front)-.026*(1-t)*front))
                beard.append(row+[(x*.97,y+.008,z+.003) for x,y,z in reversed(row)])
            rings(beard,'hair')
            for s in (-1,1):
                for j in range(5):
                    pipe([(s*(.116-.01*j),-.100-.008*j,1.55-.006*j),
                          (s*(.095-.01*j),-.13-.004*j,1.50),
                          (s*(.057-.009*j),-.126-.004*j,1.458-.006*j)],
                         [.011,.013,.003],'hair',8)


def bandit(role, origin, yaw):
    global offset
    offset=V((0,0,0)); start=len(vertices)
    coat={'blade':'vest','bow':'green','club':'blue'}[role]
    feet={'blade':[(-.23,-.11,0),(.17,.10,0)],'bow':[(-.23,-.10,0),(.16,.13,0)],
          'club':[(-.26,.11,0),(.25,-.12,0)]}[role]
    for i,(x,y,z) in enumerate(feet):
        s=2*i-1
        offset.z=z; cloth_shoe(x,y,s*.19); offset.z=0
        for j in range(3):
            pipe([(x-.044,y-.047+j*.022,z+.064),(x,y-.040+j*.022,z+.080),
                  (x+.044,y-.030+j*.022,z+.067)],[.004,.005,.004],'leather',7)
        limb([(s*.119,.02,.82,.105,.116),(s*.16,-.04,.62,.100,.107),
              (x,-.11+y,.42,.085,.089),(x,.029+y,.22,.068,.069),
              (x,.033+y,.071+z,.054,.055)],'pants',.012)
        for j in range(3):
            loft([(x,.026+y,.12+z+j*.035,.068,.069),
                  (x,.026+y,.144+z+j*.035,.069,.070)],'leather',18)
    first=len(faces)
    garment([(0,.02,.55,.257,.157,1),(.012,.018,.67,.251,.165,.88),
             (.006,.01,.86,.185,.127,.35),(0,-.013,.98,.20,.142,.82),
             (.015,-.020,1.21,.235,.157,.44),(.018,-.018,1.32,.232,.126,.23),
             (.006,-.020,1.395,.096,.086,.18)],coat,.035)
    project_front(first,coat,lambda p:(.5-p.x/.56,(p.z-.52)/.89))
    bar((0,-.027,1.33),(0,-.045,1.53),.068,'skin',20)
    ribbon([(-.087,-.093,1.388,.028),(-.040,-.16,1.32,.030),(.088,-.167,1.17,.030)],'linen')
    ribbon([(.083,-.10,1.386,.028),(.039,-.171,1.286,.033),(-.075,-.173,1.15,.033),
            (-.166,-.114,.932,.026)],'leather')
    loft([(0,.002,.854,.199,.149),(0,.002,.88,.203,.154),(0,.002,.932,.20,.152)],'leather',40)
    plank(.005,-.153,.870,.069,.035,.050,'gold')
    # 斜排的护肩甲片与下缘缝线只给头目，弓手和棍手留布轮廓。
    if role=='blade':
        # 单肩旧披风从颈根沿外肩垂到背后，从侧面也有连续轮廓。
        sections=[]
        for j in range(20):
            t=j/19;row=[]
            for k in range(24):
                a=-1.45+2.89*k/23
                reach=.105+.18*t+.031*sin(pi*t)
                row.append((-.035-reach*cos(a),.005+(.10+.088*t)*sin(a),
                            1.414-t*(.39+.12*cos(a-1.1))+.019*t*sin(7*a+3*t)))
            sections.append(row+[(x+.006,y,z+.004) for x,y,z in reversed(row)])
        rings(sections,'vest')
        # 系在颈侧的绛色围巾沿肩披下，露出交领和护肩。
        loft([(0,-.029,1.345,.109,.102),(0,-.033,1.38,.119,.107),
              (.008,-.033,1.421,.102,.096),(.008,-.031,1.438,.087,.083)],'red',36)
        ribbon([(.073,-.112,1.417,.030),(.117,-.119,1.343,.037),
                (.134,-.158,1.217,.035),(.171,-.133,1.143,.023)],'red')
        for s in (-1,1):
            for j in range(3):
                x=s*(.166+j*.026)
                ribbon([(x,-.105,1.333-j*.02,.045),(x+s*.022,-.132,1.28-j*.025,.048),
                        (x+s*.028,-.107,1.253-j*.023,.038)],'steel')
                orb((x,-.140,1.291-j*.025),(.009,.005,.010),'gold')
    bandit_head(role)
    if role=='blade':
        hands=[(-.245,-.286,1.21),(.34,-.21,1.46)]
        axes=[V((.90,-.11,.54)).normalized(),V((.15,0,1)).normalized()]
        elbows=[(-.37,-.07,1.09),(.39,-.05,1.19)]
    elif role=='bow':
        hands=[(-.49,-.245,1.48),(.119,-.201,1.555)]
        axes=[V((.12,0,1)).normalized(),V((.12,0,1)).normalized()]
        elbows=[(-.348,-.155,1.38),(.32,-.155,1.54)]
    else:
        hands=[(-.16,-.264,1.018),(.14,-.28,1.266)]
        axes=[(V(hands[1])-V(hands[0])).normalized()]*2
        elbows=[(-.30,-.08,1.096),(.345,-.11,1.12)]
    for i,(h,e,ax) in enumerate(zip(hands,elbows,axes)):
        s=2*i-1; h=V(h); e=V(e); shoulder=V((s*.192,-.018,1.30))
        cuff=e.lerp(h,.72)
        rows=limb([(*shoulder,.088,.102),(*shoulder.lerp(e,.37),.107,.105),
                   (*e,.081,.088),(*e.lerp(cuff,.46),.096,.094),(*cuff,.064,.067)],coat,.013)
        rings(rows[-3:],'leather')
        limb([(*cuff,.061,.057),(*cuff.lerp(h,.55),.052,.047),(*h,.033,.036)],'skin')
        if role=='bow' and i==0 or role=='club':
            bracer=limb([(*cuff,.064,.060),(*cuff.lerp(h,.30),.061,.058),
                         (*cuff.lerp(h,.67),.049,.048)],'leather',0,16)
            rings(bracer[:2],'amber');rings(bracer[-2:],'amber')
        fingers(h,ax,role=='blade' and i==1)
    h=V(hands[0]); axis=axes[0]
    if role=='blade':
        bar(h-axis*.099,h+axis*.118,.027,'leather')
        guard=h+axis*.107; across=V((axis.z,0,-axis.x))
        bar(guard-across*.082,guard+across*.083,.019,'gold')
        rows=[]
        for reach,w,curve in [(0,.048,0),(.11,.053,0),(.40,.054,.014),(.53,.043,.044),(.62,.002,.087)]:
            p=guard+axis*reach+across*curve
            rows.append([p-across*w,p+V((0,-.015,0)),p+across*w*.8,p+V((0,.012,0))])
        rings(rows,'steel',False)
        for a,b in zip(rows,rows[1:]):
            rings([[a[0],a[0].lerp(a[1],.16),a[0].lerp(a[3],.16)],
                   [b[0],b[0].lerp(b[1],.16),b[0].lerp(b[3],.16)]],'edge',False)
    elif role=='bow':
        tips=[h+V((.12,0,-.442)),h+V((.06,0,.405))]
        pipe([tips[0],h+V((-.018,0,-.27)),h,h+V((-.04,0,.226)),tips[1]],
             [.012,.024,.023,.021,.010],'wood')
        draw=V(hands[1]);pipe([tips[0],draw,tips[1]],[.004]*3,'linen',6)
        end=h+(h-draw).normalized()*.20
        bar(draw,end,.008,'wood',8)
        pipe([end,end+(end-draw).normalized()*.066],[.022,.001],'steel',8)
        # 箭羽位于拉弦手前，箭囊和备用箭在背后也能辨认。
        bar(draw,draw+V((-.094,0,.021)),.012,'linen',6)
        limb([(.115,.129,.76,.072,.070),(.18,.12,1.05,.073,.071),(.26,.114,1.37,.071,.080)],'leather')
        for j in range(3):
            a=V((.218+j*.031,.12,1.31)); b=a+V((.06,0,.23+.035*j))
            bar(a,b,.007,'wood',7);pipe([b-V((.014,0,.04)),b],[.017,.008],'linen',7)
    else:
        b=V(hands[1])
        pipe([h-axis*.115,h,b,b+axis*.13,b+axis*.36,b+axis*.42],
             [.036,.034,.039,.061,.083,.062],'wood',15)
        u=V((0,-1,0));v=axis.cross(u).normalized()
        for j in range(9):
            a=tau*j/9;points=[]
            for dist,r in [(.055,.045),(.14,.061),(.24,.073),(.35,.083),(.417,.064)]:
                angle=a+.09*sin(13*dist+j)
                points.append(b+axis*dist+(u*cos(angle)+v*sin(angle))*r)
            pipe(points,[.002,.003,.004,.003,.001],'amber' if j%3 else 'leather',6)
        for t in (.17,.34):
            p=b+axis*t; r=.064 if t<.2 else .079
            pipe([p,p+axis*.022],[r+.002,r+.004],'dark',16)
            for j in range(4):
                radial=u*cos(j*pi/2)+v*sin(j*pi/2)
                orb(p+axis*.011+radial*(r+.002),(.007,.007,.007),'steel')
    # 两前排人物各向一侧扭身，棍手屈膝压低，整体低于后排。
    for i in range(start,len(vertices)):
        x,y,z=(v*100 for v in vertices[i])
        if role=='blade': z*=1.10
        if role=='bow': z-=.085*min(1,z/.76)
        if role=='club':
            z-=.16*min(1,z/.83); y-=.096*max(0,z-.67)
        vertices[i]=tuple(.01*v for v in (origin[0]+x*cos(yaw)-y*sin(yaw),
                          origin[1]+x*sin(yaw)+y*cos(yaw),origin[2]+z))


def atlas_material(mesh):
    """色块各有自身的织纹、木纹、磨损和光泽；连续坐标投射到岛内避免逐面重复。"""
    keys=list(PALETTE); tile=256; width=tile*8; height=width
    paint=np.ones((height,width,4),dtype=np.float32)
    spec=np.zeros_like(paint); y,x=np.mgrid[0:tile,0:tile]; rng=np.random.default_rng(913)
    for i,key in enumerate(keys):
        color=np.array([int(PALETTE[key][j:j+2],16)/255 for j in (0,2,4)])
        variation=.025*rng.normal(size=x.shape)+.024*np.sin((x+2*y)/29)
        metal=0.; gloss=.19
        if key in ('blue','green','linen','vest','pants','red'):
            variation+=.045*np.cos(x*1.6)*np.sin(y*1.38)+.018*np.cos(y*.55)
        elif key=='wood':
            variation+=.14*np.sin(x*.31+1.6*np.sin(y*.032))+.08*np.cos(x*.14+y*.017);gloss=.34
        elif key in ('steel','edge','gold'):
            variation+=.06*np.sin(x*.17+y*.04)+.05*np.sin(y*1.12); metal=.78;gloss=.72
        elif key=='leather':
            variation+=.043*np.sin(x*.18+y*.19)*np.cos(y*.27);gloss=.39
        elif key=='skin': gloss=.29
        area=np.s_[(i//8)*tile:(i//8+1)*tile,(i%8)*tile:(i%8+1)*tile]
        paint[area][:,:,:3]=np.clip(color*(1+variation[:,:,None]),0,1)
        spec[area][:,:,0]=metal;spec[area][:,:,3]=np.clip(gloss+.12*variation,0,1)
    patches={'face':0,'green':1,'blue':2,'vest':3}
    for region,index in patches.items():
        photo=bpy.data.images.load(str(SOURCE/('货郎面部底色.png' if region=='face' else '货郎麻袍底色.png')),check_existing=True)
        data=np.empty(len(photo.pixels),np.float32);photo.pixels.foreach_get(data)
        data=data.reshape(photo.size[1],photo.size[0],4)
        yi=np.linspace(0,photo.size[1]-1,512).astype(int)
        xi=np.linspace(0,photo.size[0]-1,512).astype(int)
        sample=data[yi[:,None],xi[None,:],:3]
        # 文件纹理以场景线性值读取，字节图集按 sRGB 编码。
        sample=np.clip(sample,0,1)
        if region!='face':
            luma=sample@np.array([.2126,.7152,.0722])
            pigment=np.array([int(PALETTE[region][j:j+2],16)/255 for j in (0,2,4)])
            sample=np.clip((.15+luma[:,:,None]*2.0)*pigment,0,1)
        area=np.s_[1024:1536,index*512:(index+1)*512]
        paint[area][:,:,:3]=sample;spec[area][:,:,3]=.28 if region=='face' else .15
    def write(name,pixels,mask):
        img=bpy.data.images.new(name,width=width,height=height,alpha=True)
        img.colorspace_settings.name='Non-Color' if mask else 'sRGB'
        img.alpha_mode='CHANNEL_PACKED'
        img.pixels.foreach_set(pixels.flatten());img.filepath_raw=str(DEST/(name+'.png'))
        img.file_format='PNG';img.save();img.pack()
        return img
    albedo=write('robber-surface',paint,False); mask=write('robber-metal',spec,True)
    # 布纹和木纹的微表面斜率随经纬方向变化。钢刃保留雕出的平面。
    luma=paint[:,:,:3]@np.array([.2126,.7152,.0722],dtype=np.float32)
    dy,dx=np.gradient(luma)
    normal=np.ones_like(paint);normal[:,:,0]=-1.7*dx;normal[:,:,1]=-1.7*dy
    normal[:,:,:3]/=np.linalg.norm(normal[:,:,:3],axis=2,keepdims=True)
    normal[:,:,:3]=.5+.5*normal[:,:,:3]
    relief=write('robber-normal',normal,True)
    mat=bpy.data.materials.new('RobberSurface');mat.use_nodes=True
    nodes=mat.node_tree.nodes;link=mat.node_tree.links.new;bsdf=nodes.get('Principled BSDF')
    bump=nodes.new('ShaderNodeNormalMap');tex=nodes.new('ShaderNodeTexImage');tex.image=relief
    link(tex.outputs['Color'],bump.inputs['Color']);link(bump.outputs[0],bsdf.inputs['Normal'])
    for img in (albedo,mask):
        tex=nodes.new('ShaderNodeTexImage');tex.image=img
        if img==albedo: link(tex.outputs['Color'],bsdf.inputs['Base Color'])
        else:
            split=nodes.new('ShaderNodeSeparateColor');link(tex.outputs['Color'],split.inputs[0])
            link(split.outputs['Red'],bsdf.inputs['Metallic'])
            inv=nodes.new('ShaderNodeMath');inv.operation='SUBTRACT';inv.inputs[0].default_value=1
            link(tex.outputs['Alpha'],inv.inputs[1]);link(inv.outputs[0],bsdf.inputs['Roughness'])
    mesh.materials.append(mat); uv=mesh.uv_layers.new(name='山匪彩绘')
    for poly,key,smooth in zip(mesh.polygons,colors,shades):
        poly.use_smooth=smooth;idx=keys.index(key)
        for loop in poly.loop_indices:
            px,py,pz=mesh.vertices[mesh.loops[loop].vertex_index].co
            u=.50+27*px+6*py; v=.09+36*pz+5*py
            if poly.index in surface_patch:
                patch=patches[surface_patch[poly.index]]
                u,v=surface_uv[mesh.loops[loop].vertex_index]
                uv.data[loop].uv=((patch*512+3+506*min(1,max(0,u)))/width,
                                  (1027+506*min(1,max(0,v)))/height)
            else:
                uv.data[loop].uv=((idx%8+u)/8,(idx//8+v)/8)


def robber():
    global offset
    offset=V((0,0,0))
    # 六只布鞋分别着地。刀手直立，弓手压膝，棍手低身迎击。
    for role,origin,yaw in [('blade',(0,.22,0),-.13),('bow',(-.47,-.175,0),-.20),('club',(.52,-.23,0),.17)]:
        first=len(vertices)
        bandit(role,origin,yaw)
        feet=[p for p in vertices[first:] if abs(p[2])<1e-7]
        assert any(p[0]<origin[0]*.01 for p in feet) and any(p[0]>origin[0]*.01 for p in feet)
    # 统一到既有棋子高度，原点仍为底面中心。
    factor=.02036/max(p[2] for p in vertices)
    mesh=bpy.data.meshes.new('山匪三人');mesh.from_pydata([tuple(v*factor for v in p) for p in vertices],[],faces);mesh.update()
    atlas_material(mesh)
    obj=bpy.data.objects.new('robber-band',mesh);scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.fbx(filepath=str(DEST/'robber-band.fbx'),use_selection=True,object_types={'MESH'},
                             axis_forward='-Z',axis_up='Y',bake_space_transform=True,apply_scale_options='FBX_SCALE_ALL',
                             bake_anim=False,add_leaf_bones=False)
    bpy.data.libraries.write(str(SOURCE/'山匪三人.blend'),{scene},compress=True)
    mesh_objects.append(obj)
    for arr in (vertices,faces,shades,colors): arr.clear()
