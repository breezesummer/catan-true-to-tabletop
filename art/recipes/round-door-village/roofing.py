"""顺坡排瓦。两坡按实际相交线裁瓦，脊瓦、檐口和烟囱泛水各有厚度。"""
import math
from mathutils import Vector

from mesh_parts import X, Y, Z


MAIN_X, WING_Y = -.60, -.525
MAIN_TOP, WING_TOP = 2.825, 1.70
MAIN_RUN, WING_RUN = 1.62, .92
MAIN_PITCH, WING_PITCH = 1.035, .833
CHIMNEY_X, CHIMNEY_Y, CHIMNEY_HALF = .22, .50, .225
# 五种压制瓦的横拱、尾翘、尾缘厚度、扭转和两角收分，单位同建筑坐标。
PROFILES = ((.019,.008,.031,-.005,.017), (.031,.015,.036,.007,.023),
            (.024,.002,.040,-.010,.019), (.037,.009,.031,.004,.029),
            (.017,.021,.035,.011,.015))


def trim(points, signed_distance):
    if not points:
        return []
    clipped = []
    for a, b in zip(points[-1:]+points[:-1], points):
        da, db = signed_distance(a), signed_distance(b)
        if (da>0) != (db>0):
            t = da/(da-db)
            clipped.append(tuple(x+(y-x)*t for x,y in zip(a,b)))
        if db >= 0:
            clipped.append(b)
    clean = []
    for p in clipped:
        if not clean or math.dist(clean[-1],p)>.0001:
            clean.append(p)
    if len(clean)>1 and math.dist(clean[0],clean[-1])<.0001:
        clean.pop()
    return clean if len(clean)>=3 else []


def main_height(x):
    return MAIN_TOP-abs(x-MAIN_X)*MAIN_PITCH


class Slope:
    def __init__(self, parts, wing, sign):
        self.parts, self.wing, self.sign = parts, wing, sign
        self.top, self.pitch = (WING_TOP,WING_PITCH) if wing else (MAIN_TOP,MAIN_PITCH)
        self.run = WING_RUN if wing else MAIN_RUN
        self.lo, self.hi = (.36,2.19) if wing else (-1.465,1.465)
        self.along = X if wing else Y
        self.across = (Y if wing else X)*sign
        self.origin = Vector((0,WING_Y,0)) if wing else Vector((MAIN_X,0,0))
        self.axes = (self.along, (self.across-Z*self.pitch).normalized(),
                     (Z+self.across*self.pitch).normalized())

    def point(self, a, s, lift=0):
        return self.origin+self.along*a+self.across*s+Z*(self.top-self.pitch*s+lift)

    def cropped(self, poly, gap=.060):
        if not poly:
            return []
        if self.wing:
            eave = MAIN_X+MAIN_RUN
            distance = lambda q: self.top-self.pitch*q[1]-main_height(q[0])-gap
            if min(distance(q) for q in poly)>=0 or min(q[0] for q in poly)>=eave:
                return [poly]
            return [trim(poly,lambda q:q[0]-eave),
                    trim(trim(poly,lambda q:eave-q[0]),distance)]
        if self.sign < 0:
            return [poly]
        low, high = min(q[0] for q in poly), max(q[0] for q in poly)
        nearest = max(low-WING_Y,WING_Y-high,0)
        if self.top-self.pitch*max(q[1] for q in poly)-WING_TOP+nearest*WING_PITCH >= gap:
            return [poly]
        pieces = []
        for sign in (-1,1):
            segment = trim(poly,lambda q:sign*(q[0]-WING_Y))
            pieces.append(trim(segment,lambda q:self.top-self.pitch*q[1]-WING_TOP+
                               sign*(q[0]-WING_Y)*WING_PITCH-gap))
        return pieces

    def clear_chimney(self, poly):
        if not poly or self.wing or self.sign<0:
            return [poly]
        half = CHIMNEY_HALF+.006
        a,b = CHIMNEY_Y-half,CHIMNEY_Y+half
        c,d = CHIMNEY_X-MAIN_X-half,CHIMNEY_X-MAIN_X+half
        if (max(q[0] for q in poly)<=a or min(q[0] for q in poly)>=b or
            max(q[1] for q in poly)<=c or min(q[1] for q in poly)>=d):
            return [poly]
        middle = trim(trim(poly,lambda q:q[0]-a),lambda q:b-q[0])
        return [trim(poly,lambda q:a-q[0]),trim(poly,lambda q:q[0]-b),
                trim(middle,lambda q:c-q[1]),trim(middle,lambda q:q[1]-d)]

    def tile(self, a0, a1, s0, s1, material):
        rng = self.parts.rng
        crown,curl,thickness,twist,corner = rng.choice(PROFILES)
        a0, a1 = a0+.004, a1-.004
        width = a1-a0
        middle = (a0+a1)*.5
        tail = s1+rng.uniform(-.009,.009)
        chipped = rng.random()<.095 and width>.23
        left = chipped and rng.random()<.5
        edge = rng.uniform(.030,.052) if chipped else corner
        drop = rng.uniform(.024,.049) if chipped else corner*.72
        outline = [(a0,s0),(middle,s0),(a1,s0),(a1,tail-drop),(a1-edge,tail),
                   (a0+width*.75,tail+.003),(middle,tail+.005),(a0+width*.25,tail+.003),
                   (a0+corner,tail-.001),(a0,tail-corner*.72)]
        if left:
            outline = [(a0+a1-a,s) for a,s in reversed(outline)]
        def upper(a,s):
            u,v = (a-middle)*2/width,(s-s0)/(tail-s0)
            arch = crown*(1-u*u)*(.60+.40*v)
            return self.point(a,s,.014+.048*v+arch+curl*v**4+twist*u*v)
        for patch in (piece for cut in self.cropped(outline) for piece in self.clear_chimney(cut)):
            if len(patch)<3:
                continue
            n = len(patch)
            ca,cs = (sum(q[i] for q in patch)/n for i in range(2))
            ring = [upper(*q) for q in patch]
            # 外圈下翻形成软瓦唇，断面与釉面共用同一条切边。
            inner = [upper(a+(ca-a)*.085,s+(cs-s)*.085)+Z*.003 for a,s in patch]
            bottom = [pt-Z*thickness*(.78+.22*(s-s0)/(tail-s0)) for pt,(a,s) in zip(ring,patch)]
            pts = ring+inner+bottom+[upper(ca,cs)]
            faces = [(n+i,n+(i+1)%n,3*n) for i in range(n)]
            faces += [(i,(i+1)%n,n+(i+1)%n,n+i) for i in range(n)]
            faces += [(i,2*n+i,2*n+(i+1)%n,(i+1)%n) for i in range(n)]
            faces.append(tuple(reversed(range(2*n,3*n))))
            bare = []
            if chipped:
                for i,((a,s),(b,t)) in enumerate(zip(patch,patch[1:]+patch[:1])):
                    # 两端都在缺角斜切线上，排除屋谷、烟囱裁出的边。
                    if max(abs(((x-a0) if left else (a1-x))/edge+(tail-y)/drop-1) for x,y in ((a,s),(b,t)))<.015:
                        bare.extend((n+i,2*n+i))
            self.parts.solid(pts,faces,material,axes=self.axes,fired_faces=bare,smooth_faces=range(2*n))

    def cover(self):
        p = self.parts
        p.group = "屋面下承板"
        for patch in self.cropped([(self.lo,0),(self.hi,0),(self.hi,self.run),(self.lo,self.run)],gap=.012):
            if patch:
                p.prism([self.point(*q,-.006) for q in patch],-Z*.039,"trim",axes=self.axes)
        p.group = "附屋搭接陶瓦" if self.wing else "主屋搭接陶瓦"
        courses = 3 if self.wing else 5
        count = 5 if self.wing else 8
        step = (self.hi-self.lo)/count
        for row in range(courses):
            s1 = self.run*(1-row/courses)
            s0 = max(0,s1-self.run/courses-.081)
            start = self.lo-step*.5*(row%2)
            while start < self.hi-.001:
                a0, a1 = max(start,self.lo),min(start+step,self.hi)
                if a1-a0>.05:
                    self.tile(a0,a1,s0,s1,"tile"+str(p.rng.randrange(4)))
                start += step
        p.group = "檐口木作"
        ends = [self.hi] if self.wing else [self.lo,self.hi]
        for a in ends:
            p.beam(self.point(a,0,-.045),self.point(a,self.run+.009,-.045),
                   .142 if not self.wing else .110,.105,"trim",self.along,.019)
            # 沿山墙搭接的披水瓦，瓦节顺斜边排列。
            rake_count = 5 if not self.wing else 3
            for k in range(rake_count):
                low, high = self.run*k/rake_count,self.run*(k+1)/rake_count
                p.beam(self.point(a,low,.043),self.point(a,high-.003,.064),
                       .099,.135,"tile"+str(1+k%3),self.along,.017)
        start_eave = max(self.lo,1.005) if self.wing else self.lo
        for patch in self.cropped([(start_eave,self.run-.07),(self.hi,self.run-.07),
                                   (self.hi,self.run),(start_eave,self.run)]):
            if patch:
                p.prism([self.point(*q,-.029) for q in patch],-Z*.056,"oak",.006,self.axes)
        for i in range(1,count+1):
            a = self.lo+(self.hi-self.lo)*(i-.5)/count
            if self.wing and a<.97 or not self.wing and self.sign>0 and abs(a-WING_Y)<.66:
                continue
            p.beam(self.point(a,self.run-.24,-.088),self.point(a,self.run-.012,-.080),
                   .076,.075,"trim",self.along,.010)


def ridge(p, wing):
    along, across = (X,Y) if wing else (Y,X)
    center = Vector((0,WING_Y,WING_TOP+.029)) if wing else Vector((MAIN_X,0,MAIN_TOP+.029))
    lo,hi,count,r = (.47,2.246,5,.083) if wing else (-1.529,1.529,7,.103)
    for k in range(count):
        a, b = lo+(hi-lo)*k/count, lo+(hi-lo)*(k+1)/count
        pts = []
        rings = ((a,r*.93),(a+.012,r*.97),(b-.014,r*1.07),(b,r*1.01),(b,r*.79),(a,r*.72))
        for pos,rad in rings:
            for j in range(9):
                angle = math.pi*(j/8)
                pts.append(center+along*pos+across*(rad*math.cos(angle))+Z*(rad*math.sin(angle)))
        faces = [(ring*9+j,ring*9+j+1,((ring+1)%len(rings))*9+j+1,((ring+1)%len(rings))*9+j)
                 for ring in range(len(rings)) for j in range(8)]
        faces += [tuple(row*9+j for row in range(len(rings))) for j in (0,8)]
        p.solid(pts,faces,"tile"+str(1+k%3))
    for end,direction in ([(hi,1)] if wing else [(lo,-1),(hi,1)]):
        profile = [center+along*end+across*(r*math.cos(math.pi*j/12))
                   +Z*(r*math.sin(math.pi*j/12)) for j in range(13)]
        p.prism(profile,along*direction*.016,"tile2",.004)


def folded_sheet(p, rows):
    # 每折由同一张薄板连续翻边；按截面法向压出板厚，立边也有断面。
    cols = len(rows[0])
    pts = [Vector(v) for row in rows for v in row]
    count = len(pts)
    front = [(r*cols+c,(r+1)*cols+c,(r+1)*cols+c+1,r*cols+c+1)
             for r in range(len(rows)-1) for c in range(cols-1)]
    normals = [Vector((0,0,0)) for _ in pts]
    for a,b,c,d in front:
        n = (pts[b]-pts[a]).cross(pts[d]-pts[a]).normalized()
        for i in (a,b,c,d):
            normals[i] += n
    pts += [point-normal.normalized()*.005 for point,normal in zip(pts,normals)]
    border = list(range(cols)) + list(range(cols*2-1,count,cols))
    border += list(range(count-2,count-cols-1,-1))+list(range(count-cols*2,0,-cols))
    faces = front + [tuple(i+count for i in reversed(face)) for face in front]
    faces += [(i,j,j+count,i+count) for i,j in zip(border,border[1:]+border[:1])]
    p.solid(pts,faces,"lead")


def flashing(p, cx, cy, half):
    up, down = cx-half, cx+half
    # 后泛水埋入上一排瓦，折出一条脊把水分向两侧。
    back = []
    for dy in (-half-.065,0,half+.065):
        lift = .036*(1-min(1,abs(dy)/half))
        back.append([(x,cy+dy,main_height(x)+z+lift) for x,z in
                     ((up-.14,.020),(up-.072,.049),(up-.007,.075),(up-.007,.139))])
    folded_sheet(p,back)
    # 阶梯泛水从坡底向上搭接，竖边顶部水平，水平翼随每排瓦转折。
    for sign in (-1,1):
        wall = cy+sign*(half+.009)
        for k in (2,1,0):
            x0 = up+(down-up)*k/3
            x1 = min(down+.031,up+(down-up)*(k+1)/3+.024)
            lap = .008*(2-k)
            rows = []
            for x in (x0,x1):
                height = main_height(x)
                rows.append([(x,wall+sign*(.096+lap),height+.035+lap),
                             (x,wall+sign*(.052+lap),height+.076+lap),
                             (x,wall+sign*lap,height+.080+lap),
                             (x,wall+sign*lap,main_height(x0)+.115+lap),
                             (x,wall+sign*(lap-.019),main_height(x0)+.121+lap)])
            folded_sheet(p,rows)
    # 下沿围裙越过瓦头，微拱贴住相邻瓦片。末端回折收边。
    apron = []
    for t in (-1,-.58,0,.52,1):
        swell = .005*math.cos(t*math.pi)
        apron.append([(x,cy+t*(half+spread),main_height(x)+z+swell) for x,z,spread in
                      ((down+.003,.148,.012),(down+.009,.143,.012),(down+.009,.094,.015),
                       (down+.075,.102,.032),(down+.130,.106,.048),(down+.135,.091,.049))])
    folded_sheet(p,apron)


def valley(p,sign):
    x0 = MAIN_X+(MAIN_TOP-WING_TOP)/MAIN_PITCH
    x1 = MAIN_X+MAIN_RUN
    normal = Vector((-MAIN_PITCH,sign*WING_PITCH,0)).normalized()
    def section(x):
        y = WING_Y+sign*(WING_TOP-main_height(x))/WING_PITCH
        row = []
        for width,lip in ((-.105,-.014),(-.098,.000),(-.067,.002),(-.018,-.005),
                          (0,-.012),(.018,-.005),(.067,.002),(.098,.000),(.105,-.014)):
            dx,dy = normal.x*width,normal.y*width
            z = max(main_height(x+dx), WING_TOP-abs(y+dy-WING_Y)*WING_PITCH)
            row.append((x+dx,y+dy,z+lip))
        return row
    folded_sheet(p,[section(x0+.001),section(x0+.085),section(x1),section(x1+.029)])


def chimney(p):
    cx, cy, half = CHIMNEY_X, CHIMNEY_Y, CHIMNEY_HALF
    p.group = "烟囱与泛水"
    # 墙体和压顶都保留方形贯通孔。最下层积炭形成深色内膛。
    def rim(z,h,outside,inside,mat,bevel):
        pts = [(cx+sx*r,cy+sy*r,height) for r,height in
               ((outside,z-h/2),(outside,z+h/2),(inside,z+h/2),(inside,z-h/2))
               for sx,sy in ((-1,-1),(1,-1),(1,1),(-1,1))]
        faces = [(j*4+i,j*4+(i+1)%4,((j+1)%4)*4+(i+1)%4,((j+1)%4)*4+i)
                 for j in range(4) for i in range(4)]
        p.solid(pts,faces,mat,bevel)
    rim(2.365,1.09,half,.121,"plaster",.003)
    rim(2.731,.35,.124,.102,"soot",.004)
    p.box((cx,cy,2.565),(.25,.25,.024),"soot",.003)
    # 烟囱上段的细砌石砖错缝，顶沿有封水腰线。
    for k in range(3):
        z = 2.602+k*.115
        for s in (-1,1):
            cut = -.056 if k%2 else .083
            for a,b in ((-half,cut),(cut,half)):
                p.box((cx+(a+b)/2,cy+s*(half+.003),z),(b-a-.007,.020,.106),
                      "quoin" if (k+s)%3 else "arch",.007)
            for a,b in ((-half,-cut),(-cut,half)):
                p.box((cx+s*(half+.004),cy+(a+b)/2,z),(.020,b-a-.006,.106),"quoin",.007)
    rim(2.915,.068,.251,.116,"quoin",.009)
    rim(3.011,.148,.287,.131,"arch",.015)
    rim(3.018,.104,.133,.108,"soot",.004)
    flashing(p,cx,cy,half)


def build_roof(p):
    for wing in (False,True):
        for sign in (-1,1):
            Slope(p,wing,sign).cover()
        p.group = "陶制脊瓦"
        ridge(p,wing)
    p.group = "屋谷泛水"
    for sign in (-1,1):
        valley(p,sign)
    chimney(p)
