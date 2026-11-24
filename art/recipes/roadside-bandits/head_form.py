"""头面原始布线、三张脸的比例，以及由锁骨到头颅的连续转动。"""
from functools import lru_cache
from pathlib import Path
from math import sin,cos
from mathutils.bvhtree import BVHTree
import numpy as np
from sculpt import P,V,BASE_ROW,C,S


HEIGHTS=np.array([5.35,5.70,6.02,6.18,6.36,6.65,6.89,7.04,7.284,7.46,7.77,8.10,8.49])
# 头的中轴、进深、转角、宽深比例及逐点标定的正视行坐标。
FACES={
    "carrier":(278,56,-.42,31.1,28.8,[171,153,144,135.0,129.3,118.8,110.7,107.4,100.2,95.4,89.7,84.1,79.4]),
    "leader":(394,0,.24,28.2,26.4,[231,212,203,193.3,188.5,179.8,172.8,169.1,161.7,156.3,150.1,143.7,137.6]),
    "scout":(491.0,67,1.15,26.1,25.3,[201,185,174.8,165.7,161.4,155.4,148.6,145.4,137.2,132.5,125.2,115.0,107.2]),
}


@lru_cache(maxsize=1)
def head_template():
    with np.load(Path(__file__).with_name("hm08-head.npz"),allow_pickle=False) as z:
        return {k:z[k] for k in z.files}


@lru_cache(maxsize=1)
def skull_tree():
    m=head_template()
    return BVHTree.FromPolygons(m["vertices"].tolist(),m["faces"].tolist())


@lru_cache(maxsize=32768)
def skin_point(angle,level):
    angle=float(angle);level=min(8.472,float(level))
    direction=V((sin(angle),0,cos(angle)))
    centre=V((0,level,.56))
    hit=skull_tree().ray_cast(centre+direction*2.6,-direction,4)[0]
    return tuple(hit) if hit else tuple(centre+direction*.10)


def beard_hairline(angle):
    side=np.abs(np.sin(angle))
    return 6.474+.655*side**1.82+.074*np.exp(-((np.abs(angle)-.81)/.29)**2)+side*(.015*np.sin(12*angle)+.010*np.cos(21*angle))


class Anatomy:
    def __init__(self,role):
        self.role=role
        self.x,self.y,self.yaw,self.sx,self.sz,self.levels=FACES[role]
        self.right=V((cos(self.yaw),sin(self.yaw),0))
        self.forward=V((sin(self.yaw),-cos(self.yaw),0))

    def point(self,x,forward,row):
        offset=self.right*x+self.forward*forward
        return P(self.x+offset.x,row,self.y+offset.y)

    def shape(self,points):
        a=np.asarray(points,dtype=float)
        x,y,f=np.moveaxis(a,-1,0)
        ax=np.abs(x);sign=np.sign(x)
        def bump(cx,cy,wx,wy):
            return np.exp(-((ax-cx)/wx)**2-((y-cy)/wy)**2)
        front=np.clip((f-.78)/.45,0,1)
        heavy=self.role=="carrier"; lean=self.role=="scout"
        # 颧弓、咬肌和眶上缘。鼻翼、口角与眉弓有细微不对称。
        jaw=bump(.49,6.57,.24,.32); cheek=bump(.54,7.01,.205,.21)
        brow=bump(.29,7.438,.25,.090); medial=bump(.115,7.415,.095,.105)
        nose=bump(.09,6.96,.14,.18); lips=bump(.10,6.65,.29,.107)
        eye=bump(.31,7.284,.21,.12)
        xx=x+sign*((.113 if heavy else .064 if not lean else .016)*jaw+
                   (.072 if heavy else .046)*cheek+(.045 if heavy else .018)*nose)*front
        xx+=.016*np.sin((y-6.8)*3.1)*nose*front
        xx+=sign*(.112 if lean else .075)*bump(.785,7.19,.155,.29)*np.exp(-((f-.485)/.245)**2)
        yy=y+front*(-(.053 if not lean else .025)*medial-.021*brow+
                    .022*(ax/.45-.54)*brow-.024*bump(.245,6.64,.10,.082)+
                    (.006 if lean else .011)*np.tanh((7.285-y)/.035)*eye)
        zz=f+front*((.095 if heavy else .079 if not lean else .043)*brow+
                    .042*medial+(.079 if lean else .048)*cheek-.05*bump(.49,6.79,.19,.15)+
                    (.078 if heavy else .108 if lean else .077)*nose-.025*lips+
                    .051*bump(.01,6.34,.28,.16))
        if not lean:
            # 人中上方的短髭与口角连续，色素随同一块皮肤进入嘴边。
            arc=6.758-.15*(ax/.40)**1.65
            zz+=.045*np.exp(-((y-arc)/.063)**2-(ax/.385)**6)*front
        throat=np.clip((1.16-f)/.26,0,1)*np.clip((f-.27)/.38,0,1)
        sternal=np.exp(-((ax-(.14+.44*np.clip((y-5.63)/.93,0,1)))/.10)**2-((y-6.02)/.53)**4)
        zz+=throat*(.078*sternal+.084*bump(.0,6.02,.16,.16)-.049*bump(.0,5.68,.23,.135))
        # 宽阔的颈根在衣领下延入双肩，转头的斥候由锁骨逐渐过渡到耳后。
        amount=np.clip((y-5.58)/.98+.90*np.maximum(0,f-.77),0,1)
        amount=amount**2*(3-2*amount)
        yaw=self.yaw*(.16+.84*amount)
        lower=np.clip((6.29-y)/.65,0,1)*np.clip((1.12-f)/.25,0,1)
        xx=xx*(1-.065*lower)
        xx=np.sign(xx)*(np.minimum(np.abs(xx),.42)+np.maximum(0,np.abs(xx)-.42)*(1-.38*lower))
        pivot=.54-.21*lower
        width=xx*self.sx;depth=(zz-pivot)*self.sz
        wx=self.x+width*np.cos(yaw)+depth*np.sin(yaw)
        wy=self.y+width*np.sin(yaw)-depth*np.cos(yaw)
        row=np.interp(yy,HEIGHTS,self.levels)
        row+=(-.050 if heavy else -.025 if not lean else .028)*width
        # 仰头者的颏尖抬起，后颈和下颌底保有实体厚度。
        row+=(.9 if lean else -1.65 if heavy else -.39)*(zz-.73)
        return np.stack((wx,wy,(BASE_ROW-row-S*wy)/C),axis=-1)

    def at(self,coords):
        return V(self.shape(coords))

    def on(self,a,y):
        return self.at(skin_point(a,y))
