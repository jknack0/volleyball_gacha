# Headless Blender: build a single Unity-ready FBX (1 armature + 1 mesh + named takes)
# from generator output, which arrives in awkward shapes:
#   - Tripo: one FBX whose takes need renaming
#   - Meshy: a rigged base FBX + one FBX PER animation
#
# Usage:
#   Blender --background --python tools/blender_unity_prep.py -- <base.fbx> <out.fbx> [name=clip.fbx ...]
#
# Without clip args: rename/dedupe the base's own actions (Tripo shape).
# With clip args: steal each clip FBX's action, rename it, discard its objects (Meshy shape).
#
# NOTE: node-level (object) curves baked by the exporter are EXPECTED in the output —
# Unity-side clip sanitization (CharacterImporter.SanitizeClip) strips them. Do not
# try to strip them here: the FBX exporter re-bakes node curves unconditionally.
import re
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
clip_args = [a.split("=", 1) for a in argv[2:]]

CLIP_KEYS = r"(idle|run|walk|jump|dive)"


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before]


bpy.ops.wm.read_factory_settings(use_empty=True)
import_fbx(src)

arms = sorted((o for o in bpy.data.objects if o.type == "ARMATURE"), key=lambda o: o.name)
keep = arms[0]
base_actions = set(bpy.data.actions)
print(f"base: armature={keep.name} actions={[a.name for a in base_actions]}")

kept_actions = {}

if clip_args:
    # Meshy shape: base carries mesh+rig; each clip FBX donates one action.
    for name, path in clip_args:
        pre_actions = set(bpy.data.actions)
        new_objs = import_fbx(path)
        donated = [a for a in bpy.data.actions if a not in pre_actions]
        if donated:
            act = donated[0]
            act.name = name
            act.use_fake_user = True
            kept_actions[name] = act
            print(f"clip {name}: <- {path.split('/')[-1]}")
        for o in new_objs:
            bpy.data.objects.remove(o, do_unlink=True)
    # Base's own actions (bind/rest junk) are not wanted as takes.
    for act in list(base_actions):
        if act not in kept_actions.values():
            bpy.data.actions.remove(act)
else:
    # Tripo shape: rename the base's own takes; dedupe.
    for act in list(bpy.data.actions):
        m = re.search(CLIP_KEYS, act.name, re.I)
        name = m.group(1).lower() if m else act.name
        if name in kept_actions:
            bpy.data.actions.remove(act)
            continue
        act.name = name
        act.use_fake_user = True
        kept_actions[name] = act

print(f"actions kept: {sorted(kept_actions)}")

# Drop every armature copy but the first, and meshes not skinned to it.
for o in list(bpy.data.objects):
    if o.type == "ARMATURE" and o is not keep:
        bpy.data.objects.remove(o, do_unlink=True)
    elif o.type == "MESH" and o.find_armature() is not keep:
        bpy.data.objects.remove(o, do_unlink=True)

print(f"meshes kept: {[m.name for m in bpy.data.objects if m.type == 'MESH']}")

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
