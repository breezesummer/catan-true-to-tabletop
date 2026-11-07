"""方围平面和门洞沿用既有城墙。新铺装只在院地、门槛以及墙脚占地。"""
def courtyard():
    # 夯土地在石基之下收边。连续的 UV 保留砂粒与夯作的层次，不让地形图穿过院落。
    rng_ground=random.Random(6407)
    # 埋入地面的收坡石基，在海岸露出的部分一直落到水面以下。
    loft([rect(0,.005,-.288,1.59,1.60,.112),rect(0,0,-.130,1.69,1.70,.090),
          rect(0,0,.014,1.745,1.75,.065)],'stone')
    loft([rect(0,-.908,-.281,.303,.267,.027),rect(0,-.921,-.089,.35,.286,.026),
          rect(0,-.921,.036,.372,.281,.022)],'stone')
    half=.796
    for j in range(9):
        for i in range(9):
            q=[]
            for a,b in ((i,j),(i+1,j),(i+1,j+1),(i,j+1)):
                u=a/9;v=b/9
                q.append(((2*u-1)*half,(2*v-1)*half,.054+.002*sin(a*.7)*sin(b*.9)))
            mapped(q,'horse',[(i/9,j/9),((i+1)/9,j/9),((i+1)/9,(j+1)/9),(i/9,(j+1)/9)],True)
    # 路石穿过拱门后再展开，两侧露出夯土。跨过墙外路端处保持薄而平。
    for row in range(11):
        y=-1.012+row*.085
        w=.34 if y<-.77 else .34+.12*min(1,(y+.77)*3)
        count=3 if row%2==0 else 4
        widths=[1/count]*count if count==3 else [.16,.34,.34,.16]
        x=-w/2
        for n,f in enumerate(widths):
            size=w*f
            box(x+size/2,y,.056,size-.007,.079+rng_ground.uniform(-.004,.004),.012+rng_ground.uniform(0,.006),'trim' if (row+n)%6==0 else 'stone',.014)
            x+=size
    # 小块崩石和埋入土中的薄片分簇排在墙脚，角墩有较重的碎石，入口留白。
    def rubble(x,y,w,d,h,base=.005):
        rot=rng_ground.random()*pi
        shape=[]
        for i in range(7):
            a=tau*i/7;u=w*cos(a)*rng_ground.uniform(.82,1.13);v=d*sin(a)*rng_ground.uniform(.82,1.13)
            shape.append((u*cos(rot)-v*sin(rot),u*sin(rot)+v*cos(rot)))
        rows=[[(x+u*f,y+v*f,z+h*.09*sin(i)) for i,(u,v) in enumerate(shape)] for f,z in ((.91,base),(1,base+h*.36),(.57,base+h))]
        loft(rows,'stone' if rng_ground.random()<.72 else 'trim')
    for side in range(4):
        for along in (-.78,-.59,-.22,.04,.32,.71):
            if side==0 and abs(along)<.29:continue
            along+=rng_ground.uniform(-.055,.055)
            for j in range(rng_ground.randrange(5,10)):
                a=along+rng_ground.gauss(0,.055);distance=.922+abs(rng_ground.gauss(0,.040))
                x,y=((a,-distance),(distance,a),(-a,distance),(-distance,-a))[side]
                size=rng_ground.uniform(.016,.047)
                rubble(x,y,size,size*rng_ground.uniform(.67,1.20),size*rng_ground.uniform(.41,.86))
        for along in (-.56,.15,.48):
            if side==0 and abs(along)<.28:continue
            for j in range(4):
                a=along+rng_ground.gauss(0,.046);distance=.74-rng_ground.uniform(0,.043)
                x,y=((a,-distance),(distance,a),(-a,distance),(-distance,-a))[side]
                width=rng_ground.uniform(.015,.031);rubble(x,y,width,width*.8,width*.57,.052)
