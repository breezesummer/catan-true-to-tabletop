"""核对两只手的连续性、甲床、指尖落点，以及导出后是否仍为独立的完整手形。"""
from collections import deque
import bmesh
import bpy
from mathutils import Matrix,Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

from merchant_hands import DIGITS,HOLD,NAIL_FACES,OFFER
from surfaces import atlas_uv


def topology(bm):
    bm.faces.ensure_lookup_table()
    bm.verts.ensure_lookup_table()
    faces=[{v.index for v in face.verts} for face in bm.faces]
    tree=BVHTree.FromBMesh(bm)
    crossed=sum(a<b and faces[a].isdisjoint(faces[b]) for a,b in tree.overlap(tree))
    unseen={v.index for v in bm.verts}
    components=0
    while unseen:
        components+=1
        queue=[bm.verts[unseen.pop()]]
        while queue:
            v=queue.pop()
            for edge in v.link_edges:
                other=edge.other_vert(v)
                if other.index in unseen:
                    unseen.remove(other.index);queue.append(other)
    return {"连通体":components,"非流形边":sum(not e.is_manifold or not e.is_contiguous for e in bm.edges),
            "自交面组":crossed,"欧拉数":len(bm.verts)-len(bm.edges)+len(bm.faces),
            "朝外":bm.calc_volume(signed=True)>0}


def require_hand(label,stats):
    if stats!={"连通体":1,"非流形边":0,"自交面组":0,"欧拉数":2,"朝外":True}:
        raise ValueError(f"{label}的掌指表面异常：{stats}")


def source_hands(scene):
    scale=scene["原型比例"]
    center=Vector(scene["原型中心"])
    cloth=scene.objects["14 茜红条纹羊毛布卷"].data
    roll=BVHTree.FromPolygons([v.co/scale+center for v in cloth.vertices],
                              [tuple(f.vertices) for f in cloth.polygons])
    report,seeds={},{}
    for name in (OFFER,HOLD):
        obj=scene.objects[name]
        checks={}
        for stage,evaluated in (("制作基网",False),("细分表面",True)):
            mesh=(obj.evaluated_get(bpy.context.evaluated_depsgraph_get()) if evaluated else obj).data
            bm=bmesh.new()
            bm.from_mesh(mesh)
            bm.transform(Matrix.Scale(1/scale,4))
            bm.normal_update()
            checks[stage]=topology(bm)
            bm.free()
            require_hand(name+stage,checks[stage])
        seeds[name]=[]
        contact={}
        for i,digit in enumerate(DIGITS):
            group=obj.vertex_groups[digit].index
            if not any(any(w.group==group and w.weight>.85 for w in v.groups) for v in obj.data.vertices):
                raise ValueError(name+digit+"缺少指节权重")
            center_face=obj.data.polygons[NAIL_FACES[i][0]].center
            seeds[name].append(obj.matrix_world@center_face)
            if name==HOLD:
                fingertip=sum((obj.data.polygons[k].center for k in NAIL_FACES[i]),Vector())/len(NAIL_FACES[i])
                pad=[v.co/scale+center for v in mesh.vertices if any(w.group==group and w.weight>.65 for w in v.groups)
                     and (v.co-fingertip).length/scale<12]
                distance=min(roll.find_nearest(p)[3] for p in pad)*scale*1000
                if distance>.04: raise ValueError(f"扶卷手的{digit}悬空 {distance:.3f} 毫米")
                contact[digit]=round(distance,4)
        checks["指列"]=list(DIGITS)
        if contact:
            checks["指尖至布最近距离毫米"]=contact
            palm=BVHTree.FromPolygons([v.co/scale+center for v in mesh.vertices],
                                      [tuple(f.vertices) for f in mesh.polygons])
            intersections=len(palm.overlap(roll))
            if intersections:raise ValueError(f"扶卷手与羊毛布卷相交：{intersections} 对面")
            checks["细分手与布卷相交面组"]=intersections
        report[name]=checks
    return report,seeds,scale


def runtime_hands(obj,check,seeds,scale):
    mesh=obj.data
    vertices=[obj.matrix_world@v.co for v in mesh.vertices]
    kd=KDTree(len(vertices))
    for i,p in enumerate(vertices): kd.insert(p,i)
    kd.balance()
    neighbours=[[] for _ in vertices]
    for edge in mesh.edges:
        a,b=edge.vertices;neighbours[a].append(b);neighbours[b].append(a)
    for name,points in seeds.items():
        # 五个原甲床在 FBX 内必须仍通向同一个腕口。
        start=[kd.find(p)[1] for p in points]
        component={start[0]};queue=deque(component)
        while queue:
            for n in neighbours[queue.popleft()]:
                if n not in component:component.add(n);queue.append(n)
        if not set(start)<=component:raise ValueError(name+"有手指在减面后断开")
        bm=bmesh.new()
        verts={i:bm.verts.new(vertices[i]/scale) for i in sorted(component)}
        for face in mesh.polygons:
            if face.vertices[0] in component: bm.faces.new([verts[i] for i in face.vertices])
        bm.verts.index_update();bm.faces.index_update();bm.normal_update()
        stats=topology(bm)
        require_hand(name+"游戏网格",stats)
        check[name]["游戏网格"]=stats
        check[name]["游戏三角面"]=len(bm.faces)
        if name==HOLD:
            # FBX 已经合批，用布面及毛毡包边的 UV 裁片取回实际游戏三角面。
            rects=[(atlas_uv(key,0,0),atlas_uv(key,1,1)) for key in ("carpet","felt")]
            layer=mesh.uv_layers.active.data
            cloth=[]
            for f in mesh.polygons:
                u,v=layer[f.loop_start].uv
                if any(a[0]-.00001<=u<=b[0]+.00001 and a[1]-.00001<=v<=b[1]+.00001 for a,b in rects):
                    cloth.append(list(f.vertices))
            if not cloth:raise ValueError("FBX 缺少布卷曲面")
            roll=BVHTree.FromPolygons([v/scale for v in vertices],cloth)
            overlaps=len(BVHTree.FromBMesh(bm).overlap(roll))
            if overlaps:raise ValueError(f"游戏扶卷手与布卷相交：{overlaps} 对面")
            check[name]["游戏手与布卷相交面组"]=overlaps
        bm.free()
