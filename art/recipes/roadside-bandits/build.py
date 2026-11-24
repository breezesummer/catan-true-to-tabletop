"""通过 Blender MCP 在独立后台场景中构造分件源模型、裁片图集和单材质游戏网格。"""
from pathlib import Path
from tempfile import TemporaryDirectory
import sys
import shutil
import bpy
import bmesh
import numpy as np
from mathutils import Vector,Matrix

HERE=Path(__file__).resolve().parent
sys.path.insert(0,str(HERE))
# 可从独立 MCP 会话反复执行配方，载入本次修改的几何与裁片。
for module in ("figures","tailoring","portraits","head_form","portrait_finish","finishes","surface_study","hand_mesh","gestures","sculpt"):
    sys.modules.pop(module,None)
from figures import assemble
from finishes import ROOT,ART,SOURCE,Layout,MATERIALS,NAP,bake,ground_contact
from surface_study import textile_coordinates,fibre_detail
from sculpt import V
from hand_mesh import hand_uvs,template
from head_form import head_template


def material(images):
    mat=bpy.data.materials.new("RobberSurface")
    mat.use_nodes=True
    nodes=mat.node_tree.nodes;connect=mat.node_tree.links.new
    bsdf=nodes.get("Principled BSDF")
    bsdf.location=(300,90)
    def sampler(name,label,x,y):
        image=bpy.data.images.load(str(images/("robber-"+name+".png")))
        image.colorspace_settings.name="sRGB" if name=="surface" else "Non-Color"
        image.alpha_mode="CHANNEL_PACKED"
        image.pack()
        image.filepath="//../../../Unity/Catan/Assets/Resources/NeutralPieces/v001/"+image.name
        tex=nodes.new("ShaderNodeTexImage");tex.label=label;tex.image=image;tex.location=(x,y)
        return tex
    color=sampler("surface","染色、皮肤与材料本色",-650,390)
    norm=sampler("normal","编织、毛孔与皮革粒面",-650,70)
    metal=sampler("metal","金属度、遮蔽、织物绒面、光滑度",-650,-230)
    connect(color.outputs["Color"],bsdf.inputs["Base Color"])
    bump=nodes.new("ShaderNodeNormalMap");bump.location=(-200,90)
    connect(norm.outputs["Color"],bump.inputs["Color"]);connect(bump.outputs[0],bsdf.inputs["Normal"])
    channel=nodes.new("ShaderNodeSeparateColor");channel.location=(-215,-115)
    connect(metal.outputs[0],channel.inputs[0]);connect(channel.outputs["Red"],bsdf.inputs["Metallic"])
    invert=nodes.new("ShaderNodeMath");invert.operation="SUBTRACT";invert.inputs[0].default_value=1
    invert.location=(-205,-290)
    connect(metal.outputs["Alpha"],invert.inputs[1]);connect(invert.outputs[0],bsdf.inputs["Roughness"])
    nap=nodes.new("ShaderNodeMath");nap.operation="MULTIPLY";nap.inputs[1].default_value=.18
    nap.location=(0,-280);connect(channel.outputs["Blue"],nap.inputs[0])
    connect(nap.outputs[0],bsdf.inputs["Sheen Weight"]);bsdf.inputs["Sheen Roughness"].default_value=.90
    connect(color.outputs["Color"],bsdf.inputs["Sheen Tint"])
    spec=nodes.new("ShaderNodeMapRange");spec.location=(0,-480)
    spec.inputs["To Min"].default_value=.50;spec.inputs["To Max"].default_value=.12
    connect(channel.outputs["Blue"],spec.inputs[0]);connect(spec.outputs[0],bsdf.inputs["Specular IOR Level"])
    fibre=sampler("fibres","黄麻、亚麻、呢绒和圈绒的细纤维",-1200,-640)
    uv=nodes.new("ShaderNodeUVMap");uv.uv_map="纱线尺度";uv.location=(-1410,-640)
    connect(uv.outputs[0],fibre.inputs["Vector"])
    separate=nodes.new("ShaderNodeSeparateColor");separate.location=(-950,-610)
    connect(fibre.outputs["Color"],separate.inputs[0])
    def calculate(op,a,b,x,y):
        node=nodes.new("ShaderNodeMath");node.operation=op;node.location=(x,y)
        for sock,val in zip(node.inputs,(a,b)):
            if isinstance(val,float):sock.default_value=val
            else:connect(val,sock)
        return node.outputs[0]
    mask=channel.outputs["Blue"]
    grain=separate.outputs["Red"]
    for index,(edge,signal) in enumerate(((.40,separate.outputs["Green"]),(.63,separate.outputs["Blue"]),(.89,fibre.outputs["Alpha"]))):
        choose=calculate("GREATER_THAN",mask,edge,-750+index*175,-540)
        mix=nodes.new("ShaderNodeMixRGB");mix.location=(-750+index*175,-750)
        connect(choose,mix.inputs[0]);connect(grain,mix.inputs[1]);connect(signal,mix.inputs[2])
        grain=mix.outputs[0]
    signal=calculate("SUBTRACT",grain,.5,-165,-660)
    signal=calculate("MULTIPLY",signal,calculate("GREATER_THAN",mask,.1,-170,-875),20,-660)
    tint=calculate("ADD",calculate("MULTIPLY",signal,.83,195,-650),1.0,355,-650)
    dye=nodes.new("ShaderNodeVectorMath");dye.operation="SCALE";dye.location=(530,-380)
    connect(color.outputs["Color"],dye.inputs[0]);connect(tint,dye.inputs["Scale"])
    connect(dye.outputs[0],bsdf.inputs["Base Color"])
    micro=nodes.new("ShaderNodeBump");micro.location=(540,-650)
    micro.inputs["Strength"].default_value=.25;micro.inputs["Distance"].default_value=.000014
    connect(signal,micro.inputs["Height"]);connect(bump.outputs[0],micro.inputs["Normal"])
    connect(micro.outputs[0],bsdf.inputs["Normal"])
    return mat


def orientation(ch):
    ar=np.asarray(ch.rows,dtype=float)
    if ch.wrap:
        du=np.roll(ar,-1,axis=1)-ar
        radial=ar-ar.mean(axis=1,keepdims=True)
        dv=np.gradient(ar,axis=0)
    else:
        du=np.gradient(ar,axis=1);dv=np.gradient(ar,axis=0)
        centre={"leader":(395,0),"carrier":(273,62),"scout":(497,68)}[ch.part.split(" ",1)[0]]
        radial=ar-np.array([*centre,0])
        radial[:,:,2]=0
    score=np.sum(np.cross(du,dv)*radial)
    if ch.material=="lining":
        score=-score
    return score<0


def source_part(group,mat,layout,scale,center,scene):
    verts=[];faces=[];uvs=[];shading=[];fibre_uv=[]
    for index,ch in group:
        start=len(verts);m=len(ch.rows);n=len(ch.rows[0])
        count=n if ch.wrap else n-1
        key=index if ch.density else ch.material
        backwards=orientation(ch)
        kind=MATERIALS[ch.material][1]
        if kind in NAP:
            grid=np.asarray(ch.rows)
            if ch.wrap:grid=np.concatenate((grid,grid[:,:1]),axis=1)
            u,v=textile_coordinates(grid,count+1,m)
            repeat={"twill":58,"bast":73,"cord":54,"wool":94,"fulled":96,"boucle":82}[kind]
            tex=np.stack((u,-v),axis=-1)/repeat
            quads=np.stack((tex[:-1,:-1],tex[:-1,1:],tex[1:,1:],tex[1:,:-1]),axis=2)
            if backwards:quads=quads[:,:,::-1,:]
            fibre_uv.append((len(uvs),quads.reshape(-1,2)))
        for row in ch.rows:
            verts.extend(((p.x-center.x)*scale,(p.y-center.y)*scale,max(0,p.z)*scale) for p in row)
        if ch.pigment_uv is None:
            coord=[[layout.uv(key,k/count,j/(m-1)) for k in range(count+1)] for j in range(m)]
        else:
            u,v=np.moveaxis(np.clip(ch.pigment_uv,0,1),-1,0)
            coord=np.stack(layout.uv(ch.paint,u,v),axis=-1)
        for j in range(m-1):
            for k in range(count):
                a=start+j*n+k;b=start+j*n+(k+1)%n
                face=(a,b,b+n,a+n)
                coords=(coord[j][k],coord[j][k+1],coord[j+1][k+1],coord[j+1][k])
                faces.append(face[::-1] if backwards else face)
                uvs.extend(coords[::-1] if backwards else coords)
                shading.append(ch.smooth)
        if ch.caps:
            from math import sin,cos,tau
            # 封口使用同种材料的小裁片，不挤占肖像 UV。
            circle=[layout.uv(ch.material,.5+.48*sin(tau*k/n),.5+.48*cos(tau*k/n)) for k in range(n)]
            for j in ((0,) if ch.hand is not None else (0,m-1)):
                face=list(range(start+j*n,start+(j+1)*n))
                reverse=backwards!=(j==0)
                cap_uv=coord[j][:n] if ch.material=="eye" else circle
                faces.append(face[::-1] if reverse else face);uvs.extend(cap_uv[::-1] if reverse else cap_uv)
                shading.append(False)
        if ch.hand is not None:
            hand=ch.hand
            indices=np.full(len(hand.vertices),-1,dtype=int)
            indices[hand.wrist]=start+(m-1)*n+np.arange(n)
            other=indices<0
            indices[other]=len(verts)+np.arange(np.sum(other))
            verts.extend((hand.vertices[other]-np.asarray(center))*scale)
            faces.extend(indices[hand.faces].tolist())
            paint,facing=hand_uvs()
            palm_uv=np.array([layout.uv("掌面",*point) for point in paint["掌面"]])
            back_uv=np.array([layout.uv("手背",*point) for point in paint["手背"]])
            loop_uv=np.where(facing[:,None,None],palm_uv[hand.faces],back_uv[hand.faces])
            uvs.extend(loop_uv.reshape(-1,2))
            shading.extend([True]*len(hand.faces))
    prefix,part=group[0][1].part.split(" · ",1)
    name={"carrier":"负袋壮汉","leader":"兜帽首领","scout":"望风斥候"}[prefix]+" · "+part
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces)
    mesh.materials.append(mat)
    layer=mesh.uv_layers.new(name="独立裁片与纹样")
    layer.data.foreach_set("uv",np.array(uvs,dtype=np.float32).ravel())
    detail=np.zeros((len(uvs),2),dtype=np.float32)
    for start,values in fibre_uv:detail[start:start+len(values)]=values
    mesh.uv_layers.new(name="纱线尺度").data.foreach_set("uv",detail.ravel())
    mesh.uv_layers.active_index=0;mesh.uv_layers[0].active_render=True
    mesh.polygons.foreach_set("use_smooth",np.array(shading,dtype=bool))
    mesh.update()
    obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
    if any(ch.hand is not None for _,ch in group):
        weights=template()["weights"][:,5:].reshape(-1,5,3).sum(axis=2)
        for number,label in enumerate(("拇指","食指","中指","无名指","小指")):
            digit=obj.vertex_groups.new(name=label)
            for v in np.flatnonzero(weights[:,number]>.01):
                digit.add([int(indices[v])],float(weights[v,number]),"REPLACE")
        subdiv=obj.modifiers.new("掌指细分","SUBSURF")
        subdiv.levels=subdiv.render_levels=1
        subdiv.uv_smooth="PRESERVE_BOUNDARIES"
    return obj


def source_head(role,xyz,mat,layout,scale,center,scene):
    data=head_template()
    label={"carrier":"负袋壮汉","leader":"兜帽首领","scout":"望风斥候"}[role]+" · 连续头颈"
    mesh=bpy.data.meshes.new(label)
    mesh.from_pydata(((xyz-np.asarray(center))*scale).tolist(),[],data["faces"].tolist())
    mesh.materials.append(mat)
    u,v=np.moveaxis(data["uv"],-1,0)
    packed=np.stack(layout.uv("head-"+role,u,v),axis=-1).astype(np.float32)
    mesh.uv_layers.new(name="独立裁片与纹样").data.foreach_set("uv",packed.ravel())
    mesh.uv_layers.new(name="纱线尺度").data.foreach_set("uv",np.zeros_like(packed).ravel())
    mesh.uv_layers.active_index=0;mesh.uv_layers[0].active_render=True
    mesh.polygons.foreach_set("use_smooth",np.ones(len(mesh.polygons),dtype=bool))
    mesh.update()
    obj=bpy.data.objects.new(label,mesh);scene.collection.objects.link(obj)
    subdiv=obj.modifiers.new("眼睑、唇缘与耳轮细分","SUBSURF")
    subdiv.levels=subdiv.render_levels=1
    subdiv.uv_smooth="PRESERVE_BOUNDARIES"
    return obj


def build():
    if not bpy.app.background:raise RuntimeError("须在独立 Blender MCP 后台会话执行")
    scene=bpy.data.scenes.new("路上强盗团")
    bpy.context.window.scene=scene
    for other in list(bpy.data.scenes):
        if other!=scene:bpy.data.scenes.remove(other)
    bpy.data.orphans_purge(do_recursive=True)
    scene.unit_settings.system="METRIC";scene.unit_settings.length_unit="MILLIMETERS"
    figure=assemble()
    charts=figure.charts
    groups={}
    for i,ch in enumerate(charts):groups.setdefault(ch.part,[]).append((i,ch))
    xyz=np.concatenate([np.asarray(ch.rows).reshape(-1,3) for ch in charts]+[pts for _,pts in figure.heads])
    low=xyz.min(axis=0);high=xyz.max(axis=0)
    assert low[2]>-.82,"存在穿过支承平面的部件"
    centre=V((*((high+low)[:2]*.5),0));scale=.02036/high[2]
    scene["原型比例"]=float(scale);scene["原型中心"]=list(centre)
    layout=Layout(charts)
    ART.mkdir(parents=True,exist_ok=True);SOURCE.parent.mkdir(parents=True,exist_ok=True)
    (ROOT/".local").mkdir(exist_ok=True)
    with TemporaryDirectory(dir=ROOT/".local") as folder:
        stage=Path(folder)
        bake(charts,layout,stage,figure.heads)
        fibre_detail(SOURCE.parent/"织物与皮革母图.png",stage/"robber-fibres.png")
        mat=material(stage)
        parts=[source_part(g,mat,layout,scale,centre,scene) for g in groups.values()]
        parts.extend(source_head(role,pts,mat,layout,scale,centre,scene) for role,pts in figure.heads)
        ground_contact(parts,stage/"robber-contact.png")
        for screen in bpy.data.screens:
            for area in screen.areas:
                if area.type=="VIEW_3D":
                    sp=area.spaces.active;sp.clip_start=.00004;sp.clip_end=2
                    sp.region_3d.view_distance=.046;sp.region_3d.view_location=(0,0,.0100)
                    sp.region_3d.view_perspective="ORTHO"
                    sp.region_3d.view_rotation=Vector((0,.97,-.225)).to_track_quat("-Z","Y")
                    sp.overlay.show_floor=sp.overlay.show_cursor=False
                    sp.overlay.show_axis_x=sp.overlay.show_axis_y=False;sp.shading.type="MATERIAL"
        bpy.context.preferences.filepaths.save_version=0
        bpy.ops.wm.save_as_mainfile(filepath=str(stage/SOURCE.name),compress=True,relative_remap=False)
        for obj in parts:
            if obj.modifiers:
                bpy.context.view_layer.objects.active=obj
                for mod in list(obj.modifiers):bpy.ops.object.modifier_apply(modifier=mod.name)
                obj.vertex_groups.clear()
            obj.select_set(True)
        bpy.context.view_layer.objects.active=parts[0]
        bpy.ops.object.join();obj=bpy.context.object;obj.name=obj.data.name="robber-band"
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.triangulate(bm,faces=list(bm.faces))
        tiny=[f for f in bm.faces if f.calc_area()<6e-17]
        if tiny:bmesh.ops.delete(bm,geom=tiny,context="FACES")
        bm.to_mesh(obj.data);bm.free();obj.data.update()
        # 在雕塑坐标中做误差比较，避免毫米尺度下的小发束或指尖被误判为重合。
        obj.data.transform(Matrix.Scale(1/scale,4))
        reducer=obj.modifiers.new("棋盘近景网格","DECIMATE")
        reducer.ratio=min(1,142000/max(1,len(obj.data.polygons)))
        reducer.use_collapse_triangulate=True
        bpy.ops.object.modifier_apply(modifier=reducer.name)
        # 减面求解会把共面的靴底顶点偏移数个浮点单位；保住原本的落地平面。
        for vertex in obj.data.vertices:
            if abs(vertex.co.z)<.02:vertex.co.z=0
        raw=np.array([v.co[:] for v in obj.data.vertices])
        lo=raw.min(axis=0);hi=raw.max(axis=0)
        mid=V(((hi[0]+lo[0])*.5,(hi[1]+lo[1])*.5,lo[2]))
        obj.data.transform(Matrix.Scale(.02036/(hi[2]-lo[2]),4)@Matrix.Translation(-mid))
        obj.data.update()
        bpy.ops.export_scene.fbx(filepath=str(stage/"robber-band.fbx"),use_selection=True,object_types={"MESH"},
                axis_forward="-Z",axis_up="Y",apply_unit_scale=True,apply_scale_options="FBX_SCALE_ALL",
                bake_space_transform=True,use_mesh_modifiers=False,add_leaf_bones=False,bake_anim=False,path_mode="STRIP")
        targets=[ART/("robber-"+tag+".png") for tag in ("surface","normal","metal","fibres","contact")]
        targets.extend((ART/"robber-band.fbx",SOURCE))
        written=[]
        try:
            for path in targets:
                if path.exists():shutil.copyfile(path,stage/("previous-"+path.name))
                (stage/path.name).replace(path)
                written.append(path)
        except OSError:
            for path in reversed(written):
                backup=stage/("previous-"+path.name)
                if backup.exists():backup.replace(path)
                else:path.unlink()
            raise
    bpy.context.view_layer.update()
    return {"角色":3,"分件":len(parts),"三角面":len(obj.data.polygons),
            "宽深高毫米":[round(v*1000,3) for v in obj.dimensions],"图集尺寸":2048}


result=build()
