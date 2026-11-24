"""用全城几何计算局部环境可见度。第二组 UV 直接存两个标量，运行时无需采样阴影图。"""
from math import cos, exp, pi, sin, sqrt

from mathutils import Vector
from mathutils.bvhtree import BVHTree


def bake_exposure(objects):
    vertices,polygons=[],[]
    for obj in objects:
        base=len(vertices)
        mesh=obj.data
        mesh.calc_loop_triangles()
        vertices.extend(v.co.copy() for v in mesh.vertices)
        polygons.extend([base+i for i in tri.vertices] for tri in mesh.loop_triangles)
    tree=BVHTree.FromPolygons(vertices,polygons,all_triangles=True)
    directions=[]
    for k in range(28):
        radius=sqrt((k+.5)/28)
        az=k*pi*(3-sqrt(5))
        directions.append((radius*cos(az),radius*sin(az),sqrt(1-radius*radius)))
    span=.0027
    for obj in objects:
        mesh=obj.data
        layer=mesh.uv_layers.new(name="天光可见度与石脚风化",do_init=False)
        cache={}
        for face in mesh.polygons:
            n=face.normal
            if n.length_squared<.5:
                continue
            a=n.cross(Vector((0,0,1)) if abs(n.z)<.80 else Vector((0,1,0))).normalized()
            b=n.cross(a)
            rays=[a*x+b*y+n*z for x,y,z in directions]
            normal_key=tuple(round(c,3) for c in n)
            for loop in face.loop_indices:
                index=mesh.loops[loop].vertex_index
                key=(index,normal_key)
                if key not in cache:
                    p=mesh.vertices[index].co
                    origin=p+n*.000012
                    hits=0
                    for ray in rays:
                        _,_,_,distance=tree.ray_cast(origin,ray,span)
                        if distance is not None:
                            hits+=(1-distance/span)**.65
                    occlusion=hits/len(rays)
                    tide=exp(-max(p.z,0)/.00080)*(.77+.23*sin(p.x*1950+p.y*1160))
                    cache[key]=(max(.025,1-occlusion),min(1,.65*tide+.29*occlusion))
                layer.data[loop].uv=cache[key]
        mesh.uv_layers.active_index=0
        mesh.uv_layers[0].active_render=True
