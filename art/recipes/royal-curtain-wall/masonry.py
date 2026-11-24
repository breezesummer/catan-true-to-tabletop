"""以毫米施工。每块石料闭合成形，卧缝、断面、倒棱与环形拱石保留在母版中。"""
from math import ceil, cos, pi, sin
from pathlib import Path
import random
import sys

from mathutils import Vector, geometry
from wall_maps import REGIONS

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "gothic-royal-city"))
from stonework import Mason


class Stonework(Mason):
    def __init__(self):
        # 王城石作工具以 slate 标记第二个材质槽，本件把这个槽交给门旗。
        super().__init__({**REGIONS, "slate": REGIONS["cloth"]})
        self.rng = random.Random(211722)
        self.offset = (.17, .23)

    def cut(self):
        self.offset = (self.rng.random(), self.rng.random())

    def face(self, points, swatch, coords=None, outward=None):
        points = list(map(Vector, points))
        if coords is None:
            n = geometry.normal(points)
            if abs(n.z) > .72:
                planar = [(p.x, p.y) for p in points]
            else:
                t = Vector((-n.y, n.x, 0)).normalized()
                planar = [(p.dot(t), p.z) for p in points]
            period = {"soil": 13.0, "mortar": 4.6, "cloth": 2.7, "trim": 3.8}.get(swatch, 5.4)
            u, v = self.offset
            coords = [(x / period + u, y / period + v) for x, y in planar]
        super().face(points, "slate" if swatch == "cloth" else swatch, coords, outward)

    def solid(self, rings, stone):
        """横截面沿高度或进深推进。调用者按外法线给出首个截面的绕序。"""
        self.face(rings[0][::-1], stone)
        for low, high in zip(rings, rings[1:]):
            for i, a in enumerate(low):
                j = (i + 1) % len(low)
                self.face([a, low[j], high[j], high[i]], stone)
        self.face(rings[-1], stone)

    def block(self, x, y, z0, z1, length, depth, stone, angle=0, edge=.044):
        """石块四角各自崩去极小一片；顶底细倒角在斜光中显出棱线。"""
        self.cut()
        half, side = length / 2, depth / 2
        edge = min(edge, half * .23, side * .23, (z1 - z0) * .23)
        corners = [edge * self.rng.uniform(.85, 1.95) for _ in range(4)]
        ca, sa = cos(angle), sin(angle)

        def ring(z, inset):
            a, b = half - inset, side - inset
            sw, se, ne, nw = corners
            outline = [(-a + sw, -b), (a-se, -b), (a, -b+se), (a, b-ne),
                       (a-ne, b), (-a+nw, b), (-a, b-nw), (-a, -b+sw)]
            return [(x+u*ca-v*sa, y+u*sa+v*ca, z) for u, v in outline]
        self.solid([ring(z0, edge*.72), ring(z0+edge, 0),
                    ring(z1-edge, 0), ring(z1, edge)], stone)

    def wedge(self, x, y, a, b, r0, r1, z0, z1, stone, bevel=.033):
        """带弧面和径向接缝的扇形料，用于角塔筒身、垛与走道铺装。"""
        self.cut()
        slices = max(2, ceil((b-a) / (pi/15)))
        def ring(z, inset):
            lo, hi = a+inset/r1, b-inset/r1
            angles = [lo + (hi-lo)*i/slices for i in range(slices+1)]
            return [(x+(r1-inset)*cos(v), y+(r1-inset)*sin(v), z) for v in angles] + [
                (x+(r0+inset)*cos(v), y+(r0+inset)*sin(v), z) for v in reversed(angles)]
        self.solid([ring(z0, bevel*.72), ring(z0+bevel, 0), ring(z1-bevel, 0), ring(z1, bevel)], stone)

    def courses(self, center, angle, start, end, bottom, top, rows, width, depth, stone="bed"):
        """交错砌筑，在端头收半砖；水平卧缝连续，逐块改变石面和榫接长度。"""
        ca, sa = cos(angle), sin(angle)
        count = max(1, round((end-start)/width))
        pitch = (end-start) / count
        for row in range(rows):
            shift = .5 if row % 2 else 0
            cuts = [start] + [start + (n+shift)*pitch + self.rng.uniform(-.09, .09)*pitch
                              for n in range(count+1) if 0 < n+shift < count] + [end]
            z0, z1 = bottom+(top-bottom)*row/rows, bottom+(top-bottom)*(row+1)/rows
            for left, right in zip(cuts, cuts[1:]):
                u = (left+right)/2
                swatch = stone
                if stone == "bed" and self.rng.random() < .22:
                    swatch = "aged"
                self.block(center[0]+u*ca, center[1]+u*sa, z0+.010, z1-.010,
                           right-left-.025, depth+self.rng.uniform(-.034,.034),
                           swatch, angle, min(.045, (z1-z0)*.10))
