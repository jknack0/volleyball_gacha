# AI Character Pipeline — Cel-Shaded Cast Generation

Purpose: replace commissioned 3D character models for the MC + R/SR cast with an AI generation pipeline; keep human commissions as the fallback for the 4 SSR banner faces ("hero asset" rule). Companion to `docs/art-budget.md` (whose character line this collapses ~80–90% if the spike passes) and `docs/story-bible.md` (whose descriptions drive the prompts).

Status: **UNPROVEN until the MC spike (§2) passes.** Do not batch-generate the cast before the gate.

---

## 1. Why this works for us (and when it wouldn't)

- The cel look lives in **our** shader (`VG/Toon` + `VG/Outline`): banded light, ink hulls, flat color. AI meshes fail at micro-detail and subtle materials — both invisible under our rendering. The tools only owe us **shape + flat colors**.
- Comedy roster = silhouette-first characters. Distinct proportions are AI-easy; subtle facial acting (AI-hard) is barely needed.
- One shared humanoid animation set (~10 clips) retargets to every character. Nobody gets bespoke animation.
- This would NOT work if we wanted photoreal, precise likenesses, or per-character signature animation.

## 2. THE SPIKE (do this first — half a day)

Run **one** character (the MC, Hoshino Chihiro — see story bible §2) through the full chain. Go/no-go before any batch work or budget-doc updates.

| # | Step | Output |
|---|---|---|
| 1 | Prerequisite: add texture support to `VG/Toon` (§7) | shader samples `_BaseMap × _BaseColor` |
| 2 | Generate a character sheet (§3.1) | front/side/back T-pose PNG, flat colors |
| 3 | Image→3D in Tripo or Meshy (§3.2) | ~10k-tri textured mesh |
| 4 | Auto-rig (§3.3) | humanoid FBX |
| 5 | Import to Unity, swap materials to VG/Toon + VG/Outline (§3.5) | prefab |
| 6 | Retarget 3 test clips: idle, jump, overhead swing (§3.6) | animated prefab |
| 7 | Stand it in the grey-box next to the capsules, all 3 camera presets | screenshots + verdict |

**Go/no-go rubric — judge IN-ENGINE with our shader, never in the tool's viewer:**

- [ ] Silhouette reads as the character at net-cam distance (preset 1)
- [ ] Ink outlines are clean (no shredded hull on hair/fingers) at mobile resolution
- [ ] Shoulders/hips survive the overhead swing without ugly skinning collapse
- [ ] ≤ 15k tris, one ≤1024² texture, imports as Unity Humanoid without bone surgery
- [ ] Total hands-on time ≤ 4 h including retries

3+ boxes fail → pipeline is not ready; commission route stands; revisit in 6 months (tools move fast).
All pass → update `docs/art-budget.md` character line; this doc becomes canon.

## 3. The per-character recipe (after the gate)

### 3.1 Character sheet (image generation)

The sheet is the **consistency contract** — every character goes through the same locked style prompt.

- Model: any strong anime-capable image model with reference/character consistency.
- **Style bank (locked, verbatim for every character):**
  > *anime cel-shaded character turnaround, T-pose, front view + side view + back view, full body, flat colors, bold clean lineart, no gradients, no rim light, neutral flat lighting, plain white background, 2XKO / modern fighting-game anime proportions, sporty volleyball uniform*
- Append the character block: name, position, silhouette gag, palette, outfit details from `docs/story-bible.md §4`.
- Requirements: strict T-pose (or A-pose), feet visible, hair readable as 2–4 masses (not strands), palette ≤ ~6 flats.
- Save to `Assets/Art/CharacterSheets/<char_id>/sheet_v<N>.png` + the exact prompt in `prompt.txt` beside it. Sheets are canon; regenerating a character starts from its sheet, not from scratch.

### 3.2 Image → 3D

Primary: **Tripo** or **Meshy** (pick one, stay on it for the whole cast — cross-tool style drift is real). Local/free alternative: **Hunyuan3D** (open-source; cleanest geometry, needs GPU + manual rigging step).

Settings that matter:
- Input: the front T-pose (add side/back views if the tool takes multi-view — better occluded-region guesses).
- Topology: quad / "game" preset; target **8–15k tris** (mobile + outline hull doubles vertex cost — stay lean).
- Texture: albedo only. **Ignore/discard PBR maps** (metallic/roughness/normal) — VG/Toon reads none of it, and baked-in gloss fights the cel look.
- 2–4 generations per character is normal; pick by silhouette, not by face detail (faces get judged in-engine at real camera distance).

### 3.3 Auto-rig

- Meshy: built-in rig (humanoid preset) → FBX.
- Tripo: one-click rigging → FBX.
- Hunyuan3D output or rig-less meshes: run through **Mixamo** auto-rigger (free) → FBX with skin.
- Export: **FBX** (most reliable skeleton+skinning transfer into Unity).

### 3.4 The anime-face escape hatch (per character, as needed)

Image-to-3D anime **heads/hair** are still the weak spot. For characters where the generated head reads badly:
- Build the base in **VRoid Studio** (free, anime-native face/hair, VRM export — MToon-adjacent and toon-friendly by construction), then apply AI-generated **outfit textures** to the VRoid body.
- Import via **UniVRM** (add the package when first needed — deliberately deferred from the base manifest).
- The cartoonier gag characters tolerate full AI generation; the "straight-man" characters are the VRoid candidates.

### 3.5 Unity import

1. Drop FBX under `Assets/Art/Characters/<char_id>/`.
2. Rig tab → Animation Type: **Humanoid** → verify avatar mapping (green bones). Fix mis-mapped fingers by ignoring them (we don't finger-animate).
3. Materials: extract, then replace every material with:
   - slot 0: `VG/Toon`, `_BaseMap` = the albedo texture, `_BaseColor` = white (or a tint for team-variant reuse)
   - slot 1: `VG/Outline` (width ~0.02 for characters — thinner than the 0.035 capsule default; hair/fingers shred at large widths)
4. Disable cast-shadows if the banded look fights the shadow map (cel purity beats correctness here — [tunable]).

### 3.6 Animation (shared set, retargeted)

One humanoid clip set serves the entire cast. Sources: Mixamo (free) or Meshy's preset library; retarget via Unity Humanoid.

| Clip | Used at |
|---|---|
| Idle (athletic) | slots |
| Serve toss + overhead swing | ServeContact |
| Bump/receive | ReceiveWindow contact |
| Overhead set | SetSelect resolve |
| Approach + jump + spike swing | AttackApproach/Contact |
| Block jump | BlockWindow |
| Dive | DigWindow |
| Celebrate ×2, dejected ×1 | PointResolved |

Store under `Assets/Art/Anim/Shared/`. Per-character animation is PROHIBITED until post-launch (signature-move cut-ins are 2D, per ui-screens §5).

## 4. Consistency system

- One tool, one style bank, one palette discipline (≤6 flats/char, team accents from `GreyBoxMatch` palette).
- Sheets + prompts versioned in-repo (§3.1) — the sheet is the source of truth, the mesh is a build artifact.
- Line up every accepted character in the grey-box side by side after each addition; style drift is judged there, in-shader, at gameplay camera distance.

## 5. Provenance log (compliance)

Append one row per accepted asset to `docs/art-provenance.csv`:

```
asset_id, kind, tool+version, date, prompt_or_source_ref, license_tier, human_cleanup_hours
```

- Use paid tiers of Meshy/Tripo (commercial rights). Keep receipts.
- Apple/Google currently require no AI-content disclosure; Steam does — if we ever ship there, this log IS the disclosure. (compliance doc owns the policy; this file owns the data.)

## 6. Cost & time model (validated by the spike, then written into art-budget)

| Item | Estimate |
|---|---|
| Tooling | ~$20–60/mo (one tool tier) |
| Generation credits | ~$2–5 per character (2–4 attempts) |
| Hands-on time | 2–4 h/character (sheet, generation, import, QA) |
| Cleanup contract (optional) | ~$100–300/character for skinning fixes |
| SSR fallback | 4 × commission per art-budget §2 (unchanged) |

## 7. Engineering prerequisites (owned by the code side)

- [ ] `VG/Toon`: add `_BaseMap` texture sampling (`half4 albedo = SAMPLE_TEXTURE2D(...) * _BaseColor`) — currently flat-color only; character atlases need it. ~10-line shader change + material updates.
- [ ] Character prefab convention: `CharacterView` swap point in `GreyBoxMatch` (capsule → prefab by `char_id`) — lands with VB-14/15 polish, not before.
- [ ] UniVRM package (only if/when the §3.4 VRoid path is first used).

## 8. What NOT to do

- Don't judge assets in the generator's viewer — its lighting lies; our shader is the only judge.
- Don't accept PBR gloss into the project. Albedo only.
- Don't batch the cast before the spike gate. One character, full chain, verdict.
- Don't chase face detail the camera never sees; silhouette and palette carry the read.
- Don't generate per-character animations; the shared set is a hard budget wall.
