#!/usr/bin/env python3
"""Meshy character factory (A/B counterpart to gen_character.py's Tripo route).

One image uses image-to-3d; two to four ``--view`` inputs use multi-image-to-3d.
The legacy single-sheet command remains supported and defaults to a T-pose.

Usage: export MESHY_API_KEY=msy_...
       python tools/gen_character_meshy.py char.mc_meshy path/to/sheet.png
       python tools/gen_character_meshy.py char.mc --view front.png --view right.png \
           --view back.png --view left.png --pose-mode a-pose
"""
import argparse
import base64
import csv
import datetime
import json
import os
import re
import time
import urllib.request

API = "https://api.meshy.ai/openapi/v1"


def api_key():
    """Return the Meshy API key without requiring it for imports or dry runs."""
    return os.environ.get("MESHY_API_KEY")


# Volleyball-analog picks from the 680-entry catalog [tunable].
ACTIONS = {"idle": 0, "jump": 382, "dive": 506}  # + walk/run ship free with the rig


def call(method, path, payload=None):
    key = api_key()
    if not key:
        raise RuntimeError("MESHY_API_KEY is required for Meshy API calls")
    headers = {"Authorization": f"Bearer {key}", "Content-Type": "application/json"}
    req = urllib.request.Request(
        f"{API}{path}",
        data=json.dumps(payload).encode() if payload is not None else None,
        headers=headers,
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


def image_data_uri(path):
    """Encode a Meshy-supported PNG or JPEG based on its file signature."""
    with open(path, "rb") as handle:
        raw = handle.read()
    if raw.startswith(b"\x89PNG\r\n\x1a\n"):
        media_type = "image/png"
    elif raw.startswith(b"\xff\xd8\xff"):
        media_type = "image/jpeg"
    elif raw.startswith(b"RIFF") and raw[8:12] == b"WEBP":
        raise ValueError(f"Meshy accepts PNG and JPEG inputs; convert WebP before submission: {path}")
    else:
        raise ValueError(f"unsupported image format: {path}")
    encoded = base64.b64encode(raw).decode("ascii")
    return f"data:{media_type};base64,{encoded}"


def build_mesh_request(view_paths, *, textured=True, target_polycount=15000, pose_mode="t-pose"):
    """Build an image-to-3D request for one view or a multi-image request for 2-4."""
    paths = list(view_paths)
    if not 1 <= len(paths) <= 4:
        raise ValueError("Meshy requires between one and four source images")

    images = [image_data_uri(path) for path in paths]
    payload = {
        "ai_model": "meshy-6",
        "should_texture": textured,
        "should_remesh": textured,
        "pose_mode": pose_mode,
        "target_formats": ["glb", "fbx"],
    }
    if textured:
        payload.update({
            "topology": "triangle",
            "target_polycount": target_polycount,
            "remove_lighting": True,
            "enable_pbr": False,
        })
    if len(images) == 1:
        payload["image_url"] = images[0]
        return "image-to-3d", payload
    payload["image_urls"] = images
    return "multi-image-to-3d", payload


def download(url, dst):
    urllib.request.urlretrieve(url, dst)
    print(f"  saved {dst} ({os.path.getsize(dst)//1024} KB)", flush=True)


def parse_args(argv=None):
    parser = argparse.ArgumentParser(description="Generate a rigged Meshy character from 1-4 views")
    parser.add_argument("char_id")
    parser.add_argument("sheet", nargs="?", help="legacy single-image input")
    parser.add_argument("--view", action="append", default=[], help="source view; repeat up to four times")
    parser.add_argument("--shape-only", action="store_true", help="generate geometry candidates without texture/rig")
    parser.add_argument("--skip-animations", action="store_true", help="rig but do not request animation presets")
    parser.add_argument("--dry-run", action="store_true", help="validate inputs and print a redacted request")
    parser.add_argument("--pose-mode", choices=("a-pose", "t-pose"), default="t-pose")
    parser.add_argument("--polycount", type=int, default=15000)
    parser.add_argument("--height-meters", type=float, default=1.6)
    args = parser.parse_args(argv)
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", args.char_id) or args.char_id in {".", ".."}:
        parser.error("char_id must be a safe single path component")
    if args.sheet and args.view:
        parser.error("use either the legacy sheet argument or --view, not both")
    args.views = args.view or ([args.sheet] if args.sheet else [])
    if not args.views:
        parser.error("provide a sheet or at least one --view")
    if len(args.views) > 4:
        parser.error("Meshy accepts at most four views")
    if not 100 <= args.polycount <= 300000:
        parser.error("--polycount must be between 100 and 300000")
    if args.height_meters <= 0:
        parser.error("--height-meters must be greater than zero")
    return args


def run(args):
    kind, payload = build_mesh_request(
        args.views,
        textured=not args.shape_only,
        target_polycount=args.polycount,
        pose_mode=args.pose_mode,
    )
    if args.dry_run:
        redacted = dict(payload)
        if "image_url" in redacted:
            redacted["image_url"] = "<embedded image 1>"
        if "image_urls" in redacted:
            redacted["image_urls"] = [f"<embedded image {i}>" for i in range(1, len(args.views) + 1)]
        print(json.dumps({"endpoint": f"/{kind}", "payload": redacted}, indent=2, sort_keys=True))
        return 0

    char_id = args.char_id
    character_root = os.path.abspath("Assets/Art/Characters")
    out_dir = os.path.abspath(os.path.join(character_root, char_id))
    if os.path.commonpath([character_root, out_dir]) != character_root:
        raise ValueError("character output must remain under Assets/Art/Characters")
    os.makedirs(out_dir, exist_ok=True)
    provenance = []

    print(f"1/3 {kind} ...", flush=True)
    mesh_id = call("POST", f"/{kind}", payload)["result"]
    mesh = poll(kind, mesh_id, "mesh")
    download(mesh["model_urls"]["fbx"], f"{out_dir}/meshy_model.fbx")
    tex = (mesh.get("texture_urls") or [{}])[0].get("base_color")
    if tex:
        download(tex, f"{out_dir}/Color.png")
    mesh_tool = f"meshy {kind} meshy-6"
    provenance.append((
        "meshy_model.fbx",
        "mesh+texture" if tex else "mesh",
        mesh_tool,
        mesh_id,
        mesh.get("consumed_credits"),
    ))
    if tex:
        provenance.append(("Color.png", "texture", mesh_tool, mesh_id, None))

    if args.shape_only:
        append_provenance(char_id, provenance)
        print("\nDONE shape candidate. Review geometry before texturing or rigging.", flush=True)
        return 0

    print("2/3 rigging ...", flush=True)
    rig_id = call("POST", "/rigging", {
        "input_task_id": mesh_id,
        "height_meters": args.height_meters,
    })["result"]
    rig = poll("rigging", rig_id, "rig")
    res = rig["result"]
    download(res["rigged_character_fbx_url"], f"{out_dir}/meshy_rigged.fbx")
    provenance.append(("meshy_rigged.fbx", "rig", "meshy rigging", rig_id, rig.get("consumed_credits")))
    basic = res.get("basic_animations") or {}
    for name in ("walking", "running"):
        url = basic.get(f"{name}_fbx_url")
        if url:
            clip_name = "walk" if name == "walking" else "run"
            filename = f"meshy_anim_{clip_name}.fbx"
            download(url, f"{out_dir}/{filename}")
            provenance.append((filename, "animations", f"meshy rigging basic animation {name}", rig_id, None))

    if not args.skip_animations:
        print("3/3 animations ...", flush=True)
        anim_tasks = {}
        for name, action in ACTIONS.items():
            anim_tasks[name] = call("POST", "/animations", {
                "rig_task_id": rig_id, "action_id": action,
            })["result"]
        for name, tid in anim_tasks.items():
            t = poll("animations", tid, f"anim:{name}")
            download(t["result"]["animation_fbx_url"], f"{out_dir}/meshy_anim_{name}.fbx")
            provenance.append((
                f"meshy_anim_{name}.fbx",
                "animations",
                f"meshy animation action_id={ACTIONS[name]}",
                tid,
                t.get("consumed_credits"),
            ))

    append_provenance(char_id, provenance)

    print("\nDONE. Next: blender_unity_prep.py merge -> unity_model.fbx -> VG menu build.", flush=True)
    return 0


def append_provenance(char_id, records):
    with open("docs/art-provenance.csv", "a", newline="") as f:
        writer = csv.writer(f)
        today = datetime.date.today().isoformat()
        for fname, kind, tool, task_id, credits in records:
            source_ref = task_id
            if credits is not None:
                source_ref = f"{task_id}; {credits} credits"
            writer.writerow([
                f"{char_id}/{fname}",
                kind,
                tool,
                today,
                source_ref,
                "paid-tier commercial",
                "",
            ])


def main():
    return run(parse_args())


if __name__ == "__main__":
    raise SystemExit(main())
