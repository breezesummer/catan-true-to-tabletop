"""从正式 FBX 回读网格、UV 和落位，再拍摄四向材质样片。棚拍承影面只在检查场景中建立。"""
from pathlib import Path
import json
import sys
import bpy
from mathutils import Quaternion,Vector

sys.path.insert(0,str(Path(__file__).resolve().parent))
from kiln_atlas import MODEL,PREVIEW,SOURCE


sys.path.insert(0,str(Path(__file__).resolve().parents[1]/"ore-harbor"))
from mounted_plaque import bind_preview


def contact_sheet():
    from PIL import Image,ImageDraw,ImageFont
    views=("海侧斜视","正面","侧面","俯视")
    if not all((PREVIEW/(v+".png")).is_file() for v in views): return
    sheet=Image.new("RGB",(1600,1364),(241,238,231))
    pen=ImageDraw.Draw(sheet)
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
    for i,view in enumerate(views):
        x,y=22+(i%2)*789,20+(i//2)*671
        with Image.open(PREVIEW/(view+".png")) as frame:
            sheet.paste(frame.convert("RGB").resize((768,614),Image.Resampling.LANCZOS),(x,y))
        pen.text((x+2,y+622),view,font=font,fill=(65,52,44))
    sheet.save(PREVIEW/"砖港四视图.jpg",quality=92,subsampling=0)


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene=bpy.context.scene
    scene.name="砖港回读摄影"
    with bpy.data.libraries.load(str(SOURCE/"砖港.blend")) as (data,dest):
        source_count=len(data.objects)
        dest.materials=["BrickHarbor","OreHarbor"]
    mat, ore = dest.materials
    maps=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(maps)==3 and all(img.packed_file and tuple(img.size)==(2048,2048) for img in maps)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"brick-harbor.fbx"),use_anim=False)
    meshes={obj.name:obj for obj in scene.objects if obj.type=="MESH"}
    assert set(meshes)=={"HarborHouse","TradePlaque","BrickStack","LoadingCrane","PierDeck","QuayWall"}
    points=[]
    bounds={}
    for name,obj in meshes.items():
        bind_preview(obj, mat, ore)
        pts=[obj.matrix_world @ v.co for v in obj.data.vertices]
        bounds[name]=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
        points.extend(pts)
    assert bounds["PierDeck"][2][0]<-.0045 and bounds["QuayWall"][2][0]<-.0035<bounds["QuayWall"][2][1]
    assert .0099<bounds["QuayWall"][1][1]<.0111 and bounds["HarborHouse"][1][1]<.0099
    assert bounds["BrickStack"][0][0]>0 and bounds["LoadingCrane"][0][1]<0
    assert bounds["TradePlaque"][2][0]>.005 and bounds["PierDeck"][1][0]<-.0052
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"brick-selection.fbx"),use_anim=False)
    proxy=bpy.data.objects["ShoreFootprint"]
    assert len(proxy.data.polygons)==6
    lip=min((proxy.matrix_world @ v.co).y for v in proxy.data.vertices)
    assert abs(lip-bounds["PierDeck"][1][0])<.00004
    for name in ("ShoreFootprint","PickVolume"): bpy.data.objects.remove(bpy.data.objects[name],do_unlink=True)
    limits=[(min(p[i] for p in points),max(p[i] for p in points)) for i in range(3)]
    report={"Blender":bpy.app.version_string,"母版分件":source_count,"正式网格":len(meshes),
            "尺寸毫米":[round((b-a)*1000,2) for a,b in limits],
            "最低与最高相对岸面毫米":[round(z*1000,2) for z in limits[2]],"内嵌图集":[2048,2048,3]}
    (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    scene.render.engine="CYCLES"
    scene.cycles.samples=24
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    world=bpy.data.worlds.new("漫射天光")
    world.use_nodes=True
    scene.world=world
    bg=world.node_tree.nodes.get("Background")
    bg.inputs["Color"].default_value=(.81,.85,.94,1)
    bg.inputs["Strength"].default_value=.25
    floor=bpy.data.meshes.new("无边摄影棚")
    levels=((-1,-.0035),(.0094,-.0035),(.0102,-.00004),(1,-.00004))
    floor.from_pydata([(x,y,z) for y,z in levels for x in (-1,1)],[],[(2*i,2*i+1,2*i+3,2*i+2) for i in range(3)])
    obj=bpy.data.objects.new("摄影承影面",floor)
    scene.collection.objects.link(obj)
    backdrop=bpy.data.materials.new("暖灰摄影纸")
    backdrop.diffuse_color=(.68,.61,.50,1)
    obj.data.materials.append(backdrop)
    aim=Vector((0,.0024,.0044))
    def lamp(name,loc,power,size,color):
        data=bpy.data.lights.new(name,"AREA")
        data.energy,data.shape,data.size,data.color=power,"DISK",size,color
        ob=bpy.data.objects.new(name,data)
        scene.collection.objects.link(ob)
        ob.location=loc
        ob.rotation_euler=(aim-ob.location).to_track_quat("-Z","Y").to_euler()
    lamp("左前窗光",(-.037,-.044,.069),.083,.039,(1,.90,.78))
    lamp("右前补光",(.044,-.033,.031),.023,.047,(.83,.90,1))
    lamp("后缘天光",(.019,.058,.065),.056,.041,(1,.92,.79))
    looks={"海侧斜视":(.56,-1,.46),"正面":(0,-1,.17),"侧面":(1,-.045,.15),"俯视":(0,0,1)}
    offset=Vector(looks[view]).normalized()
    rotation=Quaternion((1,0,0,0)) if view=="俯视" else (-offset).to_track_quat("-Z","Y")
    right,up=(rotation @ Vector(axis) for axis in ((1,0,0),(0,1,0)))
    xr=[p.dot(right) for p in points]; yr=[p.dot(up) for p in points]
    center=right*((min(xr)+max(xr))*.5)+up*((min(yr)+max(yr))*.5)+offset*aim.dot(offset)
    data=bpy.data.cameras.new("正交摄影机")
    data.type="ORTHO"
    data.clip_start=.0001
    data.ortho_scale=max(max(xr)-min(xr),(max(yr)-min(yr))*1.25)*1.15
    ob=bpy.data.objects.new("正交摄影机",data)
    scene.collection.objects.link(ob)
    ob.location=center+offset*.14
    ob.rotation_euler=rotation.to_euler()
    scene.camera=ob
    scene.render.resolution_x=1320
    scene.render.resolution_y=1056
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.filepath=str(PREVIEW/(view+".png"))
    bpy.ops.render.render(write_still=True)
    if view=="俯视": contact_sheet()
    return {"机位":view,**report}


result=inspect(globals().get("view_name") or "海侧斜视")
