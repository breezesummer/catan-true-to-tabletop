"""通过 Blender MCP 将正式 FBX 读入独立场景，核对实际导出坐标、材质槽和 UV。"""
from pathlib import Path
import math

import bpy
import numpy as np
from mathutils import Vector

root = Path(__file__).resolve().parents[3]
art = root/"Unity/Catan/Assets/Art/NaturalPieces/v001"
scene = bpy.data.scenes.new("圆门村庄 FBX 回读")
bpy.context.window.scene = scene
bpy.ops.import_scene.fbx(filepath=str(art/"village.fbx"),use_anim=False)
objects = list(scene.objects)
assert len(objects)==1 and objects[0].type=="MESH","导出必须只有建筑网格"
obj = objects[0]
mesh = obj.data
mesh.calc_loop_triangles()
points = [obj.matrix_world @ v.co for v in mesh.vertices]
lo = Vector(tuple(min(p[k] for p in points) for k in range(3)))
hi = Vector(tuple(max(p[k] for p in points) for k in range(3)))
assert abs(hi.x-lo.x-.018)<1e-6 and abs(lo.z)<1e-7
assert abs(lo.x+hi.x)<1e-6 and abs(lo.y+hi.y)<1e-6
assert len(mesh.uv_layers)==2 and len(obj.material_slots)==2
uv = mesh.uv_layers[0].data
contact_uv = mesh.uv_layers[1].data
for loop in uv:
    assert all(math.isfinite(k) and 0<k<1 for k in loop.uv)
for loop in contact_uv:
    assert all(math.isfinite(k) and 0<=k<=1 for k in loop.uv)
occupied = set()
smallest = float("inf")
coverage = np.zeros((512,512),dtype=np.uint16)
for tri in mesh.loop_triangles:
    p,q,r = [points[i] for i in tri.vertices]
    area = (q-p).cross(r-p).length/2
    assert math.isfinite(area) and area>1e-16,"导出含退化三角面"
    smallest = min(smallest,area)
    roof = "Player" in mesh.materials[tri.material_index].name
    for i in tri.loops:
        u,v = uv[i].uv
        assert roof==(int(4*v)==1),"陶瓦席位色越出材质图集分区"
        occupied.add((int(4*u),int(4*v)))
    # 以像素中心采样，剔除共边；计数能抓出多个分件各自占满整张图的错误。
    a,b,c = (np.array(contact_uv[i].uv)*512 for i in tri.loops)
    ix,iy = np.maximum(0,np.ceil(np.minimum(np.minimum(a,b),c)-.5)).astype(int)
    jx,jy = np.minimum(511,np.floor(np.maximum(np.maximum(a,b),c)-.5)).astype(int)
    if jx<ix or jy<iy:
        continue
    bx,by = b-a
    cx,cy = c-a
    det = bx*cy-by*cx
    if abs(det)<1e-9:
        continue
    sy,sx = np.ogrid[iy:jy+1,ix:jx+1]
    px,py = sx+.5-a[0],sy+.5-a[1]
    s,t = (px*cy-py*cx)/det,(py*bx-px*by)/det
    coverage[iy:jy+1,ix:jx+1] += (s>1e-6)&(t>1e-6)&(s+t<1-1e-6)
overlap = np.count_nonzero(coverage>1)/max(1,np.count_nonzero(coverage))
assert overlap<.003 and np.count_nonzero(coverage)>.18*coverage.size,("整屋 UV 展开异常",overlap)
path = root/"art/source/round-door-village/圆门村庄.blend"
if Path(bpy.data.filepath).resolve()==path:
    source = bpy.data.scenes["圆门村庄"]
else:
    with bpy.data.libraries.load(str(path)) as (src,dst):
        dst.scenes=[src.scenes[0]]
    source = dst.scenes[0]
assert all(o.type=="MESH" for o in source.objects),"源场景中混入摄影棚"
maps = {n.image for o in source.objects for m in o.data.materials for n in m.node_tree.nodes
        if n.type=="TEX_IMAGE"}
assert len(maps)==5 and all(img.packed_file for img in maps),"源文件未内嵌所有表面图像"
contact = next(img for img in maps if img.name.startswith("village-contact"))
pixels = np.empty(len(contact.pixels),dtype=np.float32)
contact.pixels.foreach_get(pixels)
w,h = contact.size
rgb = pixels.reshape(h,w,-1)
signals = {}
for part in source.objects:
    part.data.calc_loop_triangles()
    layer = part.data.uv_layers[1]
    locations = np.array([[sum(layer.data[i].uv[k] for i in tri.loops)/3 for k in range(2)]
                          for tri in part.data.loop_triangles])
    xy = np.clip((locations*[w,h]).astype(int),0,[w-1,h-1])
    sample = rgb[xy[:,1],xy[:,0],:3]
    signals[part.name.split(".")[0]] = sample
assert np.max(signals["烟囱与泛水"][:,0])>.65 and np.max(signals["双扇圆门"][:,0])>.35,"烟口或门脚的磨损没有烘入"
assert np.max(signals["锻铁与门扣"][:,2])>.75 and np.max(signals["主屋搭接陶瓦"][:,2])<.15,"抛光区域未对齐铜扣"
assert np.percentile(signals["石灰墙体"][:,1],20)<.8 and np.percentile(signals["主屋搭接陶瓦"][:,1],85)>.82,"接触遮蔽缺少有效层次"
result = {"源场景分组数":len(source.objects),"网格数":len(objects),"三角面":len(mesh.loop_triangles),
          "宽深高毫米":[round(n*1000,3) for n in hi-lo],"底面毫米":round(lo.z*1000,5),
          "材质槽":len(mesh.materials),"UV色样数":len(occupied),"最小三角面平方毫米":smallest*1e6,
          "整屋UV覆盖率":round(np.count_nonzero(coverage)/coverage.size,4),"整屋UV重叠率":round(overlap,5),
          "源场景内嵌贴图数":len(maps)}
