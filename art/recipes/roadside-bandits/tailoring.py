"""按三人的重心裁衣。长披风、袖管、皮靴、腰带与膝部补丁各有独立截面。"""
from math import pi,tau,sin,cos,exp,sqrt
from sculpt import V,P,C,S,BASE_ROW,UP,RIGHT,FRONT,curve,profile,loft,sweep,tube,orb,trim,stitches,ribbon,frame_loop


def creases(t,a):
    return .62*sin(6*a+1.4*t)+.48*cos(9*a-5*t)


def garment(g, stops, material, *, opening=0, rag=0, crease=1, paint="", sides=60, neckline=0):
    stations=curve(stops,4)
    rows=[]
    for i,(cx,row,d,rx,ry) in enumerate(stations):
        t=i/(len(stations)-1)
        gap=opening(t) if callable(opening) else opening
        ring=[]
        for k in range(sides+bool(opening)):
            a=gap+(tau-2*gap)*k/sides
            fold=crease*(sin(7*a+1.2*t)+.55*sin(11*a-2.4*t)+.31*cos(14*a+4*t))*(.48+.62*t)
            if material=="russet":
                back=max(0,-cos(a))**2
                fold+=back*(2.7*exp(-((t-.55-.19*sin(a))/.067)**2)
                            -2.3*exp(-((t-.49-.19*sin(a))/.054)**2))
            hem=rag*t**10*(.7*sin(7*a+.6)+.58*cos(13*a)+.42*sin(21*a))
            if rag:
                hem-=t**18*rag*2.1*exp(-(sin(4.5*a+.8)/.13)**2)
            ring.append(P(cx+(rx+fold)*sin(a),row+hem-.12*ry*(1-cos(a))+neckline*(1-t)**8*max(0,cos(a))**2+
                          .9*crease*sin(2*a+1.2*t),d-(ry+fold)*cos(a)))
        rows.append(ring)
    if opening:
        g.grid(rows,material,wrap=False,density=1.2,paint=paint)
        # 开襟与衣摆的反折料具有厚度，从侧面能看见衬里。
        cy=stops[-1][2]
        def inner(p):
            n=V((p.x-stops[-1][0],p.y-cy,0)).normalized()
            return p-n*1.35+UP*.45
        border=rows[0]+[r[-1] for r in rows[1:]]+list(reversed(rows[-1][:-1]))+[r[0] for r in reversed(rows[1:-1])]
        g.grid([border,[inner(p) for p in border]],"welt",caps=False)
        for edge in ([r[0] for r in rows],[r[-1] for r in rows]):
            trim(g,edge,.69,"welt")
    else:
        g.grid(rows,material,density=1.13,paint=paint)
    return rows


def shoe(g,x,depth,turn,size,scout=False):
    toe=V((sin(turn),-cos(turn),0))
    side=V((cos(turn),sin(turn),0))
    c=V((x,depth,0))
    def ring(z,rx,ry,shift,shape):
        pts=[]
        for k in range(36):
            a=-pi+tau*k/36
            f=cos(a);s=sin(a)
            transverse=(abs(s)**.88)*(1 if s>=0 else -1)
            length=(abs(f)**.83)*(1 if f>=0 else -1)
            w=rx*(.95+.12*f+.07*sin(2*a))
            pts.append(c+size*(side*(w*transverse)+toe*(ry*length+shift)+
                       UP*(z+shape*max(0,f)**1.75)))
        return pts
    layers=[ring(*s) for s in ((0,15.3,25.6,4.2,0),(.75,16.7,27,4.5,0),
               (2.4,16.9,27,4.5,.25),(4.1,16,26.1,4.1,.35))]
    g.grid(layers,"sole",density=.76,paint="boot")
    trim(g,layers[-2],.50,"welt",True)
    uppers=[ring(*s) for s in ((3.65,15.8,26.2,4.1,.5),(5.5,16.4,26,4.3,.5),
            (9.8,16.3,24.8,4.15,-1.3),(13,15.1,23,3,-1.65),(16.3,13.8,18.9,.9,-1.9),
            (19.6,12.7,14,-2.9,-.7),(23.2,12.15,12.5,-4.6,0),(27.8,12.55,12.2,-4.8,0))]
    g.grid(uppers,"boot",density=1.2,paint="boot")
    trim(g,uppers[0],.54,"welt",True)
    def vamp(level,angle,lift=.42):
        from math import floor
        u=(angle+pi)*36/tau;i=floor(u);j=floor(level)
        lower=uppers[j][i%36].lerp(uppers[j][(i+1)%36],u-i)
        upper=uppers[j+1][i%36].lerp(uppers[j+1][(i+1)%36],u-i)
        return lower.lerp(upper,level-j)+(UP*.79+toe*.55)*lift*size
    for sign in (-1,1):
        quarter=[[vamp(3.65+j*.38,sign*a,.67) for a in (.45,.51,.62,.74)] for j in range(8)]
        g.grid(quarter,"boot",wrap=False,density=.80)
        stitches(g,[r[1] for r in quarter],"lace",spacing=2.7,length=1.35,radius=.25)
    for j in range(3):
        start=4.0+j*.65
        for sign in (-1,1):
            lace=[vamp(start+.62*u,sign*.55*(1-2*u),1.06+sin(pi*u)*.18)
                  for u in (0,.2,.4,.6,.8,1)]
            tube(g,lace,[.38,.62,.62,.62,.60,.38],"lace",sides=7,steps=1)
    for sign in (-1,1):
        seam=[c+toe*f*size+side*sign*width*size+UP*h*size
              for f,width,h in ((-14,10.9,22),(-9,13.2,17),(-1,15,11),(8,15.4,8),(17,12.9,7.4))]
        stitches(g,seam,"lace",spacing=3.0,radius=.26)
    return c-toe*4.5*size


def patch(g,c,w,h,mat,*,n=FRONT,across=RIGHT,up=UP,phase=0):
    n=n.normalized();across=across.normalized();up=up.normalized()
    rows=[]
    for j in range(8):
        v=j/7*2-1;row=[]
        for k in range(11):
            u=k/10*2-1
            x=u*(w+.7*sin(v*6+phase))+.48*v
            z=v*(h+.7*sin(u*6.4+phase))+.8*u
            row.append(c+across*x+up*z+n*(.68+.65*(1-u*u)-1.7*u*u-.43*v*v))
        rows.append(row)
    g.grid(rows,mat,wrap=False,density=1.38)
    border=rows[0]+[r[-1] for r in rows[1:]]+list(reversed(rows[-1][:-1]))+[r[0] for r in reversed(rows[1:-1])]
    g.grid([border,[p-n*.87 for p in border]],"welt",caps=False)
    stitches(g,border+[border[0]],"twine",spacing=4.35,length=2.15,radius=.46,slant=(across+up)*.69)


def fitted_patch(g,cloth,t0,t1,angle,half,mat,phase=0):
    """沿母裁片取样，把补丁、反折边与缝线一起贴合到屈膝或鼓胀的袋面。"""
    from math import floor
    out=[];n=len(cloth[0]);m=len(cloth)
    for j in range(8):
        v=j/7;row=[]
        for k in range(12):
            u=k/11*2-1
            t=t0+(t1-t0)*v+.006*sin(7*u+phase)+.004*u
            a=angle+u*(half+.025*cos(v*9+phase))+.028*v
            fi=t*(m-1);fk=(a+pi)/tau*n;ii=int(fi);ik=floor(fk)
            def pos(idx):
                return cloth[idx][ik%n].lerp(cloth[idx][(ik+1)%n],fk-ik)
            origin=pos(ii).lerp(pos(ii+1),fi-ii)
            normal=(cloth[ii][(ik+1)%n]-cloth[ii][(ik-1)%n]).cross(pos(ii+1)-pos(ii)).normalized()
            if normal.dot(V((sin(a),-cos(a),0)))<0:normal=-normal
            row.append(origin+normal*(.58+.23*(1-u*u)))
        out.append(row)
    g.grid(out,mat,wrap=False,density=1.4)
    border=out[0]+[r[-1] for r in out[1:]]+list(reversed(out[-1][:-1]))+[r[0] for r in reversed(out[1:-1])]
    trim(g,border,.36,mat,True,6)
    # 针脚穿过补丁两侧。回针顶起袋面，针眼留在两端。
    center=out[len(out)//2][len(out[0])//2]
    path=border+[border[0]]
    distance=0;next_stitch=1.2
    for a,b in zip(path,path[1:]):
        length=(b-a).length
        if length<.001:continue
        tangent=(b-a)/length
        while next_stitch<=distance+length:
            pt=a.lerp(b,(next_stitch-distance)/length)
            outward=V((pt.x-center.x,0,pt.z-center.z))
            outward-=tangent*outward.dot(tangent)
            outward.normalize()
            normal=tangent.cross(outward).normalized()
            if normal.y>0:normal=-normal
            lean=tangent*.61+outward*1.38
            tube(g,[pt-lean+normal*.28,pt+normal*1.10,pt+lean+normal*.26],
                 [.27,.47,.27],"repairthread",sides=6,steps=2)
            next_stitch+=4.35+.56*sin(next_stitch*1.06+phase)
        distance+=length


# 脚底的纵向错步决定三人的实际进深；两列均从髋、膝过渡到踝。
LEGS={
 "carrier": [((216,79,-.52,1.11),(246,65),(231,68),391),((312,112,.19,1.05),(292,71),(300,95),389)],
 "leader": [((330,0,-.48,1.0),(369,-1),(353,-11),418),((439,43,.64,1.0),(411,8),(420,24),418)],
 "scout": [((458,98,-.16,.87),(479,64),(468,83),381),((532,99,.65,.96),(512,75),(521,87),377)]
}


def legs(g,role):
    cloth={"carrier":"trousers","leader":"olive","scout":"slatepants"}[role]
    for index,(foot,hip,knee,bottom) in enumerate(LEGS[role]):
        g.part=role+(" · 左腿与靴" if index==0 else " · 右腿与靴")
        x,dep,angle,size=foot
        ankle=shoe(g,x,dep,angle,size,role=="scout")
        ax,ad=ankle.x,ankle.y
        y0={"carrier":294,"leader":331,"scout":306}[role]
        mid=y0+(bottom-y0)*.58
        fat={"carrier":24.4,"leader":20.1,"scout":17.5}[role]
        kx,kd=knee
        if role=="carrier" and index==0:
            kx-=5
        dx=ax+(kx-ax)*.12
        def folds(t,a):
            pinch=exp(-((t-.81)/.18)**2)
            return ((.65+.78*pinch)*sin(5*a+2*t)+(.44+1.7*pinch)*cos(26*t+1.9*sin(a)+.4*index)
                    +.36*sin(8*a-5*t)),1.15*sin(2*a+5*t)*t
        trousers=loft(g,[(hip[0],y0,hip[1],fat*.88,fat*.83),(hip[0]*.63+kx*.37,y0+17,kd-1,fat*1.05,fat),
               (kx+1,mid-5,kd-3,fat,fat*.92),(kx,mid+9,kd-5,fat*.91,fat*.93),
               (dx,bottom-13,ad-.4,fat*.94,fat*.77),(dx,bottom-5,ad,fat*1.02,fat*.81),
               (dx,bottom+1,ad,fat*.77,fat*.68)],cloth,warp=folds,sides=36,steps=6,density=1.1,paint="pants")
        bottom_world=P(dx,bottom,ad).z
        if role=="scout":
            h0=24*size; h1=bottom_world+5
            rows=[]
            for j in range(16):
                t=j/15;rad=(12.7+3.1*t+1.1*sin(pi*t))*size
                rows.append([V((ax+(dx-ax)*t+rad*sin(a),ad-rad*.87*cos(a),
                                h0+(h1-h0)*t)) for a in [tau*k/30 for k in range(30)]])
            g.grid(rows,"wrap",density=.95,paint="wrap")
            # 螺旋缠腿首尾收进布层，每匝上下叠压并保留窄毛边。
            spiral=[]
            turns=4.4
            for i in range(131):
                t=i/130;a=t*tau*turns+.75; rad=(13.1+3.2*t+1.1*sin(pi*t))*size
                band=[]
                for zz,raise_by in ((-3.7,.1),(-3.15,.75),(-1.4,1.15),(2.85,1.26),(3.55,.29),(1.4,-.1)):
                    band.append(V((ax+(dx-ax)*t+(rad+raise_by)*sin(a),ad-(rad*.87+raise_by)*cos(a),
                                   h0+3+(h1-h0-5)*t+zz+1.0*sin(2*a+.4*t))))
                spiral.append(band)
            g.grid(spiral,"wrap",density=.65,paint="wrap")
        else:
            h0=19*size;h1=bottom_world+3
            rows=[]
            for i in range(15):
                t=i/14
                rad=(12.45+3.5*t+1.2*sin(t*pi))*size
                rows.append([V((ax+(dx-ax)*t+(rad+1.1*cos(12*t+1.9*sin(a)))*sin(a),
                                ad-(rad*.85+1.2*cos(13*t+2*cos(a)))*cos(a),
                                h0+(h1-h0)*t+1.5*t*sin(2*a))) for a in [tau*k/30 for k in range(30)]])
            g.grid(rows,"boot",density=1.0,paint="boot")
            trim(g,rows[-1],.79,"welt",True)
        if role=="carrier":
            loft(g,[(dx,bottom-3,ad,21,16),(dx,bottom+2,ad,22,16.4),
                    (dx-.8,bottom+15,ad,21.2,16.5),(dx-.6,bottom+17,ad,20,15.6)],
                 "cuff",sides=30,density=.95,paint="pants")
        if role in ("carrier","leader"):
            fitted_patch(g,trousers,.34,.61,.15 if index==0 else .38,.46,"patch",phase=.8+index)


def sleeve(g,coords,rads,mat,paint,*,rolled):
    cx={"carrier":275,"leader":394,"scout":497}[g.part.split(" ")[0]]
    x,row,dep=coords[0]
    coords=[(cx+(x-cx)*.62,row+4,dep+2)]+list(coords)
    rads=[(rads[0][0]*.55,rads[0][1]*.60)]+list(rads)
    station=curve([(*P(*p),*r) for p,r in zip(coords,rads)],10)
    points=[V(p[:3]) for p in station]
    direction=1 if x<cx else -1
    def folds(t,a):
        bias=direction*sin(a-.6)
        fold=.64*sin(4*a+2.6*t)+.45*cos(7*a-3*t)
        fall=sin(pi*min(1,max(0,(t-.16)/.74)))**.76
        for centre,width,lift in ((1.09+.79*t,.33,2.9),(2.73-.66*t,.27,2.35)):
            cross=(a-centre+pi)%tau-pi
            fold+=fall*lift*(exp(-(cross/width)**2)-.79*exp(-((cross+.31)/.23)**2))
        for start,lift,slope in ((.40,2.2,-.082),(.59,3.1,.096),(.745,2.75,-.065),(.875,2.1,.047)):
            ridge=start+slope*bias+.019*sin(2*a+start*9)
            side=.20+.80*(.5+.5*cos(a-direction*start*8))**.65
            fold+=side*lift*(exp(-((t-ridge)/.041)**2)-.69*exp(-((t-ridge-.047)/.028)**2))
        return fold
    rows=sweep(g,points,[p[3:] for p in station],mat,sides=48,warp=folds,density=1.95,paint=paint)
    g.charts[-1].caps=False
    for j in range(1,len(points)-1):
        before=points[j]-points[j-1];after=points[j+1]-points[j]
        bend=after.normalized()-before.normalized()
        if bend.length<.045:continue
        inner=.46*(before.length+after.length)/bend.length
        inward=bend.normalized()
        for p in rows[j]:
            excess=(p-points[j]).dot(inward)-inner
            if excess>0:p-=inward*excess
    if rolled:
        axis=(points[-1]-points[-2]).normalized()
        mid=points[-1]
        ring=[]
        for pos,f in ((-8.6,.94),(-8.0,1.04),(-7.0,1.18),(-5.3,1.24),(-3.5,1.12),
                      (-2.1,1.09),(-.8,1.21),(1.1,1.31),(2.8,1.24),(3.7,1.11),(3.45,.99)):
            edge=[]
            for i,p in enumerate(rows[-1]):
                a=i*tau/len(rows[-1])
                lip=1.4*sin(a+.26)+.72*sin(3*a-.63)+.18*pos*cos(2*a+.4)
                tuck=.036*cos(3*a+.24*pos)+.023*sin(5*a-.1*pos)
                edge.append(mid+axis*(pos+lip)+(p-mid)*(f+tuck))
            ring.append(edge)
        g.grid(ring,mat,density=1.52,paint=paint,caps=False)
        g.grid([ring[-1],[mid+(p-mid)*.91-axis*.35 for p in ring[-1]]],mat,caps=False)
        stitches(g,ring[-3]+[ring[-3][0]],"hemp",spacing=3.6,length=1.24,radius=.22)
    return rows


def arm(g,coords,radii,paint):
    rows=stroke_skin(g,coords,radii,"skin",paint)
    g.charts[-1].skin_region="forearm"
    return rows


def stroke_skin(g,coords,radii,mat,paint):
    return tube(g,[P(*p) for p in coords],radii,mat,steps=4,sides=20,density=1.42,paint=paint)


def belt(g,cx,row,d,rx,ry,tilt,scout=False):
    rows=[]
    for h,offset in ((-4,.05),(-3.5,.9),(0,1.2),(3.7,.86),(4.2,.05)):
        ring=[]
        for k in range(52):
            a=-pi+tau*k/52
            ring.append(P(cx+(rx+offset)*sin(a),row+h+tilt*sin(a)-1.35*cos(a),
                          d-(ry+offset)*cos(a)))
        rows.append(ring)
    g.grid(rows,"belt",density=1.3,paint="belt")
    for j in (1,3):
        trim(g,rows[j],.41,"welt",True,6)
    c=P(cx+(.1 if scout else 3),row-1.4,d-ry-2.3)
    frame_loop(g,c,RIGHT,UP,7.7,6.45,1.22)
    for i in range(3):
        q=P(cx-12-i*5,row-1.2-tilt*.25,d-ry+(.5+i*.8))
        orb(g,q,(.74,.33,.65),"crease",sides=8,bands=4)
    for xoff in (-22,16):
        q=P(cx+xoff,row+tilt*xoff/rx,d-ry*sqrt(max(.01,1-(xoff/rx)**2))-1.15)
        ribbon(g,[q+UP*5.3,q,q-UP*5.1],[1.35,1.52,1.32],"boot",thickness=.5)


def toggles(g,coords):
    for cx,row,depth in coords:
        c=P(cx,row,depth)
        for side in (-1,1):
            pts=[c+RIGHT*(side*x)+UP*z+FRONT*b for x,z,b in ((.2,-.2,0),(3.1,1.7,.2),(5.8,1.4,.39),
                 (6.15,-.3,.4),(5.0,-1.35,.3),(2.4,-.75,0),(.4,-.2,0))]
            tube(g,pts,.53,"twine",sides=6)
        tube(g,[c-RIGHT*2.4-UP*.11,c+FRONT*.87,c+RIGHT*2.2+UP*.48],
             [.65,.88,.56],"brass",sides=7)


def inset_cloth(rows,cx,depth,thickness):
    """衬里沿外层的局部法线翻入。陡斜的披风和翻领也保持一致的内外顺序。"""
    inside=[]
    for j,row in enumerate(rows):
        band=[]
        for k,p in enumerate(row):
            du=row[min(k+1,len(row)-1)]-row[max(0,k-1)]
            dv=rows[min(j+1,len(rows)-1)][k]-rows[max(0,j-1)][k]
            normal=du.cross(dv).normalized()
            radial=V((p.x-cx,p.y-depth,0))
            if normal.dot(radial)<0:normal=-normal
            band.append(p-normal*thickness)
        inside.append(band)
    return inside


def mantles(g,leader):
    cx,cy,d=(395,209,0) if leader else (499,193,68)
    mat="moss" if leader else "slate"
    if leader:
        back=[]
        for j in range(35):
            t=j/34
            ring=[]
            for k in range(52):
                r=k/51;a=1.47+(tau-2*1.47)*r
                length=168+29*(-cos(a))+6*sin(3*a)
                # 左前襟低垂，后中央有交替深谷和脊线，破口沿经向撕开。
                fullness=sin(7*a-1.6*t)+.7*sin(12*a+1.5*t)
                rad=23+65*t**.58+fullness*(1.1+4.2*t)
                rear=max(0,-cos(a))**.65
                across=(a-pi)/1.67
                pleat=0
                for origin,slant,width,amount in ((-.72,.24,.14,4.9),(-.29,.17,.12,4.1),
                                                 (.14,-.15,.15,5.6),(.64,-.19,.14,5.0)):
                    line=origin+slant*t+.048*sin(t*4.7+origin*5)
                    pleat+=amount*(exp(-((across-line)/width)**2)
                                  -.76*exp(-((across-line-width*1.05)/(width*.79))**2))
                pleat*=rear*(.20+.88*t)
                swag=.33+.14*across+.105*across**2
                pleat+=rear*(3.6*exp(-((t-swag)/.069)**2)-2.8*exp(-((t-swag-.09)/.059)**2))
                yy=cy-5+t*length+t**12*(2.8*sin(17*a)+2*sin(26*a)-5*exp(-(sin(8*a)/.13)**2))
                ring.append(P(cx+(rad+pleat)*sin(a)-7*t,yy-.28*pleat,
                              d-(21+43*t+2.9*t*fullness+pleat)*cos(a)+7*t))
            back.append(ring)
        g.grid(back,mat,wrap=False,density=1.18,paint="moss")
        lining=inset_cloth(back,cx,d,1.60)
        g.grid([list(reversed(row)) for row in lining],"lining",wrap=False,density=.70)
        for outer,inner in ((back[-1],lining[-1]),([r[0] for r in back],[r[0] for r in lining]),
                            ([r[-1] for r in back],[r[-1] for r in lining])):
            g.grid([outer,inner],"binding",wrap=False)
    top=[]
    for j in range(39):
        t=j/38
        ring=[]
        for k in range(97):
            gap=.012+(.36 if leader else .29)*t**2.3
            a=gap+(tau-2*gap)*k/96
            face=max(0,cos(a))
            end=(48+10*sin(a)-8*face+31*max(0,-cos(a))**3) if leader else (49-17*sin(a)+9*face)
            # 从领口向肩头沿斜向放褶，褶脊在同一块料上起伏。
            spread=sin(t*pi*.5)**.65
            fan=2.6*sin(8*t-3.9*a+sin(a))+.85*sin(15*t-4.8*a)
            ripple=(1.5*sin(7*a+1.2*t)+.80*cos(11*a-1.7*t))*t**.65+fan*sin(pi*t)**.7
            rx=(23 if leader else 17)+(59 if leader else 43)*spread+ripple
            ry=(20 if leader else 17)+(22 if leader else 17)*spread+ripple
            yy=(cy-10 if leader else cy-16+5*face)+end*t+2.7*sin(pi*t)*sin(3*a-.8)+1.15*sin(5*a)*t
            # 扣点收绉向两侧松开。跨过肩峰的呢料折回上臂，襟缘才有向下的重量。
            spread_angle=abs((a+pi)%tau-pi)
            drape=(5.7 if leader else 7.8)*max(0,1-spread_angle/2.45)
            line=.28+.24*spread_angle+.038*sin(3*a)
            fold=(exp(-((t-line)/.105)**2)-.82*exp(-((t-line-.12)/.074)**2)
                  +.59*exp(-((t-line-.31)/.10)**2))
            rx+=drape*fold-6.8*t**3*abs(sin(a))**2
            ry+=drape*fold*(1-.46*face)+2.0*t**2*face
            yy+=2.2*t**2+2.6*sin(pi*t)*cos(2*a)
            span=abs(rx*sin(a))
            pinrow=cy+(6 if leader else 8)
            gathering=0
            for slope,width,amount in ((.80,2.8,6.6),(.38,2.6,5.6),(-.13,2.9,5.0),(-.48,3.4,3.6)):
                line=pinrow-slope*span+.0015*span**2
                gathering+=amount*(exp(-((yy-line)/width)**2)-.57*exp(-((yy-line-width*1.37)/width)**2))
            gathering*=face**1.15*min(1,span/9)*(.82 if leader else 1.14)
            ring.append(P(cx+rx*sin(a),yy,d-ry*cos(a)-gathering))
        top.append(ring)
    collar_fold=[]
    cut=10 if leader else 12
    for j in range(14):
        t=j/13;row=[]
        landing=top[cut]
        for k,end in enumerate(landing):
            gap=.012+(.36 if leader else .29)*(cut/38)**2.3+(.10 if leader else .42)*(1-t)**1.15
            a=gap+(tau-2*gap)*k/(len(landing)-1)
            neck_x=cx if leader else 491.5
            y=(201+9*max(0,cos(a))) if leader else (163+20*max(0,cos(a))+2.8*sin(a))
            close=P(neck_x+(16 if leader else 11.4)*sin(a),y,d-(14 if leader else 12.1)*cos(a))
            n=V((sin(a),-cos(a),.60)).normalized()
            roll=1.75*sin(pi*t) if leader else 2.7*sin(pi*t)+1.2*sin(3*pi*t+.8*sin(a))*sin(pi*t)
            row.append(close.lerp(end,t)+n*((.80+roll)*(1-t*t)))
        collar_fold.append(row)
    top=collar_fold[:-1]+top[cut:]
    g.grid(top,mat,wrap=False,density=1.64,paint=mat)
    collar=inset_cloth(top,cx,d,1.26)
    g.grid([list(reversed(row)) for row in collar],"lining" if leader else "slate",wrap=False,density=.65)
    edging="binding" if leader else "hatwelt"
    for out,inside in ((top[-1],collar[-1]),([r[0] for r in top],[r[0] for r in collar]),
                       ([r[-1] for r in top],[r[-1] for r in collar])):
        g.grid([out,inside],edging,wrap=False)
        trim(g,out,.57,edging)
    stitches(g,top[-2],"hemp" if leader else "thread",spacing=3.8,radius=.28)
    trim(g,top[0],.63,mat)
    # 领布回折在后颈叠出有内外唇的软兜，后中线垂得比肩端深。
    cowl=[]
    for j in range(18):
        t=j/17;row=[]
        for k in range(49):
            start=1.11 if leader else 1.27
            a=start+(tau-2*start)*k/48
            backness=(1-cos(a))*.5
            turn_in=.25+.75*sin(pi*k/48)**.6
            rise=sin(t*pi*.62)*turn_in
            rx=(22 if leader else 16)+(29 if leader else 23)*rise
            ry=(17 if leader else 14)+(23 if leader else 21)*rise
            lip=turn_in*(2.4*exp(-((t-.76)/.12)**2)+1.6*exp(-((t-.38)/.12)**2))
            margin=backness**2.3
            yy=(cy-6 if leader else cy-15)+t*(8+(32 if leader else 30)*margin)+3.4*sin(a)*t
            row.append(P(cx+(rx+lip)*sin(a),yy,d-(ry+lip)*cos(a)))
        cowl.append(row)
    g.grid(cowl,mat,wrap=False,density=1.12,paint=mat)
    turn=inset_cloth(cowl,cx,d,1.1)
    g.grid([list(reversed(r)) for r in turn],mat,wrap=False,density=.66)
    for edge,inside in ((cowl[-1],turn[-1]),([r[0] for r in cowl],[r[0] for r in turn]),
                        ([r[-1] for r in cowl],[r[-1] for r in turn])):
        g.grid([edge,inside],mat,wrap=False)
    clasp_row=cy+(6 if leader else 8)
    clasp_x=cx+(4 if leader else 0)
    near_pin=[p.y for panel in (top,collar_fold,cowl) for rr in panel for p in rr
              if abs(BASE_ROW-C*p.z-S*p.y-clasp_row)<7 and abs(p.x-clasp_x)<9 and p.y<d]
    c=P(clasp_x,clasp_row,min(near_pin)-1.85)
    if leader:
        frame_loop(g,c,RIGHT,UP,4.9,3.9,.88,"brass")
        ribbon(g,[c+RIGHT*3,c+RIGHT*7-UP*4,c+RIGHT*12-UP*13],
               [1.55,1.9,.92],"belt",thickness=.66)
    else:
        orb(g,c,(7.3,2.2,7.7),"brass",density=.86)
        orb(g,c+FRONT*1.65,(5.8,1.65,6.0),"bucklecore",density=.90)
        orb(g,c+FRONT*3.1,(1.25,.45,1.25),"brass",sides=12,bands=6)
