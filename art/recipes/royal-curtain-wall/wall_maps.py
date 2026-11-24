"""从王城同源石料取色；雕石、石灰砂浆、庭土、亚麻与门旗五金各有独立的表面响应。"""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "Unity/Catan/Assets/Art/ExpansionPieces/v001"
COLOURS = ART.parents[1] / "PlayerColors"
SOURCE = ROOT / "art/source/royal-curtain-wall/王城石墙.blend"
PREVIEW = ROOT / "art/previews/royal-curtain-wall"
REGIONS = {
    "bed": (0, 0, 1024), "aged": (1024, 0, 1024),
    "trim": (0, 1024, 512), "foot": (512, 1024, 512),
    "pave": (1024, 1024, 512), "mortar": (1536, 1024, 512),
    "soil": (0, 1536, 512), "cloth": (512, 1536, 512),
    "brass": (1024, 1536, 512), "iron": (1536, 1536, 512),
}


def make_maps():
    import numpy as np
    from PIL import Image

    mother = Image.open(ROOT / "art/source/gothic-royal-city/王城材质母图.png").convert("RGB")
    half_x, half_y = mother.width // 2, mother.height // 2
    limestone = mother.crop((7, 7, half_x - 7, half_y - 7))
    bronze = mother.crop((half_x + 7, half_y + 7, mother.width - 7, mother.height - 7))
    rng = np.random.default_rng(21171122)
    colour = Image.new("RGB", (2048, 2048))
    normal = Image.new("RGB", colour.size)
    packed = Image.new("RGBA", colour.size)
    dye = Image.new("L", colour.size)
    # 固有色中值、光滑度、法线起伏。石壁的灰缝与砌块倒棱由几何提供。
    tones = {
        "bed": ((182, 172, 151), .24, 3.2), "aged": ((168, 156, 131), .21, 3.7),
        "trim": ((203, 193, 173), .32, 2.6), "foot": ((142, 136, 118), .20, 4.3),
        "pave": ((168, 160, 143), .31, 3.0), "mortar": ((141, 133, 115), .11, 4.1),
        "soil": ((146, 129, 104), .12, 4.5), "cloth": ((144, 143, 136), .18, 2.7),
        "brass": ((149, 109, 61), .51, 1.9), "iron": ((83, 78, 69), .35, 2.1),
    }
    def to_image(data):
        return Image.fromarray(np.uint8(np.clip(data * 255 + .5, 0, 255)))

    for index, (kind, (left, top, size)) in enumerate(REGIONS.items()):
        metal = kind in ("brass", "iron")
        sample = bronze if metal else limestone
        rgb = np.asarray(sample.resize((size, size), Image.Resampling.LANCZOS), dtype=np.float32) / 255
        rgb = np.roll(rgb, (17 * index, 37 * index), axis=(0, 1))
        y, x = np.indices((size, size), dtype=np.float32)
        luminance = rgb @ [.2126, .7152, .0722]
        grain = (luminance / np.median(luminance) - 1).clip(-.8, 1.5)
        tint = rgb - luminance[:, :, None]
        tint -= np.median(tint, axis=(0, 1))
        tone, gloss, relief = tones[kind]
        fine = rng.normal(0, .017, grain.shape)
        # 锤凿留下断续的平行短线；孔隙和壳屑随岩层变化。
        ripples = np.sin(x * .55 + y * .31 + 2.2 * np.sin(y * .056) + 2 * grain)
        tool = .037 * np.maximum(ripples, 0) ** 4 * np.clip(.6 + 1.6 * grain, .12, 1)
        h = .57 * grain + fine + tool
        shade = .51 * grain + .28 * fine + .52 * tool
        glossiness = gloss - .10 * grain + .2 * fine
        metallic = .0
        if metal:
            ox = np.clip((rgb[:, :, 1] - rgb[:, :, 0] + .055) * 4.0, 0, 1)
            shade = .66 * grain
            glossiness = glossiness - .17 * ox
            metallic = (.79 if kind == "brass" else .67) - .45 * ox
        elif kind == "cloth":
            # 平纹织法，经、纬浮长交错；布边和燕尾卷边另有几何。
            warp = np.cos(x * np.pi / 3.2 + .19 * np.sin(y * .09))
            weft = np.cos(y * np.pi / 3.9 + .13 * np.sin(x * .07))
            over = np.cos(x * np.pi / 6.4) * np.cos(y * np.pi / 7.8)
            weave = np.maximum(warp * (.68 + .32 * over), weft * (.68 - .32 * over))
            h = .24 * weave + .10 * grain + fine
            shade = .14 * weave + .18 * grain + .5 * fine
            dye.paste(to_image(np.clip(.79 + .085 * weave - .09 * grain, .56, .91)), (left, top))
        elif kind in ("soil", "mortar"):
            coarse = np.asarray(Image.fromarray(np.float32(rng.normal(0, .12, (56, 56))))
                                .resize((size, size), Image.Resampling.BICUBIC))
            grit = rng.normal(0, .085, grain.shape)
            h = .32 * grain + .56 * grit + .4 * coarse
            shade = .34 * grain + .60 * grit + .55 * coarse
        albedo = np.array(tone) / 255 * (1 + shade[:, :, None]) + tint * .30
        dy, dx = np.gradient(h)
        vectors = np.stack((-dx * relief, dy * relief, np.ones_like(h)), axis=-1)
        vectors /= np.linalg.norm(vectors, axis=2, keepdims=True)
        channels = np.zeros((size, size, 4), dtype=np.float32)
        channels[:, :, 0], channels[:, :, 3] = metallic, glossiness
        colour.paste(to_image(albedo), (left, top))
        normal.paste(to_image(vectors * .5 + .5), (left, top))
        packed.paste(to_image(channels), (left, top))
    colour.save(ART / "wall-surface.png")
    normal.save(ART / "wall-normal.png")
    Image.merge("RGBA", [b.resize((1024, 1024), Image.Resampling.BOX) for b in packed.split()]).save(ART / "wall-finish.png")
    dye.resize((1024, 1024), Image.Resampling.BOX).save(COLOURS / "wall-mask.png")


if __name__ == "__main__":
    make_maps()
