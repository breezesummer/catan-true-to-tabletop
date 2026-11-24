"""回读正式 FBX 与拾取网格，核对尺寸与水线，并从四个方向拍摄材质样片。"""
import json
from pathlib import Path
import sys
import bpy
from mathutils import Quaternion,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from wool_surfaces import MODEL,PREVIEW,SOURCE

VIEWS={"海侧斜视":(.49,-1,.44),"正面":(0,-1,.17),"右侧":(1,-.035,.16),"俯视":(0,0,1)}


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import bind_preview


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene=bpy.context.scene
    with bpy.data.libraries.load(str(SOURCE/"羊毛港.blend")) as (src,dst):
        count=len(src.objects)
        dst.materials=["WoolHarbor","OreHarbor"]
    mat, ore = dst.materials
    images=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(images)==3 and all(m.packed_file and tuple(m.size)==(2048,2048) for m in images)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"wool-selection.fbx"),use_anim=False)
    proxies=list(scene.objects)
    assert {p.name for p in proxies}=={"PickVolume","ShoreFootprint"}
    foot=next(p for p in proxies if p.name=="ShoreFootprint")
    shape=[foot.matrix_world @ v.co for v in foot.data.vertices]
    assert min(v.y for v in shape)<-.00565 and max(v.y for v in shape)>.0099
    for obj in proxies: bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"wool-harbor.fbx"),use_anim=False)
    meshes={ob.name:ob for ob in scene.objects}
    assert set(meshes)=={"HarborHouse","PierDeck","TradePlaque","WoolBales","Hoist"}
    points=[]
    limits={}
    for name,ob in meshes.items():
        bind_preview(ob, mat, ore)
        pts=[ob.matrix_world @ v.co for v in ob.data.vertices]
        points+=pts
        limits[name]=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
    assert limits["HarborHouse"][1][1]<.0101 and limits["PierDeck"][1][1]>.0103
    assert limits["PierDeck"][2][0]<-.0045 and limits["WoolBales"][2][0]>=-.0001
    assert limits["TradePlaque"][2][0]>.0058
    report={"Blender":bpy.app.version_string,"母版分件":count,"正式网格":len(meshes),
            "各轴尺寸毫米":[round(1000*(max(p[i] for p in points)-min(p[i] for p in points)),2) for i in range(3)],
            "岸线相对港根节点毫米":9.9,"接岸石后沿毫米":round(1000*limits["PierDeck"][1][1],2),
            "桩底相对平均水线毫米":round(1000*limits["PierDeck"][2][0]+3.50,2)}
    (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    scene.render.engine="CYCLES"
    scene.cycles.samples=18
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    world=bpy.data.worlds.new("港区天光")
    world.use_nodes=True
    scene.world=world
    bg=world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(.78,.85,.96,1)
    bg.inputs["Strength"].default_value=.33
    def card(name,verts,color):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata(verts,[],[(0,1,2,3)])
        ob=bpy.data.objects.new(name,mesh)
        scene.collection.objects.link(ob)
        paper=bpy.data.materials.new(name)
        paper.diffuse_color=(*color,1)
        ob.data.materials.append(paper)
    card("棚拍承影水线",[(-1,-1,-.00353),(1,-1,-.00353),(1,1,-.00353),(-1,1,-.00353)],(.22,.33,.34))
    card("接岸量规",[(-1,.00989,-.00004),(1,.00989,-.00004),(1,1,-.00004),(-1,1,-.00004)],(.49,.455,.387))
    card("量规岸壁",[(-1,.00990,-.00353),(1,.00990,-.00353),(1,.00990,-.00004),(-1,.00990,-.00004)],(.38,.355,.30))
    aim=Vector((0,.003,.0044))
    def lamp(name,loc,power,size,color):
        data=bpy.data.lights.new(name,"AREA")
        data.energy,data.shape,data.size,data.color=power,"DISK",size,color
        ob=bpy.data.objects.new(name,data)
        scene.collection.objects.link(ob)
        ob.location=loc
        ob.rotation_euler=(aim-ob.location).to_track_quat("-Z","Y").to_euler()
    lamp("海侧主光",(-.047,-.046,.064),.101,.033,(1,.89,.73))
    lamp("海面反光",(.047,-.032,.032),.034,.045,(.81,.90,1))
    lamp("屋脊轮廓光",(.012,.046,.058),.072,.042,(1,.94,.84))
    data=bpy.data.cameras.new("港口机位")
    data.type="ORTHO"
    data.clip_start=.0001
    ob=bpy.data.objects.new("港口机位",data)
    scene.collection.objects.link(ob)
    scene.camera=ob
    scene.render.resolution_x=1280
    scene.render.resolution_y=1000
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    render_views=list(VIEWS) if view=="全部" else [view]
    for name in render_views:
        offset=Vector(VIEWS[name]).normalized()
        rot=Quaternion((1,0,0,0)) if name=="俯视" else (-offset).to_track_quat("-Z","Y")
        right,up=(rot @ Vector(v) for v in ((1,0,0),(0,1,0)))
        visible=[p for p in points if p.z>=-.00357]
        xr=[p.dot(right) for p in visible]; yr=[p.dot(up) for p in visible]
        center=right*((min(xr)+max(xr))*.5)+up*((min(yr)+max(yr))*.5)+offset*aim.dot(offset)
        data.ortho_scale=max(max(xr)-min(xr),(max(yr)-min(yr))*1.28)*1.115
        ob.location=center+offset*.15
        ob.rotation_euler=rot.to_euler()
        scene.render.filepath=str(PREVIEW/(name+".png"))
        bpy.ops.render.render(write_still=True)
    if all((PREVIEW/(v+".png")).is_file() for v in VIEWS):
        from PIL import Image,ImageDraw,ImageFont
        sheet=Image.new("RGB",(1636,1374),(237,235,228))
        pen=ImageDraw.Draw(sheet)
        font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
        for i,name in enumerate(VIEWS):
            x,y=14+(i%2)*811,12+(i//2)*681
            with Image.open(PREVIEW/(name+".png")) as frame:
                sheet.paste(frame.convert("RGB").resize((796,622),Image.Resampling.LANCZOS),(x,y))
            pen.text((x+3,y+628),name,font=font,fill=(55,56,53))
        sheet.save(PREVIEW/"羊毛港四视图.jpg",quality=92,subsampling=0)
    return report


result=inspect(globals().get("view","全部"))
