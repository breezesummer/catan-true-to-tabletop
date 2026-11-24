"""裁片、截面与细工。造型坐标取自彩色视图，深度由侧、背视图确定。"""
from math import sin, cos, pi, tau, exp

import bmesh
import bpy
from mathutils import Vector
from surfaces import atlas_uv

V=Vector
UP=V((0,0,1))
RIGHT=V((1,0,0))
FRONT=V((0,-1,0))


def P(x,row,depth=0):
    return V((x-230,depth,795-row))


def smooth(stops,steps=4):
    """向心距离差较小时，均匀 Catmull–Rom 能保持截面端点和轮廓控制点。"""
    out=[]
    for i in range(len(stops)-1):
        a,b,c,d=[stops[max(0,min(len(stops)-1,k))] for k in (i-1,i,i+1,i+2)]
        for j in range(steps):
            t=j/steps
            out.append(tuple(.5*(2*y+(z-x)*t+(2*x-5*y+4*z-w)*t*t+
                           (-x+3*y-3*z+w)*t*t*t) for x,y,z,w in zip(a,b,c,d)))
    return out+[tuple(stops[-1])]


def lerp_profile(stops,t):
    for first,last in zip(stops,stops[1:]):
        if t<=last[0]:
            w=max(0,min(1,(t-first[0])/(last[0]-first[0])))
            return tuple(a+(b-a)*w for a,b in zip(first[1:],last[1:]))
    return stops[-1][1:]


class Workshop:
    def __init__(self):
        self.parts=[]
        self.verts=[]
        self.faces=[]
        self.uv=[]
        self.shades=[]
        self.charts={}

    def part(self,name):
        if self.verts:
            self.parts.append((self.label,self.verts,self.faces,self.uv,self.shades))
        self.label=name
        self.verts,self.faces,self.uv,self.shades=[],[],[],[]

    def face(self,indices,uvs,key,smooth_face=True):
        self.faces.append(indices)
        self.uv.append([atlas_uv(key,*uv) for uv in uvs])
        self.shades.append(smooth_face)

    def grid(self,rows,key,wrap=True,caps=True,uv=None,chart=None,cap_uv=None,chart_rows=None):
        m,n=len(rows),len(rows[0])
        start=len(self.verts)
        self.verts.extend(p for row in rows for p in row)
        count=n if wrap else n-1
        if chart:
            chart_rows=chart_rows or m
            self.charts[chart]=(rows[:chart_rows],wrap,key(0,0) if callable(key) else key)
        def vertex(j,k): return start+j*n+k%n
        for j in range(m-1):
            for k in range(count):
                tag=key(j,k) if callable(key) else key
                pairs=((j,k),(j,k+1),(j+1,k+1),(j+1,k))
                coords=[uv(h,l,tag,rows[h][l%n]) if uv else (l/count,h/(m-1)) for h,l in pairs]
                if chart and j<chart_rows-1:
                    tag=chart
                    coords=[(l/count,h/(chart_rows-1)) for h,l in pairs]
                self.face([vertex(h,l) for h,l in pairs],coords,tag)
        if wrap and caps:
            for j in (0,m-1):
                # 平截面保留单独的 UV 与硬边，不会把鞋底或皮带扣的法线压扁。
                tag=key(max(0,j-1),0) if callable(key) else key
                indices=[vertex(j,k) for k in range(n)]
                circle=[cap_uv(rows[j][k]) if cap_uv else
                        (.5+.49*sin(k*tau/n),.5+.49*cos(k*tau/n)) for k in range(n)]
                self.face(indices[::-1] if j==0 else indices,circle[::-1] if j==0 else circle,tag,False)

    def sheet(self,outer,inner,key,lining,edge,chart=None):
        """有两面和包边的裁片，两层共用边界顶点。"""
        m,n=len(outer),len(outer[0])
        start=len(self.verts)
        self.grid(outer,key,False,False,chart=chart)
        self.grid(inner,lining,False,False)
        boundary=[(0,k) for k in range(n)]+[(j,n-1) for j in range(1,m)]
        boundary+=[(m-1,k) for k in range(n-2,-1,-1)]+[(j,0) for j in range(m-2,0,-1)]
        for a,b in zip(boundary,boundary[1:]+boundary[:1]):
            i=start+a[0]*n+a[1]
            k=start+b[0]*n+b[1]
            self.face((i,k,k+m*n,i+m*n),((0,0),(1,0),(1,1),(0,1)),edge)

    def export_parts(self,scene,mat,scale,center):
        self.part("结束")
        objects=[]
        for name,verts,faces,uvs,shades in self.parts:
            mesh=bpy.data.meshes.new(name)
            mesh.from_pydata([tuple((p-center)*scale) for p in verts],[],faces)
            mesh.materials.append(mat)
            layer=mesh.uv_layers.new(name="裁片展纹")
            for poly,coordinates,shading in zip(mesh.polygons,uvs,shades):
                poly.use_smooth=shading
                for i,uv in zip(poly.loop_indices,coordinates):
                    layer.data[i].uv=uv
            bm=bmesh.new()
            bm.from_mesh(mesh)
            bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
            bm.to_mesh(mesh)
            bm.free()
            mesh.update()
            obj=bpy.data.objects.new(name,mesh)
            scene.collection.objects.link(obj)
            objects.append(obj)
        return objects


def sweep(g,points,radii,key,sides=12,ref=RIGHT,ripple=None):
    """沿弯曲中心线运输截面，保证袖子、手指和发束不随曲线拐角翻转。"""
    rows=[]
    prev=None
    for i,(c,rad) in enumerate(zip(points,radii)):
        tangent=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        if prev is None:
            seed=ref if abs(ref.dot(tangent))<.94 else UP if abs(UP.dot(tangent))<.94 else FRONT
            u=(seed-tangent*tangent.dot(seed)).normalized()
        else:
            u=prev.rotation_difference(tangent)@u
        v=tangent.cross(u).normalized()
        u=v.cross(tangent).normalized()
        prev=tangent
        rx,ry=(rad,rad) if isinstance(rad,(float,int)) else rad
        t=i/(len(points)-1)
        row=[]
        for k in range(sides):
            a=tau*k/sides
            bump=ripple(t,a) if ripple else 0.
            row.append(c+u*(rx+bump)*cos(a)+v*(ry+bump)*sin(a))
        rows.append(row)
    g.grid(rows,key)
    return rows


def tube(g,points,radius,key,n=9,steps=3,ref=RIGHT):
    if isinstance(radius,(int,float)): radius=[radius]*len(points)
    stations=smooth([(*p,r) for p,r in zip(points,radius)],steps)
    return sweep(g,[V(s[:3]) for s in stations],[max(.12,s[3]) for s in stations],key,n,ref)


def stroke(g,coords,radius,key,n=9,steps=3):
    return tube(g,[P(*s) for s in coords],radius,key,n,steps)


def orb(g,c,axes,key,n=18,rings=10,frame=None):
    u,v,w=frame or (RIGHT,V((0,1,0)),UP)
    rows=[]
    for j in range(rings+1):
        lat=-1.558+j*3.116/rings
        rows.append([c+u*(axes[0]*cos(lat)*cos(tau*k/n))+
                     v*(axes[1]*cos(lat)*sin(tau*k/n))+w*(axes[2]*sin(lat)) for k in range(n)])
    g.grid(rows,key)


def trim(g,pts,radius,key,loop=False,sides=7):
    points=list(pts)
    if loop: points.append(points[0])
    sweep(g,points,[radius]*len(points),key,sides)


def stitches(g,points,step=7,length=3.7,radius=.59,key="thread"):
    pts=list(points)
    distances=[0.]
    for a,b in zip(pts,pts[1:]): distances.append(distances[-1]+(b-a).length)
    def along(d):
        for i in range(len(pts)-1):
            if distances[i+1]>=d:
                return pts[i].lerp(pts[i+1],(d-distances[i])/max(.001,distances[i+1]-distances[i]))
        return pts[-1]
    d=step*.45
    while d+length<distances[-1]:
        a,b=along(d),along(d+length)
        sweep(g,[a,(a+b)*.5,b],[radius*.6,radius,radius*.65],key,5)
        d+=step


def rounded_loop(w,h,r,steps=5):
    pts=[]
    for x,y,start in ((w-r,h-r,0),(-w+r,h-r,pi/2),(-w+r,-h+r,pi),(w-r,-h+r,3*pi/2)):
        pts.extend((x+r*cos(start+j*pi/(2*steps)),y+r*sin(start+j*pi/(2*steps))) for j in range(steps+1))
    return pts


def buckle(g,c,horizontal,vertical,w,h,rad=2.4,key="brass"):
    pts=[c+horizontal*x+vertical*z for x,z in rounded_loop(w,h,min(w,h)*.28)]
    trim(g,pts,rad,key,True,9)
    p=c-horizontal*w*.96
    tube(g,[p,p+horizontal*w*.44+FRONT*1.4,p+horizontal*w*.96+FRONT*.8],[rad*.7,rad*.58,rad*.26],key,7)
