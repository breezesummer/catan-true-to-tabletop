"""在固定镜头和布光下，从回读的棋子拍摄三人肖像与三种面料。"""
from math import radians,sin,cos
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
from mathutils import Vector,Matrix
from sculpt import P


# 中心使用原型彩稿的像素坐标。每幅按相同的宽高比截取，便于直接比对。
STUDIES=(
    ("负袋壮汉",(279,116,52),97),
    ("兜帽首领",(394,169,0),93),
    ("望风斥候",(495,152,67),93),
    ("粗呢与卷袖",(218,188,52),98),
    ("蓝灰披肩",(492,208,53),93),
    ("兜帽与亚麻",(351,244,-17),108),
)


def check_portraits(source):
    import bmesh
    from head_form import head_template
    report={}
    native=head_template()
    for title in ("负袋壮汉","兜帽首领","望风斥候"):
        obj=source.objects[title+" · 连续头颈"]
        mesh=obj.data
        assert len(mesh.vertices)==len(native["vertices"]) and len(mesh.polygons)==len(native["faces"])
        assert any(m.type=="SUBSURF" and m.levels==1 for m in obj.modifiers)
        bm=bmesh.new();bm.from_mesh(mesh)
        unseen=set(bm.verts)
        first=unseen.pop()
        frontier=[first]
        while frontier:
            vertex=frontier.pop()
            for edge in vertex.link_edges:
                other=edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other);frontier.append(other)
        assert not unseen,title+"头颈断开"
        floor=max(v.co.z for edge in bm.edges if edge.is_boundary for v in edge.verts)
        assert floor<min(v.co.z for v in bm.verts)+.0019,title+"颊颈交界有开边"
        bm.free()
        # 同一分件中，两只眼球均由环状四边面连至极区封口。
        details=source.objects[title+" · 头颈与肖像"].data
        assert sum(p.loop_total==40 for p in details.polygons)==4
        report[title]={"头颈连通体":1,"头颈四边面":len(mesh.polygons),"眼球":2}
    return report


def photograph_portraits(scene,source,destination):
    import bpy
    destination.mkdir(parents=True,exist_ok=True)
    center=Vector(source["原型中心"])
    scale=source["原型比例"]
    scene.render.resolution_x=512
    scene.render.resolution_y=384
    scene.cycles.samples=48
    camera=scene.camera
    a,e=radians(0),radians(13)
    direction=Vector((sin(a)*cos(e),-cos(a)*cos(e),sin(e)))
    right=Vector((cos(a),sin(a),0))
    up=direction.cross(right)
    camera.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
    for title,coords,width in STUDIES:
        camera.location=(P(*coords)-center)*scale+.08*direction
        camera.data.ortho_scale=width*scale
        scene.render.filepath=str(destination/(title+".png"))
        bpy.ops.render.render(write_still=True)


def portrait_plates(reference,current,destination,previous=None):
    ref=Image.open(reference).convert("RGB")
    fonts=(Path("C:/Windows/Fonts/msyh.ttc"),Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"))
    path=next((p for p in fonts if p.is_file()),None)
    font=ImageFont.truetype(str(path),22) if path else ImageFont.load_default()
    headings=("原型","上一版","本次模型") if previous else ("原型","模型")
    w,h=368,276
    for studies,name in ((STUDIES[:3],"三人头颈对照.jpg"),(STUDIES[3:],"衣料同光对照.jpg")):
        page=Image.new("RGB",(20+len(headings)*(w+8),48+3*(h+39)+12),(248,245,239))
        pen=ImageDraw.Draw(page)
        for j,title in enumerate(headings):
            pen.text((14+j*(w+8)+w/2,24),title,fill=(71,56,43),anchor="mm",font=font)
        for i,(title,(x,y,_),span) in enumerate(studies):
            top=48+i*(h+39)
            pen.text((14,top+16),title,fill=(71,56,43),anchor="lm",font=font)
            top+=35
            half=span*.5
            box=(x-half,y-.75*half,x+half,y+.75*half)
            page.paste(ref.transform((w,h),Image.Transform.EXTENT,box,Image.Resampling.BICUBIC),(14,top))
            directories=(previous,current) if previous else (current,)
            for j,folder in enumerate(directories,1):
                with Image.open(folder/(title+".png")) as image:
                    page.paste(image.convert("RGB").resize((w,h),Image.Resampling.LANCZOS),(14+j*(w+8),top))
        page.save(destination/name,quality=95,subsampling=0)
