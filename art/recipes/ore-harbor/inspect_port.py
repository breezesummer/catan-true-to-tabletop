"""回读游戏 FBX 后拍摄四个机位。摄影用承影面不随港口导出。"""
import json
import math
from pathlib import Path
import sys
import bpy
from mathutils import Quaternion,Vector
sys.path.insert(0,str(Path(__file__).resolve().parent))
from surfaces_ore import MODEL,PREVIEW,SOURCE

V=Vector
VIEWS={"海侧斜视":(.48,-1,.51),"正面":(0,-1,.115),"右侧":(1,0,.12),"俯视":(0,0,1)}


def sheet():
    from PIL import Image,ImageDraw,ImageFont
    img=Image.new("RGB",(1632,1370),(241,239,234)); pen=ImageDraw.Draw(img)
    font=ImageFont.truetype("C:/Windows/Fonts/msyh.ttc",24)
    for i,label in enumerate(VIEWS):
        x,y=16+i%2*810,16+i//2*681
        with Image.open(PREVIEW/(label+".png")) as source:
            img.paste(source.convert("RGB").resize((790,627),Image.Resampling.LANCZOS),(x,y))
        pen.text((x+7,y+635),label,font=font,fill=(65,62,57))
    img.save(PREVIEW/"原矿商港四视图.jpg",quality=94,subsampling=0)


def inspect(view):
    bpy.ops.wm.read_homefile(use_empty=True,use_factory_startup=True)
    scene=bpy.context.scene
    with bpy.data.libraries.load(str(SOURCE/"原矿商港.blend")) as (src,dst):
        source_count=len(src.objects); dst.materials=["OreHarbor"]
    mat=dst.materials[0]
    maps=[n.image for n in mat.node_tree.nodes if n.type=="TEX_IMAGE"]
    assert len(maps)==3 and all(im.packed_file and tuple(im.size)==(2048,2048) for im in maps)
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"ore-harbor.fbx"),use_anim=False)
    parts={ob.name:ob for ob in scene.objects if ob.type=="MESH"}
    assert set(parts)=={"HarborHouse","TradePlaque","PierDeck","OreCargo","OreSling","LoadingCrane"}
    points=[]; bounds={}; tris={}; degenerate={}
    for name,obj in parts.items():
        mesh=obj.data
        assert len(mesh.materials)==1 and len(mesh.uv_layers)==2
        mesh.materials[0]=mat
        mesh.calc_loop_triangles()
        pts=[obj.matrix_world@v.co for v in mesh.vertices]
        tri=mesh.loop_triangles
        tris[name]=len(tri)
        degenerate[name]=sum((pts[t.vertices[1]]-pts[t.vertices[0]]).cross(pts[t.vertices[2]]-pts[t.vertices[0]]).length<1e-14 for t in tri)
        assert all(math.isfinite(x) for p in pts for x in p)
        assert all(0<=v<=1 for layer in mesh.uv_layers for uv in layer.uv for v in uv.vector)
        bounds[name]=[(min(p[i] for p in pts),max(p[i] for p in pts)) for i in range(3)]
        points.extend(pts)
    assert bounds["PierDeck"][2][0]<-.0045 and bounds["OreSling"][2][0]>.0009
    assert .0099<bounds["PierDeck"][1][1]<.0105 and bounds["HarborHouse"][1][1]<=bounds["PierDeck"][1][1], bounds
    assert bounds["TradePlaque"][2][0]>.005 and bounds["TradePlaque"][1][1]<.005
    limits=[(min(p[k] for p in points),max(p[k] for p in points)) for k in range(3)]
    report={"Blender":bpy.app.version_string,"母版分件":source_count,"正式网格":len(parts),
            "最高精度三角面":sum(tris.values()),"零面积三角面":degenerate,
            "尺寸毫米":[round((b-a)*1000,3) for a,b in limits],"桩底相对静水面毫米":round((limits[2][0]+.0035)*1000,3),
            "后檐搭岸毫米":round((bounds["HarborHouse"][1][1]-.0099)*1000,3),
            "后栈板搭岸毫米":round((bounds["PierDeck"][1][1]-.0099)*1000,3)}
    bpy.ops.import_scene.fbx(filepath=str(MODEL/"ore-selection.fbx"),use_anim=False)
    proxies=[scene.objects[key] for key in ("PickVolume","ShoreFootprint")]
    for o in proxies: o.data.calc_loop_triangles()
    report["拾取及平面轮廓三角面"]=[len(o.data.loop_triangles) for o in proxies]
    for o in proxies: bpy.data.objects.remove(o,do_unlink=True)
    (PREVIEW/"模型回读.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
    if view=="回读": return report
    scene.render.engine="CYCLES"; scene.cycles.samples=24; scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    # 在有计算设备的工作站优先使用 GPU。无独显时按同一取景在 CPU 渲染。
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for backend in ("OPTIX","CUDA","HIP","METAL"):
        try:
            prefs.compute_device_type=backend; prefs.refresh_devices()
            found=[d for d in prefs.devices if d.type==backend]
            if found:
                for d in prefs.devices: d.use=d.type==backend
                scene.cycles.device="GPU"
                break
        except (TypeError,RuntimeError): pass
    world=bpy.data.worlds.new("漫射天光"); world.use_nodes=True; scene.world=world
    bg=world.node_tree.nodes.get("Background"); bg.inputs[0].default_value=(.83,.89,1,1); bg.inputs[1].default_value=.36
    floor=bpy.data.meshes.new("摄影纸"); floor.from_pydata([(-1,-1,-.00350),(1,-1,-.00350),(1,1,-.00350),(-1,1,-.00350)],[],[(0,1,2,3)])
    obj=bpy.data.objects.new("摄影承影面",floor); scene.collection.objects.link(obj)
    paper=bpy.data.materials.new("浅暖灰"); paper.diffuse_color=(.65,.62,.57,1); floor.materials.append(paper)
    aim=V((0,.002,.0046))
    def lamp(name,loc,power,size,color):
        data=bpy.data.lights.new(name,"AREA"); data.energy=power; data.shape="DISK"; data.size=size; data.color=color
        ob=bpy.data.objects.new(name,data); scene.collection.objects.link(ob); ob.location=loc
        ob.rotation_euler=(aim-ob.location).to_track_quat("-Z","Y").to_euler()
    lamp("左前大窗",(-.035,-.049,.061),.105,.039,(1,.90,.77))
    lamp("冷天光",(.054,-.022,.025),.047,.052,(.79,.89,1))
    lamp("檐后高光",(.021,.041,.067),.102,.035,(1,.93,.78))
    eye=V(VIEWS[view]).normalized()
    rot=Quaternion((1,0,0,0)) if view=="俯视" else (-eye).to_track_quat("-Z","Y")
    right,up=(rot@V(a) for a in ((1,0,0),(0,1,0)))
    visible=[p for p in points if p.z>=-.00356]
    xr=[p.dot(right) for p in visible]; yr=[p.dot(up) for p in visible]
    target=right*((min(xr)+max(xr))/2)+up*((min(yr)+max(yr))/2)+eye*aim.dot(eye)
    cam=bpy.data.cameras.new("材质正交摄影"); cam.type="ORTHO"; cam.clip_start=.0001
    cam.ortho_scale=max(max(xr)-min(xr),(max(yr)-min(yr))*1.26)*1.13
    ob=bpy.data.objects.new(cam.name,cam); scene.collection.objects.link(ob); scene.camera=ob
    ob.location=target+eye*.14; ob.rotation_euler=rot.to_euler()
    scene.render.resolution_x,scene.render.resolution_y=1280,1016
    scene.render.resolution_percentage=100
    scene.view_settings.view_transform="AgX"; scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.filepath=str(PREVIEW/(view+".png"))
    bpy.ops.render.render(write_still=True)
    if view=="俯视" and all((PREVIEW/(s+".png")).is_file() for s in VIEWS): sheet()
    return {"机位":view,**report}

result=inspect(globals().get("view_name","海侧斜视"))
