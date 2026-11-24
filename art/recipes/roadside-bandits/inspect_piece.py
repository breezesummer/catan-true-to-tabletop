"""回读正式 FBX 并从同一网格拍摄。摄影纸、灯和相机只加入检查场景。"""
from pathlib import Path
from tempfile import TemporaryDirectory
import json
import sys
from math import radians,sin,cos
import bpy
import numpy as np
from PIL import Image
from mathutils import Vector,Matrix

sys.path.insert(0,str(Path(__file__).resolve().parent))
from finishes import ART,SOURCE,VIEWS,REF,ROOT
sys.modules.pop("inspect_hands",None)
sys.modules.pop("inspect_portraits",None)
from inspect_hands import check_hands,photograph
from inspect_portraits import photograph_portraits,portrait_plates,check_portraits


def inspect_piece(draft=False):
    VIEWS.mkdir(parents=True,exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE),load_ui=False)
    source=bpy.context.scene
    pieces=list(source.objects)
    feet=[o for o in pieces if "腿与靴" in o.name]
    assert len(feet)==6 and all(abs(min(v.co.z for v in o.data.vertices))<1e-8 for o in feet)
    source_mat=bpy.data.materials["RobberSurface"]
    images=[node.image for node in source_mat.node_tree.nodes if node.type=="TEX_IMAGE"]
    assert len(images)==4 and all(img.packed_file and tuple(img.size)==
        ((512,512) if img.name.startswith("robber-fibres") else (2048,2048)) for img in images)
    channels=np.asarray(Image.open(ART/"robber-metal.png").convert("RGBA"))
    assert channels[:,:,0].max()>210 and np.any(channels[:,:,0]==0)
    assert channels[:,:,1].min()<155 and channels[:,:,1].max()==255
    nap=channels[:,:,2]
    assert nap.max()>230 and (nap==0).any() and ((nap>60)&(nap<140)).any()
    assert np.median(channels[:,:,3][nap>205])<24
    assert np.percentile(channels[:,:,3],99)>np.percentile(channels[:,:,3],20)+38
    scene=bpy.data.scenes.new("强盗团 · 导出回读摄影")
    bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(ART/"robber-band.fbx"),use_anim=False)
    obj,=scene.objects
    assert obj.type=="MESH" and len(obj.data.materials)==1 and obj.data.materials[0].name.startswith("RobberSurface")
    obj.data.materials[0]=source_mat
    mesh=obj.data
    xyz=np.array([tuple(obj.matrix_world@v.co) for v in mesh.vertices])
    low,high=xyz.min(axis=0),xyz.max(axis=0)
    assert abs(low[2])<1e-7 and abs(high[2]-.02036)<1e-7 and np.max(abs((low+high)[:2]))<1e-7
    assert len(mesh.uv_layers)==2
    uv=np.empty(len(mesh.loops)*2,dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv",uv)
    assert np.isfinite(uv).all() and uv.min()>0 and uv.max()<1
    fibre_uv=np.empty_like(uv)
    mesh.uv_layers[1].data.foreach_get("uv",fibre_uv)
    assert np.isfinite(fibre_uv).all() and np.ptp(fibre_uv)>2
    mesh.calc_loop_triangles()
    assert .018<high[0]-low[0]<.024 and .007<high[1]-low[1]<.015
    floor_tri=[tri for tri in mesh.loop_triangles if np.max(xyz[list(tri.vertices),2])<1e-7]
    assert floor_tri
    for tri in floor_tri:
        points=xyz[list(tri.vertices)]
        assert max(np.linalg.norm(p-q) for p in points for q in points)<.0035,"落地处存在跨足底的面"
    floor_points=xyz[xyz[:,2]<1e-7,:2]
    for foot in feet:
        sole=np.array([v.co[:2] for v in foot.data.vertices if v.co.z<1e-7])
        inside=(floor_points>=sole.min(axis=0)-1e-6)&(floor_points<=sole.max(axis=0)+1e-6)
        assert np.sum(inside.all(axis=1))>=3,"导出后靴底未落地："+foot.name
    report={"原件分组":len(pieces),"脚底接触":len(feet),"正式网格数":1,"材质数":1,
            "源件腕掌":check_hands(source),"源件肖像":check_portraits(source),
            "三角面":len(mesh.loop_triangles),"宽深高毫米":np.round((high-low)*1000,3).tolist(),
            "支承面毫米":abs(round(float(low[2])*1000,5)),"贴图":[i.name for i in images],
            "接触遮蔽范围":[int(channels[:,:,1].min()),int(channels[:,:,1].max())],
            "绒面遮罩范围":[int(nap.min()),int(nap.max())]}
    if not draft:(VIEWS/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True;scene.cycles.max_bounces=5
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for mode in ("OPTIX","HIP","METAL"):
        try:
            prefs.compute_device_type=mode;prefs.refresh_devices()
            if any(device.type==mode for device in prefs.devices):
                for device in prefs.devices:device.use=device.type==mode
                scene.cycles.device="GPU";break
        except (TypeError,RuntimeError):continue
    scene.world=bpy.data.worlds.new("柔和天光");scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background");bg.inputs[0].default_value=(.84,.90,1,1)
    bg.inputs[1].default_value=.245
    aim=Vector((0,0,.010))
    for label,pos,power,size,col in (("左前窗",(-.038,-.052,.064),.141,.046,(1,.92,.82)),
           ("右侧反光板",(.041,-.034,.036),.028,.049,(.84,.91,1)),
           ("轮廓柔光",(.012,.051,.052),.092,.037,(1,.9,.77))):
        data=bpy.data.lights.new(label,"AREA");data.energy=power;data.shape="DISK";data.size=size;data.color=col
        light=bpy.data.objects.new(label,data);scene.collection.objects.link(light);light.location=pos
        light.rotation_euler=(aim-light.location).to_track_quat("-Z","Y").to_euler()
    paper=bpy.data.materials.new("摄影暖灰纸");paper.use_nodes=True
    nodes=paper.node_tree.nodes;links=paper.node_tree.links
    shader=nodes.get("Principled BSDF");shader.inputs["Base Color"].default_value=(.56,.50,.418,1)
    shader.inputs["Roughness"].default_value=.93
    emission=nodes.new("ShaderNodeEmission");emission.inputs[0].default_value=(.78,.714,.626,1)
    radius=nodes.new("ShaderNodeVectorMath");radius.operation="LENGTH"
    links.new(nodes.new("ShaderNodeNewGeometry").outputs["Position"],radius.inputs[0])
    ramp=nodes.new("ShaderNodeMapRange");ramp.interpolation_type="SMOOTHERSTEP"
    ramp.inputs["From Min"].default_value=.025;ramp.inputs["From Max"].default_value=.085
    links.new(radius.outputs["Value"],ramp.inputs[0])
    visible=nodes.new("ShaderNodeMath");visible.operation="MULTIPLY"
    links.new(ramp.outputs[0],visible.inputs[0]);links.new(nodes.new("ShaderNodeLightPath").outputs["Is Camera Ray"],visible.inputs[1])
    blend=nodes.new("ShaderNodeMixShader");links.new(visible.outputs[0],blend.inputs[0])
    links.new(shader.outputs[0],blend.inputs[1]);links.new(emission.outputs[0],blend.inputs[2])
    links.new(blend.outputs[0],nodes.get("Material Output").inputs["Surface"])
    floor=bpy.data.meshes.new("摄影纸")
    floor.from_pydata([(-2,-2,-.000005),(2,-2,-.000005),(2,2,-.000005),(-2,2,-.000005)],[],[(0,1,2,3)])
    plane=bpy.data.objects.new("接影纸",floor);scene.collection.objects.link(plane);floor.materials.append(paper)
    data=bpy.data.cameras.new("正交镜头");data.type="ORTHO";data.clip_start=.00003
    cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);scene.camera=cam
    scene.view_settings.view_transform="AgX";scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.resolution_percentage=100;scene.render.image_settings.color_mode="RGB"
    scene.render.image_settings.file_format="PNG"
    views=[("正面",0,13),("侧面",66,12),("背面",180,13),("棋盘俯角",-12,52)]
    for name,az,el in (views[:1] if draft else views):
        a,e=radians(az),radians(el)
        direction=Vector((sin(a)*cos(e),-cos(a)*cos(e),sin(e)))
        right=Vector((cos(a),sin(a),0));up=direction.cross(right)
        xx=xyz@np.asarray(right);yy=xyz@np.asarray(up)
        center=right*float((xx.max()+xx.min())*.5)+up*float((yy.max()+yy.min())*.5)
        cam.location=center+.09*direction
        cam.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
        data.ortho_scale=max(np.ptp(xx),np.ptp(yy)*1.12)*1.105
        scene.render.resolution_x,scene.render.resolution_y=(664,592) if draft else (1280,1144)
        scene.cycles.samples=12 if draft else 44
        path=(VIEWS/"打样.png") if draft else (VIEWS/(name+".png"))
        scene.render.filepath=str(path)
        bpy.ops.render.render(write_still=True)
    if not draft:
        photograph(scene,source)
        with TemporaryDirectory(dir=ROOT/".local") as tmp:
            photograph_portraits(scene,source,Path(tmp))
            baseline=globals().get("PREVIOUS_PORTRAITS")
            portrait_plates(REF,Path(tmp),VIEWS,Path(baseline) if baseline else None)
    return {**report,"渲染设备":scene.cycles.device}


result=inspect_piece(globals().get("DRAFT",False))
