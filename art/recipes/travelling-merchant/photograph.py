"""回读棋盘 FBX，在隔离的摄影场景中检查网格、材质和四个机位。"""
from math import radians,cos,sin
from pathlib import Path
import json
import sys
from tempfile import TemporaryDirectory
import bpy
from mathutils import Vector,Matrix

sys.path.insert(0,str(Path(__file__).resolve().parent))
from surfaces import ART,HEIGHT,ROOT,SOURCE,VIEWS
from merchant_hands import OFFER,HOLD
from proof_sheet import compose, hand_sheet
from hand_review import source_hands,runtime_hands


def photograph(views,art,source,output,quick=False):
    bpy.ops.wm.open_mainfile(filepath=str(source),load_ui=False)
    scene=bpy.context.scene
    checked,seeds,unit=source_hands(scene)
    hand_points={}
    for part in (OFFER,HOLD):
        obj=scene.objects[part]
        hand_points[part]=[obj.matrix_world@v.co for v in obj.data.vertices if v.groups]
    material=bpy.data.materials["PeddlerSurface"]
    material.use_fake_user=True
    for o in list(scene.objects): bpy.data.objects.remove(o,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(art/"peddler-stall.fbx"),use_anim=False)
    pieces=[o for o in scene.objects if o.type=="MESH"]
    if len(pieces)!=1: raise ValueError("棋盘 FBX 应只有一个网格")
    obj=pieces[0]
    runtime_hands(obj,checked,seeds,unit)
    if len(obj.data.materials)!=1 or not obj.data.materials[0].name.startswith("PeddlerSurface"):
        raise ValueError("导出材质槽与商人材质映射不符")
    obj.data.materials[0]=material
    coords=[obj.matrix_world@pt.co for pt in obj.data.vertices]
    lo=Vector([min(p[i] for p in coords) for i in range(3)])
    hi=Vector([max(p[i] for p in coords) for i in range(3)])
    if abs(hi.z-HEIGHT)>2e-6 or abs(lo.z)>1e-6: raise ValueError("导出后靴底或帽顶高度变化")
    soles=sorted(p.x for p in coords if p.z-lo.z<.000015)
    gap=max(b-a for a,b in zip(soles,soles[1:]))
    if gap<.00055 or not soles[0]<0<soles[-1]:
        raise ValueError("双靴底部应有两个独立的落地区域")
    output.mkdir(parents=True,exist_ok=True)
    scene.render.engine="CYCLES"
    scene.cycles.use_denoising=True
    scene.cycles.max_bounces=5
    prefs=bpy.context.preferences.addons["cycles"].preferences
    for backend in ("HIP","OPTIX","METAL"):
        try:
            prefs.compute_device_type=backend
            prefs.refresh_devices()
            if any(d.type==backend for d in prefs.devices):
                for d in prefs.devices: d.use=d.type==backend
                scene.cycles.device="GPU"
                break
        except (RuntimeError,TypeError): continue
    world=bpy.data.worlds.new("暖灰摄影棚")
    world.use_nodes=True
    world.node_tree.nodes["Background"].inputs[0].default_value=(.76,.82,.90,1)
    world.node_tree.nodes["Background"].inputs[1].default_value=.22
    scene.world=world
    scene.view_settings.view_transform="AgX"
    scene.view_settings.look="AgX - Medium High Contrast"
    scene.render.image_settings.file_format="PNG"
    scene.render.image_settings.color_mode="RGBA"
    scene.render.resolution_percentage=100
    aim=Vector((0,0,.0082))
    for name,loc,energy,size,color in (
        ("左侧柔光",(-.029,-.044,.052),.033,.031,(1,.90,.81)),
        ("右侧反光板",(.034,-.026,.028),.009,.038,(.86,.91,1)),
        ("帽沿轮廓光",(.011,.036,.044),.032,.032,(1,.90,.80))):
        data=bpy.data.lights.new(name,"AREA")
        data.energy,data.shape,data.size,data.color=energy,"DISK",size,color
        light=bpy.data.objects.new(name,data)
        scene.collection.objects.link(light)
        light.location=loc
        light.rotation_euler=(aim-light.location).to_track_quat("-Z","Y").to_euler()
    camera=bpy.data.cameras.new("收藏棋子正交镜头")
    camera.type,camera.clip_start,camera.clip_end="ORTHO",.00005,1
    cam=bpy.data.objects.new("摄影机",camera)
    scene.collection.objects.link(cam)
    scene.camera=cam
    floor=bpy.data.meshes.new("接影纸")
    floor.from_pydata([(-1,-1,-.000002),(1,-1,-.000002),(1,1,-.000002),(-1,1,-.000002)],[],[(0,1,2,3)])
    paper=bpy.data.objects.new("摄影棚地面",floor)
    scene.collection.objects.link(paper)
    paper.is_shadow_catcher=True
    scene.render.film_transparent=True
    shots={"试形":(0,4,560,760,10), "商人三分之四":(-7,8,990,1300,64),
           "正面":(22,4,720,1000,44), "侧面":(100,4,720,1000,44),
           "背面":(201,5,720,1000,44), "俯视":(14,74,720,720,36),
           "面部与衣料":(-3,3,920,900,56),
           "摊掌原型角":(0,4,610,480,40),"摊掌转面":(-30,32,610,480,40),
           "扶卷原型角":(0,4,610,480,40),"扶卷转面":(40,16,610,480,40)}
    hand_views={"摊掌原型角":OFFER,"摊掌转面":OFFER,"扶卷原型角":HOLD,"扶卷转面":HOLD}
    requested=[v for v in views if v!="双手原型对照"]
    if "双手原型对照" in views: requested+=list(hand_views)
    def take(title,destination):
        a,b,w,h,n=shots[title]
        az,el=radians(a),radians(b)
        view=Vector((sin(az)*cos(el),-cos(az)*cos(el),sin(el)))
        right=Vector((cos(az),sin(az),0))
        up=view.cross(right)
        points=hand_points[hand_views[title]] if title in hand_views else coords
        proj=[[p.dot(v) for p in points] for v in (right,up)]
        centre=right*(max(proj[0])+min(proj[0]))*.5+up*(max(proj[1])+min(proj[1]))*.5
        scale=max((max(proj[0])-min(proj[0]))*max(1,h/w),
                  (max(proj[1])-min(proj[1]))*max(1,w/h))*1.115
        if title=="面部与衣料":
            centre=right*.00015+up*.0127
            scale=.0065
        elif title in hand_views:
            scale*=1.26
        cam.location=centre+view*.14
        cam.rotation_euler=Matrix((right,up,view)).transposed().to_euler()
        camera.ortho_scale=scale
        scene.render.resolution_x,scene.render.resolution_y=w,h
        scene.cycles.samples=min(12,n) if quick else n
        scene.render.filepath=str(destination/(title+".png"))
        bpy.ops.render.render(write_still=True)
    with TemporaryDirectory(dir=ROOT/".local") as temp:
        for title in dict.fromkeys(requested):
            take(title,Path(temp) if title in hand_views and "双手原型对照" in views else output)
        if "双手原型对照" in views: hand_sheet(Path(temp),output)
    obj.data.calc_loop_triangles()
    result={"Blender":bpy.app.version_string,"回读三角面":len(obj.data.loop_triangles),
            "网格":len(pieces),"材质槽":len(obj.data.materials),"底面毫米":round(lo.z*1000,5),
            "双靴底部间距毫米":round(gap*1000,4),
            "宽深高毫米":[round(v*1000,4) for v in hi-lo],"视图":views,"设备":scene.cycles.device,
            "双手":checked}
    if {"商人三分之四","正面","侧面","背面"}.issubset(views):
        compose(output)
        (output/"模型回读.json").write_text(json.dumps(result,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    return result


result=photograph(globals().get("views",["商人三分之四","正面","侧面","背面","俯视","面部与衣料","双手原型对照"]),
    Path(globals().get("art_dir",ART)),Path(globals().get("source_file",SOURCE)),
    Path(globals().get("output_dir",VIEWS)),globals().get("quick",False))
