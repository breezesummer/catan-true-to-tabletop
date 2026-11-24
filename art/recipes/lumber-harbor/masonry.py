"""沿真实墙面砌灰岩、留拱洞、加工拱券。坐标和厚度均以毫米计。"""
import math
from dataclasses import dataclass
from joinery import RNG, V, UP, grain, panel, tube

STONE=("ashlar","ashlar","ashlar","greyStone","buffStone")


def skin(part, points, inward, tile, uv, edge=None):
    points=list(map(V,points))
    normal=sum(((b-a).cross(c-a) for a,b,c in zip(points,points[1:],points[2:])),V((0,0,0)))
    if normal.dot(V(inward))>0:
        points.reverse()
        uv=uv[::-1]
    panel(part,points,inward,tile,uv,edge)


class Face:
    def __init__(self,x,y,axis):
        self.origin=V((x,y,0))
        self.u=V((*axis,0))
        self.out=self.u.cross(UP)

    def at(self,u,s,z):
        return self.origin+self.u*u+self.out*s+UP*z


def ashlar(part,wall,outline,tile,front=0,depth=.44,bevel=.045):
    """每块石料的迎光棱、断角和墙身分别成面；门内侧保留真实进深。"""
    corners=[V(p) for p in outline]
    if len(corners)<3:
        return
    center=sum(corners,V((0,0)))/len(corners)
    border=[]
    for k,p in enumerate(corners):
        prev,nxt=corners[k-1]-p,corners[(k+1)%len(corners)]-p
        cut=bevel*RNG.uniform(.73,1.54)
        if RNG.random()<.09:
            cut*=2.2
        for delta in (prev,nxt):
            border.append(p+delta.normalized()*min(cut,.22*delta.length))
    bevel=min(bevel,min((p-center).length for p in border)*.23)
    inner=[p+(center-p).normalized()*bevel for p in border]
    back=part.verts([wall.at(u,front-depth,z) for u,z in border])
    shoulder=part.verts([wall.at(u,front-bevel,z) for u,z in border])
    top=part.verts([wall.at(u,front+RNG.uniform(-.005,.005),z) for u,z in inner])
    x0,x1=min(v.x for v in border),max(v.x for v in border)
    z0,z1=min(v.y for v in border),max(v.y for v in border)
    tex=grain(max(.05,x1-x0),max(.05,z1-z0))
    def uv(p):
        return tex((p.x-x0)/max(.01,x1-x0),(p.y-z0)/max(.01,z1-z0))
    part.face(top,tile,[uv(p) for p in inner])
    part.face(back[::-1],tile,[uv(p) for p in border][::-1])
    for i in range(len(border)):
        j=(i+1)%len(border)
        edge=[uv(border[i]),uv(border[j]),uv(inner[j]),uv(inner[i])]
        part.face([shoulder[i],shoulder[j],top[j],top[i]],tile,edge)
        part.face([back[i],back[j],shoulder[j],shoulder[i]],tile,
                  [(.06,.13),(.80,.13),(.80,.51),(.06,.51)])


@dataclass(frozen=True)
class Opening:
    center: float
    half: float
    bottom: float
    spring: float
    rise: float
    pointed: bool=False

    @property
    def peak(self):
        return self.spring+self.rise

    @property
    def radius(self):
        return (self.half**2+self.rise**2)/(2*(self.half if self.pointed else self.rise))

    def top(self,u):
        d=min(abs(u-self.center),self.half)
        r=self.radius
        if self.pointed:
            return self.spring+math.sqrt(max(0,r*r-(d+r-self.half)**2))
        return self.peak-r+math.sqrt(max(0,r*r-d*d))

    def arc(self,n):
        # 等角划分，肩部也有独立的楔石。
        a=math.atan2(self.rise,self.radius-self.half) if self.pointed else math.asin(self.half/self.radius)
        pts=[]
        for i in range(n+1):
            t=2*i/n-1
            if self.pointed:
                ang=(1-abs(t))*a
                x=(self.radius*math.cos(ang)-self.radius+self.half)*(1 if t>=0 else -1)
                z=self.spring+self.radius*math.sin(ang)
            else:
                x=self.radius*math.sin(t*a)
                z=self.peak-self.radius+self.radius*math.cos(t*a)
            pts.append(V((self.center+x,z)))
        return pts


def cut_stone(a,b,low,high,opening):
    """沿券口裁当前这块石头。保留相邻整石的水平砌缝，不把窗台标高切进整面墙。"""
    rect=[V((a,low)),V((b,low)),V((b,high)),V((a,high))]
    if opening is None:
        return [rect]
    hole=[V((opening.center-opening.half,opening.bottom)),V((opening.center+opening.half,opening.bottom)),
          *reversed(opening.arc(18))]
    def edges(points):
        return list(zip(points,points[1:]+points[:1]))
    outer,inner=edges(rect),edges(hole)
    def cross(p,q):
        return p.x*q.y-p.y*q.x
    def inside(p,ring):
        return all(cross(v-u,p-u)>=-.000003 for u,v in ring)
    # 对保留区的有向边逐一截段，闭合后得到整石、异形券肩石或洞口两侧的分件。
    links={}
    for boundary,other,reverse in ((outer,inner,False),(inner,outer,True)):
        for u,v in boundary:
            d=v-u
            marks={0.,1.}
            for c,e in other:
                t=e-c
                den=cross(d,t)
                if abs(den)<.0000001:
                    continue
                s=cross(c-u,t)/den
                f=cross(c-u,d)/den
                if -.000001<s<1.000001 and -.000001<f<1.000001:
                    marks.add(round(min(1,max(0,s)),7))
            vals=sorted(marks)
            for l,r in zip(vals,vals[1:]):
                if inside(u+d*((l+r)*.5),other)!=reverse:
                    continue
                a0,b0=(r,l) if reverse else (l,r)
                start=tuple(round(x,4) for x in u+a0*d)
                end=tuple(round(x,4) for x in u+b0*d)
                if start!=end:
                    links[start]=end
    result=[]
    while links:
        start=next(iter(links))
        pts=[]
        v=start
        while v in links:
            pts.append(v)
            v=links.pop(v)
        if v==start and len(pts)>=3:
            area=sum(x*h-z*u for (x,z),(u,h) in edges(pts))
            if area>.004:
                result.append(pts)
    return result


def lay_wall(p,wall,start,end,low,high,openings=(),step=.76,block=1.59,quay=False):
    courses=round((high-low)/step)
    count=max(1,round((end-start)/block))
    for row in range(courses):
        base=low+(high-low)*row/courses
        cap=low+(high-low)*(row+1)/courses
        marks=[start]
        for i in range(1,count+row%2):
            fraction=(i-(row%2)*.49+RNG.uniform(-.12,.12))/count
            marks.append(start+(end-start)*fraction)
        marks.append(end)
        for a,b in zip(marks,marks[1:]):
            hole=next((o for o in openings if base<o.peak and cap>o.bottom and
                       b>o.center-o.half and a<o.center+o.half),None)
            for outline in cut_stone(a,b,base,cap,hole):
                skin(p,[wall.at(u,-.095,z) for u,z in outline],-wall.out*.33,"mortar",
                     [(.08+.81*(u-a)/(b-a),.08+.79*(z-base)/(cap-base)) for u,z in outline])
            for outline in cut_stone(a+.017,b-.017,base+.019,cap-.019,hole):
                tile=RNG.choice(STONE)
                if base<-2.47 and (cap<-2.72 or RNG.random()<.40):
                    tile="tideStone"
                ashlar(p,wall,outline,tile,RNG.uniform(-.017,.026),.52 if quay else .44,.046)


def dress_opening(p,wall,o,width,n,project=.128):
    inner=o.arc(n)
    outer=[]
    for i,pt in enumerate(inner):
        tangent=(inner[min(n,i+1)]-inner[max(0,i-1)]).normalized()
        normal=V((-tangent.y,tangent.x))
        outer.append(pt+normal*width*(1.18 if o.pointed and i*2==n else 1))
    for i in range(n):
        a,b=inner[i],inner[i+1]
        c,d=outer[i+1],outer[i]
        gap=.026 if width>.23 else .037
        coords=(a.lerp(b,gap),d.lerp(c,gap),c.lerp(d,gap),b.lerp(a,gap))
        # 券石外弧在上，按墙外法线排序。
        ashlar(p,wall,list(reversed(coords)),"dressed",project+(.043 if i in ((n-1)//2,n//2) else 0),
               .62,min(.045,width*.13))
    nrow=max(1,round((o.spring-o.bottom)/(.72 if width>.25 else .58)))
    for s in (-1,1):
        edge=o.center+s*o.half
        for i in range(nrow):
            lo=o.bottom+(o.spring-o.bottom)*i/nrow+.017
            hi=o.bottom+(o.spring-o.bottom)*(i+1)/nrow-.012
            a,b=sorted((edge,edge+s*width*(1.08 if i==0 else 1)))
            ashlar(p,wall,[(a,lo),(b,lo),(b,hi),(a,hi)],"dressed",project,.62,min(.045,width*.14))


def lancet(stone,glass,wall,o):
    dress_opening(stone,wall,o,.178,8,.105)
    outline=[V((o.center-o.half,o.bottom)),V((o.center+o.half,o.bottom)),*reversed(o.arc(16))]
    panel(glass,[wall.at(u,-.20,z) for u,z in outline],-wall.out*.12,"glass",
          [(.5+(u-o.center)/(2*o.half)*.9,.06+(z-o.bottom)/(o.peak-o.bottom)*.86) for u,z in outline])
    # 中梃分成两条尖瓣，玻璃在石窗棂和铅条后面。
    s=-.055; c=o.center; w=o.half
    fork=o.spring-.10
    def bar(points,r=.052,tile="dressed"):
        tube(glass,[wall.at(u,s,z) for u,z in points],r,tile,8)
    bar([(c,o.bottom),(c,fork)])
    for sign in (-1,1):
        bar([(c,fork),(c+sign*.10*w,fork+.20),(c+sign*.40*w,fork+.42),(c+sign*.60*w,fork+.56)])
        bar([(c+sign*.96*w,o.bottom+.55),(c+sign*.03*w,o.bottom+.55)],.022,"iron")
    a,b=c-w-.26,c+w+.26
    ashlar(stone,wall,[(a,o.bottom-.16),(b,o.bottom-.16),(b,o.bottom+.02),(a,o.bottom+.02)],"dressed",.21,.55,.033)
