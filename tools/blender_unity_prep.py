# Headless Blender: collapse Tripo's animate_retarget FBX (one armature+mesh PER CLIP,
# stacked) into a single Unity-ready FBX: 1 armature + 1 mesh + all clips as takes.
# Usage: Blender --background --python tools/blender_unity_prep.py -- <anim.fbx> <out.fbx>
import re
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
arms.sort(key=lambda o: o.name)
keep = arms[0]
print(f"armatures: {[a.name for a in arms]} -> keeping {keep.name}")

# Rename actions to clean clip names (tripo:preset:biped:idle -> idle) and dedupe.
seen = set()
for act in list(bpy.data.actions):
    m = re.search(r"(idle|run|jump|dive|walk)", act.name, re.I)
    name = m.group(1).lower() if m else act.name
    if name in seen:
        bpy.data.actions.remove(act)
        continue
    seen.add(name)
    act.name = name
    act.use_fake_user = True  # survive object deletion
    # Strip OBJECT-level curves (location/rotation_euler/scale on the Armature node):
    # they bake Blender's axis conversion as animation, which Unity double-applies —
    # twisting/offsetting the whole character every frame. Clips must drive BONES only.
    for fc in [f for f in act.fcurves if not f.data_path.startswith("pose.bones")]:
        act.fcurves.remove(fc)
print(f"actions kept: {sorted(seen)}")

# Delete every armature copy but the first, plus all meshes not skinned to it.
doomed = []
for o in bpy.data.objects:
    if o.type == "ARMATURE" and o is not keep:
        doomed.append(o)
    elif o.type == "MESH" and o.find_armature() is not keep:
        doomed.append(o)
for o in doomed:
    bpy.data.objects.remove(o, do_unlink=True)

kept_meshes = [o for o in bpy.data.objects if o.type == "MESH"]
print(f"meshes kept: {[m.name for m in kept_meshes]}")

# One take per action on export.
if keep.animation_data is None:
    keep.animation_data_create()

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
    path_mode="COPY",
    embed_textures=False,
)
print(f"PREPPED: {dst}")
