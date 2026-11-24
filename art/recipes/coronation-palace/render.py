"""通过 Blender MCP 从打包源件拍摄。摄影地面、灯和相机均只存在于拍摄会话。"""
import math
from pathlib import Path
import sys
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from finishes import BLEND,VIEWS_DIR


def render(views):
    bpy.ops.wm.open_mainfile(filepath=str(BLEND),load_ui=False)
    scene=bpy.context.scene
    points=[o.matrix_world@v.co for o in scene.objects for v in o.data.vertices]
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=6
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for mode in ("HIP","OPTIX","METAL"):
        try:
            prefs.compute_device_type=mode
            prefs.refresh_devices()
            if any(d.type==mode for d in prefs.devices):
                for d in prefs.devices:
                    d.use=d.type==mode
                scene.cycles.device="GPU"
                break
        except (TypeError,RuntimeError):
            continue
    scene.world=bpy.data.worlds.new("散射天光")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value=(.76,.85,1,1)
    bg.inputs[1].default_value=.26
    world=scene.world.node_tree
    camera_paper=world.nodes.new("ShaderNodeBackground")
    camera_paper.inputs[0].default_value=(.80,.78,.75,1)
    mix=world.nodes.new("ShaderNodeMixShader")
    world.links.new(world.nodes.new("ShaderNodeLightPath").outputs["Is Camera Ray"],mix.inputs[0])
    world.links.new(bg.outputs[0],mix.inputs[1])
    world.links.new(camera_paper.outputs[0],mix.inputs[2])
    world.links.new(mix.outputs[0],world.nodes.get("World Output").inputs["Surface"])
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.view_settings.exposure=-.13
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGB"
    scene.render.resolution_percentage=100
    aim=Vector((0,0,.010))
    for name,xyz,power,size,rgb in (
        ("西南大窗",(-.046,-.053,.080),.25,.049,(1,.925,.81)),
        ("东墙反光",(.052,-.026,.041),.050,.055,(.79,.86,1)),
        ("屋脊反光",(.001,.065,.065),.186,.037,(1,.91,.78))):
        lamp=bpy.data.lights.new(name,"AREA")
        lamp.energy,lamp.shape,lamp.size,lamp.color=power,"DISK",size,rgb
        obj=bpy.data.objects.new(name,lamp)
        scene.collection.objects.link(obj)
        obj.location=xyz
        obj.rotation_euler=(aim-obj.location).to_track_quat("-Z","Y").to_euler()
    paper=bpy.data.materials.new("暖灰摄影纸")
    paper.use_nodes=True
    paper.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value=(.57,.554,.523,1)
    paper.node_tree.nodes.get("Principled BSDF").inputs["Roughness"].default_value=.96
    # 正、侧视的低机位能看很远。摄影纸远端过渡到同色背景，近处保留真实的接触阴影。
    nodes,links=paper.node_tree.nodes,paper.node_tree.links
    radius=nodes.new("ShaderNodeVectorMath")
    radius.operation="LENGTH"
    links.new(nodes.new("ShaderNodeNewGeometry").outputs["Position"],radius.inputs[0])
    fade=nodes.new("ShaderNodeMapRange")
    fade.interpolation_type="SMOOTHERSTEP"
    fade.inputs["From Min"].default_value=.040
    fade.inputs["From Max"].default_value=.11
    links.new(radius.outputs["Value"],fade.inputs["Value"])
    visible=nodes.new("ShaderNodeMath")
    visible.operation="MULTIPLY"
    links.new(fade.outputs[0],visible.inputs[0])
    links.new(nodes.new("ShaderNodeLightPath").outputs["Is Camera Ray"],visible.inputs[1])
    horizon=nodes.new("ShaderNodeEmission")
    horizon.inputs["Color"].default_value=(.80,.78,.75,1)
    backdrop=nodes.new("ShaderNodeMixShader")
    links.new(visible.outputs[0],backdrop.inputs[0])
    links.new(nodes.get("Principled BSDF").outputs[0],backdrop.inputs[1])
    links.new(horizon.outputs[0],backdrop.inputs[2])
    links.new(backdrop.outputs[0],nodes.get("Material Output").inputs[0])
    mesh=bpy.data.meshes.new("摄影地面")
    mesh.from_pydata([(-2,-2,-.00001),(2,-2,-.00001),(2,2,-.00001),(-2,2,-.00001)],[],[(0,1,2,3)])
    floor=bpy.data.objects.new("接影纸",mesh)
    scene.collection.objects.link(floor)
    mesh.materials.append(paper)
    camera=bpy.data.cameras.new("移轴正交摄影机")
    camera.type,camera.clip_start,camera.clip_end="ORTHO",.00005,4
    obj=bpy.data.objects.new(camera.name,camera)
    scene.collection.objects.link(obj)
    scene.camera=obj
    VIEWS_DIR.mkdir(parents=True,exist_ok=True)

    def shot(name,az,el,px,margin,samples,focus=None):
        az,el=map(math.radians,(az,el))
        direction=Vector((math.sin(az)*math.cos(el),-math.cos(az)*math.cos(el),math.sin(el)))
        right=Vector((math.cos(az),math.sin(az),0))
        up=direction.cross(right)
        bounds=[[p.dot(axis) for p in points] for axis in (right,up)]
        center=[(max(c)+min(c))*.5 for c in bounds]
        ratio=px[0]/px[1]
        width=max(max(bounds[0])-min(bounds[0]),(max(bounds[1])-min(bounds[1]))*ratio)*margin
        target=right*center[0]+up*center[1]
        if focus:
            target,width=focus
        obj.location=target+direction*.19
        obj.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
        camera.ortho_scale=width
        scene.render.resolution_x,scene.render.resolution_y=px
        scene.cycles.samples=samples
        scene.render.filepath=str(VIEWS_DIR/(name+".png"))
        bpy.ops.render.render(write_still=True)
    for key in views:
        if key=="draft":
            shot("打样",27,19,(760,680),1.105,14)
        elif key=="hero":
            shot("冠冕议政宫",22,23,(1440,1280),1.105,72)
        elif key=="views":
            for name,az,el in (("正视",0,3),("右视",90,3),("俯视",0,89.99)):
                shot(name,az,el,(980,980),1.115,36)
        elif key=="detail":
            shot("王冠盾徽与门套",5,4,(1180,1040),1,80,(Vector((0,.0009,.0130)),.0129))
    return {"视图":views,"设备":scene.cycles.device}


result=render(globals().get("VIEWS") or ["hero","views","detail"])
