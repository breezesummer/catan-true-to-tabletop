"""构件在建筑坐标内成形，逐件定木纹纵向和取样位置，统一导出米制网格。"""
from collections import defaultdict
from math import cos, sin, tau
import random

import bmesh
import bpy
from mathutils import Vector

from surfaces import LAYOUT


X, Y, Z = (Vector(a) for a in ((1, 0, 0), (0, 1, 0), (0, 0, 1)))
WORLD = (X, Y, Z)


class Parts:
    def __init__(self):
        self.rng = random.Random(211719)
        self.groups = defaultdict(lambda: ([], [], [], []))
        self.group = "主屋"

    def solid(self, points, faces, swatch, bevel=0, axes=WORLD, *, fired_faces=(), smooth_faces=()):
        bm = bmesh.new()
        verts = [bm.verts.new(p) for p in points]
        exposed_clay = set()
        for i, face in enumerate(faces):
            f = bm.faces.new([verts[j] for j in face])
            f.smooth = i in smooth_faces
            if i in fired_faces:
                exposed_clay.add(f)
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        if bevel:
            bm.normal_update()
            edges = [e for e in bm.edges if e.calc_face_angle(0) > .55]
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, segments=1,
                            affect='EDGES', clamp_overlap=True)
        bm.normal_update()
        bm.verts.index_update()
        positions, polygons, uv, slots = self.groups[self.group]
        start = len(positions)
        verts = sorted(bm.verts, key=lambda v: v.index)
        positions.extend(tuple(v.co) for v in verts)
        local = [tuple(v.co.dot(axis) for axis in axes) for v in verts]
        low = [min(p[k] for p in local) for k in range(3)]
        spans = [max(p[k] for p in local)-low[k] for k in range(3)]
        density = .78 if swatch in ("oak", "door", "trim") else 1.14
        scales = [min(density, .90/max(.001, s)) for s in spans]
        offsets = [self.rng.uniform(.025, .975-s*rate) for s, rate in zip(spans, scales)]
        for f in bm.faces:
            if f.calc_area() < 1e-11:
                continue
            ids = [v.index for v in f.verts]
            side = max(range(3), key=lambda k: abs(f.normal.dot(axes[k])))
            a, b = [k for k in range(3) if k != side]
            region = "endgrain" if side == 0 and swatch in ("oak", "door", "trim") else swatch
            if f in exposed_clay:
                region = "tileBody"
            cell = LAYOUT.index({"lead":"iron", "tileBody":"tile3"}.get(region, region))
            def coord(index, k):
                return offsets[k] + (local[index][k]-low[k])*scales[k]
            def sample(index):
                u, v = coord(index,a), coord(index,b)
                if region == "lead":
                    v = .64 + .33*v
                elif region == "iron":
                    v *= .56
                if region == "tileBody":
                    u = .81 + .16*u
                elif region == "tile3":
                    u *= .75
                return (cell%4+u)*.25, (3-cell//4+v)*.25
            polygons.append(tuple(start+j for j in ids))
            uv.append([sample(j) for j in ids])
            slots.append((int(region.startswith("tile")), f.smooth))
        bm.free()

    def prism(self, boundary, extrusion, material, bevel=0, axes=WORLD):
        ring = [Vector(p) for p in boundary]
        n = len(ring)
        pts = ring + [p+Vector(extrusion) for p in ring]
        faces = [tuple(range(n)), tuple(reversed(range(n, n*2)))]
        faces.extend((i, (i+1)%n, (i+1)%n+n, i+n) for i in range(n))
        self.solid(pts, faces, material, bevel, axes)

    def box(self, center, dims, material, bevel=.012, axes=WORLD):
        a, b, c = [axis*(span*.5) for axis, span in zip(axes, dims)]
        p = Vector(center)-c
        self.prism([p-a-b, p+a-b, p+a+b, p-a+b], 2*c, material, bevel, axes)

    def beam(self, start, end, width, depth, material="oak", side=Y, bevel=.012):
        p, q = Vector(start), Vector(end)
        a = (q-p).normalized()
        if abs(a.dot(side)) > .99:
            side = X if abs(a.dot(X)) < .9 else Y
        b = (side-a*a.dot(side)).normalized()
        c = a.cross(b)
        self.box((p+q)*.5, ((q-p).length, depth, width), material, bevel, (a, b, c))

    def turning(self, center, axis, profile, material, sides=12):
        # 截面按轴向高度、半径排列。端面与侧面共用轮廓点。
        a = Vector(axis).normalized()
        b = (Z if abs(a.z) < .9 else X).cross(a).normalized()
        c = a.cross(b)
        p = Vector(center)
        pts = [p+a*d+r*(b*cos(tau*i/sides)+c*sin(tau*i/sides))
               for d, r in profile for i in range(sides)]
        faces = [tuple(reversed(range(sides))), tuple(range(len(pts)-sides, len(pts)))]
        for j in range(len(profile)-1):
            for i in range(sides):
                k = j*sides+i
                nxt = j*sides+(i+1)%sides
                faces.append((k, nxt, nxt+sides, k+sides))
        self.solid(pts, faces, material, axes=(a, b, c))

    def objects(self, scene, materials, width):
        points = [p for positions, _, _, _ in self.groups.values() for p in positions]
        low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        origin = Vector(((low.x+high.x)/2, (low.y+high.y)/2, low.z))
        scale = width/max(high.x-low.x, high.y-low.y)
        objects = []
        for name, (positions, faces, coords, slots) in self.groups.items():
            mesh = bpy.data.meshes.new(name)
            mesh.from_pydata([(Vector(p)-origin)*scale for p in positions], [], faces)
            for mat in materials:
                mesh.materials.append(mat)
            mesh.update()
            layer = mesh.uv_layers.new(name="构件定向 UV")
            for face, points_uv, (slot, smooth) in zip(mesh.polygons, coords, slots):
                face.material_index, face.use_smooth = slot, smooth
                for loop, point in zip(face.loop_indices, points_uv):
                    layer.data[loop].uv = point
            obj = bpy.data.objects.new(name, mesh)
            scene.collection.objects.link(obj)
            objects.append(obj)
        return objects, origin, scale
