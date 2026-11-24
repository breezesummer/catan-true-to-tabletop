"""正面尖券装卸塔与横向长仓。两道屋脊正交，所有门窗均在分皮砌石中留洞。"""
import math
from wool_craft import (Face,Opening,Part,R,V,beam,extrude,grain,masonry,pin,rect,
                        roof,stone,surround,tube,window)

TOWER_X, TOWER_HALF = -8.15, 3.82
TOWER_FRONT, BACK = 1.08, 9.48
HALL_LEFT, HALL_RIGHT, HALL_FRONT = -4.49, 11.92, 1.57
FLOOR = .18


def boarded_gable(p, f, half, base, eave, peak, framed=True):
    width=2*half
    count=round(width/.64)
    def top(u):
        return peak-abs(u)*(peak-eave)/half
    for j in range(count):
        a=-half+width*j/count+.009
        b=a+width/count-.018
        points=[(a,base),(b,base),(b,top(b)),(a,top(a))]
        if a<0<b: points.insert(3,(0,peak))
        tex=grain(peak-base,b-a)
        f.plate(p,points,0,.27,R.choice(("wall","wall","dock","wall")),
                [tex((z-base)/(peak-base),(u-a)/(b-a)) for u,z in points])
    if framed:
        f.member(p,(-half,eave-.08),(half,eave-.08),.38,.39,"oak",.14)
        for u in ((-half,0,half) if eave-base>.4 else (-half/2,0,half/2)):
            f.member(p,(u,base),(u,top(u)-.08),.44,.45,"oak",.15)
        for side in (-1,1):
            f.member(p,(side*half,eave),(0,peak),.29,.32,"oak",.16)


def upper_frame(p,f,half,base,eave,peak=None,hatch=False):
    if peak:
        boarded_gable(p,f,half,base,eave,peak)
    else:
        boarded_gable(p,f,half,base,eave,eave,False)
        for u in (-half,0,half):
            f.member(p,(u,base),(u,eave),.45,.49,"oak",.16)
        f.member(p,(-half,eave),(half,eave),.36,.44,"oak",.16)
    f.member(p,(-half-.14,base+.12),(half+.14,base+.12),.45,.51,"oak",.18)
    for side in (-1,1):
        inner=.96 if hatch else .21
        f.member(p,(side*(half-.30),base+.37),(side*inner,eave-.30),.37,.34,"oak",.12)
        for u,z in ((side*(half-.20),base+.17),(side*(half-.20),eave-.07),(side*inner,eave-.34)):
            pin(p,f.at(u,z,.34),f.at(u,z,.41),.067,"agedcut")
    if hatch:
        for u in (-.98,.98):
            f.member(p,(u,base+.34),(u,eave-.18),.19,.29,"oak",.27,.027)
        f.member(p,(-1.08,eave-.43),(1.08,eave-.43),.23,.32,"oak",.25)
        for j in range(5):
            a=-.82+j*.334
            f.plate(p,rect(a,a+.321,base+.40,eave-.53),.20,.20,"door",
                    [(0,.1),(0,.21),(.91,.21),(.91,.1)])
        f.member(p,(-.79,base+.74),(.79,base+.74),.15,.13,"iron",.25,.018)


def door(p,f,h):
    surround(p,f,h,.43,d=.20)
    f.plate(p,h.outline,-.36,.17,"shadow")
    for j in range(10):
        a=h.x-h.r+2*h.r*j/10+.013
        b=a+2*h.r/10-.026-(.022 if j==4 else 0)
        points=[(a,h.bottom+.015),(b,h.bottom+.015),(b,h.head(b)-.042),
                ((a+b)*.5,h.head((a+b)*.5)-.04),(a,h.head(a)-.042)]
        tex=grain(4.6,b-a)
        f.plate(p,points,-.17,.18,R.choice(("door","door","oak")),
                [tex((z-h.bottom)/4.8,(u-a)/(b-a)) for u,z in points])
    for s in (-1,1):
        for z in (1.05,3.19):
            a=h.x+s*(h.r-.09)
            b=h.x+s*(h.r-.70)
            f.member(p,(a,z),(b,z),.11,.064,"iron",-.108,.015)
            for u in (a,b):
                pin(p,f.at(u,z,-.073),f.at(u,z,-.045),.044)
        u=h.x+s*.38
        f.plate(p,[(u-.16,1.87),(u,1.61),(u+.16,1.87),(u,2.11)],-.072,.066,"iron")
        pin(p,f.at(u,1.93,-.08),f.at(u,1.93,.029),.072,"brass")
        tube(p,[f.at(u+.13*math.cos(math.tau*i/14),1.80+.18*math.sin(math.tau*i/14),.031)
                for i in range(14)],.037,"brass",6,True)


def house():
    shell=Part("双仓石工与窗券")
    timber=Part("吊楼板壁与榫架")
    leaves=Part("长仓门扇、提环与窗棂")
    tunnel=Part("拱廊内壁与货道")
    tiles=Part("逐片青灰板岩与弧脊")
    tfront=Face(TOWER_X,TOWER_FRONT,1,0)
    tback=Face(TOWER_X,BACK,-1,0)
    cy=(TOWER_FRONT+BACK)/2
    side_half=(BACK-TOWER_FRONT)/2
    tleft=Face(TOWER_X-TOWER_HALF,cy,0,-1)
    tright=Face(TOWER_X+TOWER_HALF,cy,0,1)
    gate=Opening(0,1.62,FLOOR-.09,3.01,1.89,True,5)
    # 拱廊贯入仓房，洞后留出实深，让装卸口从侧面也能看出厚度。
    masonry(shell,tfront,-TOWER_HALF,TOWER_HALF,-4.87,5.94,[gate],.82,1.68,True)
    surround(shell,tfront,gate,.43,d=.22)
    masonry(shell,tback,-TOWER_HALF,TOWER_HALF,-4.85,5.94,(),.94,1.74,True)
    # 拱楼两角的扶壁逐层收分，交替的长短角石盖住纵横墙的接缝。
    for side in (-1,1):
        for j in range(8):
            low=-.45+j*.675
            width=(.93 if j%2 else 1.15)-.032*j
            if side<0:
                a,b=-TOWER_HALF-.18,-TOWER_HALF+width
            else:
                a,b=TOWER_HALF-width,TOWER_HALF+.14
            stone(shell,tfront,rect(a,b,low,low+.66),"dressed",.40-j*.035,.87,.058)
    side_window=Opening(-.63,.49,2.13,3.43,.71)
    masonry(shell,tleft,-side_half,side_half,-4.87,5.94,[side_window],.89,1.65,True)
    window(leaves,tleft,side_window)
    for s in (-1,1):
        f=Face(TOWER_X+s*gate.r,3.35,0,-s)
        masonry(tunnel,f,-2.08,2.08,FLOOR,3.08,(),.76,1.28)
    for a,b in zip(gate.crown,gate.crown[1:]):
        extrude(tunnel,[tfront.at(*a,-.50),tfront.at(*b,-.50),
                        tfront.at(*b,-4.55),tfront.at(*a,-4.55)],(0,0,.14),"shadow")
    masonry(tunnel,Face(TOWER_X,5.56,1,0),-1.68,1.68,FLOOR,5,(),.91,1.7)
    beam(tunnel,(TOWER_X,1,FLOOR-.08),(TOWER_X,5.72,FLOOR-.08),3.55,.15,"dock")
    for f in (tfront,tback,tleft,tright):
        end= f in (tfront,tback)
        half=TOWER_HALF+.14 if end else side_half+.11
        upper_frame(timber,f,half,5.91,10.38,14.22 if end else None,f is tfront)
        for u in (-half+.48,-half/2,half/2,half-.48):
            f.member(timber,(u,5.40),(u,5.98),.25,.64,"oak",.11)
    # 次仓在拱楼右侧，面海长墙设两扇哥特小窗与较低的弓形双扇门。
    hx=(HALL_LEFT+HALL_RIGHT)/2
    hw=(HALL_RIGHT-HALL_LEFT)/2
    hy=(HALL_FRONT+BACK)/2
    hd=(BACK-HALL_FRONT)/2
    front=Face(hx,HALL_FRONT,1,0)
    back=Face(hx,BACK,-1,0)
    right=Face(HALL_RIGHT,hy,0,1)
    entrance=Opening(.48,2.18,FLOOR,3.27,.91,False,6)
    windows=[Opening(u,.56,2.20,3.46,.83,True,4) for u in (-4.21,5.35)]
    masonry(shell,front,-hw,hw,-4.82,6.19,[entrance]+windows,.83,1.69,True)
    door(leaves,front,entrance)
    for w in windows: window(leaves,front,w)
    side=Opening(.11,.77,1.85,3.32,1.10,True,5)
    masonry(shell,right,-hd,hd,-4.86,6.18,[side],.84,1.62,True)
    window(leaves,right,side)
    boarded_gable(timber,right,hd,6.10,6.18,10.15)
    rears=[Opening(u,.53,2.45,3.72,.79,True,4) for u in (-4.55,3.70)]
    masonry(shell,back,-hw,hw,-4.82,6.17,rears,.87,1.80,True)
    for w in rears: window(leaves,back,w)
    for f,half in ((front,hw),(back,hw),(right,hd)):
        f.member(timber,(-half-.08,5.90),(half+.08,5.90),.28,.35,"oak",.10)
        for i in range(round(half*1.45)):
            u=-half+.37+i*1.35
            f.member(timber,(u,5.42),(u,5.98),.23,.52,"oak",.11)
    roof(tiles,TOWER_X,5.29,(0,1),9.29,4.36,10.20,14.63,6)
    roof(tiles,3.93,5.48,(1,0),16.92,4.48,6.12,10.47,7)
    return shell,timber,leaves,tunnel,tiles
