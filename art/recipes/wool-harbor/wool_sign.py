"""贴在临海屋坡上的双线招牌，铸字、羊脸、耳廓和卷毛均有实体厚度。"""
import math
from wool_craft import Part,V,extrude,ovoid,tube

SLOPE=(10.47-6.12)/4.48
COS=1/math.sqrt(1+SLOPE*SLOPE)
SIN=COS*SLOPE
PX,PY=4.03,3.16
PZ=6.12+(PY-1.0)*SLOPE+.25
WIDTH,HEIGHT=12.50,4.29


def mount(x,y,z):
    # 局部负 Y 为牌面向外，正 Z 为从檐口到屋脊。
    return (PX+x,PY+COS*z+SIN*y,PZ+SIN*z-COS*y)


def sheep():
    p=Part("雕刻母羊徽记")
    offset=3.91
    def pt(x,y,z): return (offset+1.20*x,y,z*1.045+.015)
    # 在下颌、鼻梁、眼窝和额头分别布环。侧向收窄的口鼻前伸，耳朵在眼睛后方。
    profiles=[(-1.37,.26,.22,-.62),(-1.29,.45,.31,-.69),(-1.11,.54,.35,-.71),
              (-.83,.54,.38,-.72),(-.51,.63,.39,-.64),(-.15,.80,.43,-.60),
              (.28,.84,.39,-.59),(.68,.80,.32,-.56),(.94,.65,.26,-.50),(1.10,.34,.17,-.50)]
    seg=24
    rings=[]
    for z,rx,ry,yc in profiles:
        rings.append(p.verts([pt(rx*math.cos(math.tau*i/seg),yc+ry*math.sin(math.tau*i/seg),z)
                              for i in range(seg)]))
    for j,(a,b) in enumerate(zip(rings,rings[1:])):
        for i in range(seg):
            k=(i+1)%seg
            p.face((a[i],a[k],b[k],b[i]),"ivory",[(i/seg,j/10),((i+1)/seg,j/10),((i+1)/seg,(j+1)/10),(i/seg,(j+1)/10)],True)
    for ids,sgn in ((rings[0],-1),(rings[-1],1)):
        p.face(ids[::sgn],"ivory",[(.5+.4*math.cos(math.tau*i/seg),.5+.4*math.sin(math.tau*i/seg))
                                   for i in range(seg)][::sgn],True)
    for sign in (-1,1):
        # 软垂耳廓由外缘到内凹耳窝逐圈转折，前视能看见内侧的暖色。
        def ear(i,scale,depth):
            a=math.tau*i/24
            along=scale*math.cos(a)
            spread=scale*math.sin(a)
            return pt(sign*(1.32+.85*along),depth-.09*(1-along*along),
                      .36-.27*along+.338*spread*(1-.26*along)+.16*(1-along*along))
        rims=[p.verts([ear(i,scale,depth) for i in range(24)]) for scale,depth in
              ((.14,-.28),(1,-.41),(.91,-.67),(.66,-.65),(.30,-.535),(.06,-.53))]
        for j,(outer,inner) in enumerate(zip(rims,rims[1:])):
            for i in range(24):
                k=(i+1)%24
                face=[outer[i],outer[k],inner[k],inner[i]]
                coords=[(i/24,j/6),((i+1)/24,j/6),((i+1)/24,(j+1)/6),(i/24,(j+1)/6)]
                p.face(face[::sign],"ivory" if j<3 else "horn",coords[::sign],True)
        center=p.verts([pt(sign*1.32,-.62,.52)])[0]
        for i in range(24):
            ids=[rims[-1][i],rims[-1][(i+1)%24],center]
            p.face(ids[::sign],"horn",[(.4,.4),(.5,.4),(.45,.50)],True)
        # 眼线埋在眉弓下，瞳孔横向收窄。
        ovoid(p,pt(sign*.568,-.897,.14),(.203,.070,.116),"horn",16,7,sign*.23)
        ovoid(p,pt(sign*.568,-.960,.136),(.153,.048,.077),"nose",16,7,-sign*.16)
        ovoid(p,pt(sign*.574,-.996,.138),(.105,.022,.026),"tar",12,5,-sign*.16)
        tube(p,[pt(sign*u,v,z) for u,v,z in ((.36,-.928,.22),(.47,-.970,.29),
               (.62,-.957,.29),(.75,-.86,.23))],[.035,.064,.055,.024],"ivory",8)
        ovoid(p,pt(sign*.205,-.964,-1.02),(.339,.190,.239),"ivory",18,8,sign*.18)
        ovoid(p,pt(sign*.144,-1.109,-.921),(.190,.084,.102),"horn",16,7,-sign*.28)
        ovoid(p,pt(sign*.151,-1.171,-.926),(.102,.039,.039),"nose",12,6,-sign*.26)
        tube(p,[pt(0,-1.16,-1.00),pt(0,-1.15,-1.10),pt(sign*.124,-1.12,-1.17),
                pt(sign*.25,-1.082,-1.154)],[.020,.021,.018,.009],"nose",6)
    # 羊额的密毛有真实的剪影与凹线，卷束在两耳根前逐渐变细。
    curls=[(-.79,.61,.29),(-.50,.66,.34),(-.18,.58,.32),(.16,.58,.32),(.48,.63,.32),(.78,.61,.29),
           (-.70,.94,.32),(-.34,.94,.35),(.03,.89,.34),(.39,.95,.35),(.69,.93,.31),
           (-.40,1.17,.29),(-.10,1.23,.29),(.24,1.19,.29),(.48,1.10,.26)]
    for j,(x,z,r) in enumerate(curls):
        depth=-.72-.13*(1-abs(x))+.12*(z-.75)
        ovoid(p,pt(x,depth,z),(r*1.16,.26,r*.87),"fleece",14,7,.31*math.sin(j*2))
        points=[]
        for i in range(17):
            t=i/16
            a=j*2.4+(1 if j%2 else -1)*t*math.tau*.93
            sc=.10+.59*t
            points.append(pt(x+r*sc*math.cos(a),depth-.264*math.sqrt(1-sc*sc),z+r*.83*sc*math.sin(a)))
        tube(p,points,[.019+.018*math.sin(math.pi*i/16) for i in range(17)],"ivory",6)
    return p
