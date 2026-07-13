# Tooling & Pipeline — Content Import, Headless Balance Sim, Unity Bootstrap

Source of truth: `PLAN.md`. Builds on `docs/data-schemas.md` (schemas, asmdefs, determinism contract), `docs/economy-progression.md §8.2` (PI trajectory), `docs/m0-gameplay-spec.md §6` (AI tiers). Spec only — nothing here is implemented yet. Everything is orientation-agnostic; the M0 portrait/landscape A/B (PLAN §2.5) does not touch this doc.

---

## 1. Content Authoring Pipeline

### 1.1 Source-of-truth decision: CSV files in the repo [structural]

Authoring source = CSV files committed under `Assets/Content/CSV/`. Google Sheets is an **optional editing surface**: a sheet may mirror a CSV, and a one-way export script pulls sheet → CSV → PR. The repo CSV is always canonical.

Rejected: live Google Sheets as source of truth.

| Criterion | CSV in repo | Live sheets |
|---|---|---|
| Diffable / PR-reviewable | native `git diff` | opaque; review happens nowhere |
| Deterministic import | same commit ⇒ same assets | sheet can mutate mid-import |
| CI access | no auth, no network | service-account creds in CI, rate limits, outages |
| Bisectable balance regressions | `git bisect` over content | impossible |
| Rollback | `git revert` | manual sheet surgery |
| Concurrent-editor comfort | worse (merge conflicts) | better — recovered via the optional export flow |

The one thing sheets do better (formula-assisted mass editing) is preserved by the export flow; the things git does better are non-negotiable for a balance-sensitive game.

### 1.2 CSV file inventory (one file per def type → data-schemas §1)

One row = one def. Column names = schema field names verbatim. Encodings: lists = `;`-joined (`bond.a;bond.b`); nested structs = numbered column groups (`effect1_primitive, effect1_percent, …, effect2_*`); one-to-many children = child CSV keyed by parent id. All parsing uses invariant culture, UTF-8, LF [structural: byte-stable import].

| CSV | Def (schema §) | Child CSVs |
|---|---|---|
| `characters.csv` | `CharacterDef` (§1.3), embeds `StatBlock` (§1.1) as 6 columns | — |
| `signatures.csv` | `SignatureMoveDef` (§1.4), effect1/effect2 column groups | — |
| `passives.csv` | `PassiveDef` (§1.5) | — |
| `equipment.csv` | `EquipmentDef` (§1.6) | `substat_tables.csv` (`SubstatRollTableDef`, one row per pool row) |
| `equipment_sets.csv` | `EquipmentSetDef` (§1.7) | — |
| `banners.csv` | `BannerDef` (§1.8) | — |
| `stages.csv` | `StageDef` (§1.9) | `stage_drops.csv` (one row per drop-table entry) |
| `chapters.csv` | `StoryChapterDef` (§1.10) | — |
| `drills.csv` | `DailyDrillDef` (§1.11) | — |
| `bonds.csv` | `BondGroupDef` (§1.14) | — |
| `growth_curves.csv` | `GrowthCurveDef` (§1.2), `samples` = `;`-joined ints | — |
| `tactic_profiles.csv` | `TacticProfileDef` (§1.12) | — |
| `opponent_teams.csv` | `OpponentTeamDef` (§1.13), lineup as 6 column pairs | — |
| `trajectories.csv` | `TrajectoryDef` (m0 spec §2.2 params) | — |

### 1.3 Importer design (Unity editor script, `VG.EditorTools`)

Menu item + CLI entry (`-executeMethod VG.EditorTools.ContentImporter.Run`). Reads every CSV, validates the **whole content set as one graph** (§1.4), then regenerates ScriptableObjects under `Assets/Content/Generated/<DefType>/<id>.asset`.

- **Id conventions** — already minted in data-schemas: lowercase dotted, prefixed per type (`char.*`, `sig.*`, `passive.*`, `equip.*`, `set.*`, `banner.*`, `stage.*`, `chapter.*`, `drill.*`, `bond.*`, `curve.*`, `subs.*`, `tactic.*`, `team.*`, `traj.*`). Importer rejects rows whose id prefix doesn't match the file it lives in (Error).
- **Idempotent** [structural]: same CSV bytes ⇒ byte-identical `.asset` files. Achieved by: sorted-by-id write order, fixed field serialization order, invariant-culture parsing, no timestamps/machine info in assets, `;`-list order preserved from CSV.
- **Stable asset GUIDs** [structural]: existing assets are updated **in place** (never delete/recreate), preserving `.meta` GUIDs so cross-asset and scene references survive regeneration. New assets get a deterministic GUID = MD5(`defType + ":" + id`) written into the generated `.meta` — two machines importing the same new row mint the same GUID.
- **Deletions**: a def removed from CSV ⇒ its asset is deleted only after the reference-resolution pass proves nothing references it; otherwise Error (dangling ref, §1.4 #2).
- **Validation is engine-free** [structural]: validators live in a pure-C# `VG.Content.Validation` source folder (referenced by both the editor importer and the CI runner, §2.6), so CI validates content **without launching Unity**.

### 1.4 Validation pass — invariants enforced at import

**Error** = import aborts, zero assets written. **Warning** = printed + report annotation, import proceeds (`--strict` promotes Warnings to Errors on release branches [tunable]).

| # | Invariant (source) | Class |
|---|---|---|
| 1 | Id unique within type; id prefix matches def type; lowercase-dotted grammar | Error |
| 2 | Every cross-reference resolves (`signatureRef`, `setId`, `bondLinkIds`, `trajectoryId`, `stageIds`, `growthCurveRef`, …); no dangling refs after deletions | Error |
| 3 | `char.mc` in NO `BannerDef.poolCharacterIds`; `char.mc.position == S` (schemas §1.3) | Error |
| 4 | `SigEffect` count ∈ [1,2] per signature/passive; each effect populates **only** the params its primitive consumes (schemas §1.4) | Error |
| 5 | Primitive restrictions per def type: passives ⊆ (b)(d)(e)(f); set `contactEffects` ⊆ (b)(d)(f); bond `contactEffects` ⊆ (b)(f) (schemas §1.5/1.7/1.14) | Error |
| 6 | Bond budget: per-group `statMods` total ≤ +3%; `memberCharacterIds.Count ≥ activationThreshold`; members ≥ 2 (schemas §1.14) | Error |
| 7 | Set has ≥ 2 member `EquipmentDef`s across ≥ 2 distinct slots (schemas §1.7) | Error |
| 8 | Substat table: no row duplicates the piece's `mainStat`; `minPct ≤ maxPct`; `(max−min)` divisible by `stepPct`; weights > 0 (schemas §1.6) | Error |
| 9 | Banner math: `softPityStart < hardPity`; `baseSsrRate + srRate + ramp ≤ 1` at every pity count 0..80; featured ∈ pool and rarity SSR; `startUtc < endUtc` (schemas §1.8) | Error |
| 10 | Growth curves: `samples.Length ≥ 2`, monotonic non-decreasing; length == 60 when used as a character stat/xp curve (schemas §1.2) | Error |
| 11 | `StatBlock` fields ∈ [0, 200]; `TacticProfileDef` fields ∈ [0,1] (schemas §1.1/1.12) | Error |
| 12 | `OpponentTeamDef`: exactly one `S` in lineup; libero def position == `L`; bench ≤ 4; `rubberBandProfile` empty unless every referencing stage is a story-chapter stage (schemas §1.13, PLAN §2.6) | Error |
| 13 | Stage: drop weights > 0; story-chapter stages `To15` except finales `To25`, validated against owning chapter (schemas §1.9) | Error |
| 14 | Chapter: `orderIndex` contiguous per arc; `bannerUnlockId` resolves and its featured character appears in the arc's stages (schemas §1.10) | Error |
| 15 | Drill: `targetDurationSec ≤ 60`; exactly one active def per `DrillType` (schemas §1.11) | Error |
| 16 | `hypeCost > 0`; signature (a) effects always `contacts == 1` (schemas §1.4) | Error |
| 17 | Orphan def: nothing references it and it's not a root type (banner/stage/chapter/drill) | Warning |
| 18 | Placeholder `displayName` (contains "placeholder") — fine pre-M4, flagged so StoryBible sweep can find them | Warning |
| 19 | Tunable outside its documented band (e.g. `baseSsrRate` ≠ 0.006±50%, bond statMod near the 3% ceiling, `aiTimingStdDevMs` ≤ 0) | Warning |
| 20 | Addressables ref columns empty (`portraitRef` etc.) — legal through M3 grey-box, noise later | Warning |

**Failure mode [structural]: fail loudly, never write partial.** Validation runs over the complete graph first; any Error ⇒ report **all** errors (not first-fail) and write **nothing** — the `Generated/` folder is only touched after a fully green validation pass. Exit code ≠ 0 for CLI/CI.

### 1.5 Sheets export flow (optional editing surface)

`tools/sheets_pull.py <sheetId> <tab> → Assets/Content/CSV/<file>.csv`, run locally by the editing designer; output goes through a normal PR, where the diff **is** the review. CI never talks to Google. Column-header mismatch vs schema ⇒ script refuses to write.

---

## 2. Headless Balance Simulator

### 2.1 Why engine-free is the point [structural]

Rally resolution, AI, gacha, and economy math are pure C# with injected seeded RNG (PLAN §5.3; data-schemas §3 `VG.Gameplay` pure-sim subfolder, §4 determinism contract; curves are baked int tables, not `AnimationCurve`). Therefore the balance runner is a **plain .NET console app** (`tools/VG.SimRunner/`) that compiles the pure-sim source folders directly (source-level include — asmdefs are Unity-only), plus the engine-free validators from §1.3.

Consequences: no Unity license or ~60s batchmode startup in CI; sweeps parallelize with `dotnet` on any runner; a balance question is answerable in seconds locally; and the runner doubles as the proof that no `UnityEngine` type has leaked into the sim (it simply won't compile if one does).

Content input: the runner parses the **CSVs directly** through the same parse+validate code as the importer — CSV is the source of truth (§1.1), so headless sim never needs `.asset` files.

### 2.2 Player proxy [structural]

Headless matches have no human, so the "player" side executes via the same mechanism as AI: per-contact timing grades sampled from a distribution (m0 spec §6.2), plus a `TacticProfileDef`. Named **skill profiles** [tunable initial values]:

| Profile | P(Perfect) | P(Great) | P(Good) | P(Miss) | Models |
|---|---|---|---|---|---|
| `proxy.casual` | 0.10 | 0.35 | 0.40 | 0.15 | new / distracted player |
| `proxy.median` | 0.22 | 0.42 | 0.28 | 0.08 | tuning baseline |
| `proxy.skilled` | 0.45 | 0.40 | 0.13 | 0.02 | Perfect-heavy play (economy §8.2 "PI − 0.08" clause) |

Proxies use player-side stats/windows exactly (m0 §6.4 hard rule applies in reverse: proxies get no AI-side vocabulary cheats).

### 2.3 Inputs (CLI / config JSON — fully typed)

`vgsim run --config sweep.json` or flags; config echoed into every output.

| Field | Type | Notes |
|---|---|---|
| `playerTeam` | `LineupSpec` | either `{ teamId: string }` (an `OpponentTeamDef` id) or inline `{ lineup: [{ characterId, level }×6], liberoId, equipment?, bonds: auto }` |
| `opponentTeam` | `LineupSpec` | same shape |
| `playerProxy` | `string` | skill profile id (§2.2); omitted ⇒ player side is a plain AI tier |
| `tacticOverride` | `{ side: player\|opponent, tacticId: string }[]` | overrides the team's `tacticProfileRef` |
| `difficultyTier` | `Easy \| Normal \| Hard` | applied to AI side(s) per m0 §6 |
| `seedStart` / `seedCount` | `ulong` / `int` | master seeds `seedStart..seedStart+N−1`, streams derived per data-schemas §4.1 |
| `matches` | `int` | matches per seed (usually 1; seeds are the replication axis) |
| `format` | `To11 \| To15 \| To25` | |
| `piSweep` | `{ min: float, max: float, step: float }?` | optional: rescale player lineup levels to hit each PI point (economy §8.2 definition) and repeat the run per point |
| `out` | `string` | output dir |
| `report` | `json \| md \| both` | default `both` |

### 2.4 Outputs

**JSON** (`results.json`) — machine-consumed by CI gates:

| Field | Type |
|---|---|
| `config` | input echo + simVersion + content git SHA |
| `aggregate.winRate` | `{ mean: float, wilson95: [lo, hi], n: int }` |
| `aggregate.rallyLength` | `{ histogram: int[], median: float, p90: float }` (contacts per rally) |
| `aggregate.gradeDist` | per side × per contact type (Serve/Receive/Set/Spike/Block) → `{ Perfect, Great, Good, Miss: float }` |
| `aggregate.receiveGrades` | per side → `{ S, A, B, C, Shank: float }` |
| `aggregate.hype` | `{ ignitionsPerMatch: float, peakMean: float }` per side |
| `aggregate.pi` | `{ player: float, opponent: float }` (computed per economy §8.2) |
| `piCurve` | when `piSweep` set: `[{ pi, winRate, wilson95 }]` — the PI-vs-outcome curve |
| `perSeed` | optional (`--keep-per-seed`): `[{ masterSeed, score, rallyCount, durationTicks }]` — any row is replayable by re-running that seed |
| `suiteVerdicts` | `[{ suite: a\|b\|c\|d, pass: bool, detail }]` when run via §2.5 |

**Markdown** (`report.md`) — human review artifact: win-rate table with CIs, rally-length histogram (ASCII), grade-distribution tables, PI curve table, red/green suite verdicts. Attached to PRs by CI (§2.6).

### 2.5 Standing validation suites

All bands are **initial targets [tunable]** unless marked; CI compares the Wilson 95% CI against the band — pass requires the CI to overlap the band and the point estimate to sit inside it.

**(a) AI tier calibration** — `proxy.median` player at the on-curve PI for each tier's story placement (economy §8.2), vs reference lineups per tier, `To15`, N = 1,000 seeds/cell:

| Tier | Reference opponent | Player PI | Target win-rate band [tunable] |
|---|---|---|---|
| Easy | `team.opp_arc1_practice`-class | 0.23 (D1 row) | 80–92% |
| Normal | mid-bracket-class | 0.38 | 55–72% |
| Hard | finale-class | 0.50 | 32–48% |

Also asserted: tier ordering is strict (winRate(Easy) > winRate(Normal) > winRate(Hard) with non-overlapping CIs) [structural — tiers must be distinguishable, PLAN §6 M1 gate].

**(b) Economy validation** — re-checked on every content import. For each economy §8.2 trajectory row, run its story gate stage:

| §8.2 row | Player PI | Gate PI | Check 1: `proxy.median` @ listed PI | Check 2: `proxy.skilled` @ PI − 0.08 |
|---|---|---|---|---|
| D1 | 0.23 | 0.20 | win ≥ 60% [tunable] | win ≥ 30% [tunable] |
| D7 | 0.30 | 0.28 | ≥ 55% | ≥ 30% |
| D30 | 0.42 | 0.38 | ≥ 55% | ≥ 30% |
| D45–60 | 0.50 | 0.45 | ≥ 55% | ≥ 30% |

Check 2 is the **no-hard-wall guarantee** (economy §8.2 [structural]): every gate clearable at PI − 0.08 with Perfect-heavy play. N = 500 seeds/row. A new/edited stage joins the table by its chapter position.

**(c) Mirror-match bias check [structural]** — identical lineup, tactic, tier on both sides; serve/side assignment alternated across seeds; N = 2,000 seeds; per format (To11/To15/To25). Pass: win rate CI contains 0.50 **and** |point − 0.50| ≤ 3 pp [tunable width, structural check]. Catches side bias, serve-order bias, and asymmetric tie-breaks in the §3.6 resolution table (the `Resolution_OutcomeTable` mirror-case bug class from data-schemas §5.1).

**(d) Degenerate-strategy sweep** — every `TacticProfileDef` vs every other (round-robin matrix), equal stats, Normal-tier execution both sides, N = 500 seeds/pairing. Flag: any profile whose **row mean win rate ≥ 58%** [tunable], or that beats every other profile with CI fully above 0.50 (a dominant strategy — meta-killing). Report emits the full matrix; flagged rows fail the suite.

### 2.6 CI wiring [tunable cadence]

| Trigger | Runs | Budget |
|---|---|---|
| PR touching `Assets/Content/CSV/**` | engine-free validation (§1.4) + suites (b) on changed stages + (c) quick (N=500, To15 only) | < 5 min, report artifact + PR comment |
| PR touching sim code (`VG.Gameplay` pure folders) | (a) + (c) quick + (d) quick (N=200/pairing) | < 10 min |
| Nightly | full sweep: (a)–(d) at full N, all formats, `piSweep` 0.15–0.60 step 0.05 | unbounded; report published as build artifact |

Suite failure on PR = red check; nightly failure = issue filed with the offending `masterSeed`s (each verdict is replayable, §2.4 `perSeed`).

---

## 3. Unity Project Bootstrap Checklist

Ordered; each step gates the next.

### 3.1 Version pin

- **Unity 6 LTS, `6000.0` stream, latest patch at project creation** (e.g. `6000.0.xxf1`); record in `ProjectSettings/ProjectVersion.txt` (automatic) and pin the CI image tag to the **exact same string** (§3.6).
- Patch upgrades only at milestone boundaries; never mid-M0 (feel tuning must not chase engine diffs). Stream upgrades (6000.1+ / next LTS) are an explicit decision gate, not routine.

### 3.2 Packages (install at creation)

| Package | Why (one line) |
|---|---|
| `com.unity.render-pipelines.universal` (URP) | Toon/cel look + mobile perf, per PLAN §5.1 — create project from URP template |
| `com.unity.inputsystem` | Touch gestures, input-thread timestamps required by m0 spec §7.2 |
| `com.unity.cinemachine` (3.x) | Camera director vocabulary — spike cut-ins, net cams, punch-ins (PLAN §5.1) |
| `com.unity.timeline` | Signature-move cut-in sequences (`cutInTimelineRef`, schemas §1.4) |
| `com.unity.addressables` | Live content delivery — banners/story without store review (PLAN §5.1) |
| `com.unity.test-framework` | EditMode/PlayMode inventory in data-schemas §5 |
| UniVRM (`com.vrmc.gltf` + `com.vrmc.univrm` via git UPM, pin a release tag) | VRoid → VRM character import (PLAN §5.2) |
| **PrimeTween** (chosen over DOTween [tunable]) | Juice/UI tweening; zero-allocation, free, no scene singletons — swap cost is low if animators prefer DOTween's ecosystem |
| `com.unity.nuget.newtonsoft-json` | Save JSON + replay blobs (schemas §2.1/§4.3) with dictionary/GUID fidelity |

Deliberately **not** installed now: Unity IAP, analytics SDKs — M4/M5 backend gate (PLAN §5.4).

### 3.3 Git hygiene

`.gitignore` essentials (Unity-standard):

```
[Ll]ibrary/  [Tt]emp/  [Oo]bj/  [Bb]uild/  [Bb]uilds/  [Ll]ogs/
[Uu]serSettings/  MemoryCaptures/  Recordings/
*.csproj  *.sln  *.user  .vs/  .idea/  .utmp/
crashlytics-build.properties
# Addressables local build output (rebuilt from content)
/[Aa]ssets/AddressableAssetsData/*/link.xml
/ServerData/
```

`git lfs track` list (binary assets; run **before** first asset commit):

```
*.fbx *.vrm *.blend                       # models
*.png *.jpg *.tga *.psd *.exr *.hdr      # textures & source art
*.wav *.mp3 *.ogg *.aif                  # audio
*.mp4 *.mov                              # video
*.ttf *.otf                              # fonts
```

`.asset`, `.anim`, `.unity`, `.prefab`, `.meta` stay in plain git — they are YAML under §3.4 and their diffs are the review surface (incl. generated content, §1.3).

### 3.4 EditorSettings [structural for reviewable diffs]

- **Asset Serialization → Force Text** — every scene/prefab/SO diffs as YAML.
- **Version Control → Visible Meta Files** — GUID stability under git; prerequisite for the importer's stable-GUID rule (§1.3).
- Line endings: LF, enforced via `.gitattributes` (`* text=auto eol=lf` + LFS lines above).
- Optional perf: Enter Play Mode Options with domain-reload off — allowed only if all sim statics stay zero (data-schemas §4.1 already mandates no statics).

### 3.5 Assembly creation order (per data-schemas §3)

1. `VG.Data` (no refs — leaf)
2. `VG.Gameplay` (→ Data; pure-sim subfolder has zero `UnityEngine` types)
3. `VG.Meta` (→ Data; **never** → Gameplay [structural])
4. `VG.UI` (→ Meta, Data)
5. `VG.EditorTools` (Editor platform only; → Data; hosts the importer §1.3)
6. `VG.Tests.EditMode`, `VG.Tests.PlayMode` (→ all)

Plus, outside `Assets/`: `tools/VG.SimRunner/` console csproj source-including the pure-sim + validation folders (§2.1). Create asmdefs **before** writing code — retrofitting reference edges onto Assembly-CSharp is how illegal edges (UI→Gameplay) sneak in.

### 3.6 CI sketch (GitHub Actions + game-ci) — implementation later

| Job | Trigger | Steps | Notes |
|---|---|---|---|
| `content-validate` | PR touching `Assets/Content/CSV/**` | `dotnet run` engine-free validators (§1.4) | seconds; no Unity, no license |
| `sim-suites` | per §2.6 triggers | `dotnet run` VG.SimRunner; upload `report.md` artifact + PR comment | no Unity |
| `test` | every PR | game-ci `unity-test-runner`: EditMode + PlayMode (data-schemas §5) | Unity image tag pinned to §3.1 version; license via `UNITY_LICENSE` secret |
| `build` | manual dispatch / version tag | game-ci `unity-builder`: iOS + Android dev builds | not on PRs (slow); Library cache keyed on `ProjectVersion.txt` + `Packages/packages-lock.json` |
| `nightly-sweep` | cron | full §2.6 nightly sim sweep | artifact retention 30 days [tunable] |

---

Cross-doc: schema invariants enforced here are defined in `docs/data-schemas.md §1`; PI definition and trajectory in `docs/economy-progression.md §8.2`; AI tier mechanics in `docs/m0-gameplay-spec.md §6`; store/compliance CI additions → `docs/compliance-localization.md`.
