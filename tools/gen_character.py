#!/usr/bin/env python3
"""Tripo character factory (docs/ai-character-pipeline.md, production route).

sheet PNG -> textured quad mesh (FBX) -> mixamo-spec rig -> retargeted preset clips.

Usage:
    export TRIPO_API_KEY=tsk_...
    ~/.venvs/vg/bin/python tools/gen_character.py char.mc \
        [--sheet Assets/Art/CharacterSheets/char.mc/sheet_tpose_v2.png] \
        [--skip-anim]

Costs credits per stage (texture ~20-30, quad +5, rig, retarget) — see docs.tripo3d.ai pricing.
Every accepted asset gets a provenance row (docs/art-provenance.csv).
"""
import argparse
import csv
import datetime
import os
import pathlib
import sys
import time

import requests

API = "https://api.tripo3d.ai/v2/openapi"
KEY = os.environ.get("TRIPO_API_KEY")

# Volleyball-relevant presets available in animate_retarget (max 5 per task).
CLIPS = ["preset:idle", "preset:run", "preset:jump", "preset:dive"]


def die(msg):
    print(f"ERROR: {msg}", file=sys.stderr)
    sys.exit(1)


def auth():
    return {"Authorization": f"Bearer {KEY}"}


def upload(path):
    with open(path, "rb") as f:
        r = requests.post(f"{API}/upload", headers=auth(), files={"file": f}, timeout=120)
    r.raise_for_status()
    data = r.json()["data"]
    token = data.get("image_token") or data.get("file_token") or data.get("token")
    if not token:
        die(f"upload gave no token: {r.json()}")
    print(f"uploaded {path} -> {token}")
    return token


def submit(payload):
    r = requests.post(f"{API}/task", headers={**auth(), "Content-Type": "application/json"}, json=payload, timeout=60)
    if r.status_code != 200:
        die(f"task submit {r.status_code}: {r.text[:500]}")
    task_id = r.json()["data"]["task_id"]
    print(f"task {payload['type']} -> {task_id}")
    return task_id


def poll(task_id, every=4, timeout_s=1800):
    start = time.time()
    while True:
        r = requests.get(f"{API}/task/{task_id}", headers=auth(), timeout=60)
        r.raise_for_status()
        data = r.json()["data"]
        status = data.get("status")
        if status == "success":
            return data
        if status in ("failed", "cancelled", "banned", "expired"):
            die(f"task {task_id} ended {status}: {data}")
        if time.time() - start > timeout_s:
            die(f"task {task_id} timed out in status {status}")
        print(f"  {task_id[:8]} {status} …", flush=True)
        time.sleep(every)


def download(url, dst):
    dst.parent.mkdir(parents=True, exist_ok=True)
    with requests.get(url, stream=True, timeout=300) as r:
        r.raise_for_status()
        with open(dst, "wb") as f:
            for chunk in r.iter_content(1 << 16):
                f.write(chunk)
    print(f"saved {dst} ({dst.stat().st_size // 1024} KB)")


def save_outputs(data, outdir, stem):
    """Persist every URL found in data.output; return saved paths."""
    saved = []
    output = data.get("output") or {}
    for key, val in output.items():
        if isinstance(val, str) and val.startswith("http"):
            ext = pathlib.Path(val.split("?")[0]).suffix or ".bin"
            dst = outdir / f"{stem}_{key}{ext}"
            download(val, dst)
            saved.append(dst)
    if not saved:
        print(f"note: no downloadable urls in output: {list(output.keys())}")
    return saved


def provenance(char_id, kind, detail):
    row = [char_id, kind, "tripo-api", datetime.date.today().isoformat(), detail, "paid-tier", "0"]
    path = pathlib.Path("docs/art-provenance.csv")
    new = not path.exists()
    with open(path, "a", newline="") as f:
        w = csv.writer(f)
        if new:
            w.writerow(["asset_id", "kind", "tool", "date", "source_ref", "license_tier", "cleanup_hours"])
        w.writerow(row)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("char_id")
    ap.add_argument("--sheet", default=None)
    ap.add_argument("--model-version", default="v3.0-20250812")
    ap.add_argument("--face-limit", type=int, default=12000)
    ap.add_argument("--skip-anim", action="store_true")
    args = ap.parse_args()

    if not KEY:
        die("TRIPO_API_KEY is not set. Create one at platform.tripo3d.ai, then: export TRIPO_API_KEY=...")

    sheet = args.sheet or f"Assets/Art/CharacterSheets/{args.char_id}/sheet_tpose_v2.png"
    if not pathlib.Path(sheet).exists():
        die(f"sheet not found: {sheet}")
    outdir = pathlib.Path(f"Assets/Art/Characters/{args.char_id}")

    # 1. Mesh: textured, NO PBR (cel look — our shader owns lighting), quad topology (forces FBX).
    token = upload(sheet)
    gen_id = submit({
        "type": "image_to_model",
        "file": {"type": "png", "file_token": token},
        "model_version": args.model_version,
        "texture": True,
        "pbr": False,
        "quad": True,
        "face_limit": args.face_limit,
        "texture_alignment": "original_image",
        "orientation": "align_image",
        "model_seed": 20260713,
        "texture_seed": 20260713,
    })
    gen = poll(gen_id)
    save_outputs(gen, outdir, "tripo_model")
    provenance(args.char_id, "mesh+texture", f"image_to_model {args.model_version} task {gen_id} sheet {sheet}")

    # 2. Rig: mixamo-spec skeleton (clean Unity Humanoid mapping + Mixamo-compatible retargets).
    rig_id = submit({
        "type": "animate_rig",
        "original_model_task_id": gen_id,
        "model_version": "v2.5-20260210",
        "out_format": "fbx",
        "rig_type": "biped",
        "spec": "mixamo",
    })
    rig = poll(rig_id)
    save_outputs(rig, outdir, "tripo_rigged")
    provenance(args.char_id, "rig", f"animate_rig v2.5 mixamo task {rig_id}")

    if args.skip_anim:
        print("done (rig only).")
        return

    # 3. Retarget the volleyball-relevant presets onto the rig.
    ret_id = submit({
        "type": "animate_retarget",
        "original_model_task_id": rig_id,
        "out_format": "fbx",
        "export_with_geometry": True,
        "animations": CLIPS,
    })
    ret = poll(ret_id)
    save_outputs(ret, outdir, "tripo_anim")
    provenance(args.char_id, "animations", f"animate_retarget {'+'.join(CLIPS)} task {ret_id}")

    print("\nDONE. Import the FBX in Unity (Humanoid), materials -> VG/Toon + VG/Outline "
          "(docs/ai-character-pipeline.md §3.5).")


if __name__ == "__main__":
    main()
