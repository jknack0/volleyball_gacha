# M0 Hardening — Device Floor, Rally Telemetry, Audio Direction

Closes the three M0 inputs that would otherwise bias the feel gate (`docs/m0-gameplay-spec.md §8.3`): an undefined performance floor makes latency/fps checks unfalsifiable, missing telemetry makes gate evidence anecdotal, and silence undercounts juice. Non-goals: gameplay math changes, UI design, budgets (see `docs/art-budget.md`).

Orientation note: everything here is orientation-agnostic or covers both rigs — the portrait/landscape decision is unmade until the M0 A/B (PLAN.md §2.5, m0 spec §8.3 check 3).

---

## 1. Device Floor & Performance Budget

### 1.1 Min-spec tiers

| Tier | iPhone reference | Android reference | Role |
|---|---|---|---|
| **Floor** | iPhone SE (3rd gen, 2022, A15) [tunable] | Samsung Galaxy A52s 5G (2021, Snapdragon 778G, 6 GB) [tunable] | The contract device. All gate measurements (m0 spec §8.3 checks 2, 4) run here. 2021-class Snapdragon 7-series or equivalent (Dimensity 900, Exynos 1280) qualifies. |
| **Reference mid** | iPhone 13 (2021, A15) [tunable] | Pixel 7a (2023, Tensor G2) [tunable] | Daily dev device; tuning happens here, then verified on floor. |
| **Showcase** | Current-gen iPhone | Current-gen SD 8-series | Marketing capture only; never a tuning target. |

Devices below floor: game runs with the full degradation ladder engaged (§1.3) and no fps promise; store listings set min OS = iOS 16 / Android 10 (API 29) [tunable].

### 1.2 The hard rule [structural]

**60 fps sustained during rallies on floor devices.** "Sustained" = the thermal protocol in §1.4, not a cold-start benchmark. Rendering interpolates over the fixed 1/60 s sim tick (m0 spec §0 conventions); a dropped render frame is cosmetic, a dropped sim tick is a bug. This rule is load-bearing for the feel gate: the ≤ 50 ms input→feedback check (m0 spec §8.3 check 2) is arithmetically impossible at unstable frame rates, so a soft floor silently biases the gate toward "feels bad."

### 1.3 Graceful-degradation ladder [structural order, tunable steps]

Engaged automatically top-down when frame time exceeds budget for 2 consecutive seconds; disengaged bottom-up after 30 s of headroom [tunable]. Each step is presentation-only.

| Step | Degrades | From → To |
|---|---|---|
| D1 | Crowd density | full sprite crowd → 50% → static billboard [tunable] |
| D2 | Post FX | full stack → bloom-only → none [tunable] |
| D3 | Shadow quality | soft 1-cascade → hard blob shadows [tunable] |
| D4 | Render scale | 1.0 → 0.85 → 0.75 [tunable] |

**Never-degrade invariant [structural]:** sim tick (1/60 s), input sampling and timestamping (m0 spec §7.2), and timing-window math (m0 spec §3.1) are NEVER degraded, scaled, or throttled at any ladder step or thermal state. Difficulty and fairness live in the sim; only pixels degrade. Slow-mo dilation stays presentation-layer per m0 spec §2.5 regardless of ladder state. Contact slow-mo (C7, m0 spec §5) and cut-ins are also never degraded — they are the product, not polish (PLAN.md §2.3).

Ladder state is logged (§2, `settings_changed` + fps trace) so gate playtests can be excluded/flagged if a floor device spent the session degraded.

### 1.4 Thermal protocol [structural protocol, tunable numbers]

15-minute sustained-play test on each floor device, per build-of-record (end of each 2-week block, m0 spec §8.2):

- **Load:** continuous Quick Matches vs Normal AI (auto-restart), charging cable disconnected, screen brightness 75% [tunable], airplane mode.
- **Capture cadence:** fps (mean, 1%-low) + device thermal state (`ThermalStatus` on Android, `ProcessInfo.thermalState` on iOS) + ladder step, sampled every 5 s into the telemetry sink (§2.3).
- **Pass criteria:** over minutes 5–15: mean fps ≥ 59 and 1%-low ≥ 50 [tunable]; thermal state never exceeds "serious"/"moderate+1" [tunable]; ladder never exceeds step D2 [tunable]; zero sim-tick overruns (tick > 16.67 ms) [structural].
- Fail → performance work before feature work in the next block; the feel gate is not run on a build that fails thermal.

### 1.5 Frame budget at floor (16.67 ms) [all tunable]

| Subsystem | Budget (ms) | Notes |
|---|---|---|
| Sim tick (`RallySim` + AI utility + arcs) | 1.5 | pure C#, no allocations in steady state |
| Animation / IK (12 capsules→rigs) | 2.5 | |
| Rendering (URP opaque + transparent) | 6.5 | court, players, ball, crowd |
| Post FX | 1.5 | first ladder casualty after crowd |
| VFX / juice (particles, decals, hit-stop) | 1.0 | |
| UI / HUD | 1.0 | |
| Audio (mixer + adaptive layers §3) | 0.5 | |
| Telemetry enqueue + IO amortization | 0.2 | writes off main thread; enqueue only (§2.3) |
| Headroom / GC / OS | 2.0 | GC alloc budget in rally states: 0 B/frame [structural target] |
| **Total** | **16.67** | |

---

## 2. Rally Telemetry (from the FIRST M0 build)

Extends the per-rally log line already mandated by m0 spec §8.3 ("instrumentation shipped in M0") into a typed event schema. Events are emitted by the sim/presentation seam, not from inside pure sim code — the deterministic core stays engine- and IO-free (data-schemas §3).

### 2.1 Envelope (every event) 

| Field | Type | Notes |
|---|---|---|
| `schema` | `int` | telemetry schema version, starts 1 |
| `event` | `string` | event name below |
| `session_id` | `string` GUID | per app launch |
| `ts_utc` | `string` ISO-8601 | wall clock (envelope only — sim uses ticks) |
| `build` | `string` | build + sim version (matches replay stamp, data-schemas §4.2) |
| `device_tier` | `string` | `floor / mid / showcase / below_floor` (§1.1) |
| `orientation` | `string` | `portrait / landscape` — active rig (A/B analysis, PLAN §2.5) |

### 2.2 Events

**`session_start`**

| Field | Type | Notes |
|---|---|---|
| `device_model` | `string` | e.g. `SM-A528B` |
| `soc` | `string` | |
| `os` | `string` | |
| `assist_level` | `int` | 0 / 25 / 50 (m0 spec §7.4) |
| `reduce_slow_mo` | `bool` | m0 spec §7.4 |
| `locale` | `string` | BCP-47 |

**`match_start`**

| Field | Type | Notes |
|---|---|---|
| `match_id` | `string` GUID | joins all rally/contact events |
| `format` | `MatchFormat` | data-schemas enum |
| `difficulty` | `DifficultyTier` | |
| `seed_set_hash` | `string` | replay joinability without embedding seeds twice |

**`contact_resolved`** — one per resolved contact, the workhorse event.

| Field | Type | Notes |
|---|---|---|
| `match_id` / `rally_index` / `contact_index` | `string` / `int` / `int` | contact_index resets per rally |
| `contact_type` | `string` | `serve_float, serve_jump, receive, set, spike, roll, feint, block, dig, free_ball` |
| `actor_side` | `string` | `player / opponent` |
| `actor_human` | `bool` | AI grades are sampled, not tapped (m0 spec §6.2) — never pool them with human grades |
| `timing_grade` | `TimingGrade` | Perfect/Great/Good/Miss |
| `timing_delta_ms` | `float` | signed Δ = t − t*; null for AI |
| `quality` | `float` | [0,1], m0 spec §3.2 |
| `window_ms` | `{ perfect: float, great: float, good: float }` | final widths after stat/ctx/assist multipliers — makes window tuning data-driven |
| `assist_level` | `int` | active at this contact |
| `zone` | `ZoneId` | aim/landing zone, data-schemas enum; null where N/A |
| `signature_active` | `bool` | contact modified by a signature primitive |

**`rally_ended`**

| Field | Type | Notes |
|---|---|---|
| `match_id` / `rally_index` | `string` / `int` | |
| `length_contacts` | `int` | feeds chart 1 |
| `winner_side` | `string` | `player / opponent` |
| `end_reason` | `string` | `kill, stuff, tooled, ace, service_error, out, net, unplayable_shank, no_commit_ace` (terminal outcomes, m0 spec §3.6) |
| `duration_ticks` | `long` | |
| `hype_after` | `{ player: int, opponent: int }` | 0–100 |
| `serving_side` | `string` | |

**`match_ended`**

| Field | Type | Notes |
|---|---|---|
| `match_id` | `string` GUID | |
| `result` | `MatchResult` | embedded verbatim per data-schemas §2.8 (seedSet, finalScore, rallyCount, durationTicks, grade, hypePeak, …) |
| `abandoned` | `bool` | true when match ended via quit, not match point |

**`settings_changed`**

| Field | Type | Notes |
|---|---|---|
| `key` | `string` | `assist_level, orientation, reduce_slow_mo, ladder_step` |
| `old_value` / `new_value` | `string` | stringified |
| `match_id` | `string` | null outside a match |

**`app_background`** — the quit-point proxy [structural: backgrounding is the only reliable mobile quit signal].

| Field | Type | Notes |
|---|---|---|
| `match_id` / `rally_index` | `string` / `int` | null / −1 outside a match |
| `rally_state` | `string` | current state-machine state (m0 spec §1.1) at background |
| `score` | `{ player: int, opponent: int }` | |
| `session_elapsed_sec` | `float` | |

Plus §1.4's fps/thermal samples as a low-rate `perf_sample` stream: `{ fps_mean, fps_1pct_low, thermal_state, ladder_step, in_rally: bool }` every 5 s [tunable].

### 2.3 Sink — local JSONL, no vendor SDK [structural until M4 per PLAN.md §5.4]

- One line per event, appended to `{persistentDataPath}/telemetry/{yyyyMMdd}.jsonl`; writer thread off the main loop, main thread only enqueues (§1.5 budget).
- Rotation: per-day files, 50 MB cap per file, 14-day retention [tunable]. Pulled off device by cable/`adb`/Xcode for analysis; a repo-side notebook/script renders the five charts (owner: `docs/tooling-pipeline.md`).
- No network, no analytics SDK, no PII beyond device model. Vendor analytics is an M4 decision (PLAN.md §5.4); the JSONL schema is designed so M4 is a sink swap, not a re-instrumentation.

### 2.4 The five feel-gate charts

These operationalize the gate **beyond** the manual protocol in m0 spec §8.3 — **both run**; charts catch what testers can't articulate, testers catch what charts can't see. Pooling rule: charts 1–4 filter `actor_human = true` where applicable.

| # | Chart | Built from | Pass | Investigate |
|---|---|---|---|---|
| 1 | **Rally-length histogram** vs the 4–9 contact target band | `rally_ended.length_contacts`, 50-rally windows vs Normal AI | median ∈ [4, 9] (m0 spec §8.3 check 1) | p25 < 3 (too terminal) or p75 > 11 (toothless attacks) [tunable] |
| 2 | **Per-contact-type grade distribution** | `contact_resolved` grouped by `contact_type × timing_grade` | per type: Perfect ∈ [10%, 35%] and Miss ≤ 20% [tunable] | any type outside band → window/`ctx` retune for that contact (m0 spec §3.1), not global |
| 3 | **Quit-point curve** | `app_background.rally_state` + `match_ended.abandoned`, mid-match only | ≥ 70% of mid-match backgrounds at natural breaks (`PointResolved`, `Rotation`, `MatchEnd`) [tunable] | any single in-rally state holding > 25% of quits [tunable] — that state is losing players (e.g. `ServeAim` = meter frustration) |
| 4 | **Time-to-first-input** | `session_start` → first human `contact_resolved`; secondary: window-open → gesture reaction per contact type | median ≤ 30 s from launch [tunable]; reaction-time medians stable across a session (no fatigue cliff) | launch-to-input > 60 s (boot/flow friction) or any contact's reaction median > 80% of its `W_Good` (window practically unhittable) [tunable] |
| 5 | **fps / thermal trace** | `perf_sample` over the §1.4 protocol | §1.4 pass criteria on floor devices; supports m0 spec §8.3 check 2 (≤ 50 ms is unfalsifiable off 60 fps) | any in-rally fps dip below 55 [tunable]; any sim-tick overrun (auto-fail, [structural]) |

Gate hygiene [structural]: a playtest session only counts toward m0 spec §8.3 if chart 5 passed on that device during that session — otherwise feel complaints may be perf complaints in disguise.

---

## 3. Audio Direction & Adaptive Music

### 3.1 Direction brief

Tone follows the story-bible directive (`docs/story-bible.md` tone directive + §1.5 do/don't): **comedy-forward world, sincere volleyball.** Applied to audio:

- **During rallies, audio is 100% sincere sports drama.** Impact, tension, crowd. No comedy SFX between serve and point-resolved — the audio analogue of "never undercut a climax rally with a gag."
- Comedy lives where the world lives: menu/VN stingers, whistle-adjacent bureaucracy flourishes (the Arbiter's gavel-like whistle), crowd texture (one audible seagull at the Fisheries match, M4). None of this ships in M0.
- Palette: taiko-adjacent percussion + modern sports-anime hybrid (band + synth), bright and forward; reference the "last two minutes of a Haikyuu!! episode" fantasy (PLAN.md §1).
- Mix priority [structural]: contact SFX > crowd > music. The contact sound is the feel; music ducks ~2 dB [tunable] on every contact via sidechain.

### 3.2 Hype → music state machine

Driver: **player-team Hype** (0–100, PLAN.md §2.3); opponent Ignition adds a "threat" layer without changing state. Layered vertical mix — one looping cue, layers faded in/out.

| State | Condition | Layers active |
|---|---|---|
| `M_Warmup` | pre-first-serve, timeouts | ambience + sparse motif |
| `M_Base` | Hype 0–29 [tunable] | rhythm bed |
| `M_Building` | Hype 30–59 [tunable] | + percussion |
| `M_Heated` | Hype 60–99 [tunable] | + melodic lead |
| `M_Ignition` | player-team Ignition active | full mix + Ignition motif — "music layer kicks in" is the PLAN §2.3 contract, this is that layer |
| `M_MatchPoint` | either side at match point | **override**: high-tension variant (riser + thinned percussion — the crowd-holding-its-breath register). If Ignition is also active, Ignition motif persists over the variant. |
| `threat` (modifier, any state) | opponent Ignition active | + low-menace layer, no state change |

Transition rules:

- **Bar-quantized**: layer changes latch and apply at the next bar boundary [structural — musicality]; crossfade 250 ms [tunable]. `M_MatchPoint` enters at the next **beat**, not bar (urgency) [tunable].
- Upward transitions may occur mid-rally; **downward transitions only at rally end** [structural — music never deflates a live rally]. Hype loss from own errors (m0 spec §3.7) thus reads at the natural break.
- Signature cut-ins (sim clock paused, m0 spec §1.3): music ducks −6 dB [tunable] under the cut-in's own stinger, resumes at the paused bar position.
- Ignition onset syncs with the 0.3 s hit-stop (m0 spec §1.3): music hits a downbeat accent as the overlay lands.
- Kill/stuff/ace at `PointResolved`: one-shot stinger over the bed, no state change.

### 3.3 SFX inventory

Grade-layer rule [structural]: every contact sound = **base sample (per contact type) + grade layer (shared identity cue)**. The Perfect layer is one signature sound family — a crisp sweet-spot *crack* with a short bright tail — recognizable across all contact types. **Perfect has a signature sound; it is feel juice [structural]** — the ear must confirm the grade before the HUD does.

Contact type × grade matrix (all assets [tunable], the matrix itself [structural]):

| Contact base | Perfect | Great | Good | Miss |
|---|---|---|---|---|
| Serve (float) | sig. crack + air-knuckle flutter | clean palm hit | flat hit | mis-toss duff (service error per §3.6) |
| Serve (jump) | sig. crack + whip | sharp palm snap | flat hit | frame-shank |
| Receive / Dig | sig. crack + platform *pop* | solid forearm thump | dull thump | shank squeal-duff (Shank display grade) |
| Set | sig. crack + fingertip *tick* (soft variant) | clean double-touch tick | heavy push | double-contact fumble → free-ball whoosh |
| Spike | sig. crack + kill-whistle tail — **the** sound of the game | heavy hand slap | glancing slap | whiff (apex missed → free ball) |
| Block | sig. crack + roof *slam* (stuff register) | firm rejection thud | partial touch tick (deflection) | no-touch whoosh by the ears |

Event SFX:

| Event | Sound | Trigger |
|---|---|---|
| Whistle | Arbiter whistle: point / set / match variants | `PointResolved`, `MatchEnd` (m0 spec §1.1) |
| Shoe squeak | squeak pool, 6+ variants, round-robin | player locomotion direction changes |
| Net cord | tape rattle + dead-ball tension beat | net-cord roll (m0 spec §2.4 — the one visible drama RNG; it gets its own sound) |
| Floor slam | boom + decal sync | kill landing (camera C10, m0 spec §5) |
| Ball bounce / out | hollow bounce; line-judge flag *snap* on out | terminal out |
| Crowd swell T0 | ambient murmur bed | always on |
| Crowd swell T1 | rising anticipation loop | rally ≥ 6 contacts (matches Hype accrual row, m0 spec §3.7) |
| Crowd swell T2 | eruption pop | kill / stuff / ace |
| Crowd swell T3 | sustained roar | Ignition active |
| Crowd swell T4 | held-breath hush → delayed eruption | match point rally: hush at serve, eruption at resolution [structural — the PLAN §1 fantasy beat] |
| Hype tick / Ignition onset | rising shimmer; Ignition hit synced to hit-stop | Hype threshold events |
| Slow-mo enter/exit | low-pass sweep + tail | C7 contact slow-mo (m0 spec §5) — audio sells the dilation |
| UI | tap / confirm / back pool | menus (minimal in M0) |

### 3.4 M0 placeholder plan

- Temp/stock assets are acceptable for every row above (asset packs, freesound-class SFX, one licensed loop-stem set for the music states) — **but audio MUST be present in the M0 prototype**, including the grade-layer scheme and at least states `M_Base/M_Building/M_Heated/M_Ignition` [structural].
- **Silence-bias risk, named:** the feel gate measures *juice*. Impact sound is a large fraction of perceived contact weight; a silent grey-box undercounts tension in §8.3 check 4 and could fail the gate (or worse, pass it) for the wrong reasons — the same class of gate bias as testing on a non-floor device. Feel-gate playtest builds without the placeholder audio pass are invalid gate evidence [structural].
- Perfect's signature layer specifically must be *tuned*, not just present, before gate week (m0 spec §8.2 W7–8) — it is the audio half of the grade readout.
- Adaptive plumbing: Unity mixer snapshots + a thin layer controller is sufficient for M0; FMOD/Wwise is an M1+ decision, out of scope here.
- **VO: explicitly deferred** to the scope decision in `docs/art-budget.md` (cast size and VO scope are budget calls). M0 ships zero VO; the `voBankRef` hook already exists in `CharacterDef` (data-schemas §1.3), so no schema work blocks on it.
