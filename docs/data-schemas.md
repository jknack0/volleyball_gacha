# Data Schemas, Assemblies, Determinism & Test Inventory

Source of truth: `PLAN.md`. Vocabulary per wave-2 contract. Spec only — no code files exist yet.
Conventions: asset ids are lowercase dotted strings (`char.rival_ace_01`), unique per type. All defs are ScriptableObjects in `VG.Data`. Display strings/names here are placeholders — StoryBible owns naming/tone. Reward/cost *amounts* are placeholders — EconomyWorkbook owns values.
Localization [structural, per `docs/compliance-localization.md` §1]: `LocKey` = `string` alias naming an entry in the Unity Localization string tables. Every user-facing display field below is a `LocKey`; internal `id` fields stay plain strings. Dev-time fallback: a missing table entry renders the key text itself, so EN-only authoring stays frictionless — author the EN copy as the key's value and move on.

Shared enums (verbatim, [structural]):

| Enum | Values |
|---|---|
| `Position` | `S, OH, MB, OP, L` |
| `Playstyle` | `Power, Quick, Technique, Guts` |
| `Rarity` | `R, SR, SSR` |
| `EquipSlot` | `Shoes, Kneepads, Jersey, Accessory` |
| `TimingGrade` | `Perfect, Great, Good, Miss` |
| `ReceiveGrade` | `S, A, B, C, Shank` |
| `MatchFormat` | `To11, To15, To25` (quick / story / finale) |
| `StatId` | `Power, Jump, Technique, Serve, Receive, Speed` (+ `Stamina` behind M1 experiment flag; no schema below requires it [structural]) |
| `CurrencyId` | `gems_free, gems_paid, coins, training_points` |
| `SigPrimitive` | `GuaranteedTimingGrade` (a), `TimingWindowAdjust` (b), `TrajectoryOverride` (c), `OpponentContactDebuff` (d), `HypeDelta` (e), `TeamQualityBuff` (f) — closed set, verbatim from contract [structural] |
| `DifficultyTier` | `Easy, Normal, Hard` (per `docs/m0-gameplay-spec.md`) |
| `ZoneId` | `z_LF, z_CF, z_RF, z_LM, z_CM, z_RM, z_LB, z_CB, z_RB` — 3×3 court grid, cols L/C/R × rows F/M/B (per m0 spec) |

---

## 1. Asset Schemas (design-time)

### 1.1 `StatBlock` (serializable struct, not an SO)

| Field | Type | Notes |
|---|---|---|
| `power, jump, technique, serve, receive, speed` | `int` each | Raw meta-layer values, range 0..200 (scale owned by `docs/economy-progression.md`) [tunable] |

Invariants: each stat in `[0, STAT_RAW_MAX]`; `STAT_RAW_MAX = 200` (per economy doc) [tunable].
Normalization rule [structural — the meta layer owns raw→normalized; gameplay math consumes only 0..1]:
`normalized = clamp01(raw / STAT_RAW_MAX)`. Gameplay never sees raw ints.

```yaml
# embedded in CharacterDef below
power: 124
jump: 108
technique: 62
serve: 96
receive: 70
speed: 100
```

### 1.2 `GrowthCurveDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `samples` | `int[]` | Value per level/step, index 0 = level 1. Baked integer table — no `AnimationCurve` in the sim path [structural: keeps deterministic core engine-free] |

Invariants: `samples.Length >= 2`; monotonic non-decreasing; `samples.Length == 60` when used as a character stat/xp curve (level cap 60 [structural]).

```yaml
id: curve.stat_std_ssr
samples: [40, 42, 44, ..., 198, 200]   # 60 entries, monotonic
```

### 1.3 `CharacterDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | unique |
| `displayName` | `LocKey` | StoryBible owns final names |
| `position` | `Position` | |
| `playstyle` | `Playstyle` | |
| `rarity` | `Rarity` | |
| `baseStats` | `StatBlock` | value at level 1 |
| `growthCurveRef` | `GrowthCurveDef` | per-level stat scale, applied uniformly [tunable: may split per-stat later] |
| `signatureRef` | `SignatureMoveDef` | exactly one |
| `passiveRef` | `PassiveDef` | exactly one; may be a shared no-op def for low-rarity R units [tunable] |
| `bondLinkIds` | `string[]` | ids of bond groups (`bond.*`), not character ids; same-group characters in lineup grant synergy [structural] |
| `portraitRef` | `AssetReferenceSprite` | Addressables |
| `modelRef` | `AssetReference` | VRoid/VRM prefab |
| `voBankRef` | `AssetReference` | VO event bank |

Invariants: `id` unique; MC (`char.mc`) MUST NOT appear in any `BannerDef.pool` and has `position: S` [structural — MC is the team's setter, not in gacha]; bond ids must resolve to a declared bond group.

```yaml
id: char.rival_ace_01
displayName: "Rival Ace (placeholder)"
position: OH
playstyle: Power
rarity: SSR
baseStats: { power: 124, jump: 108, technique: 62, serve: 96, receive: 70, speed: 100 }
growthCurveRef: curve.stat_std_ssr
signatureRef: sig.off_the_tape
passiveRef: passive.clutch_server
bondLinkIds: [bond.rival_school_01]
portraitRef: addr/portraits/rival_ace_01
modelRef: addr/models/rival_ace_01
voBankRef: addr/vo/rival_ace_01
```

### 1.4 `SignatureMoveDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `displayName` | `LocKey` | |
| `hypeCost` | `int` | 0..100; spends team Hype [structural mechanism, value tunable] |
| `effects` | `List<SigEffect>` | 1–2 entries, contract primitives only [structural] |
| `levelScalingRef` | `GrowthCurveDef` | scales effect magnitude by signature level 1–10 |
| `cutInTimelineRef` | `AssetReference` | Unity Timeline, 1–2s cut-in |

`SigEffect` (serializable struct):

| Field | Type | Notes |
|---|---|---|
| `primitive` | `SigPrimitive` | a–f |
| `grade` | `TimingGrade` | used by (a) only |
| `percent` | `float` | used by (b) ±window, (f) +quality; signed |
| `contacts` | `int` | duration in contacts, used by (b)(d)(f); (a) is always 1 contact |
| `trajectoryId` | `string` | used by (c); names an authored `TrajectoryDef` arc asset — fields `{start, end, h_apex, u_apex, T, ease, wobble_seed?}` per `docs/m0-gameplay-spec.md`; endpoints in court zones `z_LF..z_RB` |
| `hypeDelta` | `int` | used by (e); signed (drain = negative on opponent) |

Invariants: `effects.Count ∈ [1,2]`; each effect populates only the params its primitive consumes (editor validation); `hypeCost > 0`.

```yaml
id: sig.off_the_tape
displayName: "Off the Tape (placeholder)"
hypeCost: 40            # [tunable]
effects:
  - { primitive: TrajectoryOverride, trajectoryId: traj.tape_wipe }
  - { primitive: OpponentContactDebuff, percent: -15, contacts: 1 }   # [tunable]
levelScalingRef: curve.sig_scale_std
cutInTimelineRef: addr/cutins/off_the_tape
```

### 1.5 `PassiveDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `trigger` | `PassiveTrigger` | `MatchStart, RallyWon, RallyLost, PerfectContact, IgnitionStart, ScoreDeficit` [structural set, extend deliberately] |
| `effects` | `List<SigEffect>` | reuses primitives; restricted to (b)(d)(e)(f) — passives never grant guaranteed grades or authored trajectories [structural: keeps ults special] |
| `procLimitPerSet` | `int` | 0 = unlimited [tunable] |

Invariants: primitive restriction above; effects.Count ∈ [1,2].

```yaml
id: passive.clutch_server
trigger: ScoreDeficit
effects:
  - { primitive: TimingWindowAdjust, percent: 10, contacts: 2 }   # own serves only, [tunable]
procLimitPerSet: 2
```

### 1.6 `EquipmentDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `slot` | `EquipSlot` | |
| `rarity` | `Rarity` | R / SR / SSR (per economy doc; replaces numeric tiers) |
| `setId` | `string` | → `EquipmentSetDef.id`; empty = setless |
| `mainStat` | `StatId` | deterministic — fixed by `slot` mapping (Accessory: fixed by `setId`) per economy doc, never rolled [structural] |
| `mainStatCurveRef` | `GrowthCurveDef` | main stat value by upgrade level 0..+12 |
| `substatTableRef` | `SubstatRollTableDef` | light RNG at acquisition, `substats` RNG stream |
| `iconRef` | `AssetReferenceSprite` | |

`SubstatRollTableDef` (aux SO): `id`, `rows: List<{ statId: StatId, minPct: float, maxPct: float, stepPct: float, weight: int }>` — percent bonuses +2..4% in 0.5% steps (5 discrete values) per economy doc; pool = the 6 stats minus the piece's `mainStat`; `rollCountByRarity: { R: 1, SR: 2, SSR: 3 }` (substats granted at acquisition, per economy doc). Invariants: `minPct <= maxPct`; `(maxPct − minPct)` divisible by `stepPct`; weights > 0; no row duplicating `mainStat`.

```yaml
id: equip.featherlight_shoes_ssr
slot: Shoes
rarity: SSR
setId: set.featherlight
mainStat: Jump
mainStatCurveRef: curve.equip_main_ssr
substatTableRef: subs.table_ssr
iconRef: addr/icons/featherlight_shoes
```

### 1.7 `EquipmentSetDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `displayName` | `LocKey` | |
| `bonus2pc` | `SetBonus` | |
| `bonus4pc` | `SetBonus` | |

`SetBonus`: `statMods: List<{ statId: StatId, percent: float }>` (always-on raw-stat % — lands upstream in the meta layer's raw→normalized mapping, never inside rally formulas, per m0 spec §3) **and/or** `contactEffects: List<SigEffect>` restricted to (b)(d)(f) with a condition tag (`onQuickAttack`, `onServe`, …) [structural: conditional set effects compile to the same primitives the sim already resolves — window mods multiply m0 §3.1 `ctx`, quality mods apply post-formula clamped to [0,1] per m0 §3.2].

Invariants: a set has ≥ 2 member `EquipmentDef`s across ≥ 2 slots; 4pc implies the 2pc also active; the same set counted once (no 4pc = 2×2pc).

```yaml
id: set.featherlight
displayName: "Featherlight (placeholder)"
bonus2pc:
  statMods: [{ statId: Jump, percent: 8 }]          # [tunable]
bonus4pc:
  contactEffects:
    - { primitive: TeamQualityBuff, percent: 15, contacts: 1, condition: onQuickAttack }  # [tunable]
```

### 1.8 `BannerDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `featuredCharacterId` | `string` | must be `SSR` |
| `poolCharacterIds` | `string[]` | full pull pool incl. featured; MUST NOT contain `char.mc` [structural] |
| `startUtc` / `endUtc` | `string` (ISO-8601) | schedule window |
| `costSingle` / `costMulti` | `int` | 160 / 1600 `gems_free`-or-`gems_paid` [structural per contract] |
| `baseSsrRate` | `float` | 0.008 [tunable] — mirrors economy §3.1 (derivations live there); published in-UI (compliance) |
| `srRate` | `float` | 0.080 [tunable] — economy §3.1 |
| `softPityStart` | `int` | 65 [structural] |
| `softPityRampPerPull` | `float` | +0.05 SSR rate per pull past start [tunable] — economy §3.1 |
| `hardPity` | `int` | 80 [structural] |
| `featuredOdds` | `float` | 0.5 (50/50) with guarantee after loss [structural] |

Invariants: `softPityStart < hardPity`; rates sum ≤ 1 at every pity count; featured ∈ pool; pool ids all gacha-eligible (rarity set, not MC).

```yaml
id: banner.rival_ace_debut
featuredCharacterId: char.rival_ace_01
poolCharacterIds: [char.rival_ace_01, char.libero_02, char.mb_03, ...]
startUtc: 2026-09-01T04:00:00Z
endUtc: 2026-09-15T03:59:59Z
costSingle: 160
costMulti: 1600
baseSsrRate: 0.008
srRate: 0.080
softPityStart: 65
softPityRampPerPull: 0.05
hardPity: 80
featuredOdds: 0.5
```

### 1.9 `StageDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `opponentTeamRef` | `OpponentTeamDef` | |
| `matchFormat` | `MatchFormat` | To11 / To15 / To25 |
| `firstClearRewards` | `List<Reward>` | `Reward = { kind: Currency\|Item\|Equipment\|Character, refId: string, qty: int }` |
| `repeatDropTable` | `List<{ reward: Reward, weight: int }>` | rolled on the `Gacha` RNG stream — see §4 [structural: all player-economy RNG on one auditable stream] |
| `autoPlayRule` | struct | `{ unlock: ARankManualClear, manualBonusPct: 20, skipTicketsAfterStars: 3 }` — first two [structural per contract], third [tunable] |
| `manualGradeCapOnAuto` | `string` | e.g. auto capped at "A" for story/event stages [tunable] |

Invariants: drop weights > 0; story-chapter stages use `To15` except finales `To25` (validated against owning `StoryChapterDef`).

```yaml
id: stage.arc1_practice_03
opponentTeamRef: team.opp_arc1_practice
matchFormat: To11
firstClearRewards: [{ kind: Currency, refId: gems_free, qty: 50 }]   # amount → economy doc
repeatDropTable:
  - { reward: { kind: Equipment, refId: equip.featherlight_shoes_ssr, qty: 1 }, weight: 10 }
  - { reward: { kind: Currency, refId: coins, qty: 400 }, weight: 90 }
autoPlayRule: { unlock: ARankManualClear, manualBonusPct: 20, skipTicketsAfterStars: 3 }
manualGradeCapOnAuto: A
```

### 1.10 `StoryChapterDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `arcId` | `string` | |
| `orderIndex` | `int` | unique within arc |
| `sceneRefs` | `AssetReference[]` | VN-lite scene assets (dialogue content = StoryBible territory); dialogue lines inside scene assets are `LocKey`-based |
| `stageIds` | `string[]` | matches in this chapter, played in order |
| `unlockRequirement` | `string` | prior chapter id or empty (first) |
| `bannerUnlockId` | `string` | banner opened on completion; empty if none [structural: story feeds gacha] |

Invariants: `orderIndex` contiguous per arc; `stageIds` non-empty for match chapters; `bannerUnlockId` resolves to a `BannerDef` whose featured character appears in this arc.

```yaml
id: chapter.arc1_ch05
arcId: arc.01
orderIndex: 5
sceneRefs: [addr/scenes/arc1_ch05_pre, addr/scenes/arc1_ch05_post]
stageIds: [stage.arc1_rival_match]
unlockRequirement: chapter.arc1_ch04
bannerUnlockId: banner.rival_ace_debut
```

### 1.11 `DailyDrillDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `displayName` | `LocKey` | drill name shown in dailies UI |
| `drillType` | `DrillType` | `JumpTraining, ServePractice, SpikeDrill, ReceiveDrill` [structural] |
| `statTrained` | `StatId` | Jump / Serve / Power / Receive respectively |
| `xpCurveRef` | `GrowthCurveDef` | MC xp per score grade |
| `dailyCap` | `int` | attempts/day, 3 [tunable] |
| `targetDurationSec` | `int` | ≤ 60 [structural per PLAN §1.4] |
| `trainingPointsReward` | `int` | `training_points` for gacha units; amount → economy doc |

Invariants: `targetDurationSec <= 60`; one def per `drillType` active at a time.

```yaml
id: drill.jump_training
drillType: JumpTraining
statTrained: Jump
xpCurveRef: curve.mc_drill_xp
dailyCap: 3
targetDurationSec: 45
trainingPointsReward: 10   # [tunable]
```

### 1.12 `TacticProfileDef` (AI weights; reused by ghost PvP in V1.x)

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `serveAggression` | `float` 0..1 | aim-near-lines frequency |
| `quickAttackBias` | `float` 0..1 | prefer quick middle when lit |
| `lineShotBias` | `float` 0..1 | line vs cross on spikes |
| `setterDumpRate` | `float` 0..1 | |
| `blockCommitBias` | `float` 0..1 | commit-early vs read |
| `targetWeakReceiverBias` | `float` 0..1 | serve/spike targeting |
| `riskTolerance` | `float` 0..1 | grade-gamble willingness |

Invariants: all fields in `[0,1]`. Timing skill is NOT here — it lives on `OpponentTeamDef` (difficulty), so a ghost-PvP player tactic never carries an execution-skill cheat [structural].

```yaml
id: tactic.balanced_default
serveAggression: 0.4
quickAttackBias: 0.5
lineShotBias: 0.5
setterDumpRate: 0.1
blockCommitBias: 0.5
targetWeakReceiverBias: 0.3
riskTolerance: 0.5      # all [tunable]
```

### 1.13 `OpponentTeamDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | |
| `displayName` | `LocKey` | |
| `lineup` | `LineupEntry[6]` | `LineupEntry = { characterId: string, levelOverride: int }` — rotation order 1..6 |
| `liberoCharacterId` | `string` | position `L`; auto-swap per contract |
| `benchCharacterIds` | `string[]` | ≤ 4 [tunable] |
| `tacticProfileRef` | `TacticProfileDef` | |
| `difficultyTier` | `DifficultyTier` | `Easy, Normal, Hard` (per m0 spec) [structural count, tunable membership] |
| `aiTimingMeanMs` / `aiTimingStdDevMs` | `float` | AI timing-grade distribution — difficulty = tighter distribution, never player input latency [structural per PLAN §2.6] |
| `rubberBandProfile` | `string` | empty = none; story-only drama lever, PROHIBITED on ranked/event stages [structural] |

Invariants: exactly one `S` in lineup; libero def has `position: L`; front/back legality derivable from rotation order.

```yaml
id: team.opp_arc1_practice
displayName: "Practice Squad (placeholder)"
lineup:
  - { characterId: char.npc_s_01,  levelOverride: 8 }
  - { characterId: char.npc_oh_01, levelOverride: 8 }
  - { characterId: char.npc_mb_01, levelOverride: 8 }
  - { characterId: char.npc_op_01, levelOverride: 8 }
  - { characterId: char.npc_oh_02, levelOverride: 8 }
  - { characterId: char.npc_mb_02, levelOverride: 8 }
liberoCharacterId: char.npc_l_01
benchCharacterIds: []
tacticProfileRef: tactic.balanced_default
difficultyTier: Easy
aiTimingMeanMs: 55
aiTimingStdDevMs: 40    # [tunable]
rubberBandProfile: ""
```

---

### 1.14 `BondGroupDef`

| Field | Type | Notes |
|---|---|---|
| `id` | `string` | `bond.*` — the groups referenced by `CharacterDef.bondLinkIds` (§1.3) |
| `displayName` | `LocKey` | comedic theme name → story bible |
| `memberCharacterIds` | `string[]` | ≥ 2; MAY include `char.mc` |
| `activationThreshold` | `int` | members required among slots+libero, default 2 — resolution rule lives in `LineupState` §2.7 [tunable] |
| `bonus` | `SetBonus` | reuses §1.7 encoding verbatim; amounts → economy doc §4.6 |

Invariants: `memberCharacterIds.Count >= activationThreshold`; every member id exists; `bonus.contactEffects` restricted to (b)(f) — bonds never debuff opponents; keeps bond math lineup-local [structural]; per-group `statMods` total ≤ +3%, lineup-wide bond contribution capped per economy §4.6 [structural budget].

```yaml
id: bond.rival_school_01
displayName: "Former Teammates"        # fiction → story bible
memberCharacterIds: [char.rival_ace_01, char.rival_setter_01]
activationThreshold: 2
bonus:
  statMods: [{ statId: Power, percent: 2 }, { statId: Receive, percent: 1 }]   # [tunable, ≤3% budget]
```

## 2. Runtime State Schemas (serialized to local JSON through M4)

### 2.1 `SaveGame`

| Field | Type | Notes |
|---|---|---|
| `schemaVersion` | `int` | starts at 1, bump on any breaking shape change |
| `createdUtc` / `lastSavedUtc` | `string` ISO-8601 | |
| `playerId` | `string` (GUID) | |
| `currencies` | `Dictionary<CurrencyId, long>` | the four canonical ids only |
| `characters` | `List<CharacterInstance>` | |
| `equipment` | `List<EquipmentInstance>` | |
| `inventory` | `Inventory` | |
| `pity` | `Dictionary<string, PityState>` | key = banner id [structural: per-banner] |
| `mcTraining` | `MCTrainingState` | |
| `lineup` | `LineupState` | |
| `stageProgress` | `Dictionary<string, StageProgress>` | `StageProgress = { bestGrade: string, stars: int, autoUnlocked: bool, clearCount: int }` |
| `storyProgress` | `Dictionary<string, bool>` | chapter id → completed |
| `matchHistory` | `List<MatchResult>` | ring buffer, last 20 [tunable] |

Migration [structural]: single-step migrations `v(n)→v(n+1)` applied in order, never skipped; pre-migration file copied to `save.bak.v{n}.json` before applying; unknown JSON fields dropped only after successful migration; a save newer than the client's max version is read-only rejected (prompt update).

### 2.2 `CharacterInstance`

| Field | Type | Invariants |
|---|---|---|
| `instanceId` | `string` GUID | unique |
| `defId` | `string` | resolves to `CharacterDef` |
| `level` | `int` | 1..60 [structural cap] |
| `xp` | `long` | < xp-to-next at current level |
| `lbStars` | `int` | 0..5 [structural] |
| `signatureLevel` | `int` | 1..10 [structural]; advancement costs → economy doc |
| `equipped` | `Dictionary<EquipSlot, string>` | value = `EquipmentInstance.instanceId` or absent; an item instance equipped by at most one character (global invariant) |

### 2.3 `EquipmentInstance`

| Field | Type | Invariants |
|---|---|---|
| `instanceId` | `string` GUID | unique |
| `defId` | `string` | → `EquipmentDef` |
| `upgradeLevel` | `int` | 0..12 (per economy doc) |
| `rolledSubstats` | `List<{ statId: StatId, pct: float }>` | rolled once at acquisition via `substats` stream; count/bounds per def's `SubstatRollTableDef`; immutable thereafter [structural: no substat re-roll hell, per economy doc] |
| `locked` | `bool` | blocks salvage |

### 2.4 `Inventory`

| Field | Type | Notes |
|---|---|---|
| `lbShards` | `Dictionary<string, int>` | key = character def id; per-character shards [structural] |
| `materials` | `Dictionary<string, int>` | crafting mats, skip tickets, etc. |
| `craftingPity` | `Dictionary<string, int>` | key = craft recipe id → attempts since top-tier result [structural mechanism per PLAN §3.3] |

(Currency balances live on `SaveGame.currencies`; equipment instances on `SaveGame.equipment` — no duplication.)

### 2.5 `PityState` (one per banner)

| Field | Type | Invariants |
|---|---|---|
| `pullsSinceSsr` | `int` | 0..80; reset to 0 on any SSR |
| `featuredGuarantee` | `bool` | set when 50/50 lost; cleared when featured granted [structural] |
| `lifetimePulls` | `int` | monotonic; UI rate-disclosure counter |

### 2.6 `MCTrainingState`

| Field | Type | Notes |
|---|---|---|
| `statXp` | `Dictionary<StatId, long>` | permanent MC growth |
| `todayUtcDate` | `string` `yyyy-MM-dd` | rollover key |
| `drillAttemptsToday` | `Dictionary<DrillType, int>` | ≤ each drill's `dailyCap` |
| `streakDays` | `int` | missed day resets streak, never removes gains [structural] |
| `softCap` | `Dictionary<StatId, { tier: int, xpIntoTier: long }>` | per-stat diminishing-returns schedule; tier thresholds → economy doc |

### 2.7 `LineupState`

| Field | Type | Invariants |
|---|---|---|
| `slots` | `string[6]` | `CharacterInstance.instanceId`, rotation order 1..6; exactly one instance whose def position is `S`; no duplicate instances |
| `liberoInstanceId` | `string` | def position `L`; auto-swap handled by sim |
| `benchInstanceIds` | `string[]` | ≤ 4 [tunable]; no overlap with slots/libero |
| `resolvedBonds` | `List<{ bondId: string, memberInstanceIds: string[], active: bool }>` | DERIVED — recomputed on any lineup/roster change, persisted only as a display cache, never authoritative [structural] |

Bond-link resolution rule: a bond group is `active` when ≥ 2 of its members are in `slots`/libero (bench excluded) [tunable threshold].

### 2.8 `MatchResult`

| Field | Type | Notes |
|---|---|---|
| `matchId` | `string` GUID | |
| `stageId` | `string` | empty for Quick Match |
| `format` | `MatchFormat` | |
| `seedSet` | `SeedSet` | see §4 — enables replay |
| `finalScore` | `{ player: int, opponent: int }` | |
| `rallyCount` | `int` | |
| `durationTicks` | `long` | fixed-timestep ticks |
| `grade` | `string` | S/A/B/C reward grade |
| `manualPlay` | `bool` | drives +20% reward rule |
| `hypePeak` | `{ player: int, opponent: int }` | analytics |
| `rewardsGranted` | `List<Reward>` | as granted, post-bonus |
| `replayRef` | `string` | path/addressable of replay blob, optional |

---

## 3. Assembly Layout (asmdefs, per PLAN §5.3)

| Asmdef | Contents | References | Rationale |
|---|---|---|---|
| `VG.Data` | Enums, StatBlock, all SO defs, shared result/event types (`MatchResult`, gameplay event interfaces) | (engine only) | Leaf everyone shares; putting shared runtime types here is what lets Gameplay and Meta never see each other |
| `VG.Gameplay` | Rally state machine, contacts, ball physics, AI, camera director, deterministic sim core | `VG.Data` | The product; pure-C# sim subfolder has zero UnityEngine types so it can move server-side |
| `VG.Meta` | Gacha, pity, inventory, progression, dailies, economy, save/migration | `VG.Data` | **NEVER references `VG.Gameplay`** [structural] — consumes `MatchResult` via `VG.Data`, so meta churn can't destabilize the sacred rally |
| `VG.UI` | Menus, HUD, lineup builder, banner screens | `VG.Meta`, `VG.Data` | UI is a projection of meta+data state; HUD binds to gameplay via event interfaces declared in `VG.Data`, keeping UI off the sim's dependency graph |
| `VG.Tests.EditMode` | Deterministic core tests (§5) | all of the above | Must reach every pure function |
| `VG.Tests.PlayMode` | Rally sims, AI sanity, replay determinism | all of the above | Needs scenes + fixed-timestep loop |

Dependency direction (→ = "references"): `UI → Meta → Data ← Gameplay`; `Tests → all`. Any edge not listed is PROHIBITED (enforced by asmdef references being exhaustive).

---

## 4. Determinism Contract

### 4.1 Seeded RNG

```csharp
public interface IRng {
    uint NextUInt();
    int NextInt(int minInclusive, int maxExclusive);
    float NextFloat01();
}
public enum RngStream { Gacha, Rally, Ai, Substats }
public interface IRngSource { IRng Get(RngStream stream); }
```

- PRNG: xoshiro128** seeded via splitmix64 [structural: engine-independent, `UnityEngine.Random` PROHIBITED in `VG.Data`/sim code].
- `SeedSet = { version: int, master: ulong, gacha: ulong, rally: ulong, ai: ulong, substats: ulong }` — per-stream seeds derived `stream = splitmix64(master ^ hash(streamName))`.
- **Named streams are the point [structural]:** one system's consumption never shifts another's sequence — a UI-driven extra gacha roll cannot change the next rally, and inserting an AI decision cannot move the next substat roll. Stage drop rolls consume the `Gacha` stream (all player-economy RNG on one auditable stream).
- All rally resolution and gacha rolls are pure C# taking `IRng` by injection; no statics, no time-based seeding in the sim.

### 4.2 Float policy (rally math)

- Plain C# `float` + fixed timestep (60 Hz sim tick [structural]) — bit-exact on a single device, which covers M0–M4 needs (replays, tests, ghost PvP executed locally).
- **Cross-platform caveat:** IEEE-754 ops can differ across ARM/x86 (FMA contraction, denormals), so a replay recorded on one platform is not guaranteed bit-exact elsewhere.
- Mitigation [structural]: (1) inputs quantized at capture (tick index + quantized aim, see 4.3) so drift can't enter via input; (2) replay format is versioned and stamps platform + sim version — mismatch downgrades to "verify by MatchResult checksum, tolerate divergence" instead of asserting bit-exactness; (3) if ghost PvP ever needs cross-platform exactness, swap the sim's math to fixed-point behind the same pure-C# seam — a contained change, not a rewrite.

### 4.3 Replay format (sketch)

```yaml
version: 1                  # replay format version, bump on any breaking change
simVersion: "0.3.0"         # gameplay/balance code version
platform: ios-arm64
seedSet: { version: 1, master: 0x9E3779B97F4A7C15, ... }
lineups: { player: <LineupState snapshot>, opponent: team.opp_arc1_practice }
format: To15
inputs:                     # player inputs only; AI is derived from seeds
  - { tick: 412, kind: ServeRelease, power_q: 187, aimX_q: -42, aimY_q: 96 }
  - { tick: 655, kind: ReceiveCommit, slot: 5 }
  - { tick: 701, kind: ReceiveTap }
  - { tick: 890, kind: SetChoice, lane: QuickMiddle }
  - { tick: 1043, kind: SpikeTap, dir: Cross }
```

- Inputs keyed by sim tick (never wall-clock); analog values quantized to `int` (e.g. aim in 1/256 court units) [structural — quantization is what makes replay input platform-independent].
- Replay of `(seedSet, lineups, inputs)` MUST reproduce the recorded `MatchResult` bit-exactly on the same platform+simVersion.

---

## 5. Test Inventory

### 5.1 EditMode (deterministic core)

| Test | Contract it defends | Plausible bug it catches |
|---|---|---|
| `TimingGrade_BoundaryMs_Exact` | Grade boundaries at exact ms offsets map Perfect/Great/Good/Miss with documented edge rule (boundary value → better grade) | Off-by-one / `<` vs `<=` flips a frame-perfect input to Great |
| `TimingGrade_StatWidensPerfectWindow_Monotonic` | Higher stat ⇒ Perfect window strictly ≥, never removes input (window < contact duration) | Widening formula overflows and inverts at high stats, or widens to auto-Perfect |
| `Quality_ClampAtStat0_Floor` | `quality = floor + grade×(ceiling−floor)` stays in [0,1] at normalized stat 0 | Negative floor from a debuff pushes quality below 0 and corrupts downstream cascade |
| `Quality_ClampAtStat1_Ceiling` | Quality ≤ 1 at stat 1 with Perfect + buffs stacked | Buff stacking exceeds 1.0 and makes some spikes literally unreceivable |
| `Normalization_RawToUnit_Bounds` | Meta raw→0..1 mapping clamps at 0 and STAT_RAW_MAX; gameplay never receives raw ints | Over-cap raw stat (LB + equip) leaks >1.0 into sim math |
| `ReceiveGrade_SetOptionsMatrix_Exhaustive` | All 5 grades S/A/B/C/Shank → exact set-option sets (Shank ⇒ desperation high ball only; S ⇒ full menu) | A refactor silently lights quick-middle off a C receive, gutting the quality cascade |
| `Resolution_OutcomeTable_SpikeBlockDig` | Spike-vs-block-vs-dig resolution matches authored outcome table for all quality combos | Tie-break at equal quality resolves inconsistently between mirror cases |
| `Resolution_ShankReceive_ForcesFreeBallPath` | Shank ⇒ no attack options; opponent receives free ball | Shank accidentally still allows a pipe attack |
| `Resolution_PerfectEverything_AttackerFavored` | Perfect serve-receive-set-spike vs Perfect block resolves per table (attacker-favored kill or tool, never dig-neutral) | "Perfect everything" degenerates to coin flip, breaking pillar 2 |
| `Resolution_AimIntoCommittedBlock` | Spiking into a committed block with matching lane ⇒ block wins/tools per table | Committed block ignored when spike quality is high, making blocking pointless |
| `Hype_Accumulation_Sources` | Long rally / Perfect contact / block / ace each add spec'd Hype; clamped 0..100 | Double-crediting a blocked-then-dug rally inflates Hype 2× |
| `Hype_IgnitionThreshold_EnterExit` | Team enters Ignition exactly at threshold; signature spend below cost rejected; Hype never < 0 | `>=` vs `>` at threshold; spend underflow wraps negative |
| `Sig_a_GuaranteedGrade_OneContact` | (a) forces stated grade for exactly one contact, then expires | Effect persists a second contact, becoming a permanent buff |
| `Sig_b_WindowAdjust_PctAndDuration` | (b) widens/shrinks window ±X% for exactly N contacts, composable with stat widening | Shrink applied as negative width crashes or auto-Misses |
| `Sig_c_TrajectoryOverride_UsesAuthoredArc` | (c) replaces trajectory with the named arc; ball still resolvable by receiver | Override skips landing-indicator computation, receiver gets no input |
| `Sig_d_OpponentDebuff_TargetsAndExpiry` | (d) debuffs opponent block/dig quality only, N contacts, own team unaffected | Debuff applied symmetrically to both teams |
| `Sig_e_HypeDelta_GainDrainClamped` | (e) adds/drains Hype, clamped 0..100, drain can't push opponent negative | Drain below 0 wraps via uint |
| `Sig_f_TeamQualityBuff_ScopeAndStack` | (f) +X% quality for N contacts, team-scoped, clamped by quality ceiling | Buff applies to opponent contacts during their touches |
| `Sig_EffectCount_1or2_Validated` | Defs with 0 or 3+ effects rejected at validation | Content author ships a 4-effect ult that no sim path budgeted for |
| `Pity_Statistical_10kSeededRolls` | 10k pulls on fixed seed: observed SSR rate within binomial CI of published rate (incl. soft-pity uplift modeled) | Rate table typo (0.06 vs 0.006) — a compliance and lawsuit bug |
| `Pity_HardAt80_Exact` | Pull #80 since last SSR is SSR with probability 1; counter resets | Reset-on-SSR happens before grant, making hard pity fire at 81 |
| `Pity_SoftRamp_From65_Monotonic` | SSR rate strictly increases each pull from 65→80, base rate before 65 | Ramp starts at 66 or applies retroactively |
| `Pity_5050_GuaranteeStateMachine` | Lose 50/50 ⇒ flag set ⇒ next SSR is featured ⇒ flag cleared; win ⇒ flag untouched | Flag cleared on a non-SSR pull, silently eating the guarantee |
| `Pity_PerBanner_Isolation` | Pulls on banner A never mutate banner B's `PityState` | Shared static counter across banners |
| `Rng_StreamIsolation` | Consuming N values from `Gacha` stream leaves `Rally`/`Ai`/`Substats` sequences unchanged | All streams backed by one shared PRNG instance |
| `LB_ShardMath_StarUpCosts` | Dupe → shard grant by rarity; star-up decrements exact cost; lbStars clamped 0..5 | 6th star purchasable; shards go negative on double-tap |
| `SetBonus_2pc2pc_Stacks` | Two different 2pc sets both active simultaneously | Second set overwrites first in a keyed dict |
| `SetBonus_4pc_ImpliesNot2x2pc` | 4 pieces of one set ⇒ 2pc+4pc active exactly once each, never double-counted | 4pc also counted as two 2pc, double-dipping the stat mod |
| `SetBonus_5thPiece_NoExtraStack` | A 5th piece of a 4pc set adds nothing | Count-based loop grants 2pc again at 6 pieces |
| `XpCurve_Monotonic_NoLevelSkipGaps` | Growth curve samples strictly ordered; xp-to-next positive at every level 1..59 | A flat segment makes level 37 cost 0 xp |
| `MC_SoftCapSchedule_DiminishingReturns` | Per-stat xp gain multiplier non-increasing across tiers; never reaches 0 | Soft cap hard-stops MC growth, killing the F2P anchor |
| `MC_DailyCap_RolloverResets` | Drill attempts capped per day; UTC date rollover resets counters; streak survives per rule | Timezone math grants double dailies at local midnight |
| `Substat_RollBounds_AndCount` | 10k seeded rolls: every substat within `[minPct,maxPct]`, on a 0.5% step (5 discrete values), count matches rarity's `rollCountByRarity`, never duplicates `mainStat` | `max` exclusive vs inclusive yields impossible top rolls; float roll bypasses step quantization |
| `Craft_Pity_CountsAndResets` | Crafting pity increments per craft, guarantees top tier at threshold, resets after | Pity counter reset by unrelated craft recipe |
| `Save_RoundTrip_Lossless` | Serialize → deserialize `SaveGame` ⇒ deep-equal, all dictionaries and GUIDs intact | `Dictionary<EnumKey,…>` silently serialized as ordinal ints and shuffled by enum reorder |
| `Save_Migration_V1toV2_Chained` | v1 fixture migrates stepwise to current; backup file written; over-version save rejected read-only | Skipped intermediate migration drops `PityState`, wiping a paying user's pity |
| `AutoPlay_UnlockRule` | Auto flag set only after A-rank manual clear; manual grants exactly +20% rewards | Auto unlocked by auto-capped A grade (circular unlock) |
| `Bond_ResolutionThreshold` | Bond active iff ≥2 members among slots+libero; bench excluded; derived cache matches recompute | Benched character keeps a bond lit |
| `Bond_EffectApplication` | Active bond applies its `SetBonus` exactly once; deactivating (member benched/swapped) removes it in the same recompute; per-group ≤3% and lineup cap enforced | Bond buff persists after lineup edit, or double-applies when two bonds share a member |

### 5.2 PlayMode

| Test | Contract it defends | Plausible bug it catches |
|---|---|---|
| `RallySim_PointResolved_FromEveryContactState` | Scripted rally driver reaches `PointResolved` starting from each contact state (Serve, Receive, Set, Spike, Block) with both scripted-perfect and scripted-miss inputs | A Shank→free-ball transition dead-ends the state machine and hangs the match |
| `RallySim_QualityCascade_EndToEnd` | Receive grade fed through set→spike matches EditMode math when run through the real sim loop | Sim loop reads raw stats instead of normalized (unit drift between layers) |
| `AI_LegalActions_AllTiers` | AI at Easy/Normal/Hard emits only legal actions (front/back-row legality, rotation, libero restrictions) over N seeded matches | Hard AI back-row-attacks from front-row libero after rotation |
| `AI_TierDistributions_Distinguishable` | Timing-grade distributions across tiers are statistically distinct in the spec'd direction | Tier config swap makes "hard" easier than "normal" |
| `Rotation_LiberoAutoSwap_Legality` | Auto-rotation + libero swap produce legal formations across full sideout cycles in all 3 formats | Libero left front-row after a double sideout |
| `Replay_Determinism_SameSeedsSameResult` | Replaying `(seedSet, lineups, inputs)` reproduces the recorded `MatchResult` bit-exactly (same platform+simVersion) | Frame-rate-coupled code path (`Time.deltaTime` in sim) breaks tick purity |
| `Replay_TamperedInput_DivergesDetectably` | Altering one quantized input changes the result checksum (replays are not vacuous) | Sim ignores replayed inputs and re-derives from AI, masking desyncs |
| `FixedTimestep_FrameRateIndependence` | Sim at 30fps vs 120fps render produces identical tick sequence and result | Physics stepped in `Update` instead of the fixed tick |

---

Cross-doc: gameplay formulas/windows → `docs/m0-gameplay-spec.md`; all reward/cost/xp amounts → `docs/economy-progression.md`; names, schools, bond-group fiction → `docs/story-bible.md`; `LocKey`/string-table rules → `docs/compliance-localization.md`.
