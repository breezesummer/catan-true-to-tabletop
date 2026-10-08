"""Build the M1 metre-scale mountain mesh in Blender 4.5 LTS.

Run: blender --background --python tools/art/generate_mountain.py --
     --output-root <clean-root> [--recipe <recipe.json>] [--skip-previews]

Each output root has source/, exports/, and previews/ subdirectories. Refuses
to overwrite any existing version directory. Cameras and crowded-node test
pieces belong to a preview-only collection and are never exported as game art.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import random
import sys

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[2]
parser = argparse.ArgumentParser()
parser.add_argument("--output-root", required=True, type=Path)
parser.add_argument("--recipe", type=Path, default=ROOT / "art/recipes/mountain-tile-v001/recipe.json")
parser.add_argument("--skip-previews", action="store_true")
args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:])
recipe = json.loads(args.recipe.read_text(encoding="utf-8"))
asset_id = recipe["assetId"]
out = args.output_root.resolve()
destinations = {kind: out / kind / asset_id for kind in ("source", "exports", "previews")}
for path in destinations.values():
    if path.exists():
        raise RuntimeError(f"Version output already exists; use a new clean output root: {path}")
for path in destinations.values():
    path.mkdir(parents=True)
rng = random.Random(recipe["seed"])
MM = 0.001
radius = recipe["dimensionsMm"]["edgeLength"] * MM
base_height = recipe["dimensionsMm"]["baseThickness"] * MM
corner_angle = math.radians(recipe["hexCornerAngleDegrees"])
corners = [Vector((radius * math.cos(corner_angle + i * math.tau / 6), radius * math.sin(corner_angle + i * math.tau / 6))) for i in range(6)]

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
for collection in list(bpy.data.collections):
    bpy.data.collections.remove(collection)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1.0
asset_collection = bpy.data.collections.new("MountainTile_v001_ASSET")
stage_collection = bpy.data.collections.new("PREVIEW_ONLY_NOT_EXPORTED")
scene.collection.children.link(asset_collection)
scene.collection.children.link(stage_collection)


def material(name, color, roughness, metallic=0):
    m = bpy.data.materials.new(name)
    m.diffuse_color = (*color, 1)
    m.use_nodes = True
    node = m.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, 1)
    node.inputs["Roughness"].default_value = roughness
    node.inputs["Metallic"].default_value = metallic
    return m


mats = {
    "base": material("M1_Base_Obsidian", (0.037, 0.048, 0.052), 0.37, 0.25),
    "rim": material("M1_Rim_AntiqueBronze", (0.37, 0.205, 0.073), 0.32, 0.72),
    "ground": material("M1_Ground_Slate", (0.19, 0.245, 0.25), 0.82),
    "stone_dark": material("M1_Stone_DeepSlate", (0.12, 0.18, 0.205), 0.83),
    "stone": material("M1_Stone_BlueSlate", (0.29, 0.37, 0.385), 0.75),
    "stone_light": material("M1_Stone_Weathered", (0.48, 0.54, 0.525), 0.79),
    "peak": material("M1_Stone_Quartz", (0.72, 0.75, 0.68), 0.64),
    "moss": material("M1_Ground_Moss", (0.20, 0.27, 0.13), 0.93),
    "ore": material("M1_Ore_Copper", (0.62, 0.25, 0.065), 0.28, 0.62),
    "ore_light": material("M1_Ore_GoldFace", (0.91, 0.53, 0.17), 0.30, 0.56),
    "token": material("M1_Number_Recess", (0.10, 0.145, 0.16), 0.73),
}
asset_objects = []
landscape_objects = []


def mesh_object(name, verts, faces, materials, polygon_materials=None, landscape=False, collection=None):
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    # Deterministic box-projected UVs are available for later authored textures;
    # this material version uses constants and has no image dependencies.
    uv = mesh.uv_layers.new(name="UVMap")
    for polygon in mesh.polygons:
        dominant = max(range(3), key=lambda i: abs(polygon.normal[i]))
        axes = [i for i in range(3) if i != dominant]
        for loop_index in polygon.loop_indices:
            point = mesh.vertices[mesh.loops[loop_index].vertex_index].co
            uv.data[loop_index].uv = (point[axes[0]] / .08 + .5, point[axes[1]] / .08 + .5)
    obj = bpy.data.objects.new(name, mesh)
    (collection or asset_collection).objects.link(obj)
    for mat in materials:
        mesh.materials.append(mat)
    if polygon_materials:
        for polygon, index in zip(mesh.polygons, polygon_materials):
            polygon.material_index = index
    if collection is None:
        asset_objects.append(obj)
    if landscape:
        landscape_objects.append(obj)
    return obj


def radial_mesh(name, rings, count, materials, start_angle=corner_angle, center=(0, 0), landscape=False):
    verts = [(center[0] + r * math.cos(start_angle + i * math.tau / count), center[1] + r * math.sin(start_angle + i * math.tau / count), z) for r, z in rings for i in range(count)]
    faces = [tuple(reversed(range(count)))]
    indices = [0]
    for ring in range(len(rings) - 1):
        for i in range(count):
            faces.append((ring * count + i, ring * count + (i + 1) % count, (ring + 1) * count + (i + 1) % count, (ring + 1) * count + i))
            indices.append(min(ring, len(materials) - 1))
    faces.append(tuple((len(rings) - 1) * count + i for i in range(count)))
    indices.append(len(materials) - 1)
    return mesh_object(name, verts, faces, materials, indices, landscape)


# The extreme contour is exactly 40 mm radius. Bevels are explicit geometry,
# so the exported FBX does not depend on modifier evaluation or smoothing.
radial_mesh("Mountain_Base", [(0.0392, 0), (radius, .0008), (radius, .0069), (.0392, .008)], 6,
            [mats["base"], mats["base"], mats["rim"], mats["ground"]])
radial_mesh("Mountain_Inlay", [(.0397, .0020), (.03975, .0025)], 6, [mats["rim"]])
radial_mesh("Mountain_Surface", [(.0384, .008), (.0378, .00825)], 6, [mats["ground"]])


def mountain(name, x, y, r, height, phase):
    n = 9
    angles = [phase + i * math.tau / n + rng.uniform(-.1, .1) for i in range(n)]
    modulation = [rng.uniform(.85, 1.0) for _ in range(n)]
    verts = []
    for ring, (factor, z_factor) in enumerate(((1, 0), (.75, .22), (.47, .52), (.21, .77))):
        for i, a in enumerate(angles):
            rr = r * factor * modulation[i]
            zz = .0083 + height * z_factor + (rng.uniform(-.055, .045) * height if ring else 0)
            verts.append((x + rr * math.cos(a) + ring * .00025, y + rr * math.sin(a), zz))
    verts.append((x + .0012, y + .0004, .0083 + height))
    faces, material_indices = [tuple(reversed(range(n)))], [0]
    for ring in range(3):
        for i in range(n):
            a, b, c, d = ring * n + i, ring * n + (i + 1) % n, (ring + 1) * n + (i + 1) % n, (ring + 1) * n + i
            faces.extend(((a, b, d), (b, c, d)))
            pool = [0, 1, 1, 2] if ring < 2 else [1, 2, 2, 3]
            material_indices.extend((rng.choice(pool), rng.choice(pool)))
    for i in range(n):
        faces.append((3 * n + i, 3 * n + (i + 1) % n, 4 * n))
        material_indices.append(rng.choice([2, 3, 3]))
    return mesh_object(name, verts, faces, [mats["stone_dark"], mats["stone"], mats["stone_light"], mats["peak"]], material_indices, True)


mountain("Mountain_MainSpire", -.005, .006, .0135, .0252, .16)
mountain("Mountain_EastSpire", .009, .0055, .011, .0175, .44)
mountain("Mountain_WestButtress", -.015, -.0015, .008, .011, .9)
mountain("Mountain_RearButtress", .001, .018, .0075, .010, .55)
mountain("Mountain_FrontBoulder", .014, -.008, .0068, .008, .6)
for index, (x, y, r) in enumerate(((-.017, -.009, .0034), (.020, .002, .0028), (.013, .019, .0029), (-.018, .012, .0028), (.020, -.007, .0022))):
    mountain(f"Mountain_Scree_{index:02}", x, y, r, rng.uniform(.0017, .0036), rng.random())

# Moss areas use thin polygon islands; every landscape vertex is subjected to
# the same edge/vertex exclusion checks as tall geometry.
for index, (x, y, r) in enumerate(((-.019, -.005, .005), (.018, .009, .004), (-.008, .022, .0032), (.015, -.016, .0033))):
    radial_mesh(f"Mountain_Moss_{index:02}", [(r, .00828), (r * .80, .00855)], 9, [mats["moss"]], center=(x, y), landscape=True)


def crystal(index, x, y, radius, height):
    # Faceted ore is mesh geometry, with a broad dark face and glint faces.
    obj = radial_mesh(f"Mountain_Copper_{index:02}", [(radius, .0085), (radius * .85, .0085 + height * .7), (0.00025, .0085 + height)], 5,
                      [mats["ore"], mats["ore"], mats["ore_light"]], start_angle=rng.random(), center=(x, y), landscape=True)
    for p in obj.data.polygons:
        if p.index % 4 == 0:
            p.material_index = 2


for index, (x, y, r, height) in enumerate(((-.012, -.010, .0015, .0058), (-.015, -.012, .0013, .004), (-.010, -.011, .0011, .003), (.018, .000, .0015, .0047), (.020, -.002, .0012, .0031), (.014, .018, .0012, .0035))):
    crystal(index, x, y, r, height)

# Recess is deliberately blank; game code owns resource/number glyphs.
radial_mesh("Mountain_NumberRecess", [(.0080, .00827), (.0075, .00865)], 32, [mats["rim"], mats["token"]], start_angle=0, center=(0, -.016), landscape=True)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def geometry_signature(objects):
    data = []
    for obj in sorted(objects, key=lambda o: o.name):
        data.append({"name": obj.name, "vertices": [[round(float(v), 9) for v in point.co] for point in obj.data.vertices],
                     "faces": [list(p.vertices) for p in obj.data.polygons], "materialIndices": [p.material_index for p in obj.data.polygons],
                     "uv": [[round(float(v), 9) for v in point.uv] for point in obj.data.uv_layers.active.data],
                     "materials": [m.name for m in obj.data.materials]})
    return hashlib.sha256(json.dumps(data, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


# Verify complete geometry, including every point on the preview number recess.
minimum_edge_distance = 1.0
minimum_vertex_distance = 1.0
for obj in landscape_objects:
    for vertex in obj.data.vertices:
        p = Vector(vertex.co[:2])
        for i, a in enumerate(corners):
            b = corners[(i + 1) % 6]
            edge = b - a
            distance = (edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / edge.length
            minimum_edge_distance = min(minimum_edge_distance, distance)
        minimum_vertex_distance = min(minimum_vertex_distance, min((p - corner).length for corner in corners))
all_points = [v.co for obj in asset_objects for v in obj.data.vertices]
bounds_min = [min(v[i] for v in all_points) for i in range(3)]
bounds_max = [max(v[i] for v in all_points) for i in range(3)]
assert minimum_edge_distance >= recipe["landscapeClearanceMm"]["edge"] * MM - 1e-7, minimum_edge_distance
assert minimum_vertex_distance >= recipe["landscapeClearanceMm"]["vertex"] * MM - 1e-7, minimum_vertex_distance
assert abs(bounds_min[2]) < 1e-8
assert bounds_max[2] <= recipe["dimensionsMm"]["maximumTotalHeight"] * MM
assert all(tuple(obj.location) == (0, 0, 0) and tuple(obj.rotation_euler) == (0, 0, 0) and tuple(obj.scale) == (1, 1, 1) for obj in asset_objects)

fbx_path = destinations["exports"] / f"{asset_id}.fbx"
bpy.ops.object.select_all(action="DESELECT")
for obj in asset_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = asset_objects[0]
export_settings = {"use_selection": True, "global_scale": 1.0, "apply_unit_scale": True, "apply_scale_options": "FBX_SCALE_UNITS",
                   "axis_forward": "-Z", "axis_up": "Y", "bake_anim": False, "add_leaf_bones": False, "path_mode": "AUTO",
                   "use_mesh_modifiers": True, "mesh_smooth_type": "FACE", "use_triangles": True}
bpy.ops.export_scene.fbx(filepath=str(fbx_path), object_types={"MESH"}, **export_settings)

# Controlled light rig in metres; the hero frame remains useful for inspecting
# the .blend interactively. Preview pieces are clearly segregated from exports.
floor_mat = material("PREVIEW_Table", (.042, .060, .068), .80)
floor = mesh_object("PREVIEW_Table", [(-2, -2, -.0005), (2, -2, -.0005), (2, 2, -.0005), (-2, 2, -.0005)], [(0, 1, 2, 3)], [floor_mat], collection=stage_collection)
scene.world.color = (.15, .15, .15)
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.20, .27, .32, 1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .35


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()


for name, position, energy, size, color in (("Key", (-.075, -.085, .14), .16, .11, (1, .85, .66)), ("Fill", (.08, -.015, .075), .065, .09, (.67, .82, 1)), ("Rim", (.005, .12, .13), .21, .08, (1, .76, .47))):
    light = bpy.data.lights.new("PREVIEW_" + name, "AREA")
    light.energy, light.shape, light.size, light.color = energy, "DISK", size, color
    obj = bpy.data.objects.new("PREVIEW_" + name, light)
    stage_collection.objects.link(obj)
    obj.location = position
    aim(obj, (0, 0, .008))
camera_data = bpy.data.cameras.new("PREVIEW_Camera")
camera = bpy.data.objects.new("PREVIEW_Camera", camera_data)
stage_collection.objects.link(camera)
scene.camera = camera
camera_data.type = "ORTHO"
camera_data.clip_start = .001
camera_data.clip_end = 10
scene.render.engine = "CYCLES"
scene.cycles.samples = recipe["preview"]["samples"]
scene.cycles.use_denoising = True
scene.render.resolution_x = recipe["preview"]["width"]
scene.render.resolution_y = recipe["preview"]["height"]
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.render.film_transparent = False
scene.view_settings.view_transform = "AgX"
scene.render.image_settings.color_mode = "RGBA"

preview_pieces = []
red = material("PREVIEW_Player_Rust", (.72, .07, .028), .35)
blue = material("PREVIEW_Player_Blue", (.04, .25, .55), .37)


def preview_box(name, location, size, mat, angle=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    for c in list(obj.users_collection):
        c.objects.unlink(obj)
    stage_collection.objects.link(obj)
    obj.dimensions = size
    obj.rotation_euler.z = angle
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    bevel = obj.modifiers.new("Preview piece edge softness", "BEVEL")
    bevel.width, bevel.segments = .0005, 2
    obj.hide_render = True
    preview_pieces.append(obj)
    return obj


# Three pieces demonstrate crowded-node space; this is an art clearance view,
# not a legal game position or interaction acceptance record.
cx, cy = corners[4]
preview_box("PREVIEW_SettlementBody_12x12", (cx, cy, .013), (.012, .012, .010), red)
roof = preview_box("PREVIEW_SettlementRoof", (cx, cy, .020), (.0114, .0114, .0057), red)
roof.rotation_euler.x = math.radians(45)
for i, (a, b) in enumerate(((corners[4], corners[3]), (corners[4], corners[5]))):
    center = (a + b) / 2
    d = b - a
    preview_box(f"PREVIEW_Road_{i}", (center.x, center.y, .0105), (.026, .005, .005), blue if i else red, math.atan2(d.y, d.x))

views = {
    "top": {"position": (0, 0, .18), "target": (0, 0, 0), "ortho": .125},
    "side": {"position": (.13, -.12, .037), "target": (0, 0, .014), "ortho": .102},
    "game-camera": {"position": (.09, -.13, .115), "target": (0, 0, .008), "ortho": .116},
    "crowded-vertex": {"position": (.078, -.125, .085), "target": (0, -.014, .010), "ortho": .104},
}
for name, config in views.items():
    camera.location = config["position"]
    camera_data.ortho_scale = config["ortho"]
    aim(camera, config["target"])
    for obj in preview_pieces:
        obj.hide_render = name != "crowded-vertex"
    scene.render.filepath = str(destinations["previews"] / f"{name}.png")
    if not args.skip_previews:
        bpy.ops.render.render(write_still=True)
for obj in preview_pieces:
    obj.hide_render = True
    obj.hide_set(True)
camera.location = views["game-camera"]["position"]
camera_data.ortho_scale = views["game-camera"]["ortho"]
aim(camera, views["game-camera"]["target"])
blend_path = destinations["source"] / f"{asset_id}.blend"
bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))

triangles = sum(sum(len(p.vertices) - 2 for p in obj.data.polygons) for obj in asset_objects)
material_table = []
for m in mats.values():
    node = m.node_tree.nodes.get("Principled BSDF")
    material_table.append({"name": m.name, "baseColorLinearRgba": list(m.diffuse_color), "roughness": node.inputs["Roughness"].default_value, "metallic": node.inputs["Metallic"].default_value})
manifest = {
    "schemaVersion": 1, "assetId": asset_id, "materialVersion": recipe["materialVersion"],
    "provenance": recipe["provenance"], "referenceIds": [], "measuredDimensionsMm": None,
    "tool": {"blender": bpy.app.version_string, "script": "tools/art/generate_mountain.py", "scriptSha256": sha(Path(__file__)), "recipe": str(args.recipe.relative_to(ROOT)) if args.recipe.is_relative_to(ROOT) else args.recipe.name, "recipeSha256": sha(args.recipe), "seed": recipe["seed"]},
    "units": "metres", "pivot": "bottom-centre: every exported object origin is (0,0,0)",
    "blenderBounds": {"min": bounds_min, "max": bounds_max},
    "expectedUnityBounds": {"min": [bounds_min[0], bounds_min[2], -bounds_max[1]], "max": [bounds_max[0], bounds_max[2], -bounds_min[1]]},
    "hexCornerAngleDegreesBlender": recipe["hexCornerAngleDegrees"], "piecePlacementYUnityMetres": base_height,
    "numberAnchorUnityMetres": [0, .009, .016],
    "geometry": {"meshObjects": len(asset_objects), "vertices": len(all_points), "triangles": triangles, "canonicalSha256": geometry_signature(asset_objects), "landscapeMinEdgeDistanceMm": minimum_edge_distance / MM, "landscapeMinVertexDistanceMm": minimum_vertex_distance / MM},
    "checks": {"bottomPivot": True, "landscapeEdgeClearance": True, "landscapeVertexClearance": True, "heightWithin36mm": True},
    "export": export_settings, "materials": material_table, "textureDependencies": [],
    "collision": "No collider is supplied. Unity interaction uses stable Tile/Vertex/Edge targets independently of the render mesh.",
    "previews": {"generated": not args.skip_previews, "resolution": [scene.render.resolution_x, scene.render.resolution_y], "views": views, "crowdedViewIsGameState": False},
    "files": {"fbx": {"path": f"exports/{asset_id}/{fbx_path.name}", "sha256": sha(fbx_path)}, "blend": {"path": f"source/{asset_id}/{blend_path.name}", "sha256": sha(blend_path)}},
}
manifest_path = destinations["exports"] / "manifest.json"
manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print("CATAN_M1_MOUNTAIN_OK " + json.dumps(manifest["geometry"]))
