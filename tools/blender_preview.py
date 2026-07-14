# Headless preview render: <src.glb/fbx> -> <out_prefix>_front.png / _three4.png
# Usage: Blender --background --python tools/blender_preview.py -- <src> <out_prefix>
import math
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, prefix = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)

if src.lower().endswith((".glb", ".gltf")):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    bpy.ops.import_scene.fbx(filepath=src)

# Bounds of all mesh objects.
meshes = [o for o in bpy.data.objects if o.type == "MESH"]
if not meshes:
    raise SystemExit("no meshes imported")
lo = Vector((1e9, 1e9, 1e9))
hi = Vector((-1e9, -1e9, -1e9))
for o in meshes:
    for corner in o.bound_box:
        w = o.matrix_world @ Vector(corner)
        lo = Vector(map(min, lo, w))
        hi = Vector(map(max, hi, w))
center = (lo + hi) / 2
size = max((hi - lo).length, 1e-3)

# Light.
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", type="SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(-30))
bpy.context.collection.objects.link(sun)

# Camera.
cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
bpy.context.collection.objects.link(cam)
bpy.context.scene.camera = cam

scene = bpy.context.scene
scene.render.resolution_x = 768
scene.render.resolution_y = 1024
# Blender renamed Eevee's engine id in 4.x and changed it back in 5.x. Probe the
# actual enum instead of inferring support from a Python type that still exists.
for engine in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        scene.render.engine = engine
        break
    except TypeError:
        continue
else:
    raise RuntimeError("no supported Eevee render engine found")
scene.render.film_transparent = False
world = bpy.data.worlds.new("W")
world.color = (0.16, 0.15, 0.24)
scene.world = world

def shoot(angle_deg, name):
    a = math.radians(angle_deg)
    dist = size * 1.6
    cam.location = center + Vector((math.sin(a) * dist, -math.cos(a) * dist, size * 0.15))
    direction = center - cam.location
    cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)
    print("RENDERED:", scene.render.filepath)

shoot(0, "front")
shoot(40, "three4")
