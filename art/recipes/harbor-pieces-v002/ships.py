"""商船共用旧船体的站线与舱位；海盗快船采用独立站线、后楼与双桅。"""
MERCHANT=[(-1.29,.014,.51),(-1.05,.26,.40),(-.70,.43,.34),(-.15,.49,.33),(.49,.46,.35),(.91,.35,.42),(1.10,.27,.50)]
RAIDER=[(-1.38,.009,.63),(-1.20,.19,.48),(-.88,.35,.39),(-.46,.46,.37),(.05,.48,.38),(.50,.44,.43),(.91,.35,.56),(1.13,.26,.64)]

def mapped(q,kind,uv,smooth=False):
    face(q,kind,smooth)
    sw=names.index(kind)
    UV[-1]=[((sw%4+.016+.967*u)/4,(sw//4+.016+.967*v)/4) for u,v in uv]

def board_width(stations,y):
    for (a,w,_),(b,v,_) in zip(stations,stations[1:]):
        if a<=y<=b:return w+(v-w)*(y-a)/(b-a)
    return .012

def hull(stations,pirate):
    for side in (-1,1):
        for j in range(5):
            a=j/5;b=(j+1)/5
            for idx,((y,w,h),(y1,w1,h1)) in enumerate(zip(stations,stations[1:])):
                def pt(yy,ww,hh,t):
                    return Vector((side*ww*(.44+.56*t**.65),yy,hh*t))
                q=[pt(y,w,h,a),pt(y1,w1,h1,a),pt(y1,w1,h1,b),pt(y,w,h,b)]
                kind=('shadow' if j%2 else 'wood') if pirate else ('player' if j==4 else 'wood' if j%3==0 else 'plank')
                uv_y=.07+.16*((idx+2*j)%5)
                mapped(q,kind,[(.02,uv_y),(.97,uv_y),(.97,uv_y+.12),(.02,uv_y+.12)])
                back=[p-Vector((side*.012,0,.004)) for p in q]
                face(back[::-1],kind)
                for k in range(4):face([q[k],back[k],back[(k+1)%4],q[(k+1)%4]],kind)
                rod(q[3],q[2],.012 if j<4 else .024,'iron' if pirate else 'wood',n=8)
                if j==0:
                    # 沿吃水线的一小截湿木，贴船壳本身，不向外延伸成水面圆环。
                    def wet(yy,ww,hh,z):
                        return pt(yy,ww,hh,z/hh)+Vector((side*.004,0,0))
                    lip=.073+.006*sin(idx*1.31)
                    wf=[wet(y,w,h,.015),wet(y1,w1,h1,.015),wet(y1,w1,h1,lip+.003),wet(y,w,h,lip)]
                    mapped(wf,'leather',[(0,.2),(1,.2),(1,.40),(0,.40)])
        for y,w,h in stations[1:-1]:
            rod((side*w*.83,y,.13),(side*w,y,h+.032),.025,'iron' if pirate else 'wood')
    for end in (stations[0],stations[-1]):
        y,w,h=end
        q=[(-w,y,h),(w,y,h),(w*.44,y,0),(-w*.44,y,0)]
        face(q,'shadow' if pirate else 'wood')
    # 纵向铺设甲板，板缝错开。纹理的长轴随每块木板，保留尖首轮廓。
    for i in range(12):
        x0=-.475+i*.0795;x1=x0+.076
        cuts=sorted(set([t[0] for t in stations]+[v for v in (-.94+.17*(i%3),-.18+.14*(i%4),.57+.09*(i%3)) if stations[0][0]<v<stations[-1][0]]))
        for j,(ya,yb) in enumerate(zip(cuts,cuts[1:])):
            # 每条分带与船侧折线作半平面裁切。
            poly=[(-board_width(stations,ya)*.91,ya),(board_width(stations,ya)*.91,ya),
                  (board_width(stations,yb)*.91,yb),(-board_width(stations,yb)*.91,yb)]
            for boundary,direction in ((x0,1),(x1,-1)):
                new=[]
                for (x,y),(xx,yy) in zip(poly,poly[1:]+poly[:1]):
                    inside=(x-boundary)*direction>=0;other=(xx-boundary)*direction>=0
                    if inside:new.append((x,y))
                    if inside!=other:new.append((boundary,y+(yy-y)*(boundary-x)/(xx-x)))
                poly=new
            if len(poly)<3:continue
            q=[(x,y,.251) for x,y in poly]
            mapped(q,'plank' if (i+j)%4 else 'wood',[(.03+.93*(y-ya)/max(.001,yb-ya),.06+.78*(i%5)/5+.13*(x-x0)/.0795) for x,y in poly])
            floor=[(x,y,.228) for x,y in poly]
            face(floor[::-1],'wood')
            for k in range(len(q)):face([q[k],floor[k],floor[(k+1)%len(q)],q[(k+1)%len(q)]],'wood')
            if yb-ya>.28 and board_width(stations,yb-.04)*.91>abs(x0+.038)+.013:
                column(x0+.038,yb-.044,[(.250,.009,.009),(.254,.009,.009)],'iron',8)
    for s in (-1,1):
        rod((0,stations[0][0],stations[0][2]),(s*stations[1][1],stations[1][0],stations[1][2]),.020,'iron' if pirate else 'player')

def sail(surface,colour,nx=16,ny=10):
    # 连续双面帆面只在轮廓缝合，避免每片布另起盒体造成拼缝黑边。
    grid=[[Vector(surface(i/nx,j/ny)) for i in range(nx+1)] for j in range(ny+1)]
    back=Vector((0,.014,0))
    for j in range(ny):
        for i in range(nx):
            q=[grid[j][i],grid[j][i+1],grid[j+1][i+1],grid[j+1][i]]
            uv=[(i/nx,j/ny),((i+1)/nx,j/ny),((i+1)/nx,(j+1)/ny),(i/nx,(j+1)/ny)]
            k=colour(i,j)
            mapped(q,k,uv,True);mapped([p+back for p in reversed(q)],k,uv[::-1],True)
    boundary=grid[0]+[r[-1] for r in grid[1:]]+grid[-1][-2::-1]+[r[0] for r in grid[-2:0:-1]]
    for a,b in zip(boundary,boundary[1:]+boundary[:1]):face([a,b,b+back,a+back],'linen')
    return grid

def pennon(top,kind,sign=1):
    x,y,z=top
    a=[Vector((x+sign*t*.30,y+.037*sin(t*6),z+.021*sin(t*5)-t*.048)) for t in (0,.25,.5,.75,1)]
    b=[p-Vector((0,.007,.115*(1-i/4)+.012)) for i,p in enumerate(a)]
    for i in range(4):
        q=[a[i],a[i+1],b[i+1],b[i]]
        loft([q,[p+Vector((0,.008,0)) for p in q]],kind)
    bulb((x,y,z+.01),(.023,.023,.029),'brass',10)

def merchant():
    hull(MERCHANT,False)
    box(0,.81,.252,.54,.47,.42,'wood');box(0,.81,.67,.64,.56,.064,'plank')
    roof(0,.81,.75,.67,.55,.17,False,'wood')
    # 舱顶沿脊铺设旧木板，木纹顺船长，端部留一条暗色压条。
    for sign in (-1,1):
        for j in range(4):
            t=j/4+.010;v=(j+1)/4-.010
            def cabin(u,yy):return (sign*.335*u,yy,.950-.17*u)
            q=[cabin(t,.54),cabin(t,1.078),cabin(v,1.078),cabin(v,.54)]
            mapped(q,'wood',[(.02,.10+j*.19),(.98,.10+j*.19),(.98,.22+j*.19),(.02,.22+j*.19)])
    rod((0,.51,.943),(0,1.10,.943),.031,'player')
    for x in (-.16,.16):
        box(x,.564,.40,.105,.015,.14,'shadow');box(x,.549,.40,.013,.018,.14,'brass')
    for x in (-.29,.29):
        for y in (.58,.81,1.04):box(x,y,.68,.045,.045,.17,'player')
    for x,y,w in ((-.19,-.05,.23),(.11,-.01,.21),(-.17,.21,.19),(.16,.24,.18),(.03,-.92,.15)):crate(x,y,.252,w)
    for x,y in ((-.30,-.46),(.28,-.44),(.25,.41)):barrel(x,y,.252,.084)
    rod((0,-.30,.22),(0,-.30,1.64),.043,'wood',.028)
    def canvas(u,t):
        return ((u-.5)*(1.15-.12*t),-.30+.12*(u-.5)-.76*t-.122*sin(pi*t)-.070*sin(pi*u)*sin(pi*t),
                1.47-.79*t+.054*t*(2*u-1)**2+.045*(u-.5))
    sail(canvas,lambda i,j:'linen' if j==9 or i==0 else 'player')
    # 帆条拼缝与收帆带只在近景出现，中央保留一整块玩家色。
    for u in (.28,.72):
        a=Vector(canvas(u,.57))+Vector((0,-.012,.007))
        path([a,a+Vector((.008,-.009,-.045)),a+Vector((.021,-.014,-.073))],.005,'leather',6)
    rod(canvas(0,0),canvas(1,0),.030,'plank');rod(canvas(0,1),canvas(1,1),.017,'wood')
    for u in (0,1):
        rod(canvas(u,0),((u-.5)*.77,.28,.37),.009,'linen',n=6)
        rod(canvas(u,1),((u-.5)*.40,-1.09,.36),.011,'linen',n=6)
    pennon((0,-.30,1.62),'player')
    rod((.25,.34,.25),(.25,.34,.97),.025,'wood');rod((.25,.34,.93),(.28,-.22,.81),.029,'plank')
    rod((.28,-.22,.80),(.28,-.22,.55),.009,'linen',n=6);crate(.28,-.22,.41,.16)
def pirate():
    hull(RAIDER,True)
    # 翘起的艏柱、斜伸的艏斜桅、加高的尾楼构成快船轮廓。
    path([(0,-1.15,.37),(0,-1.34,.60),(0,-1.43,.79),(0,-1.49,.81)],.039,'shadow')
    rod((0,-.74,.33),(0,-1.59,1.02),.039,'wood',.017)
    box(0,.76,.25,.54,.65,.30,'shadow')
    for i in range(6):
        x=-.268+i*.09
        box(x+.042,.78,.55,.086,.68,.052,'wood')
    for side in (-1,1):
        for y in (.54,.78,1.03):
            box(side*.25,y,.60,.033,.036,.17,'iron')
        rod((side*.25,.50,.76),(side*.25,1.055,.76),.022,'wood')
    rod((-.25,1.055,.76),(.25,1.055,.76),.021,'wood')
    for i in range(4):
        y=.27+i*.074
        box(0,y,.25,.228,.082,.058+i*.064,'plank')
    for s in (-1,1):
        for y in (-.49,-.18,.14):
            x=s*(board_width(RAIDER,y)-.02)
            rod((x,y,.34),(x+s*.12,y-.025,.38),.036,'iron',.027)
            rod((x+s*.09,y-.021,.372),(x+s*.122,y-.025,.38),.032,'steel')
    rod((0,.18,.24),(0,.34,2.13),.045,'wood',.022)
    rod((0,-.67,.25),(0,-.57,1.56),.031,'wood',.017)
    def main(u,t):
        x=(u-.5)*(1.15-.15*t)-.10+.24*t
        return (x,.34-.61*t-.14*sin(t*pi)-.055*sin(u*pi)*sin(t*pi)+.11*(u-.5),
                1.62+.49*u-.86*t+.10*t*u+.06*t*(2*u-1)**2)
    sail(main,lambda i,j:'iron' if j==9 else 'blue')
    rod(main(0,0),main(1,0),.028,'wood');rod(main(0,1),main(1,1),.022,'wood')
    # 艏三角帆沿船长方向斜张，与横置主帆在俯视下形成折角。
    peak=Vector((0,-.57,1.51));tip=Vector((.03,-1.46,.90));clew=Vector((.40,-.75,.43))
    for i in range(12):
        a=i/12;b=(i+1)/12
        p0=tip.lerp(clew,a);p1=tip.lerp(clew,b)
        for j in range(7):
            lo=j/7;hi=(j+1)/7
            def foresail(p,t,u):
                return peak.lerp(p,t)+Vector((.096*sin(pi*t)*sin(pi*u),0,0))
            if j==0:q=[peak,foresail(p0,hi,a),foresail(p1,hi,b)]
            else:q=[foresail(p0,lo,a),foresail(p0,hi,a),foresail(p1,hi,b),foresail(p1,lo,b)]
            k='linen' if i==0 else 'iron'
            face(q,k,True);face([p+Vector((-.008,0,0)) for p in reversed(q)],k,True)
    for a,b in ((peak,tip),(peak,clew),(tip,clew)):rod(a,b,.012,'leather',n=8)
    for s in (-1,1):
        a=Vector((s*.395,.00,.37));b=Vector((s*.38,.53,.41))
        c=Vector((s*.025,.30,1.86));d=c+Vector((0,.105,0))
        for start,end in ((a,c),(b,d)):rod(start,end,.011,'leather',n=8)
        for j in range(1,10):
            t=j/11;rod(a.lerp(c,t),b.lerp(d,t),.008,'plank',n=6)
    pennon((0,.34,2.12),'shadow',-1)
    for s in (-1,1):
        rod((s*.19,-.88,.31),(s*.43,-.98,.50),.017,'iron')
        path([(s*.46,-.99,.55),(s*.45,-1.03,.49),(s*.416,-1.01,.445),(s*.38,-.96,.47)],.014,'steel')
    # 索盘、舱口格栅与舵桨留在甲板视线内。
    for x in (-.20,.17):
        barrel(x,-.36,.252,.069)
    box(0,-.09,.253,.28,.24,.029,'shadow')
    for j in range(6):
        box(-.125+j*.05,-.09,.282,.018,.24,.013,'plank')
    for j in range(5):box(0,-.19+j*.05,.291,.28,.012,.011,'wood')
    rod((.20,.90,.73),(.39,1.30,.19),.021,'wood',.032)
    q=[(.33,1.19,.35),(.42,1.26,.28),(.49,1.39,.11),(.44,1.40,.093)]
    loft([q,[(x-.019,y,z) for x,y,z in q]],'plank')
