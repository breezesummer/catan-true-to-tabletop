"""检查六个腕掌的拓扑，用正式 FBX 在原型镜头和第二机位逐手拍摄。"""
from math import radians, sin, cos
from pathlib import Path
from tempfile import TemporaryDirectory
import bpy
import bmesh
from PIL import Image,ImageDraw,ImageFont
from mathutils import Vector,Matrix
from mathutils.bvhtree import BVHTree
from gestures import NAMES
from sculpt import P
from finishes import ROOT,REF,VIEWS


def check_hands(source):
    result={}
    for name in NAMES.values():
        obj,=[o for o in source.objects if o.name.endswith(" · "+name)]
        bm=bmesh.new()
        bm.from_mesh(obj.data)
        bm.verts.ensure_lookup_table()
        seen={bm.verts[0]}
        queue=list(seen)
        while queue:
            current=queue.pop()
            for edge in current.link_edges:
                neighbour=edge.other_vert(current)
                if neighbour not in seen:
                    seen.add(neighbour)
                    queue.append(neighbour)
        assert len(seen)==len(bm.verts) and all(e.is_manifold and e.is_contiguous for e in bm.edges),name
        assert len(bm.verts)-len(bm.edges)+len(bm.faces)==2 and bm.calc_volume(signed=True)>0,name
        assert any(m.type=="SUBSURF" and m.levels==1 for m in obj.modifiers),name
        digits={group.index for group in obj.vertex_groups if group.name in ("拇指","食指","中指","无名指","小指")}
        distinct={weight.group for v in obj.data.vertices for weight in v.groups if weight.weight>.80}
        assert len(digits&distinct)==5,name
        bm.faces.ensure_lookup_table()
        vertex_sets=[{v.index for v in f.verts} for f in bm.faces]
        tree=BVHTree.FromBMesh(bm)
        overlaps=sum(1 for a,b in tree.overlap(tree) if a<b and vertex_sets[a].isdisjoint(vertex_sets[b]))
        assert overlaps==0,name+"的手掌或前臂有自交"
        result[name]={"连通体":1,"开边":0,"指列":len(digits),"自交面组":overlaps}
        bm.free()
    return result


def photograph(scene,source):
    # 同一行左右照片使用相同的正交尺寸，保留指尖和道具之间的真实距离。
    gestures=[
        ("伸掌拦路",(361,222,-71),91,-29,16),
        ("托袋",(267,248,-9),73,-22,25),
        ("遮眉",(533,130,43),68,27,35),
        ("扶杖",(168,312,42),69,-35,19),
        ("垂刀",(461,342,-45),68,30,22),
        ("扶带",(493,282,39),66,30,25)]
    center=Vector(source["原型中心"]);scale=source["原型比例"]
    camera=scene.camera
    scene.render.resolution_x=460;scene.render.resolution_y=348
    scene.cycles.samples=56
    ref=Image.open(REF).convert("RGB")
    cellw,cellh=300,227
    margin,gap,label=18,6,35
    page=Image.new("RGB",(margin*2+cellw*3+gap*2,label+(label+cellh+gap)*len(gestures)+margin),(246,242,235))
    draw=ImageDraw.Draw(page)
    fonts=(Path("C:/Windows/Fonts/msyh.ttc"),Path("/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc"))
    font_path=next((p for p in fonts if p.exists()),None)
    font=ImageFont.truetype(str(font_path),18) if font_path else ImageFont.load_default()
    for n,title in enumerate(("原型","模型正面","模型转面")):
        draw.text((margin+(cellw+gap)*n+cellw/2,label/2),title,fill=(56,46,35),font=font,anchor="mm")
    with TemporaryDirectory(dir=ROOT/".local") as work:
        for i,(name,(x,row,depth),width,azimuth,elevation) in enumerate(gestures):
            y=label+i*(label+cellh+gap)
            draw.text((margin,y+label/2),name,fill=(56,46,35),font=font,anchor="lm")
            y+=label
            height=width*348/460
            reference=ref.transform((cellw,cellh),Image.Transform.EXTENT,
                (x-width/2,row-height/2,x+width/2,row+height/2),Image.Resampling.BICUBIC)
            page.paste(reference,(margin,y))
            focus=(P(x,row,depth)-center)*scale
            for j,(az,el) in enumerate(((0,13),(azimuth,elevation))):
                a,e=radians(az),radians(el)
                direction=Vector((sin(a)*cos(e),-cos(a)*cos(e),sin(e)))
                right=Vector((cos(a),sin(a),0));up=direction.cross(right)
                camera.location=focus+.08*direction
                camera.rotation_euler=Matrix((right,up,direction)).transposed().to_euler()
                camera.data.ortho_scale=width*scale
                path=Path(work)/f"{i}-{j}.png"
                scene.render.filepath=str(path);bpy.ops.render.render(write_still=True)
                with Image.open(path) as img:
                    page.paste(img.resize((cellw,cellh),Image.Resampling.LANCZOS),(margin+(cellw+gap)*(j+1),y))
    page.save(VIEWS/"六手原型与转面对照.jpg",quality=95,subsampling=0)
