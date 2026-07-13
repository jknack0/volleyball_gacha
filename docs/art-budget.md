# Art & Audio Budget — Priced Inventory, VO Decision, Staffing/Calendar Skeleton

Wave-3 doc. Owns: asset inventory with market-rate pricing, VO scope recommendation, staffing/calendar skeleton, launch total + sensitivity. Builds on `PLAN.md §5.2/§6/§8`, `docs/story-bible.md §4/§7`, `docs/m0-gameplay-spec.md §5/§8`. Non-goals: art style direction, UI layout (owned by `docs/ui-screens.md`), loc posture (owned by `docs/compliance-localization.md`).

All dollar figures are **commission spend only — solo-dev labor excluded** (see §5 assumption set). Low/mid/high = cheapest credible freelancer / experienced indie contractor / studio-adjacent quality. Every invented quantity is **[tunable]**; contract facts are **[structural]**.

---

## 1. Pricing Assumptions & Method

- Rates grounded via web search 2026-07; links per line. Where the market data was indirect, the range is a conservative **assumption [tunable]** and labeled as such.
- Sprite/illustration market quotes are frequently personal-use; commercial rights run 2–5× base ([voxillustration](https://voxillustration.com/blog/anime-illustration-pricing/)). **Mid/high columns assume commercial licensing included; low assumes negotiated indie commercial rates** [tunable].
- Quantities come from the wave-2 docs and are cited; they are only as [structural] as their source doc says.
- Orientation (portrait vs landscape) is UNDECIDED until the M0 A/B gate (PLAN §2.5, m0 spec §8.3.3). Every line below is orientation-agnostic except where noted (§6 order dates gate UI/store art on the decision). Cut-ins are full-screen on both rigs (m0 spec §5 C11) — no double-spend.

## 2. Asset Inventory & Unit Pricing

### 2.1 Characters — 3D (M4 commission phase)

Quantity basis: **13 characters = 12 gacha + MC** (story bible §4). Animations are a **shared library** because VRoid rigs are the skeleton standard and animations transfer (PLAN §5.2) — only signature moves are per-character.

| # | Item | Qty | Unit low/mid/high | Line low/mid/high | Source / label |
|---|---|---|---|---|---|
| A1 | Stylized low-poly anime model + rig + textures | 13 | $600 / $1,500 / $3,000 | **$7,800 / $19,500 / $39,000** | Low-poly game char model+rig $500–$3,000 ([VSQUAD](https://vsquad.art/blog/how-much-does-a-3d-character-cost-vsquad), [Pixune](https://pixune.com/blog/3d-model-cost/)); low end = SEA freelancer $15–40/hr ([VSQUAD](https://vsquad.art/blog/how-much-does-a-3d-character-cost-vsquad)) |
| A2 | Shared court animation library — serve ×2, receive, dig/dive, set, spike approach+swings ×4, block, idles, run/shuffle, celebrate/deflate pairs, libero swap ≈ **30 clips [tunable]** | 30 | $200 / $450 / $800 per cycle | **$6,000 / $13,500 / $24,000** | Game anim cycles $200–$1,000/cycle ([Pixune](https://pixune.com/blog/game-art-outsourcing-price/)) |
| A3 | Signature-move animation (1 bespoke clip/char, feeds C11 return-to-court beat; primitives (a)–(f) are sim-side, no anim cost) | 13 | $300 / $700 / $1,200 | **$3,900 / $9,100 / $15,600** | Upper band of cycle rate — combat-complexity clips cost more ([Pixune](https://pixune.com/blog/game-art-outsourcing-price/)) |

**Subtotal 3D: $17,700 / $42,100 / $78,600**

### 2.2 VN Portrait Sprites (M4)

Quantity basis [structural]: **88 sprites / 51 scenes** — 13 story characters × base + 6 expressions = 78, MC +2 = 80, 4 minor NPCs × 2 = 8 (story bible §7.1–7.2).

| # | Item | Qty | Unit low/mid/high | Line low/mid/high | Source |
|---|---|---|---|---|---|
| B1 | Main-cast base sprite (full-body) | 13 | $150 / $300 / $500 | $1,950 / $3,900 / $6,500 | Base sprite $50–$150 entry, $200–$500 mid-tier ([VN Paths](https://vnpaths.com/how-much-does-it-cost-to-make-a-visual-novel/)) |
| B2 | Additional expressions (5/char + MC's 2 = 67) | 67 | $15 / $40 / $80 | $1,005 / $2,680 / $5,360 | Expressions $10–$30 entry, $30–$80 mid ([VN Paths](https://vnpaths.com/how-much-does-it-cost-to-make-a-visual-novel/)) |
| B3 | Minor NPC (base + 1 expr) | 4 | $115 / $240 / $430 | $460 / $960 / $1,720 | Same rate card, smaller scope |

**Subtotal VN sprites: $3,400 / $7,500 / $13,600** (rounded)

### 2.3 Signature Cut-in Assets (M4)

Basis: 1 full-screen 2D cut-in per character (m0 spec §5 C11 [structural — gacha showcase]); motion (pan/zoom/speed-line overlays) assembled in Unity Timeline by dev, so the commission is **splash illustration only**. SSRs get the flashiest (PLAN §2.3): premium tier = 4 SSR + MC = 5; standard = 8 SR/R.

**Reuse credit:** the gacha SSR pull ceremony reuses these same C11 Timelines — only a shared rarity-tell envelope/seal VFX set is new (see E2; per ui-screens.md ceremony note). No bespoke gacha reveal animation is budgeted.

| # | Item | Qty | Unit low/mid/high | Line low/mid/high | Source |
|---|---|---|---|---|---|
| C1 | Standard cut-in splash (SR/R) | 8 | $250 / $500 / $900 | $2,000 / $4,000 / $7,200 | Splash art from $250 freelance ([zeriart](https://www.zeriart.com/)); splash/in-game assets $500–$5,000+ ([voxillustration](https://voxillustration.com/blog/anime-illustration-pricing/)) |
| C2 | Premium cut-in splash (SSR + MC), 2-pose | 5 | $500 / $1,000 / $2,000 | $2,500 / $5,000 / $10,000 | Same sources, detail/pose multiplier |

**Subtotal cut-ins: $4,500 / $9,000 / $17,200**

### 2.4 Environment (M4)

One gym, done well (PLAN §5.2). The Concord finale's "courtroom-shaped arena" (story bible §1.3) is a **dressing pass on the same gym** (banners, dais props), not a second environment [structural — scope cap].

| # | Item | Line low/mid/high | Source / label |
|---|---|---|---|
| D1 | Stylized gym, engine-ready (URP, lightmapped) + finale dressing props | $2,000 / $5,000 / $10,000 | Contained scene $1,000–$5,000 mid-tier; hero env $5k–$15k ([Skyroid](https://www.skyroidstudios.com/insights/game-environment-art-cost-pricing-guide)) |
| D2 | Crowd sprite sheets (~12 variants × 2-frame sway + swell poses) [tunable] | $300 / $800 / $1,500 | assumption [tunable] — priced as 2D sprite-sheet commission |

**Subtotal environment: $2,300 / $5,800 / $11,500**

### 2.5 VFX (M4)

Speed lines, impact frames, hit-stop treatments, floor-slam decals, Ignition overlay states, slow-mo color grade (PLAN §2.3; m0 spec §5 C7–C12). Mostly shader/particle work: base = asset-store packs, bespoke = contract 2D VFX artist at $40–90/hr — hours estimate is an assumption [tunable].

| # | Item | Line low/mid/high | Source / label |
|---|---|---|---|
| E1 | Match VFX set (packs + 30–100 contract hrs) | $1,500 / $4,000 / $9,000 | assumption [tunable]; hourly anchor $40–87 anime-style ([Fiverr guide](https://www.fiverr.com/resources/guides/costs/anime-illustrator)) |
| E2 | Gacha ceremony rarity-tell VFX (one shared envelope/seal set, all SSRs; per ui-screens.md reuse rule) | $300 / $600 / $1,200 | assumption [tunable] |

**Subtotal VFX: $1,800 / $4,600 / $10,200**

### 2.6 UI Art Pass (M4)

Skin/theme only — layout is `docs/ui-screens.md`'s. Iconography (~80 icons: stats, currencies, equipment sets, rarity gems, banner frames), 9-slice frames, backgrounds, banner-art templates. Ordered **after** the M0 orientation decision (§6).

| # | Item | Line low/mid/high | Label |
|---|---|---|---|
| F1 | UI art pass (~60–150 contract hrs at $40–90/hr) | $2,500 / $6,000 / $12,000 | assumption [tunable] |

### 2.7 Audio (M4)

| # | Item | Qty | Line low/mid/high | Source / label |
|---|---|---|---|---|
| G1 | Music: match cue = **7 stems** (5 vertical layers incl. Ignition motif + 2 conditional stems per `docs/m0-hardening.md §3.2` [structural]) + menu + VN theme + jingles ≈ **12–18 finished min [tunable]** | 12–18 min | $1,800 / $5,300 / $14,400 | $30–100/min hobbyist, $200–400 indie full-time, ~$1,000 pro ([GameDeveloper](https://www.gamedeveloper.com/game-platforms/how-to-commission-music-for-your-game), [Ninichi](https://ninichimusic.com/blog/understanding-how-much-an-indie-game-music-composer-costs), [Berklee](https://online.berklee.edu/takenote/gaming-music-how-to-price-composition-work/)); adaptive-stem delivery priced at top of each band |
| G2 | SFX: ball contacts graded per Perfect/Great/Good/Miss, whistle, crowd swells, UI, gacha ceremony, Ignition, signature stings ≈ **60–120 assets [tunable]**; low = mostly packs | 60–120 | $400 / $2,700 / $6,000 | $30–50 per custom FX ([VI-Control](https://vi-control.net/community/threads/sfx-and-sound-design-fees.15470/), [Zúmer](https://javierzumer.com/blog/2022/5/28/how-and-how-much-to-charge-as-a-game-audio-freelancer)) |
| G3 | VO — **recommended scope: gag-stings-only** (see §4) | 65–100 lines | $1,500 / $3,000 / $6,000 | §4 rate basis |

**Subtotal audio: $3,700 / $11,000 / $26,400**

### 2.8 Store & Platform Assets (M5)

| # | Item | Line low/mid/high | Label |
|---|---|---|---|
| H1 | App icon, feature graphic, screenshot frames/key art crops (produced **after** orientation lock — screenshots are orientation-dependent) | $300 / $800 / $2,000 | assumption [tunable] |
| H2 | Backend/SaaS fees through soft launch (one line per contract): Apple $99/yr + Google $25 + PlayFab free tier→entry + crash reporting free tier + AI tooling | $300 / $600 / $1,000 | vendor list prices; [tunable] |

---

## 3. Milestone Roll-up

**M0–M3 ≈ $0 art spend — verified**: PLAN §5.2 pins prototype/mid art to VRoid Studio → VRM → UniVRM (free) through M3; M0 is grey-box capsules (m0 spec §8.1). One caveat: **M1's scope includes an SFX pass** (PLAN §6) — covered by CC0/asset-store packs, $0–300 [tunable]. Crash reporting from M1 is free-tier (PLAN §5.4).

| Milestone | Art/audio spend low/mid/high | Contents |
|---|---|---|
| M0 | $0 | Grey-box; capsules; zero meta [structural per PLAN §6] |
| M1 | $0 / $150 / $300 | Placeholder SFX packs [tunable] |
| M2–M3 | $0 | VRoid placeholders; data/UI grey; **but commissioning contracts are signed here** (§6) — cash outlay starts late M3 as deliveries land |
| **M4** | **$35,900 / $86,000 / $169,500** | §2.1–2.7: 3D 17.7/42.1/78.6k + sprites 3.4/7.5/13.6k + cut-ins 4.5/9/17.2k + env 2.3/5.8/11.5k + VFX 1.8/4.6/10.2k + UI 2.5/6/12k + audio 3.7/11/26.4k |
| M5 | $600 / $1,400 / $3,000 | Store assets + SaaS/platform fees |
| **Launch total (M0→M5)** | **≈ $37,000 / $88,000 / $173,000** | Art+audio+fees only; dev labor excluded |

---

## 4. VO Scope Decision

Line-count basis: script ≈ 900–1,100 lines (51 scenes × avg lengths, story bible §7.1). Rate basis: indie non-union $3–10/line self-recorded, or $250/hr directed with 2-hr minimum ([VoiceActingClub indie guide](https://voiceactingclub.com/rates/), [GVAA](https://globalvoiceacademy.com/gvaa-rate-guide-2/), [Speechify](https://speechify.com/blog/how-much-should-voice-actors-cost/)). Per-actor session minimums dominate small scopes: 13–17 distinct voices.

| Option | Scope | Cost low/mid/high | Notes |
|---|---|---|---|
| None | text only | $0 | Zero loc drag; weakest gacha ceremony punch |
| **Gag-stings-only (RECOMMENDED)** | 13 chars × (signature-move shout + 2–3 catchphrases + effort barks) ≈ 65–100 lines | **$1,500 / $3,000 / $6,000** | Signature names are *shouted* per story bible §1.4 — this is the highest-value VO in the game and it lands in the cut-in, the monetization showcase. Driven by 13 × $100–250 session minimums, not per-line |
| Partial | above + climax scenes & banner vignettes ≈ 350 lines, 17 voices | $3,000 / $6,000 / $12,000 | Awkward middle: pays session overhead for all 17 voices but still reads as "mostly silent" |
| Full | ~1,000 lines, 17 voices, directed | $5,000 / $10,000 / $20,000 (EN only) | Each additional voiced language ≈ ×1.8–2: dub audio runs $500–1,500/finished hour/language ([Artlangs](https://artlangs.com/news-detail/How-Much-Does-Game-Localization-Cost--A-2025-Guide-for-Developers)) **on top of** transcreated scripts |

**Recommendation: gag-stings-only** [structural once user confirms]. Rationale: (1) the comedy is textual — timing gags, footnoted trash talk, and Article 7 citations don't need reads, and `docs/compliance-localization.md` (loc-posture owner) sets JP as *transcreation*, i.e., jokes get rewritten, so every voiced comedic line multiplies loc cost with re-records per language — comedy VO is the single most localization-hostile asset class in this budget; (2) signature shouts + catchphrases are short, evergreen across banners, and survive transcreation as-is or with cheap pickup sessions; (3) upgrade path to Partial stays open post-launch without re-casting.

### 4.1 Localization cost line (owner of posture: `docs/compliance-localization.md`)

JP fast-follow **evaluation** line — **not** in the §3 launch total:

| Item | Basis | Cost low/mid/high |
|---|---|---|
| JP transcreation of script + UI (≈14–17k words [tunable]) | EN→JA $0.12–0.30/wd; transcreation $0.30+/wd; +20–40% LQA ([SandVox](https://sandvox.io/glossary/game-localization-cost/), [WordsPrime](https://wordsprime.com/game-and-mobile-app-localization-cost-guide-for-global-releases-in-2026/), [Artlangs](https://artlangs.com/news-detail/How-Much-Does-Game-Localization-Cost--A-2025-Guide-for-Developers)) | $3,400 / $5,500 / $8,500 |

Gag-stings VO under JP: re-record ≈ 13 sessions ≈ same $1.5–6k band again per language [tunable].

---

## 5. Staffing & Calendar Skeleton

**Assumption set [tunable]:** solo dev (design + all code) + AI pair programming + contract artists per discipline. No salaried hires through M5. Dev labor therefore unpriced; contractor spend is §2–3.

| Role | Engagement | When |
|---|---|---|
| Solo dev | full-time | M0→M5 |
| Character artists ×2–3 (parallel) | contract, per-model | order M2/M3 → deliver by M4 start |
| VN sprite artist ×1 | contract, per-set | M3→M4 |
| Illustrator (cut-ins) ×1 | contract, per-splash | M3→M4 |
| VFX artist ×1 | contract, hourly | M4 |
| UI artist ×1 | contract, hourly | M3 mid→M4 |
| Composer ×1 | contract, per-minute | M3 mid→M4 |
| Sound designer ×1 | contract, per-asset | M3 end→M4 |
| VO ×13 | one-off remote sessions | M4 back half, after script lock |

### 5.1 Per-milestone duration ranges [all tunable]

| Milestone | Duration | Rule |
|---|---|---|
| **M0** | **open-ended — no deadline** [structural per PLAN §6 hard wall] | Internal plan is 4 × 2-week blocks (m0 spec §8.2 W1–W8); **gate review every 2 weeks** at block boundaries, running §8.3's four checks as far as the build allows. Ship nothing, promise nothing, until all four pass |
| M1 | 6–10 wks | |
| M2 | 4–8 wks | |
| M3 | 6–10 wks | |
| M4 | 10–16 wks | Longest post-M0 pole; §5.2 exists to keep it art-unblocked |
| M5 | 6–10 wks | |
| **M1→M5 total** | **32–54 wks after M0 passes** | |

### 5.2 Commissioning critical path — order-by dates (relative to milestone starts)

Serial risk: one artist × 13 models ≈ 13–26 wks. Mitigation: 2–3 artists in parallel (8–14 wks) and ordering during M2–M3 dev so nothing blocks M4.

| Order | Asset | Order-by | Delivery window | Why |
|---|---|---|---|---|
| 1 | **Pilot character** (1 model+rig through full VRoid-skeleton import) | **M2 start** | M2 | Validates rig-transfer standard (PLAN §5.2) and the artist's brief *before* committing the batch; kills the biggest integration risk for $600–3,000 |
| 2 | Character batch (remaining 12, 2–3 artists) | **M3 start** | through M3 → all in by **M4 start + 2 wks** | 8–14 wks parallel lead time overlaps M3 dev |
| 3 | Gym environment | M3 start | 4–8 wks | Needed for M4 FTUE + store shots |
| 4 | VN sprite sets (88 sprites) | M3 start | 6–10 wks | Story integration is early-M4 work |
| 5 | Cut-in splash batch | M3 start + 2 wks | 6–10 wks | Timeline assembly is dev-side, can trail models |
| 6 | Music | M3 mid — brief locks only after M1 exit + `docs/m0-hardening.md` music states stable | 4–8 wks | Stem spec depends on Hype/Ignition states |
| 7 | UI art pass | M3 mid — **gated on M0 orientation decision** being long settled + ui-screens.md stable | 4–8 wks | Theme is orientation-sensitive |
| 8 | Custom SFX | M3 end | 3–6 wks | Replaces M1 placeholder packs |
| 9 | VO sessions (gag-stings) | M4 mid, after script lock | 2–4 wks | Short scope; remote self-recorded |

Rule of thumb [tunable]: every external commission is ordered ≥ 1 milestone before it is needed, and no M4 task may have an art dependency that wasn't ordered by M3 end.

---

## 6. Grand Total & Sensitivity

**Launch (M0→M5) art + audio + platform fees: ≈ $37k low / $88k mid / $173k high.** Dev labor excluded; backend/SaaS is line H2; JP loc (§4.1) excluded as fast-follow.

The three levers that move the total most:

| Lever | Swing | Mechanism |
|---|---|---|
| 1. **VO scope** | $0 → $20k+ (EN), ×1.8–2 per voiced language after transcreation | §4 table; comedy VO couples audio spend to loc spend — the only lever that multiplies across languages |
| 2. **Character count 12 → 8** (PLAN §8 open question) | −$5.5k / −$13k / −$24k (≈ −15%) | Each gacha character carries model+rig, signature anim, cut-in, sprite set ≈ $1.4k/$3.2k/$6k fully loaded. Trim R/SRs only — the 4 SSR banners are the arc spine (story bible §6) [structural] |
| 3. **Cut-in ambition** | $4.5k (static splash) → $17k (premium multi-pose) → $30k+ if Live2D (PLAN §8 flags it) | The gacha showcase line; also drags E2 ceremony VFX and premium-tier animation upward |

Second-order note: the low column assumes offshore/entry-market rates with indie commercial licenses; if quality bar forces the mid column on characters + cut-ins alone (the two showcase lines), the floor is ≈ $60k.
