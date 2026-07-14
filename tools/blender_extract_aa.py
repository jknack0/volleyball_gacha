# Extract a chosen subset of takes from the AA_Volleyball mocap bank into a lean
# animation-only FBX for Unity (Humanoid retarget source).
# Usage: Blender --background --python tools/blender_extract_aa.py -- <bank.fbx> <out.fbx> <NNN:name> [...]
import re
import sys

import bpy

argv = sys.argv[sys.argv.index("--") + 1:]
src, dst = argv[0], argv[1]
wanted = dict(a.split(":", 1) for a in argv[2:])  # "085": "spike_power"

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)

arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")

# The bank import creates several action variants per take (name-length-truncated
# stubs). Keep, per wanted number, the action with the MOST fcurves (the real one).
best = {}
for act in bpy.data.actions:
    m = re.match(r"(?:Armature\|)?(\d{3})_", act.name)
    if not m or m.group(1) not in wanted:
        continue
    num = m.group(1)
    if num not in best or len(act.fcurves) > len(best[num].fcurves):
        best[num] = act

for num, act in sorted(best.items()):
    act.name = wanted[num]
    act.use_fake_user = True
    print(f"keep {num} -> {act.name} (fcurves={len(act.fcurves)}, frames={int(act.frame_range[1])})")

for act in list(bpy.data.actions):
    if act not in best.values():
        bpy.data.actions.remove(act)

missing = [n for n in wanted if n not in best]
if missing:
    raise SystemExit(f"MISSING takes: {missing}")

if arm.animation_data is None:
    arm.animation_data_create()

bpy.ops.object.select_all(action="SELECT")
bpy.ops.export_scene.fbx(
    filepath=dst,
    use_selection=False,
    add_leaf_bones=False,
    bake_anim=True,
    bake_anim_use_all_actions=True,
    bake_anim_use_nla_strips=False,
    bake_anim_force_startend_keying=True,
)
print(f"EXTRACTED: {dst}")
