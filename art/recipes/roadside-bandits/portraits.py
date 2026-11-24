"""三张肖像的眼睑、眼球、眉髭与卷发。毛束以头颅表面为依托。"""
from math import pi,tau,sin,cos,exp
from functools import lru_cache
import numpy as np
from sculpt import V,UP
from head_form import Anatomy,head_template,skin_point,beard_hairline


class Head(Anatomy):
    def face(self,g):
        g.heads.append((self.role,self.shape(head_template()["vertices"])))
        for sign in (-1,1):
            self.eye(g,sign)

    def eye(self,g,side):
        # 虹膜与角膜服从眼球曲率，眼白只在上下睑之间露出。眼睑属于头颈基网。
        angle=np.linspace(-pi,pi,40,endpoint=False)[None,:]
        theta=np.linspace(.008,pi-.012,24)[:,None]
        r=.139
        x=r*np.sin(theta)*np.sin(angle)
        y=r*np.sin(theta)*np.cos(angle)
        z=(r+.010)*np.cos(theta)+.005*np.exp(-(theta/.48)**4)
        gaze={"carrier":(-.15,.10),"leader":(.065,.02),"scout":(.13,.065)}[self.role]
        az,el=gaze
        forward=z*cos(el)-y*sin(el)
        points=np.stack((side*.307+x*cos(az)+forward*sin(az),
                         7.284+y*cos(el)+z*sin(el),1.245+forward*cos(az)-x*sin(az)),axis=-1)
        g.grid([[V(p) for p in rr] for rr in self.shape(points)],"eye",density=1,paint="iris-"+self.role)
        # 虹膜用正对角膜的圆盘坐标，避免减面后把极点拉成纵缝。
        radius=np.where(theta<pi/2,np.sin(theta),1.0)*.495
        az=np.linspace(-pi,pi,41)[None,:]
        g.charts[-1].pigment_uv=np.stack((.5+radius*np.sin(az),.5-radius*np.cos(az)),axis=-1)
    @lru_cache(maxsize=32768)
    def scalp(self,a,t):
        scout=self.role=="scout"
        back=(1-cos(a))*.5
        edge=7.78-(1.37 if scout else 1.16)*min(1,back*1.42)**.87
        # 两侧在耳轮上方开口，鬓角与后枕各自有清晰轮廓。
        edge+=(.83 if scout else .34)*exp(-((abs((a+pi)%tau-pi)-1.63)/.36)**2)
        if not scout:edge+=.09*sin(11*a+.7)*max(0,cos(a))**2
        edge+=.055*sin(a*3.1+.8)+.043*cos(a*7-.8)
        y=8.465+(edge-8.465)*t
        native=np.array(skin_point(a,y))
        radial=self.right*sin(a)+self.forward*cos(a)
        outward=(radial*(.12+.88*sin(t*pi*.54))+UP*max(0,1-t)**1.05).normalized()
        puff=(1.0 if scout else 2.23)+(.48 if scout else .86)*sin(7*a-3.5*t+sin(4*a+3*t))
        puff*=1-.69*max(0,min(1,(t-.73)/.27))
        return self.at(native)+outward*puff,outward

    def hair(self,g):
        scout=self.role=="scout"
        base=.53 if scout else .0
        rows=[]
        for j in range(22):
            t=base+(1-base)*j/21
            rows.append([self.scalp(-pi+tau*k/68,t)[0] for k in range(68)])
        key="scalp-"+self.role
        g.grid(rows,"hair",density=1,paint=key,caps=False)
        g.charts[-1].pigment_uv=[[(k/68,base+(1-base)*j/21) for k in range(69)] for j in range(22)]
        # 各层根部埋入下一绺，冠顶的分缝、前额的翘卷与后颈的短发有不同流向。
        layers=((16,.51,.93),(21,.67,1.055)) if scout else ((16,.025,.62),(21,.21,.85),(28,.46,1.04))
        for layer,(count,start,end) in enumerate(layers):
            for i in range(count):
                a=-pi+tau*(i+.37+.38*layer)/count
                if scout and cos(a)>.61:continue
                seed=i*1.73+layer*2.41
                lock=[];tex=[]
                for j in range(13):
                    t=j/12
                    level=start+(end-start+.042*sin(seed))*t
                    side=1 if sin(a+.18)>0 else -1
                    flow=side*(.37*(1-t)+(.23 if scout else .16)*sin(1.74*pi*t+seed*.43))+.045*sin(seed+5*t)
                    width=(.17 if scout else .22)*(.24+.76*sin(pi*t)**.56)*(1-.58*t*t)
                    band=[];strip=[]
                    for k in range(8):
                        q=2*k/7-1
                        az=a+flow+width*q
                        along=level+.022*q*sin(pi*t)
                        pos,n=self.scalp(az,along)
                        strip.append((.5+az/tau,along))
                        ridge=(1-q*q)*sin(pi*(.055+.91*t))**.67
                        rib=.74+.19*cos(9*q+.7*sin(pi*t))+.07*sin(19*q+t*4)
                        band.append(pos+n*((1.45 if scout else 2.35)*ridge*rib-.19)+UP*(.36*ridge))
                    lock.append(band);tex.append(strip)
                g.grid(lock,"hair",wrap=False,density=1,paint=key)
                g.charts[-1].pigment_uv=tex

    @lru_cache(maxsize=16384)
    def beard_point(self,a,t):
        t=max(0,t)
        side=abs(sin(a))
        start=beard_hairline(a)
        bottom=6.23+.18*side+.085*side**4
        line=min(t/.83,1)
        y=start+(bottom-start)*line
        native=np.array(skin_point(a,y))
        n=self.right*sin(a)+self.forward*cos(a)
        end=max(0,(t-.83)/.17)
        if end:
            native[0]*=1-.25*end
            native[2]=native[2]*(1-.42*end)+(.60+.11*cos(a))*.42*end
        body=sin(pi*min(1,t))**.61
        mass=(3.6 if self.role=="carrier" else 2.7)*body
        wave=.57*cos(10*a+1.7*t+sin(3*a))+.37*sin(17*a-2.4*t)
        pt=self.at(native)+n*(-.18+mass+body*wave)-UP*((8.7 if self.role=="carrier" else 5.3)*t**1.12+.6*t*cos(7*a))
        return pt,n

    def beard(self,g):
        if self.role=="scout":return
        rows=[[self.beard_point(-1.65+3.30*k/56,j/19)[0] for k in range(57)] for j in range(20)]
        key="whiskers-"+self.role
        g.grid(rows,"beard",wrap=False,density=1,paint=key)
        g.charts[-1].pigment_uv=[[(k/56,j/19) for k in range(57)] for j in range(20)]
        for layer,count in enumerate((25,31)):
            for i in range(count):
                phase=i*2.34+layer
                centre=-1.60+3.20*(i+.46+.1*layer)/count
                start=(.03 if layer==0 else .36)+.035*cos(phase)
                end=(.72 if layer==0 else .99)-.045*sin(phase)
                lock=[];tex=[]
                for j in range(10):
                    t=j/9
                    shift=.048*sin(t*4.7+phase)-.048*t*sin(centre)
                    width=.105*(.25+.75*sin(pi*t)**.57)*(1-.64*t*t)
                    band=[];strip=[]
                    for k in range(7):
                        q=k/3-1
                        az=centre+shift+q*width
                        along=start+(end-start)*t
                        pos,n=self.beard_point(az,along)
                        strip.append((.5+az/3.30,along))
                        lift=(1-q*q)*(.15+1.14*sin(pi*t)**.72)*(.81+.19*cos(8*q+phase+t))
                        band.append(pos+n*(lift-.07)-UP*(.34*t*t*(1-q*q)))
                    lock.append(band);tex.append(strip)
                g.grid(lock,"beard",wrap=False,density=1,paint=key)
                g.charts[-1].pigment_uv=tex
