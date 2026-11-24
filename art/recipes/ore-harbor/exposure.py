"""整座港口在同一棵 BVH 上采样。第二套 UV 存局部天光可见度和潮湿程度。"""
from math import cos,pi,sin,sqrt
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def bake(objects):
    verts=[]; triangles=[]
    for obj in objects:
        mesh=obj.data; base=len(verts)
        mesh.calc_loop_triangles()
        verts.extend(v.co.copy() for v in mesh.vertices)
        triangles.extend(tuple(i+base for i in t.vertices) for t in mesh.loop_triangles)
    tree=BVHTree.FromPolygons(verts,triangles,all_triangles=True)
    count=20
    samples=[]
    for i in range(count):
        r=sqrt((i+.5)/count); theta=i*pi*(3-sqrt(5))
        samples.append((r*cos(theta),r*sin(theta),sqrt(1-r*r)))
    for obj in objects:
        mesh=obj.data
        layer=mesh.uv_layers.new(name="HarborExposure",do_init=False)
        cache={}
        for face in mesh.polygons:
            normal=face.normal
            if normal.length_squared<.5: continue
            a=normal.cross(Vector((0,0,1)) if abs(normal.z)<.8 else Vector((0,1,0))).normalized()
            b=normal.cross(a)
            rays=[a*x+b*y+normal*z for x,y,z in samples]
            normal_key=tuple(round(v,2) for v in normal)
            for loop in face.loop_indices:
                vi=mesh.loops[loop].vertex_index
                key=(vi,normal_key)
                if key not in cache:
                    p=mesh.vertices[vi].co; origin=p+normal*.000014; blocked=0.; norm=0.
                    for direction in rays:
                        # 顶部天空贡献较高，低仓和吊斗也会遮住旁边物体的入射光。
                        weight=.53+max(0,direction.z)*1.36
                        norm+=weight
                        dist=tree.ray_cast(origin,direction,.015)[3]
                        if dist is not None:
                            near=max(0,1-dist/.0026)
                            far=max(0,1-dist/.015)
                            blocked+=weight*(.40*near+.60*far**.52)
                    light=max(.055,1-blocked/norm)
                    tide=max(0,min(1,(-p.z-.00148+.00018*sin(p.x*1270+p.y*747))/.00193))
                    cache[key]=(light,tide*tide*(3-2*tide))
                layer.data[loop].uv=cache[key]
        mesh.uv_layers.active_index=0
        mesh.uv_layers[0].active_render=True
