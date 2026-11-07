"""烘焙暖白玉、金边与点数。布局单位取自模型的宽度。"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[3]
out = root / "Unity/Catan/Assets/Resources/TabletopUI/v008/NumberTokens"
def jade_base():
    size = 1024
    v,u = np.mgrid[:size,:size] / (size-1)
    x,z = u-.5, (.5-v)*1.22
    qx,qz = np.abs(x)-.345, np.abs(z)-.455
    depth = .155-(np.hypot(np.maximum(qx,0),np.maximum(qz,0))+np.minimum(np.maximum(qx,qz),0))
    # 环形金线在左上高光处较亮，玉芯在四角向蜂蜜色过渡。
    grain = np.random.default_rng(2112).normal(0,.55,(size,size))
    cloud = (np.sin(13*u+7*v+2*np.sin(8*v))+np.sin(17*v-3*u)+.5*np.sin(38*u+15*v)) * 1.25
    light = np.clip(.48-.52*x+.38*z,0,1)
    stops = [-.01,0,.007,.017,.026,.033,.040,.052,.08,.13,.23]
    colors = [(98,66,36),(115,77,36),(181,129,62),(232,193,124),(183,135,68),
              (132,91,50),(201,162,111),(222,195,158),(238,219,190),(245,230,207),(247,234,212)]
    channels = []
    for ch in range(3):
        base = np.interp(depth,stops,[c[ch] for c in colors])
        gleam = (light-.5)*np.where(depth<.04,78,15)
        channels.append(np.clip(base+gleam+grain+cloud,0,255))
    return Image.fromarray(np.stack(channels,axis=-1).astype('uint8'))


if __name__ == "__main__":
    base = jade_base()
    for n in (2,3,4,5,6,8,9,10,11,12):
        image = base.copy()
        # 用物理长宽比排字，再转回方形 UV 贴图。
        design = image.resize((1024,1250),Image.Resampling.BICUBIC)
        draw = ImageDraw.Draw(design)
        font = ImageFont.truetype("C:/Windows/Fonts/times.ttf",910 if n<10 else 740)
        text = str(n)
        box = draw.textbbox((0,0),text,font=font)
        center = (512,535)
        xy = (center[0]-(box[0]+box[2])/2,center[1]-(box[1]+box[3])/2)
        ink = (124,23,18) if n in (6,8) else (52,39,32)
        draw.text(xy,text,font=font,fill=ink,stroke_width=2,stroke_fill=ink)
        count = 6-abs(7-n)
        for i in range(count):
            px = 512+(i-(count-1)/2)*127
            draw.ellipse((px-39,948,px+39,1026),fill=ink)
        design.resize((512,512),Image.Resampling.LANCZOS).save(out / f"number-{n}.png")
