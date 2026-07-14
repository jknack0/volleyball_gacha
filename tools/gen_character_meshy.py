#!/usr/bin/env python3
"""Meshy character factory (A/B counterpart to gen_character.py's Tripo route).

sheet PNG -> image-to-3d (t-pose, textured) -> rigging (+free walk/run) ->
animation presets (idle/jump/dive) -> FBX downloads into Assets/Art/Characters/<id>/

Usage: export MESHY_API_KEY=msy_...
       python tools/gen_character_meshy.py char.mc_meshy path/to/sheet.png
"""
import base64
import csv
import datetime
import json
import os
import sys
import time
import urllib.request

API = "https://api.meshy.ai/openapi/v1"
KEY = os.environ["MESHY_API_KEY"]
HDR = {"Authorization": f"Bearer {KEY}", "Content-Type": "application/json"}

# Volleyball-analog picks from the 680-entry catalog [tunable].
ACTIONS = {"idle": 0, "jump": 382, "dive": 506}  # + walk/run ship free with the rig


def call(method, path, payload=None):
    req = urllib.request.Request(
        f"{API}{path}",
        data=json.dumps(payload).encode() if payload is not None else None,
        headers=HDR,
        method=method,
    )
    with urllib.request.urlopen(req, timeout=120) as r:
        return json.load(r)


def poll(kind, task_id, label):
    while True:
        t = call("GET", f"/{kind}/{task_id}")
        status, progress = t.get("status"), t.get("progress")
        print(f"  {label}: {status} {progress}%", flush=True)
        if status == "SUCCEEDED":
            print(f"  {label}: consumed {t.get('consumed_credits')} credits", flush=True)
            return t
        if status in ("FAILED", "CANCELED"):
            raise SystemExit(f"{label} failed: {t.get('task_error')}")
        time.sleep(10)


def download(url, dst):
    urllib.request.urlretrieve(url, dst)
    print(f"  saved {dst} ({os.path.getsize(dst)//1024} KB)", flush=True)


def main():
    char_id, sheet = sys.argv[1], sys.argv[2]
    out_dir = f"Assets/Art/Characters/{char_id}"
    os.makedirs(out_dir, exist_ok=True)
    provenance = []

    b64 = base64.b64encode(open(sheet, "rb").read()).decode()
    data_uri = f"data:image/png;base64,{b64}"

    print("1/3 image-to-3d ...", flush=True)
    mesh_id = call("POST", "/image-to-3d", {
        "image_url": data_uri,
        "ai_model": "latest",
        "should_texture": True,
        "should_remesh": True,
        "topology": "triangle",
        "target_polycount": 15000,   # mobile budget [tunable]
        "pose_mode": "t-pose",
        "remove_lighting": True,     # flat colors for the toon shader
        "target_formats": ["glb", "fbx"],
    })["result"]
    mesh = poll("image-to-3d", mesh_id, "mesh")
    download(mesh["model_urls"]["fbx"], f"{out_dir}/meshy_model.fbx")
    tex = (mesh.get("texture_urls") or [{}])[0].get("base_color")
    if tex:
        download(tex, f"{out_dir}/Color.png")
    provenance.append(("meshy_model.fbx", "meshy image-to-3d meshy-6", mesh_id, mesh.get("consumed_credits")))

    print("2/3 rigging ...", flush=True)
    rig_id = call("POST", "/rigging", {
        "input_task_id": mesh_id,
        "height_meters": 1.6,
    })["result"]
    rig = poll("rigging", rig_id, "rig")
    res = rig["result"]
    download(res["rigged_character_fbx_url"], f"{out_dir}/meshy_rigged.fbx")
    basic = res.get("basic_animations") or {}
    for name in ("walking", "running"):
        url = basic.get(f"{name}_fbx_url")
        if url:
            download(url, f"{out_dir}/meshy_anim_{name}.fbx")
    provenance.append(("meshy_rigged.fbx", "meshy rigging", rig_id, rig.get("consumed_credits")))

    print("3/3 animations ...", flush=True)
    anim_tasks = {}
    for name, action in ACTIONS.items():
        anim_tasks[name] = call("POST", "/animations", {
            "rig_task_id": rig_id, "action_id": action,
        })["result"]
    for name, tid in anim_tasks.items():
        t = poll("animations", tid, f"anim:{name}")
        download(t["result"]["animation_fbx_url"], f"{out_dir}/meshy_anim_{name}.fbx")
        provenance.append((f"meshy_anim_{name}.fbx", f"meshy animation action_id={ACTIONS[name]}", tid, t.get("consumed_credits")))

    with open("docs/art-provenance.csv", "a", newline="") as f:
        w = csv.writer(f)
        today = datetime.date.today().isoformat()
        for fname, tool, tid, credits in provenance:
            w.writerow([f"{char_id}/{fname}", tool, today, tid, f"{credits} credits", "paid-tier commercial"])

    print("\nDONE. Next: blender_unity_prep.py merge -> unity_model.fbx -> VG menu build.", flush=True)


if __name__ == "__main__":
    main()
