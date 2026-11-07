"""港口货物的曲面、纹理坐标、接触遮蔽与轻量拾取网格。坐标单位为毫米。"""
import math
import random

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TAU=math.tau
UP=Vector((0,0,1))
BARK, CUT, CLAY, ORE, GRAIN, FLEECE, CORD, METAL, OAK, QUARTZ, WORN_IRON, CLEAVAGE=range(12)

def vec(p):
    return Vector(p)

def frame(tangent):
    d=vec(tangent).normalized()
    ref=vec((1,0,0)) if abs(d.x)<.88 else vec((0,1,0))
    u=(ref-d*ref.dot(d)).normalized()
    return u,d.cross(u)

class Sculpt:
    def __init__(self,proxy=False):
        self.proxy=proxy
        self.points=[]
        self.faces=[]
        self.uv=[]
        self.smooth=[]
        self.tint=[]
        self.wear=[]
        self.scale=1
        self.offset=vec((0,0,0))
        self.angle=0
        self.local_rotation=None
        self.local_anchor=None
        self.rng=random.Random(1911)

    def reset_transform(self,offset=(0,0,0),scale=1,yaw=0):
        self.offset=vec(offset)
        self.scale=scale
        self.angle=yaw

    def vertex(self,p):
        if self.local_rotation is not None:
            p=self.local_anchor+self.local_rotation@(vec(p)-self.local_anchor)
        x,y,z=p
        c,s=math.cos(self.angle),math.sin(self.angle)
        self.points.append(self.offset+vec((x*c-y*s,x*s+y*c,z))*self.scale)
        return len(self.points)-1

    def face(self,ids,uv,mat,tint,smooth=False,cut_variant=0,wear=.5):
        self.faces.append(tuple(ids))
        self.smooth.append(smooth)
        if self.proxy:
            return
        if mat==CUT:
            # 四个独立端木样本，留出边缘防止缩远时相邻图案串色。
            uv=[((cut_variant%2+.015+.970*u)*.5,(cut_variant//2+.015+.970*v)*.5) for u,v in uv]
        col,row=mat%4,2-mat//4
        self.uv.extend(((col+.018+.964*u)/4,(row+.018+.964*v)/3) for u,v in uv)
        self.tint.append(tint)
        self.wear.append(wear)

    def shell(self,layers,mat,tint=1,smooth=True,capmat=None,cap=True,wear=.5):
        # 每圈按从轴端向原点看的逆时针排列。侧壁 UV 的接缝独立于法线。
        count=len(layers[0])
        ids=[[self.vertex(v) for v in layer] for layer in layers]
        for j in range(len(layers)-1):
            v0,v1=j/(len(layers)-1),(j+1)/(len(layers)-1)
            for i in range(count):
                k=(i+1)%count;u0,u1=i/count,(i+1)/count
                self.face((ids[j][i],ids[j][k],ids[j+1][k],ids[j+1][i]),
                          ((u0,v0),(u1,v0),(u1,v1),(u0,v1)),mat,tint,smooth,wear=wear)
        if cap:
            uv=[(.5+.49*math.cos(TAU*i/count),.5+.49*math.sin(TAU*i/count)) for i in range(count)]
            self.face(ids[0][::-1],uv[::-1],mat if capmat is None else capmat,tint,wear=wear)
            self.face(ids[-1],uv,mat if capmat is None else capmat,tint,wear=wear)

    def tube(self,path,radii,mat,tint=1,sides=9,detail=False,flat=1):
        if self.proxy and detail:
            return
        path=[vec(p) for p in path]
        if isinstance(radii,(float,int)):
            radii=[radii]*len(path)
        sides=min(sides,8) if self.proxy else sides
        layers=[]
        u=None
        for i,(p,r) in enumerate(zip(path,radii)):
            d=(path[min(i+1,len(path)-1)]-path[max(i-1,0)]).normalized()
            if u is None:
                u,_=frame(d)
            else:
                u=(u-d*u.dot(d)).normalized()
            v=d.cross(u)
            layers.append([p+r*(u*math.cos(TAU*j/sides)+v*flat*math.sin(TAU*j/sides)) for j in range(sides)])
        self.shell(layers,mat,tint)

    def finish(self,name):
        mesh=bpy.data.meshes.new(name)
        mesh.from_pydata([p*.001 for p in self.points],[],self.faces)
        mesh.polygons.foreach_set("use_smooth",self.smooth)
        mesh.update()
        if not self.proxy:
            uv=mesh.uv_layers.new(name='货物表面')
            from array import array
            uv.data.foreach_set('uv',array('f',(x for pair in self.uv for x in pair)))
            layer=mesh.color_attributes.new(name='港湾光色',type='BYTE_COLOR',domain='CORNER')
            # 货堆相互压住的部分烘入接触阴影，各个视角使用同一个空间结果。
            tree=BVHTree.FromPolygons(self.points,self.faces)
            dirs=[vec(d).normalized() for d in ((1,0,.45),(-1,0,.45),(0,1,.5),(0,-1,.5),
                  (.64,.7,1),(-.6,-.8,1),(-.8,.7,1),(.63,-.7,1),(0,0,1))]
            ao=[]
            for i,p in enumerate(self.points):
                n=mesh.vertices[i].normal
                start=p+n*.025; shadow=0;total=.01
                for d in dirs:
                    w=max(0,n.dot(d))+.13
                    hit=tree.ray_cast(start,d,3.7)[3]
                    total+=w
                    if hit is not None:
                        shadow+=w*(1-hit/3.7)**1.3
                ao.append(1-.44*shadow/total)
            rgba=array('f')
            for poly,t,wear in zip(mesh.polygons,self.tint,self.wear):
                for i in poly.vertices:
                    c=max(.32,min(1,t*ao[i]))
                    rgba.extend((c,c,c,wear))
            layer.data.foreach_set('color_srgb',rgba)
        obj=bpy.data.objects.new(name,mesh)
        bpy.context.scene.collection.objects.link(obj)
        return obj
