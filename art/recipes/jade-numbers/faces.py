"""生成居中的透明点数字面；保留港口图稿使用的玉面配方。"""
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
    # 原型以 32 像素方图排 18 像素 Georgia 粗体，在高分辨率下烘焙轮廓。
    factor = 32
    font = ImageFont.truetype("C:/Windows/Fonts/georgiab.ttf", 18 * factor)
    rim = "#f6e7c8"
    for n in (2, 3, 4, 5, 6, 8, 9, 10, 11, 12):
        design = Image.new("RGBA", (32 * factor, 32 * factor))
        draw = ImageDraw.Draw(design)
        text = str(n)
        left, top, right, bottom = draw.textbbox((0, 0), text, font=font)
        origin = (512 - (left + right) / 2, 512 - (top + bottom) / 2)
        ink = "#922b20" if n in (6, 8) else "#362a23"
        draw.text(origin, text, font=font, fill=ink, stroke_width=30, stroke_fill=rim)
        count = 6 - abs(7 - n)
        for i in range(count):
            x = (16 + (i - (count - 1) / 2) * 2.8) * factor
            y = (16 + 8.8) * factor
            for radius, color in ((1.4, rim), (.85, ink)):
                r = radius * factor
                draw.ellipse((x-r, y-r, x+r, y+r), fill=color)
        design.resize((512, 512), Image.Resampling.LANCZOS).save(out / f"number-{n}.png")
