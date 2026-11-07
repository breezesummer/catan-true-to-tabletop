"""通过 Blender MCP 在独立场景内构造、展开、导出展示棋子。"""
import bpy, bmesh, math, random, json
import numpy as np
from pathlib import Path
from mathutils import Vector, Quaternion
from math import sin, cos, pi, tau
ROOT=Path('D:/Project/catan-true-to-tabletop')
ART=ROOT/'Unity/Catan/Assets/Art/ExpansionPieces/v001'
RES=ROOT/'Unity/Catan/Assets/Resources/ExpansionPieces/v001'
OUT=ROOT/'art/recipes/expansion-models-v001'
knights_only=globals().get('knights_only',False)
scene=bpy.data.scenes.new(globals().get('scene_name','三国骑士' if knights_only else '扩展棋子_第一版'))
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
rng=random.Random(1183)
# 一张颜色图集容纳各材料的纹理，所有玩家和所有网格复用。
swatches=dict(stone=(177,157,126), trim=(215,195,161), shadow=(51,42,36), wood=(127,80,45),
              plank=(177,120,65), brass=(192,145,64), steel=(155,164,167), iron=(77,82,84),
              linen=(221,204,164), player=(229,222,207), skin=(173,119,82), horse=(98,62,42),
              leather=(90,61,44), green=(83,127,64), blue=(68,99,145), yellow=(210,155,54))
names=list(swatches)
tex=bpy.data.images.new('扩展棋子手绘材质',width=1024,height=1024,alpha=False)
pix=np.ones((1024,1024,4),dtype=np.float32)
y,x=np.mgrid[:256,:256]/256
noise=np.random.default_rng(501)
for i,(name,rgb) in enumerate(swatches.items()):
    grain=.94+.033*np.sin(x*26+y*11)+.018*np.cos(y*64-x*13)+noise.normal(0,.017,x.shape)
    if name in ('wood','plank','horse'):grain+=.055*np.sin(y*169+2*np.sin(x*16+y*8))
    if name in ('linen','player'):grain+=.016*(np.sin(x*650)+np.cos(y*625))
    if name in ('iron','steel','brass'):grain+=.026*np.sin(x*75+y*17)
    vals=np.clip(np.asarray(rgb)[None,None,:]/255*grain[:,:,None],0,1)
    pix[(i//4)*256:(i//4+1)*256,(i%4)*256:(i%4+1)*256,:3]=vals
tex.pixels.foreach_set(pix.ravel());tex.filepath_raw=str(ART/'palette.png');tex.file_format='PNG'
if globals().get('save_palette',not knights_only):tex.save()
def material(name,metallic):
    m=bpy.data.materials.new(name);m.use_nodes=True
    n=m.node_tree.nodes;p=n.get('Principled BSDF');p.inputs['Metallic'].default_value=metallic;p.inputs['Roughness'].default_value=.61 if metallic else .88
    image=n.new('ShaderNodeTexImage');image.image=tex
    mix=n.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(1,1,1,1)
    m.node_tree.links.new(image.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],p.inputs['Base Color'])
    return m
mats=[material('Expansion_Natural',0),material('Expansion_Seat',0),material('Expansion_Metal',.45)]
V=[];F=[];M=[];UV=[];SM=[];models={}
def face(points,k,smooth=False):
    pts=[Vector(p) for p in points]; start=len(V);V.extend(tuple(p) for p in pts);F.append(tuple(range(start,start+len(pts))))
    M.append(1 if k=='player' else 2 if k in ('steel','iron','brass') else 0);SM.append(smooth)
    normal=(pts[1]-pts[0]).cross(pts[2]-pts[0]);axis=max(range(3),key=lambda a:abs(normal[a]))
    uaxis,vaxis=((1,2),(0,2),(0,1))[axis];i=names.index(k);du=rng.uniform(.1,.35);dv=rng.uniform(.1,.35)
    UV.append([((i%4+du+.55*((p[uaxis]+3)*.45%1))*.25,(i//4+dv+.55*((p[vaxis]+3)*.45%1))*.25) for p in pts])
def loft(rings,k,smooth=False,cap=True):
    rows=[[Vector(p) for p in row] for row in rings];n=len(rows[0])
    if cap:face(rows[0][::-1],k)
    for a,b in zip(rows,rows[1:]):
        for j in range(n):
            t=(j+1)%n;face([a[j],a[t],b[t],b[j]],k,smooth)
    if cap:face(rows[-1],k)
def rect(x,y,z,w,d,c=0):
    if c:
        a=w/2;b=d/2;c=min(a*.55,b*.55,c)
        return [(x+u,y+v,z) for u,v in [(-a+c,-b),(a-c,-b),(a,-b+c),(a,b-c),(a-c,b),(-a+c,b),(-a,b-c),(-a,-b+c)]]
    return [(x+u*w/2,y+v*d/2,z) for u,v in [(-1,-1),(1,-1),(1,1),(-1,1)]]
def box(x,y,z,w,d,h,k='stone',c=.015):
    loft([rect(x,y,z,w,d,c),rect(x,y,z+h,w,d,c)],k)
def oval(x,y,z,rx,ry,n=16):
    return [(x+rx*cos(i*tau/n),y+ry*sin(i*tau/n),z) for i in range(n)]
def column(x,y,levels,k,n=16,smooth=False):
    loft([oval(x,y,z,rx,ry,n) for z,rx,ry in levels],k,smooth)
def bulb(p,r,k,n=14):
    x,y,z=p;a,b,c=r
    column(x,y,[(z+c*sin(v),max(.002,a*cos(v)),max(.002,b*cos(v))) for v in (-1.55,-1.1,-.65,-.2,.24,.69,1.1,1.55)],k,n,True)
def rod(a,b,r,k,r2=None,n=10):
    a,b=Vector(a),Vector(b);axis=(b-a).normalized();u=axis.cross(Vector((0,0,1)))
    if u.length<.02:u=Vector((1,0,0))
    u.normalize();v=axis.cross(u)
    loft([[p+rad*(cos(i*tau/n)*u+sin(i*tau/n)*v) for i in range(n)] for p,rad in ((a,r),(b,r if r2 is None else r2))],k,n>=10)
def sheet(pts,k,t=.012):
    loft([pts,[(x,y,z+t) for x,y,z in pts]],k)
def band(x,y,z,w,d,h,k='trim'):box(x,y,z,w,d,h,k,.024)
def door(x,y,z,w,h,k='wood'):
    r=w/2;s=z+h-r
    pts=[(x-r,y,z),(x+r,y,z)]+[(x+r*cos(i*pi/10),y,s+r*sin(i*pi/10)) for i in range(11)]
    face(pts,k)
    for sign in (-1,1):box(x+sign*(r+.018),y-.008,z,.038,.044,h-r,'trim')
    for i in range(7):
        a=(i+.035)*pi/7;b=(i+.965)*pi/7
        q=[(x+rr*cos(t),yy,s+rr*sin(t)) for rr,t,yy in ((r,a,y-.025),(r,b,y-.025),(r+.046,b,y-.025),(r+.046,a,y-.025))]
        loft([q,[(a,b+.051,c) for a,b,c in q]],'trim')
    box(x,y-.024,z-.012,w+.10,.105,.032,'trim')
    if k=='wood':
        box(x,y-.01,z,.011,.012,h-.04,'shadow')
        for zz in (.25,.64):box(x,y-.011,z+h*zz,w*.88,.016,.020,'iron')
def roof(x,y,z,w,d,rise,hip=False,k='player'):
    for side in (-1,1):
        a=Vector((x+side*w/2,y-d/2,z));b=Vector((x+side*w/2,y+d/2,z))
        c=Vector((x,y-(0 if hip else d/2),z+rise));e=Vector((x,y+(0 if hip else d/2),z+rise))
        panel=[a,b,e] if hip else [a,b,e,c];sheet([tuple(pt) for pt in panel],k,.025)
        for t in (.28,.56,.81):
            rod(a.lerp(c,t),b.lerp(e,t),.011,k,n=6)
        for yy in (-1,1):
            start=(x+side*w/2,y+yy*d/2,z+.023);end=(x,y+yy*(0 if hip else d/2),z+rise+.022)
            rod(start,end,.023,k,n=8)
    for yy in (-1,1):
        endY=y+yy*d/2
        sheet([(x-w/2,endY,z),(x+w/2,endY,z),(x,y if hip else endY,z+rise)],k if hip else 'stone',.020)
        rod((x-w/2,endY,z),(x+w/2,endY,z),.023,'wood',n=6)
    if not hip:rod((x,y-d/2,z+rise+.022),(x,y+d/2,z+rise+.022),.028,k)
def masonry(x,y,z,w,d,h):
    box(x,y,z,w,d,h)
    rows=max(1,round(h/.20))
    for row in range(rows):
        zz=z+(row+.04)*h/rows
        for sign in (-1,1):
            for side in (0,1):
                length=(w,d)[side];step=length/max(2,round(length/.28));offset=.5*step if row%2 else 0
                start=-length/2-offset
                while start<length/2-.01:
                    a=max(start,-length/2);b=min(start+step,length/2)
                    if b-a>.038:
                        mid=(a+b)/2;colour='trim' if rng.random()<.12 else 'stone'
                        if side==0:box(x+mid,y+sign*d/2,zz,b-a-.012,.026,h/rows*.91,colour,.009)
                        else:box(x+sign*w/2,y+mid,zz,.026,b-a-.012,h/rows*.91,colour,.009)
                    start+=step
def steps(x,y,w,n=3):
    for j in range(n):box(x,y-.092*j,0,w+.025*j,.11,.048*(n-j),'trim')
def banner(x,y,z,k):
    pts=[(x-.085,y,z),(x+.085,y,z),(x+.08,y-.008,z-.25),(x,y-.016,z-.32),(x-.08,y-.008,z-.25)]
    loft([pts,[(a,b+.015,c) for a,b,c in pts]],k);rod((x-.12,y,z+.015),(x+.12,y,z+.015),.014,'brass')
def crate(x,y,z,w=.22):
    box(x,y,z,w,w*.83,w*.85,'plank')
    for u in (-.33,.33):box(x+u*w,y,z+.003,w*.12,w*.86,w*.86,'wood')
def barrel(x,y,z,r=.09):
    column(x,y,[(z,r*.83,r*.83),(z+r*.9,r,r),(z+1.9*r,r*.84,r*.84)],'plank',12)
    for t in (.32,1.46):column(x,y,[(z+t*r,r*.98,r*.98),(z+(t+.14)*r,r*.98,r*.98)],'iron',12)
def gatewall():
    # 门洞与原主城台阶共轴，内院可容纳完整大都会。
    for x in (-.83,.83):masonry(x,0,0,.14,1.62,.37)
    masonry(0,.81,0,1.67,.14,.37)
    for sign in (-1,1):masonry(sign*.54,-.81,0,.55,.14,.37)
    for x in (-.83,.83):
        for y in (-.81,0,.81):masonry(x,y,0,.21,.21,.49);band(x,y,.48,.24,.24,.046)
    for x in (-.264,.264):masonry(x,-.81,0,.16,.21,.51)
    for i in range(9):
        a=(i+.02)*pi/9;b=(i+.98)*pi/9
        q=[(r*cos(t),-.917,.29+r*sin(t)) for r,t in ((.186,a),(.186,b),(.263,b),(.263,a))]
        loft([q,[(u,v+.20,w) for u,v,w in q]],'trim')
    for y in (-.62,-.37,-.12,.14,.40,.65):
        for x in (-.83,.83):box(x,y,.37,.15,.10,.07,'trim')
    for x in (-.65,-.42,0,.42,.65):box(x,.81,.37,.12,.15,.07,'trim')
    for x in (-.36,.36):banner(x,-.897,.35,'player')
def market():
    for x in (-.59,.59):
        masonry(x,.03,0,.37,1.33,.41)
        roof(x,.03,.44,.50,1.46,.25)
        side=1 if x<0 else -1
        for y in (-.54,-.17,.20,.55):
            box(x+side*.27,y,.02,.060,.07,.39,'wood')
        box(x+side*.27,.02,.40,.07,1.26,.08,'wood')
        for y in (-.47,-.02,.44):
            bx=x+side*.13
            box(bx,y,.52,.17,.20,.115,'stone')
            roof(bx,y,.63,.205,.235,.087)
        masonry(x*.57,.55,0,.27,.31,.43)
        roof(x*.57,.55,.45,.31,.37,.15)
    for j in range(6):
        for sign in (-1,1):box(sign*.092,-.55+j*.16,0,.174,.152,.028,'trim' if j%3 else 'stone',.022)
    masonry(0,.56,0,.48,.42,1.10);door(0,.337,.10,.19,.36)
    box(0,.33,.80,.18,.027,.19,'shadow');bulb((0,.304,.87),(.05,.035,.067),'brass')
    roof(0,.56,1.10,.57,.54,.27,True);banner(0,.312,.76,'yellow')
    for y in (-.34,.08):
        for x in (-.37,-.13):rod((x,y,.02),(x,y,.37),.017,'wood')
        sheet([(-.42,y-.12,.40),(-.09,y-.13,.32),(-.09,y+.13,.32),(-.42,y+.13,.40)],'yellow')
        crate(-.23,y,.01,.15)
    for x,y in ((.29,-.38),(.15,-.40),(.28,.17)):barrel(x,y,0,.073)
    steps(0,-.58,.39,2)
def palace():
    masonry(0,.16,0,.82,.74,1.28);band(0,.16,1.24,.90,.83,.075)
    roof(0,.16,1.315,.79,.72,.24,True)
    for x in (-.62,.62):
        masonry(x,.07,0,.37,.47,.83);roof(x,.07,.85,.40,.50,.18,True)
        for sx in (-1,1):
            for sy in (-1,1):box(x+sx*.17,.07+sy*.20,.82,.09,.10,.14,'trim')
        door(x,-.179,.39,.105,.24,'shadow')
    for x in (-.37,-.13,.13,.37):box(x,-.24,1.27,.09,.10,.13,'trim')
    door(0,-.231,.16,.29,.54)
    for x in (-.32,.32):
        column(x,-.42,[(.16,.077,.077),(.22,.057,.057),(.76,.055,.055),(.81,.081,.081)],'trim',10)
        banner(x,-.49,.68,'blue')
    box(0,-.34,.80,.86,.38,.095,'trim');steps(0,-.48,.71,4)
    for x in (-.25,0,.25):door(x,-.222,.97,.09,.20,'shadow')
def instrument(x,y,z,r):
    for axis,tilt in [((1,0,.3),.95),((0,1,0),1.38),((1,.55,0),.39)]:
        q=Quaternion(Vector(axis).normalized(),tilt)
        pts=[Vector((x,y,z))+q@Vector((r*cos(tau*i/32),r*sin(tau*i/32),0)) for i in range(32)]
        for i in range(32):rod(pts[i],pts[(i+1)%32],r*.076,'brass',n=6)
    bulb((x,y,z),(.065,.065,.065),'brass')
    rod((x-r*.66,y,z-r*.83),(x+r*.66,y,z+r*.83),.023,'brass')
def science():
    x,y=-.29,.10
    column(x,y,[(0,.43,.43),(.13,.43,.43),(.15,.39,.39),(.78,.39,.39),(.82,.43,.43),(.90,.43,.43)],'stone',24)
    for z in (.22,.53,.83):column(x,y,[(z,.409,.409),(z+.032,.411,.411)],'trim',24)
    column(x,y,[(.91+.31*sin(a),max(.03,.46*cos(a)),max(.03,.46*cos(a))) for a in (0,.27,.55,.83,1.12,1.49)],'player',24)
    for i in range(10):
        a=tau*i/10;rod((x+.39*cos(a),y+.39*sin(a),.30),(x+.39*cos(a),y+.39*sin(a),.78),.024,'trim')
    door(x,y-.403,.12,.18,.38);banner(x-.21,y-.35,.52,'green')
    instrument(x,y,1.48,.245)
    for i in range(11):
        a=-.50+i*.20;b=a+.20;cx=.02;cy=-.02
        def arc(r,h,t):return (cx+r*cos(t),cy+r*sin(t),h)
        ring=[arc(.55,.02,a),arc(.78,.02,a),arc(.78,.02,b),arc(.55,.02,b)]
        sheet(ring,'stone',.39)
        q=[arc(.51,.41,a),arc(.82,.41,a),arc(.82,.41,b),arc(.51,.41,b)]
        sheet(q,'player',.055)
        rod(arc(.53,.01,a),arc(.53,.39,a),.028,'trim')
    steps(x,-.37,.34);column(.25,-.13,[(0,.085,.085),(.16,.056,.056)],'trim')
    bulb((.25,-.13,.19),(.060,.060,.060),'brass')

