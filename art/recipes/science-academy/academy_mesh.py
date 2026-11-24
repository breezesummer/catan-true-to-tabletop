"""学宫沿用王城的石工与窗洞工具，仪器另按车制、球面和刻度带方向展开。"""
from collections import defaultdict
from math import cos, pi, radians, sin, tau
import sys

from mathutils import Vector, geometry

from finishes import COMMON, REGIONS, SIZE

if str(COMMON) not in sys.path:
    sys.path.insert(0,str(COMMON))
from stonework import Frame, Mason, UP

WIDTH = .0278


class Academy(Mason):
    def __init__(self):
        super().__init__()
        self.smooth = defaultdict(list)

    def face(self, points, swatch, coords=None, outward=None, smooth=False):
        verts,lookup,faces,uvs,slots=self.groups[self.name]
        if swatch=="ashlar" and coords is None:
            normal=geometry.normal([Vector(p) for p in points])
            tangent=UP.cross(normal).normalized()
            raw=[(p.x,p.y) if abs(normal.z)>.75 else (p.dot(tangent),p.z) for p in map(Vector,points)]
            coords=[(u/4.05+.173,v/4.05+.071) for u,v in raw]
        if swatch=="glass":
            swatch="smoke"
            if outward is not None:
                # 大片窗玻璃由横向风撑约束，铅接菱形和风撑分别位于两个深度。
                t=UP.cross(outward).normalized()
                vertices=list(map(Vector,points))
                mid=sum(vertices,Vector())/len(vertices)
                z0,z1=min(v.z for v in vertices),max(v.z for v in vertices)
                if z1-z0>.68:
                    lo,hi=min(v.dot(t) for v in vertices),max(v.dot(t) for v in vertices)
                    mid.z=z0+(z1-z0)*.43
                    a=mid+t*(lo-mid.dot(t))+outward*.019
                    b=mid+t*(hi-mid.dot(t))+outward*.019
                    self.rod(a,b,.012,"gunmetal",6)
        elif swatch=="oak":
            swatch="darkoak"
        if swatch not in REGIONS:
            start=len(faces)
            super().face(points,swatch,coords,outward)
            for index in range(start,len(faces)):
                slots[index]=0
                if smooth:
                    self.smooth[self.name].append(index)
            return
        points=list(map(Vector,points))
        normal=geometry.normal(points)
        if normal.length_squared<.5:
            return
        if coords is None:
            # 小轴端、铆钉和平切面在金属坯上截取连续纹理。
            axis=normal.cross(UP if abs(normal.z)<.85 else Vector((0,1,0))).normalized()
            cross=normal.cross(axis)
            center=sum(points,Vector())/len(points)
            span=max(.9,2.05*max((p-center).length for p in points))
            coords=[(.5+(p-center).dot(axis)/span,.5+(p-center).dot(cross)/span) for p in points]
        left,top,w,h=REGIONS[swatch]
        entries=list(zip(points,coords))
        if outward is not None and normal.dot(outward)<0:
            entries.reverse()
        indices,texture=[],[]
        for p,(u,v) in entries:
            key=tuple(round(a,6) for a in p)
            i=lookup.get(key)
            if i is None:
                i=lookup[key]=len(verts)
                verts.append(key)
            if i in indices:
                continue
            indices.append(i)
            texture.append(((left+3+u*(w-6))/SIZE,1-(top+3+(1-v)*(h-6))/SIZE))
        if len(indices)<3:
            return
        if smooth:
            self.smooth[self.name].append(len(faces))
        faces.append(indices)
        uvs.append(texture)
        slots.append(1)

    def export_parts(self, scene, materials):
        objects=super().export_parts(scene,materials)
        for obj in objects:
            mesh=obj.data
            for v in mesh.vertices:
                v.co*=WIDTH/.0205
            for i in self.smooth[obj.name]:
                mesh.polygons[i].use_smooth=True
            mesh.update()
            mesh.set_sharp_from_angle(angle=radians(38))
        return objects


def box(m,x,y,z,w,d,h,swatch):
    m.extrude([(x-w/2,y-d/2,z),(x+w/2,y-d/2,z),(x+w/2,y+d/2,z),(x-w/2,y+d/2,z)],
              (0,0,h),swatch)


def lathe(m,origin,axis,profile,sides,swatch,cap=True):
    """剖面由 (轴位, 半径) 车成，起刀与收刀处各自有封口。"""
    o,n=Vector(origin),Vector(axis).normalized()
    a=n.cross(UP if abs(n.z)<.9 else Vector((0,-1,0))).normalized()
    b=n.cross(a)
    directions=[a*cos(tau*i/sides)+b*sin(tau*i/sides) for i in range(sides)]
    rings=[[o+n*z+r*direction for direction in directions] for z,r in profile]
    start=min(z for z,r in profile)
    span=max(.0001,max(z for z,r in profile)-start)
    if cap:
        m.face(rings[0][::-1],swatch,outward=-n)
    for j,(lo,hi) in enumerate(zip(rings,rings[1:])):
        t=(profile[j][0]-start)/span
        s=(profile[j+1][0]-start)/span
        for i in range(sides):
            k=(i+1)%sides
            uv=[(i/sides,t),((i+1)/sides,t),((i+1)/sides,s),(i/sides,s)]
            m.face([lo[i],lo[k],hi[k],hi[i]],swatch,uv,smooth=True)
    if cap:
        m.face(rings[-1],swatch,outward=n)


def band_ring(m,center,normal,radius,width,thickness,swatch="brass",face="degrees",steps=96):
    """有内外侧壁和倒棱的金属带，外圆周完整对应一圈读数。"""
    c,n=Vector(center),Vector(normal).normalized()
    a=n.cross(UP if abs(n.z)<.93 else Vector((0,1,0))).normalized()
    b=n.cross(a)
    h,w=thickness*.5,width*.5
    e=min(h,w)*.32
    section=[(h-e,-w),(h,-w+e),(h,w-e),(h-e,w),(-h+e,w),(-h,w-e),(-h,-w+e),(-h+e,-w)]
    def ring(i):
        radial=a*cos(tau*i/steps)+b*sin(tau*i/steps)
        return [c+radial*(radius+dr)+n*d for dr,d in section]
    for i in range(steps):
        ra,rb=ring(i),ring(i+1)
        for j in range(8):
            k=(j+1)%8
            # 面向观察者的前后两坡也刻读线，倒棱与外露的缘部保留磨亮的金色。
            engraving=face if j in (1,3,7) else swatch
            if engraving in ("degrees","ecliptic"):
                quarter=i*4//steps
                u,v=4*i/steps-quarter,4*(i+1)/steps-quarter
                lo,hi=1-(quarter+.992)/4,1-(quarter+.008)/4
            else:
                u,v=i/steps,(i+1)/steps
                lo,hi=0,1
            uv=[(u,lo),(v,lo),(v,hi),(u,hi)]
            m.face([ra[j],rb[j],rb[k],ra[k]],engraving,uv,smooth=True)


def orb(m,center,radius,swatch,axis=(0,0,1),longitudes=64,latitudes=32):
    north=Vector(axis).normalized()
    meridian=Vector((0,-1,0))
    meridian=(meridian-north*north.dot(meridian)).normalized()
    east=north.cross(meridian)
    center=Vector(center)
    def point(u,v):
        a,b=(u-.5)*tau,(v-.5)*pi
        return center+radius*(cos(b)*(meridian*cos(a)+east*sin(a))+north*sin(b))
    for j in range(latitudes):
        for i in range(longitudes):
            uv=[(i/longitudes,j/latitudes),((i+1)/longitudes,j/latitudes),
                ((i+1)/longitudes,(j+1)/latitudes),(i/longitudes,(j+1)/latitudes)]
            points=[point(*p) for p in uv]
            m.face(points,swatch,uv,points[1]+points[2]-2*center,True)
