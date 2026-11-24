"""回读正式 FBX，核对分件、尺度、贴图与交互轮廓，拍摄四个参考机位。"""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Quaternion, Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from grain_atlas import MODEL, PREVIEW, SOURCE

VIEWS={"海侧斜视":(.48,-1,.48),"正面":(0,-1,.145),"右视":(1,-.025,.12),"俯视":(0,0,1)}


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import bind_preview


def sheet():
    from PIL import Image, ImageDraw, ImageFont
    out=Image.new("RGB",(1600,1310),(236,232,223))
    pen=ImageDraw.Draw(out)
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",23)
    for i,view in enumerate(VIEWS):
        x,y=20+(i%2)*791,15+(i//2)*653
        with Image.open(PREVIEW/(view+".png")) as frame:
            out.paste(frame.convert("RGB").resize((766,608),Image.Resampling.LANCZOS),(x,y))
        pen.text((x+3,y+610),view,font=font,fill=(56,58,54))
    out.save(PREVIEW/"谷物商港四视图.jpg",quality=93,subsampling=0)


def backdrop(scene):
    # 海水与陆岸仅在摄影场景建立。使用世界坐标采样，避免随大平面尺寸拉伸波纹。
    def plane(name,points,color,faces=None):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(points,[],faces or [list(range(len(points)))])
        ob=bpy.data.objects.new(name,mesh)
        scene.collection.objects.link(ob)
        mat=bpy.data.materials.new(name)
        mat.use_nodes=True
        ob.data.materials.append(mat)
        shader=mat.node_tree.nodes.get("Principled BSDF")
        shader.inputs["Base Color"].default_value=(*color,1)
        return mat,shader
    water,shader=plane("摄影水面",[(-1,-1,-.00350),(1,-1,-.00350),(1,1,-.00350),(-1,1,-.00350)],(.071,.171,.166))
    shader.inputs["Roughness"].default_value=.245
    nodes=water.node_tree.nodes
    links=water.node_tree.links.new
    tex=nodes.new("ShaderNodeTexCoord")
    noise=nodes.new("ShaderNodeTexNoise")
    stretch=nodes.new("ShaderNodeVectorMath")
    stretch.operation="MULTIPLY"
    stretch.inputs[1].default_value=(1,3.7,1)
    links(tex.outputs["Object"],stretch.inputs[0])
    noise.inputs["Scale"].default_value=435
    noise.inputs["Detail"].default_value=2.2
    links(stretch.outputs[0],noise.inputs["Vector"])
    color=nodes.new("ShaderNodeValToRGB")
    color.color_ramp.elements[0].color=(.035,.105,.110,1)
    color.color_ramp.elements[1].color=(.11,.25,.232,1)
    links(noise.outputs["Fac"],color.inputs[0])
    links(color.outputs["Color"],shader.inputs["Base Color"])
    bump=nodes.new("ShaderNodeBump")
    bump.inputs["Strength"].default_value=.34
    bump.inputs["Distance"].default_value=.00023
    links(noise.outputs["Fac"],bump.inputs["Height"])
    links(bump.outputs["Normal"],shader.inputs["Normal"])
    profile=((.0037,-.0044),(.0062,-.0033),(.0088,-.00045),(.0101,-.000065),(.017,.00002),(.15,.00004))
    pts=[]
    faces=[]
    shoreline=[-1.0]+[-.080+i*.002 for i in range(81)]+[1.0]
    for i,x in enumerate(shoreline):
        for j,(y,z) in enumerate(profile):
            shift=.00057*math.sin(175*x)+.00018*math.sin(422*x+.7)
            pts.append((x,y+shift,z))
            if i and j:
                k=len(pts)-1
                faces.append((k-7,k-1,k,k-6))
    sand,paper=plane("摄影岸滩",pts,(.37,.335,.266),faces)
    paper.inputs["Roughness"].default_value=.88
    nodes=sand.node_tree.nodes
    links=sand.node_tree.links.new
    tex=nodes.new("ShaderNodeTexCoord")
    sand_noise=nodes.new("ShaderNodeTexNoise")
    sand_noise.inputs["Scale"].default_value=17000
    sand_noise.inputs["Detail"].default_value=2.5
    links(tex.outputs["Object"],sand_noise.inputs["Vector"])
    ramp=nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].color=(.19,.15,.099,1)
    ramp.color_ramp.elements[1].color=(.52,.46,.34,1)
    links(sand_noise.outputs["Fac"],ramp.inputs[0])
    links(ramp.outputs["Color"],paper.inputs["Base Color"])


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene=bpy.context.scene
    scene.name="谷仓回读摄影"
    with bpy.data.libraries.load(str(SOURCE/"谷物商港.blend")) as (src,dst):
        parts=len(src.objects)
        dst.materials=["GrainHarbor","OreHarbor"]
    mat, ore = dst.materials
    imgs=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(imgs)==3 and all(i.packed_file and tuple(i.size)==(2048,2048) for i in imgs)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"grain-harbor.fbx"),use_anim=False)
    models={o.name:o for o in scene.objects if o.type=="MESH"}
    assert set(models)=={"HarborHouse","TradePlaque","PierDeck","GrainCargo","Hoist"}
    pts=[]
    bounds={}
    for name,obj in models.items():
        bind_preview(obj, mat, ore)
        points=[obj.matrix_world@v.co for v in obj.data.vertices]
        bounds[name]=[(min(p[i] for p in points),max(p[i] for p in points)) for i in range(3)]
        pts.extend(points)
    assert bounds["HarborHouse"][1][1]>.0099 and bounds["HarborHouse"][2][1]>.0145
    assert bounds["PierDeck"][2][0]<-.0045 and bounds["PierDeck"][1][0]<-.0054
    assert bounds["TradePlaque"][2][0]>.0063 and bounds["GrainCargo"][2][0]>.00047
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"grain-selection.fbx"),use_anim=False)
    for key in ("PickVolume","ShoreFootprint"):
        proxy=scene.objects[key]
        assert len(proxy.data.polygons)<210
        if key=="ShoreFootprint":
            outline=[proxy.matrix_world@v.co for v in proxy.data.vertices]
            assert abs(min(p.y for p in outline)+.00545)<.000025
            assert max(abs(p.z) for p in outline)<.000001
        bpy.data.objects.remove(proxy,do_unlink=True)
    limits=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
    report={"Blender":bpy.app.version_string,"母版分件":parts,"正式网格":len(models),
            "尺寸毫米":[round((b-a)*1000,2) for a,b in limits],"岸面相对高度毫米":[round(z*1000,2) for z in limits[2]],
            "水线相对岸面毫米":-3.5,"图集":[2048,2048,3]}
    (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    backdrop(scene)
    scene.render.engine="CYCLES"
    scene.cycles.samples=28
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    scene.world=bpy.data.worlds.new("漫反射天光")
    scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get("Background")
    bg.inputs[0].default_value=(.79,.86,.98,1)
    bg.inputs[1].default_value=.31
    aim=Vector((0,.0032,.0058))
    def lamp(name,loc,power,size,color):
        lamp=bpy.data.lights.new(name,"AREA")
        lamp.energy,lamp.shape,lamp.size,lamp.color=power,"DISK",size,color
        ob=bpy.data.objects.new(name,lamp)
        scene.collection.objects.link(ob)
        ob.location=loc
        ob.rotation_euler=(aim-ob.location).to_track_quat("-Z","Y").to_euler()
    lamp("左前窗光",(-.032,-.040,.053),.061,.027,(1,.92,.79))
    lamp("海面反光",(.034,-.032,.026),.020,.035,(.77,.88,1))
    lamp("右后檐光",(.032,.047,.054),.061,.037,(1,.92,.81))
    offset=Vector(VIEWS[view]).normalized()
    rot=Quaternion((1,0,0,0)) if view=="俯视" else (-offset).to_track_quat("-Z","Y")
    right,up=rot@Vector((1,0,0)),rot@Vector((0,1,0))
    exposed=[p for p in pts if p.z>-.00352]
    x=[p.dot(right) for p in exposed]
    y=[p.dot(up) for p in exposed]
    focus=right*(.5*(min(x)+max(x)))+up*(.5*(min(y)+max(y)))+offset*aim.dot(offset)
    lens=bpy.data.cameras.new("原型对照正交镜头")
    lens.type="ORTHO"
    lens.clip_start=.0001
    lens.ortho_scale=1.14*max(max(x)-min(x),(max(y)-min(y))*1.26)
    camera=bpy.data.objects.new("原型对照正交镜头",lens)
    scene.collection.objects.link(camera)
    camera.location=focus+offset*.15
    camera.rotation_euler=rot.to_euler()
    scene.camera=camera
    scene.render.resolution_x,scene.render.resolution_y=1360,1080
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.filepath=str(PREVIEW/(view+".png"))
    bpy.ops.render.render(write_still=True)
    if all((PREVIEW/(v+".png")).is_file() for v in VIEWS): sheet()
    return {"机位":view,**report}


result=inspect(globals().get("view","海侧斜视"))

