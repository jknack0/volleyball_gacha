# Headless Blender converter: BVH/GLB -> FBX for Unity import.
# Usage: Blender --background --python tools/blender_convert.py -- <src> <dst.fbx>
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

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

bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    add_leaf_bones=False,
    bake_anim=True,
    path_mode="COPY",
    embed_textures=True,
)
print("CONVERTED:", src, "->", dst)
