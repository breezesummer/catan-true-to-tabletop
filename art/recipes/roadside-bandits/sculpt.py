"""原型正视坐标、裁片和雕塑截面。尺寸换算与合批只在导出时进行。"""
from dataclasses import dataclass
from math import pi, tau, sin, cos, radians
from mathutils import Vector

V = Vector
UP, RIGHT, FRONT = V((0, 0, 1)), V((1, 0, 0)), V((0, -1, 0))
S, C = sin(radians(13)), cos(radians(13))
BASE_ROW = 479


def P(x, row, depth):
    return V((x, depth, (BASE_ROW-row-S*depth)/C))


def profile(stops, t):
    if t <= stops[0][0]:
        return stops[0][1:]
    for a, b in zip(stops, stops[1:]):
        if t <= b[0]:
            u = (t-a[0])/(b[0]-a[0])
            return tuple(v+(w-v)*u for v, w in zip(a[1:], b[1:]))
    return stops[-1][1:]


def curve(stops, steps=3, closed=False):
    out = []
    def at(i):
        return stops[i % len(stops) if closed else max(0, min(len(stops)-1, i))]
    for i in range(len(stops) if closed else len(stops)-1):
        a, b, c, d = (at(i+k) for k in (-1, 0, 1, 2))
        for j in range(steps):
            t = j/steps
            out.append(tuple(.5*(2*y+(z-x)*t+(2*x-5*y+4*z-w)*t*t+
                                   (-x+3*y-3*z+w)*t*t*t) for x,y,z,w in zip(a,b,c,d)))
    return out if closed else out+[tuple(stops[-1])]


@dataclass
class Chart:
    rows: list
    material: str
    wrap: bool
    density: float
    paint: str
    caps: bool
    part: str
    smooth: bool
    skin_region: str = ""
    hand: object = None
    pigment_uv: object = None


class Figure:
    def __init__(self):
        self.charts = []
        self.heads = []
        self.part = ""

    def grid(self, rows, material, *, wrap=True, density=0, paint="", caps=True, smooth=True):
        self.charts.append(Chart(rows, material, wrap, density, paint, caps and wrap, self.part, smooth))
        return rows


def loft(g, stops, material, *, warp=None, sides=40, steps=3, density=1, paint=""):
    """带有椭圆截面、偏移轴与褶线的实体。站点为中心横位、行、进深及两条半径。"""
    stations = curve(stops, steps)
    rows = []
    for j, (cx, row, d, rx, ry) in enumerate(stations):
        t = j/(len(stations)-1)
        ring = []
        for k in range(sides):
            a = -pi+tau*k/sides
            dr, dz = warp(t, a) if warp else (0, 0)
            ring.append(P(cx+(max(.12,rx)+dr)*sin(a),row+dz-.12*ry*(1-cos(a)),
                          d-(max(.12,ry)+dr)*cos(a)))
        rows.append(ring)
    return g.grid(rows, material, density=density, paint=paint)


def sweep(g, points, radii, mat, *, sides=12, ref=RIGHT, warp=None, density=0, paint=""):
    """沿曲线平行运输截面，让肘、指骨和发绺在弯曲处连续。"""
    rows = []
    previous = None
    for j, (p, rad) in enumerate(zip(points,radii)):
        tangent = (points[min(j+1,len(points)-1)]-points[max(j-1,0)]).normalized()
        if previous is None:
            seed = ref if abs(tangent.dot(ref))<.94 else (UP if abs(tangent.z)<.94 else FRONT)
            across = (seed-tangent*seed.dot(tangent)).normalized()
        else:
            across = previous.rotation_difference(tangent) @ across
        other = tangent.cross(across).normalized()
        previous = tangent
        rx, ry = (rad,rad) if isinstance(rad,(float,int)) else rad
        t = j/(len(points)-1)
        ring = []
        for k in range(sides):
            a = tau*k/sides
            relief = warp(t,a) if warp else 0
            ring.append(p+across*(rx+relief)*cos(a)+other*(ry+relief)*sin(a))
        rows.append(ring)
    return g.grid(rows,mat,density=density,paint=paint)


def tube(g, points, radii, mat, *, steps=2, sides=9, ref=RIGHT, **kwargs):
    if isinstance(radii,(int,float)):
        radii = [radii]*len(points)
    stops = curve([(*p,*(r if isinstance(r,(tuple,list)) else (r,r))) for p,r in zip(points,radii)],steps)
    return sweep(g,[V(v[:3]) for v in stops],[(max(.06,v[3]),max(.06,v[4])) for v in stops],
                 mat,sides=sides,ref=ref,**kwargs)


def stroke(g, coords, radii, mat, **kw):
    return tube(g,[P(*p) for p in coords],radii,mat,**kw)


def orb(g, center, axes, mat, *, frame=None, sides=18, bands=10, density=0, paint=""):
    x,y,z = frame or (RIGHT, -FRONT, UP)
    rows = []
    for i in range(bands+1):
        phi = -pi*.496+pi*.992*i/bands
        rows.append([center+x*(axes[0]*cos(phi)*sin(tau*k/sides))-
                     y*(axes[1]*cos(phi)*cos(tau*k/sides))+z*(axes[2]*sin(phi)) for k in range(sides)])
    return g.grid(rows,mat,density=density,paint=paint)


def trim(g, points, radius, mat, closed=False, sides=7):
    pts = list(points)
    if closed:
        pts.append(pts[0])
    return sweep(g,pts,[radius]*len(pts),mat,sides=sides)


def stitches(g, points, mat="thread", spacing=4.4, length=2.1, radius=.37, slant=None):
    pts = list(points)
    lengths = [0.]
    for a,b in zip(pts,pts[1:]):
        lengths.append(lengths[-1]+(b-a).length)
    def at(s):
        for i in range(len(pts)-1):
            if lengths[i+1]>=s:
                return pts[i].lerp(pts[i+1],(s-lengths[i])/max(.001,lengths[i+1]-lengths[i]))
        return pts[-1]
    s = spacing*.43
    lean = slant or V((0,0,0))
    while s+length<lengths[-1]:
        a,b = at(s)-lean,at(s+length)+lean
        sweep(g,[a,(a+b)*.5+FRONT*.09,b],[radius*.6,radius,radius*.6],mat,sides=5)
        s += spacing


def ribbon(g, points, widths, mat, *, ref=RIGHT, normal=FRONT, thickness=.9, detail=False):
    path = curve([(*p,w) for p,w in zip(points,widths)],3)
    rows = []
    for j,p in enumerate(path):
        c = V(p[:3])
        tangent = (V(path[min(j+1,len(path)-1)][:3])-V(path[max(0,j-1)][:3])).normalized()
        side = tangent.cross(normal).normalized()
        if side.dot(ref)<0:
            side = -side
        n = side.cross(tangent).normalized()
        if n.dot(normal)<0:
            n = -n
        w = max(.3,p[3])
        rows.append([c+side*(w*u)+n*(thickness*v) for u,v in
                     ((-1,0),(-.88,.74),(0,1),(.88,.74),(1,0),(.88,-.6),(-.88,-.6))])
    g.grid(rows,mat,density=.95 if detail else 0)
    if detail:
        for i in (1,3):
            stitches(g,[r[i] for r in rows],spacing=4.2,radius=.28)
    return rows


def frame_loop(g, c, axis, up, width, height, r=1.2, mat="brass"):
    corner = min(width,height)*.29
    pts = []
    for x,z,start in ((width-corner,height-corner,0),(-width+corner,height-corner,pi/2),
                      (-width+corner,-height+corner,pi),(width-corner,-height+corner,3*pi/2)):
        for j in range(5):
            a=start+pi*.5*j/4
            pts.append(c+axis*(x+corner*cos(a))+up*(z+corner*sin(a)))
    trim(g,pts,r,mat,True,8)
    tube(g,[c-axis*width,c-axis*width*.42+FRONT*.7,c+axis*.9+FRONT*.85],
         [r*.8,r*.65,r*.32],mat,sides=7)
