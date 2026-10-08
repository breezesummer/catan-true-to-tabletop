"""Independently inspect the exported FBX in a fresh Blender process."""
import argparse
from collections import Counter
import hashlib
import json
import math
from pathlib import Path
import struct
import sys

import bpy
from mathutils import Vector

parser = argparse.ArgumentParser()
parser.add_argument("--output-root", required=True, type=Path)
parser.add_argument("--compare-root", type=Path)
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
root = args.output_root.resolve()
directory = root / "exports/mountain-tile-v001"
manifest = json.loads((directory / "manifest.json").read_text(encoding="utf-8"))
fbx_path = directory / "mountain-tile-v001.fbx"
assert hashlib.sha256(fbx_path.read_bytes()).hexdigest() == manifest["files"]["fbx"]["sha256"]
blend_path = root / manifest["files"]["blend"]["path"]
assert hashlib.sha256(blend_path.read_bytes()).hexdigest() == manifest["files"]["blend"]["sha256"]
preview_views = []
if manifest["previews"]["generated"]:
    for name in ("top", "side", "game-camera", "crowded-vertex"):
        data = (root / "previews/mountain-tile-v001" / f"{name}.png").read_bytes()
        assert data[:8] == b"\x89PNG\r\n\x1a\n"
        assert list(struct.unpack(">II", data[16:24])) == manifest["previews"]["resolution"]
        assert len(data) > 1000
        preview_views.append(name)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(fbx_path))
objects = list(bpy.context.scene.objects)
assert objects and all(o.type == "MESH" and o.name.startswith("Mountain_") for o in objects)
assert len(objects) == manifest["geometry"]["meshObjects"]
points = []
triangle_count = 0
minimum_edge_clearance = math.inf
minimum_vertex_clearance = math.inf
corners = [Vector((.04 * math.cos(math.radians(30 + i * 60)), .04 * math.sin(math.radians(30 + i * 60)))) for i in range(6)]
surface_names = {"Mountain_Base", "Mountain_Inlay", "Mountain_Surface"}
uv_objects = 0
for obj in objects:
    assert obj.matrix_world.translation.length < 1e-6, (obj.name, obj.matrix_world.translation)
    assert obj.data.uv_layers.active is not None, obj.name
    uv_objects += 1
    edges = Counter()
    for polygon in obj.data.polygons:
        assert len(polygon.vertices) == 3
        assert polygon.area > 1e-12, (obj.name, polygon.index, polygon.area)
        triangle_count += 1
        for i, vertex in enumerate(polygon.vertices):
            edges[tuple(sorted((vertex, polygon.vertices[(i + 1) % 3])))] += 1
    assert all(count == 2 for count in edges.values()), f"Non-manifold mesh: {obj.name}"
    for vertex in obj.data.vertices:
        point = obj.matrix_world @ vertex.co
        points.append(point)
        if obj.name in surface_names:
            continue
        p = Vector((point.x, point.y))
        for i, a in enumerate(corners):
            b = corners[(i + 1) % 6]
            edge = b - a
            inward = Vector((-edge.y, edge.x)).normalized()
            minimum_edge_clearance = min(minimum_edge_clearance, (p - a).dot(inward))
        minimum_vertex_clearance = min(minimum_vertex_clearance, min((p - c).length for c in corners))
bounds_min = [min(p[i] for p in points) for i in range(3)]
bounds_max = [max(p[i] for p in points) for i in range(3)]
dimensions = [bounds_max[i] - bounds_min[i] for i in range(3)]
assert abs(dimensions[0] - math.sqrt(3) * .04) < 1e-6, dimensions
assert abs(dimensions[1] - .08) < 1e-6, dimensions
assert abs(bounds_min[2]) < 1e-6 and 0.028 < bounds_max[2] <= .036 + 1e-6
assert minimum_edge_clearance >= .006 - 1e-6
assert minimum_vertex_clearance >= .009 - 1e-6
assert triangle_count == manifest["geometry"]["triangles"]
material_names = sorted({m.name for o in objects for m in o.data.materials})
assert material_names == sorted(m["name"] for m in manifest["materials"])
assert not any(node.type == "TEX_IMAGE" and node.image for mat in bpy.data.materials if mat.use_nodes for node in mat.node_tree.nodes)
comparison = None
if args.compare_root:
    reference = json.loads((args.compare_root.resolve() / "exports/mountain-tile-v001/manifest.json").read_text(encoding="utf-8"))
    # Container timestamps may differ; the actual geometry, material values,
    # exporter settings, seed, inputs, and cameras must be identical.
    keys = ["geometry", "materials", "export", "tool", "blenderBounds", "expectedUnityBounds"]
    for key in keys:
        assert reference[key] == manifest[key], f"Rebuild mismatch in {key}"
    for key in ("resolution", "views"):
        assert reference["previews"][key] == manifest["previews"][key], f"Rebuild mismatch in preview {key}"
    comparison = {"referenceRoot": str(args.compare_root), "comparedFields": keys + ["previews.resolution", "previews.views"], "identical": True}
report = {"assetId": manifest["assetId"], "blenderVersion": bpy.app.version_string,
          "status": "passed", "fbxRoundTrip": True, "meshObjects": len(objects), "triangles": triangle_count,
          "closedMeshes": True, "uvMeshes": uv_objects, "bottomCentrePivots": True,
          "blendHashVerified": True, "previewViewsVerified": preview_views,
          "dimensionsBlenderMetres": dimensions, "minEdgeClearanceMm": minimum_edge_clearance * 1000,
          "minVertexClearanceMm": minimum_vertex_clearance * 1000, "materials": material_names,
          "missingTextures": [], "cleanRebuildComparison": comparison,
          "notCovered": ["Unity import and material remapping", "Unity pointer picking and camera occlusion", "User visual acceptance"]}
(directory / "verification.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("CATAN_M1_MOUNTAIN_EXPORT_VERIFIED " + json.dumps(report))
