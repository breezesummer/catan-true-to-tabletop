"""厘米级棋子的绘制以人体米制尺寸进行，导出时统一缩小。"""
import bpy, bmesh, math, json
from math import sin, cos, pi, tau
from mathutils import Vector, Matrix
from pathlib import Path

ROOT=Path('D:/Project/catan-true-to-tabletop')
OUT=ROOT/'art/recipes/knight-reference-v002'
ART=ROOT/'Unity/Catan/Assets/Art/ExpansionPieces/v001'
RES=ROOT/'Unity/Catan/Assets/Resources/ExpansionPieces/v001'
names=('steel','edge','iron','gold','mail','hide','strap','horse','dark','wood','skin','eye','colour','linen','paint','hoof')
names+=('cuirass','helm','visage','shieldwood','hammer','pauldron','greave','chamfron','peytral','boots','shaft','glove')
metal={'steel','edge','iron','gold','cuirass','helm','hammer','pauldron','greave','chamfron','peytral','glove'}
player={'colour','paint','shieldwood'}
v3=Vector
scene=bpy.data.scenes.new('骑士参考雕塑')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
tex=bpy.data.images.load(str(OUT/'表面图集.png'),check_existing=False)
mats=[]
for name in ('Expansion_Natural','Expansion_Seat','Expansion_Metal'):
    m=bpy.data.materials.new(name);m.use_nodes=True;mats.append(m)
models={}
verts=[];faces=[];materials=[];coords=[];shading=[]
transform=Matrix.Identity(4)
footprints=[]

def vertex(p):
    verts.append(tuple(transform @ Vector(p)));return len(verts)-1

def polygon(indices,k,uv,smooth=False):
    faces.append(indices)
    idx=names.index(k)
    materials.append(1 if k in player else 2 if k in metal else 0)
    coords.append([((idx%4+.015+.97*u)/4,(idx//4+.015+.97*v)/8) for u,v in uv])
    shading.append(smooth)

def rings(rows,k,cap=True,smooth=True):
    ids=[[vertex(p) for p in row] for row in rows]
    n=len(ids[0]);h=len(rows)-1
    for j in range(h):
        for i in range(n):
            b=(i+1)%n
            uv=((i/n,j/h),((i+1)/n,j/h),((i+1)/n,(j+1)/h),(i/n,(j+1)/h))
            polygon((ids[j][i],ids[j][b],ids[j+1][b],ids[j+1][i]),k if isinstance(k,str) else k(i,j),uv,smooth)
    if cap:
        uv=[(.5+.46*cos(tau*i/n),.5+.46*sin(tau*i/n)) for i in range(n)]
        keys=(k,k) if isinstance(k,str) else (k(0,0),k(0,h-1))
        polygon(ids[0][::-1],keys[0],uv[::-1]);polygon(ids[-1],keys[1],uv)

def interp(data,steps):
    a=[list(p) for p in data];out=[]
    for i in range(len(a)-1):
        p,q,r,s=a[max(0,i-1)],a[i],a[i+1],a[min(len(a)-1,i+2)]
        for j in range(steps):
            t=j/steps
            out.append([.5*(2*b+(-aa+c)*t+(2*aa-5*b+4*c-d)*t*t+(-aa+3*b-3*c+d)*t**3) for aa,b,c,d in zip(p,q,r,s)])
    return out+[a[-1]]

def shell(stations,k,n=32,steps=1,fold=0):
    rows=[]
    for x,y,z,rx,ry in interp(stations,steps):
        row=[]
        for i in range(n):
            a=tau*i/n; f=1+fold*(cos(7*a+z*4)+.42*sin(11*a-z*4))
            row.append((x+max(.0005,rx)*cos(a)*f,y+max(.0005,ry)*sin(a)*f,z))
        rows.append(row)
    rings(rows,k)

def oval(p,r,k,n=22):
    x,y,z=p;rx,ry,rz=r
    shell([(x,y,z+rz*sin(a),max(.0008,rx*cos(a)),max(.0008,ry*cos(a))) for a in (-1.558,-1.26,-.96,-.64,-.32,0,.32,.64,.96,1.26,1.558)],k,n)

def tube(path,radii,k,n=12):
    pts=[Vector(p) for p in path];rows=[]
    for i,p in enumerate(pts):
        axis=(pts[min(i+1,len(pts)-1)]-pts[max(0,i-1)]).normalized()
        ref=Vector((1,0,0)) if abs(axis.x)<.85 else Vector((0,1,0))
        u=(ref-axis*axis.dot(ref)).normalized(); v=axis.cross(u)
        r=radii[i] if isinstance(radii,list) else radii
        rx,ry=r if isinstance(r,tuple) else (r,r)
        rows.append([p+u*(rx*cos(tau*j/n))+v*(ry*sin(tau*j/n)) for j in range(n)])
    rings(rows,k)

def segment(a,b,profile,k,n=22):
    a,b=Vector(a),Vector(b)
    tube([a.lerp(b,t) for t,_,_ in profile],[(x,y) for _,x,y in profile],k,n)

def rod(a,b,r,k,end=None,n=12):
    tube([a,b],[r,end if end is not None else r],k,n)

def line(points,r,k,n=8):
    tube(interp(points,3),r,k,n)

def bead(p,r=.012,k='edge'):
    oval(p,(r,r,r*.85),k,9)

def loop(center,axis1,axis2,wire,k,n=20,sides=6):
    c=Vector(center);a=Vector(axis1);b=Vector(axis2)
    # 环绕闭合，首尾切向连续。
    points=[c+a*cos(tau*i/n)+b*sin(tau*i/n) for i in range(n)]
    normal=a.cross(b).normalized()
    rows=[]
    for i,p in enumerate(points):
        tangent=(points[(i+1)%n]-points[i-1]).normalized();rad=tangent.cross(normal)
        rows.append([p+wire*(rad*cos(tau*j/sides)+normal*sin(tau*j/sides)) for j in range(sides)])
    rings(rows+[rows[0]],k,False)

def solid(outline,offset,k):
    v=Vector(offset);rings([outline,[Vector(p)+v for p in outline]],k,True,False)

def panel(grid,k,thickness=.012,outward=(0,-1,0)):
    # 网格式曲面，正反面与边缘缝合。材质可以按面指定，纹章与布料共形。
    h=len(grid)-1; w=len(grid[0])-1
    grids=[]
    outside=Vector(outward)
    grids.append([[vertex(p) for p in row] for row in grid])
    inner=[]
    for j,row in enumerate(grid):
        inside=[]
        for i,p in enumerate(row):
            du=Vector(row[min(i+1,w)])-Vector(row[max(i-1,0)])
            dv=Vector(grid[min(j+1,h)][i])-Vector(grid[max(j-1,0)][i])
            normal=du.cross(dv).normalized()
            if normal.dot(outside)<0:normal.negate()
            inside.append(vertex(Vector(p)-thickness*normal))
        inner.append(inside)
    grids.append(inner)
    for j in range(h):
        for i in range(w):
            uv=[(i/w,j/h),((i+1)/w,j/h),((i+1)/w,(j+1)/h),(i/w,(j+1)/h)]
            for side,g in enumerate(grids):
                ids=[g[j][i],g[j][i+1],g[j+1][i+1],g[j+1][i]]
                polygon(ids[::-1] if side else ids,('strap' if side else (k if isinstance(k,str) else k(i,j))),uv[::-1] if side else uv,True)
    bounds=[(0,i) for i in range(w+1)]+[(j,w) for j in range(1,h+1)]+[(h,i) for i in range(w-1,-1,-1)]+[(j,0) for j in range(h-1,0,-1)]
    for index,(j,i) in enumerate(bounds):
        y,x=bounds[(index+1)%len(bounds)];a,b=grids
        polygon([a[j][i],b[j][i],b[y][x],a[y][x]],'strap',[(0,0),(0,1),(1,1),(1,0)])

def belt(x,y,z,rx,ry,height,k):
    shell([(x,y,z,rx,ry),(x,y,z+.012,rx+.006,ry+.006),(x,y,z+height-.009,rx+.004,ry+.004),(x,y,z+height,rx,ry)],k,40)

def mail_band(x,y,z,rx,ry,rows,count):
    # 逆向相扣的金属环沿甲裙和护颈排列，锁甲面料承接环内阴影。
    for row in range(rows):
        for col in range(count):
            a=tau*(col+.5*(row%2))/count
            center=Vector((x+rx*cos(a),y+ry*sin(a),z+.015*row))
            side=Vector((-sin(a),cos(a),0));normal=Vector((cos(a),sin(a),0))
            loop(center,side*.0135,Vector((0,0,.009))+normal*(.005*(-1)**row),.0027,'iron' if (col+row)%3 else 'steel',10,5)
