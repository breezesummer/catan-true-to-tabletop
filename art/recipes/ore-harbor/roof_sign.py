"""错缝叠铺的劈石瓦、分节压脊和在低仓海侧坡面上的原矿交易牌。"""
import math
from pathlib import Path
import bpy
from craft import (BACK,HALL_EAVE,HALL_FRONT,HALL_LEFT,HALL_MID,HALL_PEAK,HALL_RIGHT,HALL_SLOPE,
                   TOWER_EAVE,TOWER_FRONT,TOWER_PEAK,TOWER_W,TOWER_X,UP,V,Part,R,
                   beam,faced_block,mineral,pin,skin,tube)
from surfaces_ore import atlas_uv


def slate_roof(p,frame,ridge,along,run,start,end,span,slope,rows):
    along,run,peak=V(along),V(run),V(ridge)
    step=span/rows
    for side in (-1,1):
        downhill=run*side
        norm=(UP+downhill*slope).normalized()
        def pos(u,v,lift=0): return peak+along*u+downhill*v+UP*(-slope*v+lift)
        felt=[pos(start,0),pos(end,0),pos(end,span),pos(start,span)]
        faced_block(frame,felt,norm,.13,"tar",.021)
        beam(frame,pos(start,span,-.10),pos(end,span,-.10),.19,.21,"tar",.023)
        for j in range(rows):
            d0=max(0,j*step-.21); d1=(j+1)*step
            nominal=.82 if rows==7 else .85
            marks=[start]; cursor=start-(nominal*.50 if j%2 else 0)
            while cursor<end:
                cursor+=nominal*R.uniform(.91,1.09)
                if cursor<end-.24: marks.append(cursor)
            marks.append(end)
            for a,b in zip(marks,marks[1:]):
                a+=.014; b-=.014
                v=d1+R.uniform(-.040,.040)
                bevel=R.uniform(.033,.088)
                chipped=R.uniform(.016,.062)
                profile=[(a,d0),(b,d0),(b,v-bevel),(b-bevel*.80,v),(a+.23*(b-a),v+.013),
                         (a+bevel,v-chipped*.38),(a,v-bevel-chipped)]
                def lift(y): return .049+.114*(y-d0)/(v-d0)
                n=(UP+downhill*(slope-.114/(v-d0))).normalized()
                faced_block(p,[pos(x,y,lift(y)) for x,y in profile],n,.108,
                            R.choice(("slate0","slate0","slate0","slate1","slate2","slate2","slate3")),.018)
        # 封山檐瓦沿坡分段，角度与每侧瓦面一致。
        for u in (start+.018,end-.018):
            for j in range(rows):
                a,b=step*j,step*(j+1)
                beam(frame,pos(u,a,.147),pos(u,b-.025,.147),.208,.178,"slate1",.039,up=norm,end="slate2")
    # 半筒形脊帽盖住两坡，端头以收窄的裙边搭在下一节之上。
    count=max(4,round((end-start)/.95))
    for i in range(count):
        s0=start+(end-start)*i/count
        s1=start+(end-start)*(i+1)/count
        rings=[]
        for s,r in ((s0-.020,.255),(s0+.067,.262),(s1-.035,.230),(s1+.014,.218)):
            angles=[math.pi*j/10 for j in range(11)]
            rings.append(p.verts([peak+along*s+run*(r*math.cos(t))+UP*(.105+r*math.sin(t)) for t in angles]))
        tile=R.choice(("slate1","slate3"))
        flip=along.cross(run).dot(UP)<0
        for i0 in range(3):
            for j in range(10):
                ids=[rings[i0][j],rings[i0+1][j],rings[i0+1][j+1],rings[i0][j+1]]
                if not flip: ids.reverse()
                p.face(ids,tile,[(.06,.08+j*.078),(.88,.08+j*.078),(.88,.158+j*.078),(.06,.158+j*.078)],True)
        for ring,n in ((rings[0],-along),(rings[-1],along)):
            pts=[V(p.vertices[k])*1000 for k in ring]
            if (pts[1]-pts[0]).cross(pts[2]-pts[0]).dot(n)<0: pts.reverse()
            skin(p,pts,tile)


def roofs():
    slate,frame=Part("双向石板瓦与盖脊"),Part("封山板与檐口")
    slate_roof(slate,frame,(TOWER_X,0,TOWER_PEAK),(0,1,0),(1,0,0),
               TOWER_FRONT-.43,BACK+.32,TOWER_W+.39,(TOWER_PEAK-TOWER_EAVE)/TOWER_W,7)
    slate_roof(slate,frame,(0,HALL_MID,HALL_PEAK),(1,0,0),(0,1,0),
               HALL_LEFT-.09,HALL_RIGHT+.38,(HALL_MID-HALL_FRONT)+.44,HALL_SLOPE,7)
    return slate,frame


X=V((1,0,0)); S=V((0,1,HALL_SLOPE)).normalized(); N=X.cross(S)
SIGN_X=3.67
SIGN_Y=2.32
SIGN=V((SIGN_X,SIGN_Y,HALL_PEAK-(HALL_MID-SIGN_Y)*HALL_SLOPE))+N*.40
SIGN_W,SIGN_H=12.50,4.29


def at(x,y,h=0):
    return SIGN+X*x+S*y+N*h


def rectangle(width,height,r):
    a,b=width/2,height/2
    return [(-a+r,-b),(a-r,-b),(a,-b+r),(a,b-r),(a-r,b),(-a+r,b),(-a,b-r),(-a,-b+r)]


def plaque_body():
    p=Part("切角铆边漆牌")
    for x in (-SIGN_W*.34,SIGN_W*.34):
        beam(p,at(x,-SIGN_H*.51,-.27),at(x,SIGN_H*.51,-.27),.28,.30,"tar",.035,up=N)
    outer=rectangle(SIGN_W,SIGN_H,.29)
    faced_block(p,[at(x,y,.02) for x,y in outer],N,.29,"oak",.067)
    faced_block(p,[at(x,y,.094) for x,y in rectangle(SIGN_W-.28,SIGN_H-.28,.235)],N,.104,"enamel",.065)
    for shrink,radius,depth,tile in ((.20,.079,.113,"bronze"),(.43,.033,.116,"iron")):
        path=[at(x,y,depth) for x,y in rectangle(SIGN_W-shrink,SIGN_H-shrink,.205)]
        tube(p,path,radius,tile,8,True)
    for x in (-SIGN_W/2+.36,SIGN_W/2-.36):
        for y in (-SIGN_H/2+.34,SIGN_H/2-.34): pin(p,at(x,y,.124),N,.087,"bronze")
    return p


def plaque():
    p=plaque_body()
    for x,y,w,h,depth in ((2.45,-.49,1.11,.85,.47),(4.38,-.52,1.14,.86,.51),(3.54,.72,1.13,.86,.51)):
        mineral(p,at(x,y,.14+depth*.15),(w,h,depth),"galena",(X,S,N))
    return p


def lettering(scene,mat,ratio="2:1"):
    c=bpy.data.curves.new("铸造衬线字","FONT")
    c.font=bpy.data.fonts.load(str(Path("C:/Windows/Fonts/georgia.ttf")))
    c.body=ratio; c.extrude=.026; c.bevel_depth=.010; c.bevel_resolution=2; c.resolution_u=7
    obj=bpy.data.objects.new("黄铜比例铸字",c)
    scene.collection.objects.link(obj)
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active=obj
    def glyph_bounds(text):
        c.body=text
        evaluated=obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
        sample=evaluated.to_mesh()
        try:
            xs=[v.co.x for v in sample.vertices]
            ys=[v.co.y for v in sample.vertices]
            return min(xs),max(xs),min(ys),max(ys)
        finally:
            evaluated.to_mesh_clear()

    if ratio=="3:1":
        # Georgia 的 3 带下伸部，用矿港 1 的上下缘对齐数字，冒号沿用原基线。
        ref_left,ref_right,ref_bottom,ref_top=glyph_bounds("2:1")
        _,_,one_bottom,one_top=glyph_bounds("1")
        _,three_right,three_bottom,three_top=glyph_bounds("3")
        c.body=ratio
    obj.select_set(True); bpy.ops.object.convert(target="MESH")
    mesh=obj.data
    pts=[v.co.copy() for v in mesh.vertices]
    x0,x1=min(v.x for v in pts),max(v.x for v in pts)
    y0,y1=min(v.y for v in pts),max(v.y for v in pts)
    uv=mesh.uv_layers.active or mesh.uv_layers.new()
    uv.name="HarborUV"
    for v,co in zip(mesh.vertices,pts):
        t,s=(co.x-x0)/(x1-x0),(co.y-y0)/(y1-y0)
        x=-5.36+6.28*t
        if ratio=="3:1":
            x=(co.x-(x0+x1)/2)*6.28/(ref_right-ref_left)
            y=co.y
            if co.x<=three_right+1e-6:
                y=one_bottom+(co.y-three_bottom)*(one_top-one_bottom)/(three_top-three_bottom)
            s=(y-ref_bottom)/(ref_top-ref_bottom)
        v.co=at(x,-1.28+2.65*s,.229+3.55*co.z)*.001
    for l in mesh.loops:
        co=pts[l.vertex_index]
        uv.data[l.index].uv=atlas_uv("bronze",.07+.85*(co.x-x0)/(x1-x0),.07+.85*(co.y-y0)/(y1-y0))
    mesh.update()
    mesh.materials.append(mat); obj.select_set(False)
    return obj
