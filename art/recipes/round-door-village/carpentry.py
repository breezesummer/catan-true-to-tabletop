"""主体墙、木构架、砂岩拱套、门扇和嵌入式窗。所有坐标均以墙面为基准。"""
import math
from mathutils import Vector

from mesh_parts import X, Y, Z


class Wall:
    def __init__(self, parts, origin, along, outward):
        self.parts = parts
        self.origin = Vector(origin)
        self.u, self.n = along, outward

    def p(self, u, z, d=0):
        return self.origin + self.u*u + Z*z + self.n*d

    def panel(self, outline, front, thick, material, bevel=0, grain=None):
        axes = (Z, self.u, self.n) if grain == "vertical" else (self.u, self.n, Z)
        self.parts.prism([self.p(u, z, front) for u, z in outline], -self.n*thick, material, bevel, axes)

    def rectangle(self, lo, hi, bottom, top, d=0, thick=.11, mat="plaster", bevel=0):
        self.panel([(lo,bottom), (hi,bottom), (hi,top), (lo,top)], d, thick, mat, bevel)

    def timber(self, u0, z0, u1, z1, width, depth, d=.015, mat="oak"):
        self.parts.beam(self.p(u0,z0,d), self.p(u1,z1,d), width, depth, mat, self.n, min(.012,width*.12))

    def stud(self, u, z, d, radius=.021, mat="endgrain"):
        self.parts.turning(self.p(u,z,d), self.n, ((0,radius),(.005,radius*.94)), mat, 8)

    def window_bay(self, lo, hi, h, opening):
        u, z, w, wh, lattice = opening
        a, b, top = u-w*.5, u+w*.5, z+wh
        self.rectangle(lo, a, 0, h)
        self.rectangle(b, hi, 0, h)
        self.rectangle(a,b,0,z)
        self.rectangle(a,b,top,h)
        self.parts.group = "窗扇与细木作"
        # 外皮和玻璃相距八厘米，露出斜口窗套与窗洞的侧壁。
        self.rectangle(a-.012,b+.012,z-.012,top+.012,-.094,.025,"soot")
        self.rectangle(a+.010,b-.010,z+.010,top-.010,-.073,.012,"glass",.003)
        for edge in (a,b):
            self.timber(edge,z,edge,top,.051,.107,-.008,"trim")
        for edge in (z,top):
            self.timber(a-.027,edge,b+.027,edge,.050,.107,-.008,"trim")
        self.timber(a-.052,z-.027,b+.052,z-.027,.047,.14,.020,"arch")
        self.timber(a-.043,top+.017,b+.043,top+.017,.052,.124,.007,"trim")
        if lattice:
            self.timber(u,z+.016,u,top-.017,.024,.040,-.039,"door")
            self.timber(a+.014,z+wh*.51,b-.014,z+wh*.51,.021,.041,-.038,"door")
        self.parts.group = "石灰墙体"

    def plinth(self, start, end, portal_side=0):
        width = end-start
        if width < .06:
            return
        def cut_at(z):
            return math.sqrt(.594**2-(z-.455)**2)
        def block(a,b,zb,zt,d,thick,mat,bevel=0):
            if portal_side < 0 and b==end:
                outline = [(a,zb),(-cut_at(zb),zb),(-cut_at(zt),zt),(a,zt)]
            elif portal_side > 0 and a==start:
                outline = [(cut_at(zb),zb),(b,zb),(b,zt),(cut_at(zt),zt)]
            else:
                outline = [(a,zb),(b,zb),(b,zt),(a,zt)]
            self.panel(outline,d,thick,mat,bevel)
        block(start,end,.003,.278,.020,.048,"stone")
        for course in range(2):
            x = start
            while x < end-.001:
                length = self.parts.rng.uniform(.26,.43)
                if x == start and course:
                    length *= .60
                last = min(end,x+length)
                if end-last < .13:
                    last = end
                zb, zt = .002 + .137*course, .133+.137*course
                mat = self.parts.rng.choice(("stone","stone","quoin"))
                a = x if x==start else x+.003
                b = last if last==end else last-.003
                block(a,b,zb,zt,self.parts.rng.uniform(.039,.056),.080,mat,.009)
                x = last

    def frame(self, lo, hi, height, gable=None, posts=(True,True)):
        self.parts.group = "承重木构"
        corners = [edge for edge,enabled in zip((lo,hi),posts) if enabled]
        for edge in corners:
            self.timber(edge,.19,edge,height-.025,.145,.15,-.004)
            self.stud(edge,height-.072,.072)
        self.timber(lo-.006,height-.007,hi+.006,height-.007,.13,.135)
        if gable:
            self.timber(0,height+.035,0,gable-.055,.123,.135)
            for sign in (-1,1):
                self.timber(sign*.086,height+.025,sign*(hi-lo)*.212,height+(gable-height)*.52,.096,.107)
            self.stud(0,height-.01,.085,.019)
        self.parts.group = "石砌墙脚"
        for edge in corners:
            self.rectangle(edge-.103,edge+.103,0,.275,.101,.206,"quoin",.020)


def clipped(poly, value, keep):
    out = []
    for a, b in zip(poly[-1:]+poly[:-1], poly):
        da, db = (a[0]-value)*keep, (b[0]-value)*keep
        if (da >= 0) != (db >= 0):
            t = da/(da-db)
            out.append((value, a[1]+(b[1]-a[1])*t))
        if db >= 0:
            out.append(b)
    return out


def round_portal(wall, height):
    zc, inside, outside = .455,.493,.603
    def arc(radius, fraction):
        ang = (2*fraction-1)*math.acos(-zc/radius)
        return (radius*math.sin(ang), max(0,zc+radius*math.cos(ang)))
    opening = [arc(inside,k/54) for k in range(55)]
    wall.panel([(-.62,0),*opening,(.62,0),(.62,height),(-.62,height)],0,.15,"plaster")
    wall.parts.group = "双扇圆门"
    wall.panel(opening,-.094,.035,"soot")
    width = 2*inside/8
    for k in range(8):
        lo = -inside+k*width
        gap = .008 if k in (3,4) else .003
        board = clipped(clipped(opening,lo+gap,1),lo+width-.003,-1)
        wall.panel(board,-.048+wall.parts.rng.uniform(-.003,.003),.043,"door",.004,"vertical")
    wall.parts.group = "砂岩门套"
    # 内口先铺暗色榫槽；外圈拱石各自收至同一水平脚线。
    for k in range(15):
        t0,t1 = k/15,(k+1)/15
        ts = [t0,(2*t0+t1)/3,(t0+2*t1)/3,t1]
        wall.panel([arc(outside-.008,t) for t in ts]+[arc(inside-.017,t) for t in reversed(ts)],
                   .022,.11,"trim")
        ts = [t + (.0012 if j==0 and k else -.0012 if j==3 and k!=14 else 0) for j,t in enumerate(ts)]
        keystone = k==7
        wall.panel([arc(outside+(.023 if keystone else 0),t) for t in ts]
                   +[arc(inside+.008,t) for t in reversed(ts)], .079 if keystone else .069,
                   .135,"arch" if k%5!=2 else "quoin",.007)
    wall.parts.group = "锻铁与门扣"
    for sign in (-1,1):
        for z in (.255,.713):
            edge,tip = sign*.397, sign*.255
            wall.panel([(edge,z-.018),(tip,z-.012),(tip-sign*.024,z),
                        (tip,z+.012),(edge,z+.018)],-.036,.013,"iron",.002)
            for u in (edge-sign*.017,tip+sign*.013):
                wall.stud(u,z,-.034,.009,"bronze")
        u = sign*.276
        wall.parts.turning(wall.p(u,.49,-.042),wall.n,
                           ((0,.05),(.009,.052),(.016,.036),(.018,.024),(.041,.018),
                            (.050,.034),(.069,.038),(.082,.027),(.087,.013)), "bronze",18)


def build_walls(p):
    cx, front, back = -.60,-1.30,1.30
    half, h, apex = 1.38,1.35,2.75
    main = Wall(p, (cx,front,0), X, -Y)
    p.group = "石灰墙体"
    for sign in (-1,1):
        lo,hi = (-half,-.62) if sign < 0 else (.62,half)
        main.window_bay(lo,hi,h,(sign*.879,.563,.337,.369,True))
    round_portal(main,h)
    p.group = "石灰墙体"
    main.panel([(-half,h),(half,h),(0,apex)],0,.125,"plaster")
    main.frame(-half,half,h,apex)
    main.plinth(-half+.100,-.54,-1)
    main.plinth(.54,half-.100,1)
    rear = Wall(p,(cx,back,0),-X,Y)
    p.group = "石灰墙体"
    rear.window_bay(-half,half,h,(0,.51,.40,.425,True))
    rear.panel([(-half,h),(half,h),(0,apex)],0,.12,"plaster")
    rear.frame(-half,half,h,apex)
    rear.plinth(-half+.100,half-.100)
    for sign in (-1,1):
        side = Wall(p,(cx+sign*half,0,0),Y*sign,X*sign)
        p.group = "石灰墙体"
        if sign>0:
            side.rectangle(.25,back,0,h)
            side.rectangle(front,.25,.93,h)
        else:
            side.window_bay(front,back,h,(.06,.51,.36,.41,True))
        side.frame(front,back,1.224,posts=(False,False))
        side.plinth(.25 if sign>0 else front+.100,back-.100)
    # 附屋的正檐与主屋正面齐平，山墙朝侧面，后檐留出主屋的侧墙。
    wing_min, wing_max, wing_front, wing_back = .70,2.035,-1.30,.25
    wing_h, wing_top = .975,1.64
    for y,axis,normal in ((wing_front,X,-Y),(wing_back,-X,Y)):
        wall = Wall(p,((wing_min+wing_max)/2,y,0),axis,normal)
        w = (wing_max-wing_min)/2
        p.group = "石灰墙体"
        if y == wing_front:
            wall.window_bay(-w,w,wing_h,(.055,.442,.315,.32,False))
        else:
            wall.rectangle(-w,w,0,wing_h)
        wall.frame(-w,w,wing_h,posts=(y==wing_back,y==wing_front))
        wall.plinth(-w+.100,w-.100)
    end = Wall(p,(wing_max,(wing_front+wing_back)/2,0),Y,X)
    w = (wing_back-wing_front)/2
    p.group = "石灰墙体"
    end.rectangle(-w,w,0,wing_h)
    end.panel([(-w,wing_h),(w,wing_h),(0,wing_top)],0,.13,"plaster")
    end.frame(-w,w,wing_h,posts=(False,False))
    end.plinth(-w+.100,w-.100)
