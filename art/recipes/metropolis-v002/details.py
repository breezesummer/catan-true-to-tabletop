"""三座大都会共享的木石细部和曲面工具，长度单位在导出时统一换算。"""
def path(points,r,k,n=8,closed=False):
    pts=[Vector(p) for p in points]
    if len(pts)<2:return
    rings=[]
    for i,p in enumerate(pts):
        a=pts[i-1] if i or closed else p
        b=pts[(i+1)%len(pts)] if i+1<len(pts) or closed else p
        axis=(b-a).normalized()
        seed=Vector((0,0,1)) if abs(axis.z)<.91 else Vector((1,0,0))
        u=axis.cross(seed).normalized();v=axis.cross(u)
        rings.append([p+r*(u*cos(tau*j/n)+v*sin(tau*j/n)) for j in range(n)])
    loft(rings+[rings[0]] if closed else rings,k,True,not closed)

def textile_face(points,k,coords,smooth=True):
    # 整块布、瓦坡或铜瓣连续取样，避免每个四边面各自截取色块。
    face(points,k,smooth)
    i=names.index(k)
    UV[-1]=[((i%4+.055+.89*u)/4,(i//4+.055+.89*v)/4) for u,v in coords]

def column_base(x,y,z,h,r,k='wood'):
    column(x,y,[(z,r*1.5,r*1.5),(z+.040,r*1.48,r*1.48),(z+.055,r*1.12,r*1.12)],'trim',16)
    column(x,y,[(z+.047,r,r),(z+h*.36,r*1.04,r*1.04),(z+h,r*.82,r*.82)],k,16,True)
    for zz in (z+.065,z+h-.050):
        column(x,y,[(zz,r*1.12,r*1.12),(zz+.026,r*1.12,r*1.12)],'brass',16)

def pavement(x,y,w,d,z=.028):
    box(x,y,0,w,d,z,'stone',.095)
    cols=max(2,round(w/.19));rows=max(2,round(d/.14))
    for j in range(rows):
        offset=.5*(j%2)
        for i in range(-1,cols):
            a=max(0,i+offset);b=min(cols,i+offset+1)
            if b-a<.22:continue
            xx=x+((a+b)/2/cols-.5)*w;yy=y+((j+.5)/rows-.5)*d
            if abs(xx-x)>w/2-.085 and abs(yy-y)>d/2-.085:continue
            box(xx,yy,z-.002,(b-a)*w/cols-.010,d/rows-.009,.015,
                'trim' if (i+2*j)%7==0 else 'stone',.010)

def pennant(x,y,z,size,k='player'):
    rod((x,y,z-.07),(x,y,z+size*.70),.012,'brass',.009)
    tip=z+size*.65
    top=[(x+size*t,y+.045*sin(pi*t*1.5),tip+.022*sin(t*pi*2)-.032*t) for t in (0,.25,.5,.75,1)]
    bottom=[(a,b-.009,h-size*(.42-.31*i/4)) for i,(a,b,h) in enumerate(top)]
    for i in range(4):sheet([top[i],top[i+1],bottom[i+1],bottom[i]],k,.007)
    bulb((x,y,tip+.02),(.022,.022,.028),'brass',10)

def podium(x,y,w,d,h):
    box(x,y,0,w,d,h*.20,'wood',.064)
    masonry(x,y,h*.18,w-.034,d-.034,h*.72)
    band(x,y,h*.88,w+.024,d+.024,h*.12,'trim')

def threshold(x,y,w,depth,h,count,entry_width=None):
    for j in range(count):
        a=j/count
        width=w if entry_width is None else entry_width+(w-entry_width)*a
        box(x,y-depth*(1-a),.004,width,depth/count+.015,(j+1)*h/count,'stone',.022)
        box(x,y-depth*(1-a)-depth/count*.46,(j+1)*h/count,width,.018,.009,'trim',.006)

def lantern(x,y,z):
    rod((x,y,z+.012),(x,y,z+.11),.008,'brass')
    column(x,y,[(z-.12,.034,.034),(z-.10,.054,.054),(z-.017,.061,.061),(z+.008,.038,.038)],'yellow',10)
    for h in (-.115,-.008): column(x,y,[(z+h,.048,.048),(z+h+.012,.049,.049)],'brass',10)
    rod((x,y,z-.12),(x,y,z-.172),.008,'brass',.004)

def rail(a,b,z,h=.16):
    a,b=Vector(a),Vector(b)
    count=max(1,round((b-a).length/.23))
    for j in range(count+1):
        p=a.lerp(b,j/count)
        box(p.x,p.y,z,.046,.046,h,'trim',.006)
        bulb((p.x,p.y,z+h+.012),(.032,.032,.027),'trim',10)
    for height in (h*.45,h*.88):
        rod((a.x,a.y,z+height),(b.x,b.y,z+height),.015,'trim',n=8)
