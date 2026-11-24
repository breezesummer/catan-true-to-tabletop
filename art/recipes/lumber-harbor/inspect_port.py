"""回读正式 FBX 并拍摄材质正面、海侧、侧面或顶视。摄影器材只在检查场景中。"""
from pathlib import Path
import sys
import json
import bpy
from mathutils import Vector,Quaternion

sys.path.insert(0,str(Path(__file__).resolve().parent))
from surfaces import MODEL, SOURCE, PREVIEW


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import bind_preview


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    with bpy.data.libraries.load(str(SOURCE/"原木商港.blend")) as (library, target):
        count=len(library.objects)
        assert count==17
        target.materials=["LumberHarbor","OreHarbor"]
    mat, ore = target.materials
    images=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(images)==3 and all(i.packed_file and tuple(i.size)==(2048,2048) for i in images)
    scene=bpy.data.scenes.new("木材港回读")
    bpy.context.window.scene=scene
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"lumber-harbor.fbx"),use_anim=False)
    meshes={o.name:o for o in scene.objects if o.type=="MESH"}
    assert set(meshes)=={"HarborHouse","TradePlaque","PierDeck","TimberHoist","LogStack"}
    for obj in meshes.values():
        bind_preview(obj, mat, ore)
    def point(obj,v):
        return obj.matrix_world @ v.co
    def extrema(part,axis):
        pts=[point(meshes[part],v)[axis] for v in meshes[part].data.vertices]
        return min(pts),max(pts)
    assert extrema("PierDeck",2)[0]<-.0045 and extrema("HarborHouse",2)[1]>.015
    assert extrema("HarborHouse",2)[0]<-.0035 and .0099<=extrema("HarborHouse",1)[1]<.011
    assert extrema("LogStack",1)[1]<.002 and extrema("TimberHoist",2)[0]>.00075
    assert extrema("TradePlaque",2)[0]>.0055 and extrema("TradePlaque",1)[1]<.007
    if view=="海侧斜视":
        bounds=[(min(extrema(part,i)[0] for part in meshes),max(extrema(part,i)[1] for part in meshes)) for i in range(3)]
        report={"Blender":bpy.app.version_string,"源文件分件":count,"运行可见网格":len(meshes),
                "尺寸毫米":[round((b-a)*1000,2) for a,b in bounds],
                "桩底与屋脊相对岸面毫米":[round(v*1000,2) for v in bounds[2]],
                "内嵌图集尺寸":[2048,2048],"内嵌图集数量":len(images)}
        (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    scene.render.engine="CYCLES"
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    scene.world=bpy.data.worlds.new("柔和天光")
    scene.world.use_nodes=True
    nodes=scene.world.node_tree.nodes
    links=scene.world.node_tree.links
    bg=nodes.get("Background")
    bg.inputs[0].default_value=(.79,.86,1,1)
    bg.inputs[1].default_value=.30
    sky=nodes.new("ShaderNodeBackground")
    sky.inputs[0].default_value=(.79,.73,.63,1)
    mix=nodes.new("ShaderNodeMixShader")
    links.new(nodes.new("ShaderNodeLightPath").outputs["Is Camera Ray"],mix.inputs[0])
    links.new(bg.outputs[0],mix.inputs[1]);links.new(sky.outputs[0],mix.inputs[2])
    links.new(mix.outputs[0],nodes.get("World Output").inputs[0])
    # 在摄影纸的水线高度承影；摄影纸和照明仅在回读场景建立。
    floor=bpy.data.meshes.new("摄影纸")
    floor.from_pydata([(x,y,-.00355) for x,y in ((-1,-1),(1,-1),(1,1),(-1,1))],[],[(0,1,2,3)])
    paper=bpy.data.objects.new("摄影棚承影面",floor)
    scene.collection.objects.link(paper)
    neutral=bpy.data.materials.new("暖灰色摄影纸")
    neutral.diffuse_color=(.69,.65,.59,1)
    paper.data.materials.append(neutral)
    target=Vector((0,.002,.0051))
    def lamp(name,loc,power,size,color):
        lamp=bpy.data.lights.new(name,"AREA")
        lamp.energy,lamp.shape,lamp.size,lamp.color=power,"DISK",size,color
        obj=bpy.data.objects.new(name,lamp)
        scene.collection.objects.link(obj)
        obj.location=loc
        obj.rotation_euler=(target-obj.location).to_track_quat("-Z","Y").to_euler()
    lamp("左前柔光",(-.045,-.040,.072),.116,.045,(1,.90,.78))
    lamp("右侧反光",(.056,-.018,.036),.036,.050,(.81,.89,1))
    lamp("后侧轮廓",(.018,.067,.058),.077,.045,(1,.89,.73))
    camera=bpy.data.cameras.new("等距镜头")
    camera.type="ORTHO"
    camera.clip_start=.0001
    obj=bpy.data.objects.new("等距镜头",camera)
    scene.collection.objects.link(obj)
    scene.camera=obj
    offsets={"海侧斜视":(.54,-1,.48),"正面":(0,-1,.16),"侧面":(1,-.035,.17),"俯视":(0,0,1)}
    offset=Vector(offsets[view]).normalized()
    rotation=Quaternion((1,0,0,0)) if view=="俯视" else (-offset).to_track_quat("-Z","Y")
    right,up=(rotation@Vector(v) for v in ((1,0,0),(0,1,0)))
    pts=[point(o,v) for o in meshes.values() for v in o.data.vertices]
    for pt in pts:
        pt.z=max(pt.z,-.00355)
    xs=[p.dot(right) for p in pts]; ys=[p.dot(up) for p in pts]
    camera.ortho_scale=max(max(xs)-min(xs),(max(ys)-min(ys))*1.25)*1.14
    center=right*((max(xs)+min(xs))*.5)+up*((max(ys)+min(ys))*.5)+offset*target.dot(offset)
    obj.location=center+offset*.12
    obj.rotation_euler=rotation.to_euler()
    scene.render.resolution_x=1320
    scene.render.resolution_y=1056
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.filepath=str(PREVIEW/(view+".png"))
    bpy.ops.render.render(write_still=True)
    if view=="俯视":
        contact_sheet()
    return {"机位":view,"像素":[1320,1056],"可见网格":list(meshes)}


def contact_sheet():
    from PIL import Image,ImageDraw,ImageFont
    names=("正面","侧面","俯视","海侧斜视")
    sheet=Image.new("RGB",(1600,1364),(241,238,231))
    pen=ImageDraw.Draw(sheet)
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
    for i,name in enumerate(names):
        x,y=22+(i%2)*789,20+(i//2)*671
        with Image.open(PREVIEW/(name+".png")) as frame:
            sheet.paste(frame.convert("RGB").resize((768,614),Image.Resampling.LANCZOS),(x,y))
        pen.text((x+2,y+620),name,font=font,fill=(65,52,44))
    sheet.save(PREVIEW/"木材港四视图.jpg",quality=93,subsampling=0)


result=inspect(globals().get("view_name") or "海侧斜视")
