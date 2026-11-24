"""将正式 FBX 的四个正交回读视图等高排版，保留近足的棚拍接触阴影。"""
import numpy as np
from PIL import Image,ImageDraw,ImageFont
from surfaces import ROOT,REFERENCE


def compose(output):
    width,height=1792,848
    y,x=np.indices((height,width),dtype=float)
    shade=np.clip(.32+.41*(1-y/height)+.18*np.cos((x/width-.43)*np.pi),0,1)
    paper=np.uint8(np.array([231,219,205])+shade[:,:,None]*np.array([15,16,16]))
    board=Image.fromarray(paper).convert("RGBA")
    for i,label in enumerate(("商人三分之四","正面","侧面","背面")):
        data=np.array(Image.open(output/(label+".png")).convert("RGBA"))
        h,w=data.shape[:2]
        sy,sx=np.indices((h,w),dtype=float)
        shadow=(data[:,:,:3].max(axis=2)<28)&(data[:,:,3]<244)
        fade=.74*np.exp(-((sx-w*.50)/(.33*w))**4-((sy-h*.935)/(.075*h))**2)
        data[:,:,3][shadow]=np.uint8(data[:,:,3][shadow]*fade[shadow])
        figure=Image.fromarray(data)
        solid=data[:,:,3]>244
        yy,xx=np.where(solid)
        cx=(xx.max()+xx.min())/2
        scale=703/(yy.max()-yy.min())
        figure=figure.resize((round(w*scale),round(h*scale)),Image.Resampling.LANCZOS)
        center=width*(i+.5)/4
        board.alpha_composite(figure,(round(center-cx*scale),round(47-yy.min()*scale)))
    draw=ImageDraw.Draw(board)
    font=ImageFont.truetype(str(ROOT/"Unity/Catan/Assets/Resources/Fonts/NotoSerifCJKsc-Regular.otf"),27)
    for i,title in enumerate(("三分之四","正面","侧面","背面")):
        draw.text((width*(i+.5)/4,803),title,font=font,fill=(70,49,36),anchor="mm")
    board.convert("RGB").save(output/"商人四视图.png")


def hand_sheet(shots,output):
    w,h,gap,header=610,480,10,56
    page=Image.new("RGB",(w*3+gap*4,h*2+gap*3+header),(237,228,217))
    draw=ImageDraw.Draw(page)
    font=ImageFont.truetype(str(ROOT/"Unity/Catan/Assets/Resources/Fonts/NotoSerifCJKsc-Regular.otf"),26)
    for i,label in enumerate(("原型局部","游戏模型 · 原型角度","游戏模型 · 转面")):
        draw.text((gap+(i+.5)*w+i*gap,header/2),label,font=font,fill=(69,48,37),anchor="mm")
    reference=Image.open(REFERENCE).convert("RGB")
    for i,(area,prefix) in enumerate((((118,301,239,396),"摊掌"),((310,340,427,432),"扶卷"))):
        y=header+gap+i*(h+gap)
        page.paste(reference.crop(area).resize((w,h),Image.Resampling.LANCZOS),(gap,y))
        for j,angle in enumerate(("原型角","转面"),1):
            photo=Image.open(shots/(prefix+angle+".png")).convert("RGBA")
            page.paste(photo,(gap+j*(w+gap),y),photo)
    page.save(output/"双手原型对照.jpg",quality=94,subsampling=0)
