"""门楣纹章。盾面起鼓，鸢尾、冠带和两支月桂均为可侧看的浮雕。"""
from math import cos, pi, sin, tau
from mathutils import Vector
from atelier import cord


def bezier(points,steps=8):
    p,q,r,s=map(Vector,points)
    return [p*(1-t)**3+q*(3*t*(1-t)**2)+r*(3*t*t*(1-t))+s*t**3
            for t in (i/steps for i in range(steps+1))]


def relief(m,f,outline,z,scales,stone):
    center=Vector((0,z))
    rings=[[f.at(*(center+(p-center)*scale),d) for p in outline] for scale,d in scales]
    for a,b in zip(rings,rings[1:]):
        for i in range(len(a)):
            j=(i+1)%len(a)
            m.face([a[i],a[j],b[j],b[i]],stone,outward=f.n)
    tip=f.at(0,z,scales[-1][1]+.014)
    for i in range(len(outline)):
        m.face([rings[-1][i],rings[-1][(i+1)%len(outline)],tip],stone,outward=f.n)


def leaf(m,f,a,b,width,d,stone):
    a,b=Vector(a),Vector(b)
    side=Vector((-(b-a).y,(b-a).x)).normalized()
    sections=[]
    divisions=(-1,-.75,-.35,-.07,0,.09,.37,.76,1)
    for t in (.012,.12,.29,.46,.62,.77,.90,.996):
        wave=sin(pi*t)
        p=a.lerp(b,t)+side*(width*.22*wave)
        spread=width*wave**.82
        section=[]
        for u in divisions:
            vein=.009*max(0,1-abs(u)/.12)
            depth=d+.002+.009*t*t+wave*(.024*(1-u*u)+vein+.004*u)
            section.append(f.at(*(p+side*spread*u),depth))
        sections.append(section)
    for row,nxt in zip(sections,sections[1:]):
        for k in range(len(divisions)-1):
            m.face([row[k],nxt[k],nxt[k+1],row[k+1]],stone,outward=f.n)
        m.face([row[-1],nxt[-1],nxt[0],row[0]],"limestone",outward=-f.n)


def heraldry(m,f):
    center=3.72
    left=bezier([(0,4.008),(-.13,3.996),(-.24,4.016),(-.341,4.03)],5)
    left+=bezier([left[-1],(-.342,3.80),(-.334,3.48),(0,3.31)],10)[1:]
    outline=left+[Vector((-p.x,p.y)) for p in reversed(left[1:-1])]
    relief(m,f,outline,center,[(1.07,.015),(1.09,.062),(1.04,.105),(.96,.115),(.91,.062)],"carving")
    relief(m,f,outline,center,[(.921,.066),(.89,.079),(.855,.077)],"shadow")
    relief(m,f,outline,center,[(.878,.084),(.848,.101),(.72,.122),(.18,.159)],"limestone")
    # 盾心鸢尾的翻瓣、束环和下垂瓣各有体积。冠带只在磨亮的嵌条上露铜。
    leaf(m,f,(0,3.69),(0,3.925),.041,.168,"carving")
    for s in (-1,1):
        curve=bezier([(s*.014,3.692),(s*.033,3.861),(s*.187,3.834),(s*.121,3.758)])
        cord(m,f,[(p.x,p.y,.177) for p in curve],.019,"carving")
        leaf(m,f,(s*.012,3.697),(s*.084,3.636),.020,.172,"carving")
        for u,z in ((s*.16,3.68),(s*.075,3.53)):
            leaf(m,f,(u,z-.033),(u,z+.044),.021,.140,"carving")
    leaf(m,f,(0,3.695),(0,3.584),.027,.17,"carving")
    m.band(f,-.056,.056,3.681,3.708,.158,.210,"bronze")
    for z,r in ((4.066,.033),(4.115,.019)):
        cord(m,f,[(-.29+i*.58/18,z-.021*(1-((i-9)/9)**2),.102) for i in range(19)],r)
    for s in (-1,1):
        for chain in ([(s*.26,4.11),(s*.44,4.36),(s*.283,4.44),(s*.200,4.283)],
                      [(s*.12,4.10),(s*.29,4.34),(s*.143,4.47),(s*.037,4.318)]):
            c=bezier(chain,12)
            cord(m,f,[(p.x,p.y,.098+.016*sin(pi*i/12)) for i,p in enumerate(c)],.024)
        for i in range(3):
            u=s*(.065+i*.081)
            leaf(m,f,(u,4.067),(u,4.12),.018,.125,"bronze")
    cord(m,f,[(0,4.098,.108),(0,4.323,.125),(0,4.46,.098)],.026)
    leaf(m,f,(0,4.398),(0,4.569),.047,.12,"carving")
    for s in (-1,1):
        leaf(m,f,(s*.009,4.422),(s*.068,4.477),.024,.13,"carving")
        branch=bezier([(-s*.093,3.273),(s*.27,3.315),(s*.63,3.51),(s*.656,3.97)],28)
        cord(m,f,[(p.x,p.y,.064) for p in branch],.016)
        for j in range(7):
            i=4+j*3
            p=branch[i]
            tangent=(branch[i+1]-p).normalized()
            out=Vector((s*tangent.y,-s*tangent.x))
            for side in (-1,1):
                start=p+tangent*.015*side
                end=start+tangent*(.106+.012*j)+out*(side*(.107-.006*j))
                leaf(m,f,start,end,.039,.069+.009*(side==1),"carving" if (j+side)%3 else "limestone")
        leaf(m,f,branch[-2],branch[-1]+Vector((s*.016,.125)),.037,.068,"carving")
