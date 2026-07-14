# Headless Blender converter: BVH/GLB -> FBX for Unity import.
# Usage: Blender --background --python tools/blender_convert.py -- <src> <dst.fbx> [target_tris]
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
target_tris = int(argv[2]) if len(argv) > 2 else 0

bpy.ops.wm.read_factory_settings(use_empty=True)

if src.lower().endswith(".bvh"):
    bpy.ops.import_anim.bvh(
        filepath=src,
        rotate_mode="NATIVE",
        axis_forward="-Z",
        axis_up="Y",
        update_scene_fps=True,
        update_scene_duration=True,
    )
elif src.lower().endswith((".glb", ".gltf")):
    bpy.ops.import_scene.gltf(filepath=src)
else:
    raise SystemExit(f"unsupported source: {src}")

# Optional decimation to a mobile tri budget (pipeline §3.2: 8-15k).
if target_tris > 0:
    for obj in [o for o in bpy.data.objects if o.type == "MESH"]:
        current = sum(len(p.vertices) - 2 for p in obj.data.polygons)
        if current > target_tris:
            mod = obj.modifiers.new("Decimate", "DECIMATE")
            mod.ratio = target_tris / current
            mod.use_collapse_triangulate = True
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.modifier_apply(modifier=mod.name)
            after = sum(len(p.vertices) - 2 for p in obj.data.polygons)
            print(f"DECIMATED {obj.name}: {current} -> {after} tris")

bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    add_leaf_bones=False,
    bake_anim=True,
    path_mode="COPY",
    embed_textures=True,
)
print("CONVERTED:", src, "->", dst)
