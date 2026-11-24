"""建筑面、线脚与板瓦用的定向 UV 网格。构造坐标保留到装配完成再换算毫米。"""
from collections import defaultdict
from math import ceil, cos, floor, pi, sin, tau

import bpy
from mathutils import Vector, geometry

from surfaces import REGIONS

UP = Vector((0,0,1))


def clip(poly, axis, bound, sign):
    output = []
    previous = poly[-1]
    for current in poly:
        a = (previous[1][axis]-bound)*sign
        b = (current[1][axis]-bound)*sign
        if (a>=0)!=(b>=0):
            t = a/(a-b)
            output.append((previous[0].lerp(current[0],t),previous[1].lerp(current[1],t)))
        if b>=0:
            output.append(current)
        previous = current
    return output


class Frame:
    """立面横向朝右，深度正向指向建筑外侧。"""
    def __init__(self,x,y,nx,ny):
        self.origin = Vector((x,y,0))
        self.n = Vector((nx,ny,0)).normalized()
        self.t = UP.cross(self.n)

    def at(self,u,z,d=0):
        return self.origin+self.t*u+UP*z+self.n*d


class Mason:
    def __init__(self, regions=REGIONS):
        self.regions = regions
        self.name = "中殿"
        self.groups = defaultdict(lambda: ([],{},[],[],[]))

    def face(self, points, swatch, coords=None, outward=None):
        points = [Vector(p) for p in points]
        if len(points)<3:
            return
        normal = geometry.normal(points)
        if normal.length_squared < .5:
            return
        if coords is None:
            # 石材卧缝始终水平。瓦片由屋面单独提供顺坡坐标。
            if abs(normal.z)>.75:
                raw = [(p.x,p.y) for p in points]
            else:
                tangent = UP.cross(normal).normalized()
                raw = [(p.dot(tangent),p.z) for p in points]
            period = {"ashlar":2.58,"foot":2.1,"slate":4.08,"glass":1.7,
                      "carving":1.55,"oak":1.4}.get(swatch,1.85)
            coords = [(u/period+.173,v/period+.071) for u,v in raw]
        pairs = list(zip(points,map(Vector,coords)))
        if outward is not None and normal.dot(outward)<0:
            pairs.reverse()
        uv = [p[1] for p in pairs]
        lo = [floor(min(v[i] for v in uv)+1e-7) for i in range(2)]
        hi = [max(lo[i],ceil(max(v[i] for v in uv)-1e-7)-1) for i in range(2)]
        left,top,size = self.regions[swatch]
        verts,lookup,faces,uvs,slots = self.groups[self.name]
        for j in range(lo[1],hi[1]+1):
            for i in range(lo[0],hi[0]+1):
                fragment = pairs
                for axis,span,index in ((0,(lo[0],hi[0]),i),(1,(lo[1],hi[1]),j)):
                    if span[0]!=span[1] and fragment:
                        fragment = clip(fragment,axis,index,1)
                        if fragment:
                            fragment = clip(fragment,axis,index+1,-1)
                indices,tex = [],[]
                for p,coord in fragment:
                    key = tuple(round(c,6) for c in p)
                    vert = lookup.get(key)
                    if vert is None:
                        vert = lookup[key] = len(verts)
                        verts.append(key)
                    if vert in indices:
                        continue
                    indices.append(vert)
                    u,v = coord
                    tex.append(((left+3+(size-6)*(u-i))/2048,
                                1-(top+size-3-(size-6)*(v-j))/2048))
                if len(indices)>2:
                    faces.append(indices)
                    uvs.append(tex)
                    slots.append(int(swatch=="slate"))

    def extrude(self, front, delta, swatch):
        front = list(map(Vector,front))
        delta = Vector(delta)
        if geometry.normal(front).dot(delta)>0:
            front.reverse()
        back = [p+delta for p in front]
        self.face(front,swatch)
        self.face(back[::-1],swatch)
        for i in range(len(front)):
            j=(i+1)%len(front)
            self.face([front[i],back[i],back[j],front[j]],swatch)

    def band(self, frame, a, b, z0, z1, d0, d1, swatch="carving"):
        self.extrude([frame.at(u,z,d1) for u,z in ((a,z0),(b,z0),(b,z1),(a,z1))],
                     frame.n*(d0-d1),swatch)

    def rod(self, start, end, radius, swatch="lead", sides=6, end_radius=None):
        p,q = Vector(start),Vector(end)
        axis=(q-p).normalized()
        if axis.length_squared<.5:
            return
        a=axis.cross(UP if abs(axis.z)<.9 else Vector((0,1,0))).normalized()
        b=axis.cross(a)
        end_radius=radius if end_radius is None else end_radius
        ring=[a*cos(tau*i/sides)+b*sin(tau*i/sides) for i in range(sides)]
        low=[p+radius*v for v in ring]
        if end_radius < .0001:
            for i in range(sides):
                self.face([low[i],low[(i+1)%sides],q],swatch)
        else:
            high=[q+end_radius*v for v in ring]
            self.face(high,swatch)
            for i in range(sides):
                j=(i+1)%sides
                self.face([low[i],low[j],high[j],high[i]],swatch)
        self.face(low[::-1],swatch)

    def turned(self,x,y,profile,sides,swatch,angle=pi/8):
        rings=[[Vector((x+r*cos(angle+tau*i/sides),y+r*sin(angle+tau*i/sides),z))
                for i in range(sides)] for z,r in profile]
        self.face(rings[0][::-1],swatch)
        for low,high in zip(rings,rings[1:]):
            for i in range(sides):
                j=(i+1)%sides
                self.face([low[i],low[j],high[j],high[i]],swatch)
        self.face(rings[-1],swatch)

    def export_parts(self, scene, materials, span=.0205):
        samples=[p for verts,_,_,_,_ in self.groups.values() for p in verts]
        low=Vector(tuple(min(p[k] for p in samples) for k in range(3)))
        high=Vector(tuple(max(p[k] for p in samples) for k in range(3)))
        factor=span/max(high.x-low.x,high.y-low.y)
        origin=Vector(((low.x+high.x)/2,(low.y+high.y)/2,low.z))
        objects=[]
        for name,(verts,_,faces,coords,slots) in self.groups.items():
            mesh=bpy.data.meshes.new(name)
            mesh.from_pydata([(Vector(p)-origin)*factor for p in verts],[],faces)
            for m in materials:
                mesh.materials.append(m)
            mesh.update()
            layer=mesh.uv_layers.new(name="建筑定向 UV")
            for poly,uv,slot in zip(mesh.polygons,coords,slots):
                poly.material_index=slot
                for loop,coord in zip(poly.loop_indices,uv):
                    layer.data[loop].uv=coord
            obj=bpy.data.objects.new(name,mesh)
            scene.collection.objects.link(obj)
            objects.append(obj)
        return objects
