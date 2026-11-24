"""从四种表面母图制作同 UV 的基础色、切线法线和金属度／光滑度图集。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/NaturalPieces/v001"
SOURCE = ROOT / "art/source/round-door-village/四材质母图.png"
SWATCH = 512
# 从左到右、从上到下。每块瓦和石料在各自的色样内取样。
LAYOUT = (
    "oak", "door", "trim", "endgrain",
    "plaster", "stone", "quoin", "arch",
    "tile0", "tile1", "tile2", "tile3",
    "iron", "bronze", "glass", "soot",
)


def smooth(field, radius):
    import numpy as np
    from PIL import Image, ImageFilter
    img = Image.fromarray(np.uint8(np.clip(field, 0, 1) * 255))
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), dtype=float) / 255


def make_maps():
    import numpy as np
    from PIL import Image
    sheet = Image.open(SOURCE).convert("RGB")
    half_x, half_y = sheet.width // 2, sheet.height // 2
    samples = []
    for row in range(2):
        for col in range(2):
            x, y = col * half_x, row * half_y
            patch = sheet.crop((x + 8, y + 8, x + half_x - 8, y + half_y - 8))
            samples.append(np.asarray(patch.resize((SWATCH, SWATCH), Image.Resampling.LANCZOS)) / 255)

    albedo = Image.new("RGB", (4 * SWATCH, 4 * SWATCH))
    normals = albedo.copy()
    finish = Image.new("RGBA", albedo.size)
    ownership = Image.new("L", albedo.size)
    rng = np.random.default_rng(211719)
    v, u = np.mgrid[0:1:complex(SWATCH), 0:1:complex(SWATCH)]
    # 固有色中值、纹理对比、凹凸幅度、金属度和光滑度。
    recipes = {
        "oak": (0, (88, 61, 39), .81, 1.8, 0, .24),
        "door": (0, (137, 94, 55), .72, 1.5, 0, .33),
        "trim": (0, (112, 81, 52), .71, 1.6, 0, .28),
        "endgrain": (0, (111, 82, 57), .43, 1.8, 0, .20),
        "plaster": (2, (204, 191, 157), .80, 1.35, 0, .10),
        "stone": (3, (125, 119, 104), .68, 2.3, 0, .17),
        "quoin": (3, (151, 139, 119), .70, 2.0, 0, .20),
        "arch": (3, (179, 163, 135), .55, 1.65, 0, .26),
        "iron": (3, (50, 53, 51), .48, 1.1, .68, .38),
        "bronze": (3, (151, 105, 48), .57, .76, .76, .52),
        "glass": (2, (83, 110, 109), .40, .35, .07, .73),
        "soot": (3, (52, 46, 39), .51, 1.6, 0, .08),
    }
    for index, colour in enumerate(((151,104,82),(167,116,88),(180,132,102),(192,141,111))):
        recipes["tile" + str(index)] = (1, colour, .93, 1.8, 0, .19 + index * .018)

    for i, name in enumerate(LAYOUT):
        source, colour, contrast, relief, metal, gloss = recipes[name]
        sample = np.roll(samples[source], (i * 41, i * 73), axis=(0, 1))
        # 先保留明度纹理，少量保留天然色变，便于四个席位共用窑烧表面。
        lum = sample @ np.array([.2126, .7152, .0722])
        grain = (lum / np.median(lum) - 1) * contrast
        chroma = (sample - lum[:, :, None])
        chroma -= chroma.mean(axis=(0, 1))
        if name == "endgrain":
            radius = np.hypot((u + .12) * 1.15, v - .25)
            grain += .12 * np.sin(radius * 186 + smooth(lum, 9) * 10)
        if name == "plaster":
            grain -= .095 * np.exp(-(1-v)*8) + .065 * np.exp(-v*10)
        if name.startswith("tile"):
            grain -= .10 * np.exp(-v*7)
            wear = np.clip((smooth(lum,3)-np.quantile(lum,.64)) * 4.1, 0, 1)
            flecks = np.clip((lum-smooth(lum,2)-.03)*5,0,1)
            pigment = np.clip(.85-.20*wear-.16*flecks,.53,.87)
            if name == "tile3":
                pigment[:, u[0]>.78] = 0
            ownership.paste(Image.fromarray(np.uint8(pigment*255)),((i%4)*SWATCH,(i//4)*SWATCH))
        if name == "glass":
            grain = .25 - .51*v + .065*np.sin(12*u + 5*v) + .18*grain
        rgb = np.array(colour)[None, None, :] / 255 * (1 + grain[:, :, None]) + .20*chroma
        height = lum - .65*smooth(lum, 8)
        if name == "endgrain":
            height += grain * .4
        if name == "glass":
            height = smooth(lum, 9) + .18 * np.sin(13*u + 5*v)
        dy, dx = np.gradient(height)
        # 图像行号朝下，切线空间的 +V 朝上。
        n = np.stack((-dx*relief, dy*relief, np.ones_like(dx)), axis=-1)
        n /= np.linalg.norm(n, axis=-1, keepdims=True)
        packed = np.zeros((SWATCH, SWATCH, 4))
        packed[:, :, 0] = metal
        packed[:, :, 3] = np.clip(gloss - grain * .065 + rng.normal(0, .008, lum.shape), 0, 1)
        if name == "iron":
            # 上条为泛水的风化铅皮，下条仍供门铰和铁钉使用。
            rolled = (.19*(smooth(lum,7)-np.median(lum)) + .28*(lum-smooth(lum,6))
                      + .012*np.sin(28*u+smooth(lum,12)*13))
            sheet = v < .40
            rgb[sheet] = (np.array([92,99,97])/255 + rolled[:,:,None])[sheet]
            n[sheet,0:2] *= .19
            n[sheet,2] = np.sqrt(np.maximum(0,1-(n[sheet,:2]**2).sum(axis=1)))
            packed[sheet,0], packed[sheet,3] = .37, (.32-.15*grain)[sheet]
        if name == "tile3":
            # 专用窄条仅被崩角断面采样，四色屋面在此处露出同一陶胎。
            fresh = u > .78
            rgb[fresh] = (np.array([143,100,73])/255*(1+.67*grain[:,:,None]))[fresh]
            packed[fresh,3] = (.11-.03*grain)[fresh]
        offset = ((i % 4)*SWATCH, (i // 4)*SWATCH)
        def pixels(values):
            return Image.fromarray(np.uint8(np.clip(values, 0, 1)*255 + .5))
        albedo.paste(pixels(rgb), offset)
        normals.paste(pixels(.5 + .5*n), offset)
        finish.paste(pixels(packed), offset)

    albedo.save(ART / "village-surface.png")
    normals.save(ART / "village-normal.png")
    # 四个通道独立缩小，避免对光滑度通道做预乘。
    bands = [c.resize((512, 512), Image.Resampling.BOX) for c in finish.split()]
    Image.merge("RGBA", bands).save(ART / "village-finish.png")
    ownership.resize((1024,1024),Image.Resampling.BOX).save(ART.parents[1]/"PlayerColors/village-mask.png")


if __name__ == "__main__":
    make_maps()
