"""将彩稿可见的衣料颜色按裁片回投，背光、遮挡与露底处保留各自的织物与皮面。"""
import numpy as np
from PIL import Image,ImageDraw,ImageFilter
from surfaces import REGIONS,CHARTS,PORTRAIT_BOX,REFERENCE,SIZE


def project_cuts(library,charts):
    art=Image.open(REFERENCE).convert("RGB").crop((0,0,435,810))
    rgb=np.array(art,dtype=float)
    lum=np.asarray(art.convert("L"),dtype=float)
    r,g,b=rgb.transpose(2,0,1)
    def mask(polys,holes=()):
        image=Image.new("L",art.size)
        pen=ImageDraw.Draw(image)
        for shape in polys: pen.polygon(shape,fill=255)
        for shape in holes: pen.polygon(shape,fill=0)
        return np.asarray(image)>0
    pouch=[(143,443),(160,430),(195,427),(213,442),(204,464),(212,502),
           (199,522),(155,525),(133,508),(137,475),(150,457)]
    palm=[(134,347),(137,331),(136,320),(143,312),(153,311),(163,324),(167,337),
          (183,348),(193,355),(205,351),(216,346),(223,351),(221,362),
          (207,377),(183,390),(154,387),(133,379)]
    grip=[(338,375),(347,368),(365,371),(358,361),(359,350),(369,350),(383,360),
          (393,380),(393,401),(380,418),(365,428),(344,418),(331,404),(331,386)]
    plum=(b>g*.78)&(r>g*1.13)&(lum<190)
    masks={}
    masks["袍身"]=mask([
        [(164,220),(184,214),(212,226),(239,267),(249,294),(250,343),(241,388),
         (225,428),(210,449),(206,458),(153,445),(139,409),(155,327)],
        [(293,216),(311,222),(328,246),(342,278),(337,412),(367,510),(387,603),
         (364,616),(315,603),(304,523),(311,436),(308,345),(302,271)],
        [(167,423),(232,426),(260,427),(244,508),(235,606),(209,619),(143,608),
         (108,611),(72,600),(101,540),(129,482)]],[pouch,palm,grip])&plum
    masks["迎客袖"]=mask([[(152,225),(178,235),(188,270),(187,319),(202,351),(195,394),
                           (181,420),(160,443),(118,460),(85,438),(63,371),(75,343),(108,293)]],[palm])&plum
    masks["持布袖"]=mask([[(307,229),(330,239),(362,273),(388,331),(414,363),
                        (414,425),(389,471),(353,475),(338,449),(343,419),(340,334)]],[grip])&plum
    masks["帽冠"]=mask([[(137,151),(147,128),(171,107),(190,88),(223,81),(244,76),
                       (267,85),(293,103),(305,128),(304,146),(291,159),(280,149),
                       (275,128),(260,116),(240,116),(213,126),(188,142),(169,171),(150,180),(138,176)]])&plum
    masks["马甲"]=mask([[(204,232),(234,249),(272,277),(293,242),(307,262),(317,324),
                       (325,383),(309,406),(239,407),(231,384),(242,325)],
                       [(240,431),(308,430),(314,536),(292,545),(251,541)]])&(b<g*.89)
    # 扣子和扣眼在模型上另有实体，避免原型高光留在相邻布面。
    yy,xx=np.indices(lum.shape)
    for x,y in ((283,310),(287,338),(292,365),(295,390),(288,467),(290,497)):
        masks["马甲"]&=((xx-x)/9)**2+((yy-y)/11)**2>1
    masks["左裤"]=mask([[(102,595),(130,601),(170,610),(222,608),(210,640),
                       (192,651),(140,653),(116,637)]])
    masks["右裤"]=mask([[(225,572),(246,542),(312,541),(319,592),(348,608),(332,633),
                       (311,650),(246,644),(227,623)]])
    for name in ("左裤","右裤"):
        masks[name]&=(lum<157)&(r>g*1.08)&(b<g*.92)
    for side,boot in (("左",[(115,639),(203,646),(206,687),(190,695),(188,733),(178,762),
                           (184,789),(176,798),(112,801),(107,786),(114,754),(122,704),(106,683)]),
                      ("右",[(230,636),(314,640),(325,677),(312,694),(312,721),(333,740),
                           (367,750),(375,772),(368,783),(314,787),(249,777),(234,766),(236,722)])):
        leather=mask([boot])&(r>g*1.075)&(lum<189)
        yy=np.indices(lum.shape)[0]
        masks[side+"鞋面"]=leather&(yy>726)
        masks[side+"靴筒"]=leather&(yy>677)&(yy<755)
        masks[side+"靴口"]=leather&(yy>640)&(yy<694)
    output=[Image.new(im.mode,(SIZE,SIZE),im.getpixel((30,30))) for im in library]
    for target,old in zip(output,library):
        target.paste(old.resize((SIZE//2,SIZE//2),Image.Resampling.LANCZOS),(0,0))

    def blur(arr,sigma):
        scaled=Image.fromarray(np.uint8(np.clip(arr,0,255)))
        return np.asarray(scaled.filter(ImageFilter.GaussianBlur(sigma)),dtype=float)

    def get_fields(name):
        valid=masks[name]
        # 归一化卷积只对同一块衣料估计宽光斑，扣件和背景不会漂入布纹。
        support=blur(valid*255,10)/255
        broad=blur(lum*valid,10)/np.maximum(.025,support)
        local=blur(lum*valid,2.1)/np.maximum(.03,blur(valid*255,2.1)/255)
        coverage=blur(valid*255,1.6)/255
        return broad,local,coverage

    def put(index,name,array):
        x,y,w,h=CHARTS[name]
        arr=np.uint8(np.clip(array,0,255))
        edge=np.pad(arr,((6,6),(6,6),(0,0)),mode="edge")
        output[index].paste(Image.fromarray(edge),(x,y))

    def generic(tag,w,h):
        x,y,n=REGIONS[tag]
        box=(x+8,y+8,x+n-8,y+n-8)
        return [np.array(im.crop(box).resize((w,h),Image.Resampling.BICUBIC),dtype=float) for im in library]

    def head_paint(xyz,coords,normal,finish):
        # 彩色角度提供五官与胡须颜色。转到耳前时，以侧视泥稿给出的颌线、鬓角和鼻唇沟接续。
        angle=coords*2*np.pi
        front=np.clip((np.cos(angle)+.04)/.76,0,1)
        front=front*front*(3-2*front)
        rx,ry,rw=REGIONS["portrait"]
        x0,y0,x1,y1=PORTRAIT_BOX
        x=np.clip((xyz[:,:,0]+230-x0)/(x1-x0),0,1)
        row=795-xyz[:,:,2]
        y=np.clip((row-y0)/(y1-y0),0,1)
        def lookup(data,x,y):
            x=np.clip(x,0,data.shape[1]-1.001);y=np.clip(y,0,data.shape[0]-1.001)
            xi,yi=x.astype(int),y.astype(int)
            tx,ty=x-xi,y-yi
            if data.ndim==3: tx,ty=tx[:,:,None],ty[:,:,None]
            return (data[yi,xi]*(1-tx)+data[yi,xi+1]*tx)*(1-ty)+(data[yi+1,xi]*(1-tx)+data[yi+1,xi+1]*tx)*ty
        face=lookup(np.asarray(library[0]),rx+8+x*(rw-16),ry+8+y*(rw-16))
        gray=np.asarray(Image.open(REFERENCE).convert("L").crop((981,111,1132,223)),dtype=float)
        sx=1060+.86*(xyz[:,:,1]+1)-981
        sy=row+4.0*np.exp(-((row-164)/27)**2)+.6-111
        tone=lookup(gray,sx,sy)
        small=lookup(blur(gray,2.2),sx,sy)
        broad=lookup(blur(gray,8),sx,sy)
        beard=Image.new("L",(151,112))
        outline=[(999,180),(1004,175),(1014,175),(1021,180),(1025,190),(1028,183),
                 (1034,177),(1040,166),(1043,158),(1054,156),(1054,169),(1047,177),
                 (1046,187),(1040,198),(1030,205),(1017,213),(1002,215),(997,209),
                 (1000,199),(1008,190),(1013,187),(1002,186)]
        ImageDraw.Draw(beard).polygon([(x-981,y-111) for x,y in outline],fill=255)
        whisker=lookup(np.asarray(beard.filter(ImageFilter.GaussianBlur(1.5))),sx,sy)/255
        soft=np.clip(.99+(tone-broad)*.0047,.70,1.20)
        skin=np.array([204,150,119])*soft[:,:,None]
        hair=np.array([89,64,49])*np.clip(.98+(tone-broad)*.017,.59,1.48)[:,:,None]
        side=skin*(1-whisker[:,:,None])+hair*whisker[:,:,None]
        skull=face*front[:,:,None]+side*(1-front[:,:,None])
        relief=(tone-small)*(1-front)
        ny,nx=np.gradient(relief)
        vec=normal/127.5-1
        vec[:,:,0]-=np.clip(nx*.025,-.23,.23);vec[:,:,1]+=np.clip(ny*.025,-.23,.23)
        vec/=np.linalg.norm(vec,axis=2,keepdims=True)
        return skull,(vec*.5+.5)*255,finish

    for name,(points,wrapped,tag) in charts.items():
        _,_,w,h=CHARTS[name]
        w,h=w-12,h-12
        colour,normal,finish=generic(tag,w,h)
        grid=np.asarray(points,dtype=float)
        if wrapped: grid=np.concatenate((grid,grid[:,:1,:]),axis=1)
        rows,columns=grid.shape[:2]
        v,u=np.indices((h,w),dtype=float)
        j=(1-v/(h-1.00001))*(rows-1.00001)
        k=u/(w-1.00001)*(columns-1.00001)
        ji,ki=j.astype(int),k.astype(int)
        jt,kt=(j-ji)[:,:,None],(k-ki)[:,:,None]
        a=grid[ji,ki]*(1-kt)+grid[ji,ki+1]*kt
        b=grid[ji+1,ki]*(1-kt)+grid[ji+1,ki+1]*kt
        xyz=a*(1-jt)+b*jt
        if name=="头颅":
            for i,arr in enumerate(head_paint(xyz,u/(w-1),normal,finish)): put(i,name,arr)
            continue
        nk=np.cross(np.gradient(xyz,axis=1),np.gradient(xyz,axis=0))
        nk/=np.maximum(np.linalg.norm(nk,axis=-1,keepdims=True),1e-7)
        center=grid[:,:-1 if wrapped else columns].mean(axis=1)
        radial=xyz-(center[ji]*(1-jt)+center[ji+1]*jt)
        if np.median(np.sum(radial*nk,axis=2))<0: nk=-nk
        ix=np.clip(xyz[:,:,0]+230,0,art.width-1.001)
        iy=795-xyz[:,:,2]
        if name.startswith("右") and name!="右裤":
            iy-=12*np.clip((iy-647)/147,0,1)
        iy=np.clip(iy,0,art.height-1.001)
        xi,yi=ix.astype(int),iy.astype(int)
        def sample(data):
            tx,ty=ix-xi,iy-yi
            if data.ndim==3: tx,ty=tx[:,:,None],ty[:,:,None]
            return ((data[yi,xi]*(1-tx)+data[yi,xi+1]*tx)*(1-ty)+
                    (data[yi+1,xi]*(1-tx)+data[yi+1,xi+1]*tx)*ty)
        broad,local,coverage=get_fields(name)
        sides=np.clip((-.98*nk[:,:,1]+.18*nk[:,:,2]-.05)/.55,0,1)
        weight=sides*np.clip((sample(coverage)-.48)*2.3,0,1)*sample(masks[name].astype(float))
        ref=sample(rgb)
        target={"coat":80,"damask":136,"boots":79,"cap":86,"breeches":86}[tag]
        lighting=np.clip((target/np.maximum(sample(broad),24))**.44,.72,1.48)
        ref=ref*lighting[:,:,None]
        # 保留纱线高频，同时让每个肘弯和靴面获得自己所在位置的磨损。
        wgt=weight[:,:,None]*.78
        micro=colour-np.stack([blur(colour[:,:,c],2.2) for c in range(3)],axis=2)
        colour=colour*(1-wgt)+(ref+micro*.76)*wgt
        wrinkle=(sample(lum)-sample(local))*weight
        ny,nx=np.gradient(wrinkle)
        vec=normal/127.5-1
        vec[:,:,0]-=np.clip(nx*.026,-.24,.24)
        vec[:,:,1]+=np.clip(ny*.026,-.24,.24)
        vec/=np.maximum(.01,np.linalg.norm(vec,axis=2,keepdims=True))
        for i,arr in enumerate((colour,(vec*.5+.5)*255,finish)): put(i,name,arr)
    from merchant_hands import hand_paint
    for name in ("掌面","手背"):
        _,_,w,h=CHARTS[name]
        for i,arr in enumerate(hand_paint(w-12,h-12,name)):
            put(i,name,arr)
    return output
