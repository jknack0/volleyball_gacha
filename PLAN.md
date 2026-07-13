# Volleyball Gacha — Project Plan

Working title: TBD. Mobile (iOS + Android). Anime volleyball gacha where the **gameplay is the product** and the gacha/meta wraps around it.

---

## 1. Vision & Pillars

**Fantasy:** You're the protagonist of a sports anime. Rallies feel like the last two minutes of a Haikyuu!! episode — slow-mo at the spike apex, impact frames, the crowd holding its breath.

Pillars, in priority order:

1. **The rally is sacred.** Every touch (serve → receive → set → spike → block) is a skill moment. If a 10-rally exchange isn't tense and readable with grey-box capsules, nothing else matters.
2. **Stats assist, skill decides.** Character stats widen timing windows and raise ceilings; the player's timing and aim pick the point inside that range. No pure stat-check gameplay.
3. **The story feeds the gacha.** Every banner character is someone you met, fought, or lost to in the story. Pulling a rival you just beat is the emotional hook.
4. **Respect the session.** A match is 3–5 minutes. Dailies are ≤60 seconds each. One-handed play if the M0 experiment supports it.

---

## 2. Core Gameplay (the volleyball)

This is where the majority of dev time goes. Everything in sections 3+ is deliberately conventional so this section can be exceptional.

### 2.1 Control model: contact-based rally

Real-time ball physics, but **you only ever control the player touching the ball**. AI positions everyone else. This is the key insight that makes 6v6 work on a phone: input complexity is constant regardless of team size — no virtual sticks, no camera juggling.

The rally is a chain of micro-interactions:

| Contact | Input | Skill expression | Governing stats |
|---|---|---|---|
| **Serve** | Hold-release power meter + drag aim | Risk/reward: aim near lines = smaller timing window. Jump serve unlockable (higher power, tighter window) | Serve, Power |
| **Receive / Dig** | Tap to commit receiver, timed tap on ball arrival | Trajectory shown as shrinking landing indicator; earlier commit = better platform. Grade: S/A/B/C/Shank | Receive, Speed |
| **Set** | Tap an attacker lane under time pressure (2–3s, slows time) | Tactical layer: quick middle, high outside, back-row pipe, setter dump. Receive grade gates which options are lit | Technique |
| **Spike** | Timed tap at jump apex + swipe direction (line / cross / roll / feint) | The anime moment: brief slow-mo at contact, read the block, aim around it | Power, Jump |
| **Block** (defense) | Read-or-commit choice + timed jump tap | Commit early vs the quick, or read the setter. Mistime = tool/free hit | Jump, Technique |

**Quality cascade** (real volleyball logic, and the depth engine):

```
receive grade → set options available → set grade → spike window size → point outcome
```

A shanked receive means only a desperate high ball; a perfect receive lights up the full attack menu. Players learn that rallies are won at the receive, which is exactly the sport's truth.

### 2.2 Stats × skill model

- Timing grades: **Perfect / Great / Good / Miss**.
- Each contact computes `quality = stat_floor + timing_grade × (stat_ceiling − stat_floor)`.
- Stats raise floor and ceiling and **widen the Perfect window**; they never remove the input.
- Stat sheet (6 stats, deliberately small): **Power, Jump, Technique, Serve, Receive, Speed** (+ Stamina as a match-length decay modifier, evaluated in M1 — cut if it reads as noise).

### 2.3 Anime feel (juice systems)

Budgeted as first-class features, not polish:

- **Contact slow-mo:** 0.2–0.4s time dilation at spike/block contact with camera punch-in. The signature sensation; tuned in M0.
- **Dynamic camera:** behind-court default → cut to side-on low angle for spikes, net-cam for blocks. Camera drama is half the anime feel (Cinemachine, see stack).
- **Momentum / Hype meter:** builds from long rallies, Perfect contacts, blocks, aces. At threshold, team enters **Ignition**: music layer kicks in, VFX intensity up, signature moves unlock.
- **Signature moves:** per-character ult with a 1–2s cut-in animation (the gacha showcase — SSRs get the flashiest). Examples: MC's time-freeze quick; rival ace's "off the tape" wipe shot. Mechanically: a guaranteed-grade contact or special trajectory, limited per set by the Hype economy.
- **Impact language:** speed lines, hit-stop frames, screen shake on kills, floor-slam decals, crowd audio swells.

### 2.4 Match structure

- **6v6, simplified rotation** (auto-rotate; front/back row legality enforced, libero auto-swaps). 6 lineup slots + libero + bench is the gacha team-building engine. *Fallback if phone readability fails in M0: 3v3 with the same contact system.*
- Formats: Quick Match (first to 11), Story (to 15; finales to 25), Events (special rules). Target 3–5 min/match.
- Rally scoring, standard sideout rotation on serve win.

### 2.5 Screen orientation — M0 experiment

Portrait vs landscape materially changes everything downstream (UI, camera, one-hand play). Both get a grey-box week in M0:

- **Portrait (lean):** behind-baseline camera frames the 9m-wide court naturally; one-thumb play; gacha-session ergonomics; genre precedent (Haikyu!! Fly High, Umamusume are portrait). Cut-ins go full-screen regardless.
- **Landscape:** better for side-on spike drama and 2-thumb aim.

Decision at the M0 feel gate; portrait wins ties.

### 2.6 Opponent AI

- Per-contact utility scoring (where to serve, which attacker, block commit vs read) + timing-grade distributions per difficulty tier.
- Difficulty = tighter AI timing distributions + wider tactical vocabulary (quicks, dumps, line shots), **never** input latency for the player.
- Story matches may rubber-band subtly for drama (comeback arcs) — a tuned design lever, always disclosed in balance docs, never in ranked/event modes.

### 2.7 Auto-play (the grind-vs-skill tension, resolved up front)

Gacha economies demand repeatable farming; skill gameplay punishes repetition. Rule:

- Manual play always available, yields **+20% rewards** and reaches higher score grades.
- **Auto-battle unlocks per stage after an A-rank manual clear**; skip tickets for farm stages after 3★.
- Story, tournaments, events: manual (or auto with capped grade). Skill stays meaningful; wrists survive.

---

## 3. Meta Systems

### 3.1 Characters & gacha

- **MC is not in the gacha.** The main character grows through dailies (§3.2) — the F2P emotional anchor.
- Gacha cast: story teammates and rivals, released as banners synced to story arcs (beat the arc → the rival's banner opens).
- Rarity: R / SR / SSR (3★–5★ display). Kit: position (S/OH/MB/OP/L), playstyle tag (Power/Quick/Technique/Guts), signature move, passive, stat spread.
- **Pity:** hard at 80, soft ramp from ~65, 50/50 featured with guarantee on loss — industry standard, published rates (legally required on both stores).
- Dupes → Limit Break shards (star-up: stat % + signature level).
- **Bond links:** same-school/team characters grant lineup synergy bonuses — ties team-building to the narrative and makes story-cast pulls strategic.

### 3.2 Dailies = training minigames

The loop trick: **each daily is a distilled core mechanic, so grinding character stats trains player skill too.**

| Daily | Minigame | Feeds |
|---|---|---|
| Jump Training | Pure timing ladder (apex taps) | Jump XP |
| Serve Practice | Target zones, risk multipliers near lines | Serve XP |
| Spike Drill | Timed hits vs moving block paddles | Power XP |
| Receive Drill | Reaction digs, escalating tempo | Receive XP |

- ≤60s each, daily-capped, primarily grow the **MC** (permanent, small increments) + team-wide training points for gacha units.
- Streak calendar bonus; missed days never punish beyond the missed gain.

### 3.3 Equipment & set bonuses

- 4 slots: **Shoes, Kneepads, Jersey, Accessory.**
- Set bonuses at 2pc/4pc, e.g. *Featherlight* — 2pc: +8% Jump; 4pc: quick attacks +15% power. Sets define builds (serve bot, wall MB, libero god).
- Sources: farmable Practice Match stages (auto-play eligible per §2.7), event shops, crafting.
- **Deterministic main stats + light substat RNG with a crafting pity** — explicitly avoiding artifact-hell; the grind should feel like gearing, not slot machines on top of slot machines.

### 3.4 Story

- Arc-based chapters: VN-lite dialogue scenes (portraits + text, cheap to produce) between stakes matches.
- Structure per arc: intro → training camp (introduces a mechanic) → tournament bracket → rival climax → banner release.
- V1 target: 1 full arc, ~12 characters, ~25 story matches.

### 3.5 Modes roadmap

- V1: Story, Quick Match, Dailies, Equipment farm, weekly Tournament (vs AI ladder).
- V1.x: **async PvP — ghost teams** (opponent's lineup + tactic settings, AI-executed). Leaderboards.
- Explicit non-goal: real-time networked PvP. Huge lift, not required for the fantasy. Revisit post-launch only.

---

## 4. Economy (planning-level)

- Dual currency (free/paid gems), pull costs standard (1×160 / 10×1600 equiv).
- Income: dailies, story firsts, tournament, events, achievements. Tune to ~1 multi/week F2P baseline.
- Monetization: banner pulls, battle pass (cosmetic + QoL lean), training boosts. No stamina paywall on dailies — dailies are the retention spine, never gate them.
- Compliance: published gacha rates, pity counters visible in-UI, regional loot-box rules reviewed pre-launch.

---

## 5. Tech Stack

### 5.1 Client: **Unity 6 (LTS) + URP** — decided

The genre runs on Unity, and the reasons are exactly our priorities:

| Need | Unity answer |
|---|---|
| Anime camera drama | **Cinemachine** — the spike cut-ins, net cams, slow-mo punch-ins are its native vocabulary |
| Cut-in/ult sequences | **Timeline** + Animation Rigging |
| Toon look | URP toon/cel shaders (mature asset-store + open options), post FX stack |
| Rapid feel iteration | Play-mode tweaking, DOTween/PrimeTween for juice, profiler maturity on mobile |
| Gacha-scale UI | UGUI/UI Toolkit + addressables-driven screens |
| Live content updates | **Addressables** — ship banners/story without store review |
| IAP + receipts | Unity IAP, battle-tested |
| Ecosystem | Deepest tutorial/asset/hiring pool; anime-style char pipelines documented |

Considered and rejected:
- **Godot 4** — capable, but weaker mobile-3D track record, thin asset ecosystem for toon/VFX, no Addressables-class content pipeline; no team familiarity to offset that.
- **Unreal** — mobile bloat, slow iteration for menu-heavy games, overkill.
- **Flutter/Flame, RN+Skia** — excellent for the meta UI, fail the 3D gameplay-feel requirement outright.
- **Cocos Creator** — genre-relevant in Asia, weaker English ecosystem/docs.

Licensing note: Unity Personal is free below $200k/yr revenue; the 2023 runtime-fee scheme was rescinded. Monitor, low risk at indie scale.

### 5.2 Art pipeline (anime chars without AAA budget)

- Prototype/mid: **VRoid Studio → VRM → UniVRM import** — free, fast anime-style 3D characters; perfect through M3.
- Production: commission stylized low-poly models for the 12-char launch cast; keep VRoid rigs as the skeleton standard so animations transfer.
- Environments: one gym, done well. Lighting + crowd sprites carry mood.

### 5.3 Architecture

```
Assets/
  Scripts/
    Gameplay/   # rally state machine, contacts, ball physics, AI, camera director
    Meta/       # gacha, inventory, progression, dailies, economy
    Data/       # ScriptableObjects: characters, moves, equipment sets, banners, stages
    UI/
  Tests/
    EditMode/   # deterministic: quality cascade math, pity math, economy, set bonuses
    PlayMode/   # scripted rally sims, AI sanity
```

- **Deterministic core:** rally resolution and gacha rolls are pure C# with injected seeded RNG — unit-testable without the engine, and ready to move server-side later.
- Data-driven everything (ScriptableObjects → later remote config via Addressables).
- Save: local JSON + platform cloud save through M4.

### 5.4 Backend — deliberately deferred

Prototype through M3 is **fully offline**. Server-authoritative gacha/currency only matters once real money exists.

- **M4 decision gate**, default: **PlayFab** (managed economy, catalogs, receipt validation, generous free tier — least ops for a small team). Self-host alternative: **Nakama** if we want control/cost at scale.
- The deterministic-core rule above means moving rolls server-side is a transport change, not a rewrite.
- Analytics: Unity Analytics or GameAnalytics from M4; crash reporting from M1 (Sentry/GlitchTip).

---

## 6. Roadmap & Feel Gates

Time is deliberately front-loaded into M0/M1 per the one rule: **gameplay first.**

| Milestone | Scope | Gate |
|---|---|---|
| **M0 — Feel prototype** (grey-box, the long pole) | Ball physics, full contact chain (serve/receive/set/spike/block), quality cascade, basic AI, slow-mo + camera director v1, portrait-vs-landscape A/B | **The Gate:** 10-rally exchanges vs AI are tense and fun with capsule players and zero meta. Not passed → iterate here, touch nothing else |
| **M1 — Full match** | Scoring, rotations, libero, match flow, 3 difficulty tiers, HUD, Hype meter + Ignition, SFX pass, crash reporting | Complete 3–5 min match holds attention; difficulty tiers distinguishable blindfolded |
| **M2 — Meta skeleton** | Character data model, lineup builder w/ bond links, local gacha + pity (seeded, tested), currencies, save | Pull → slot into lineup → measurable on-court difference |
| **M3 — Progression loops** | 4 daily minigames, XP/limit break, equipment + set bonuses + farm stages, auto-play rules | 7-day self-playtest: dailies stay <5 min total and feel worth it |
| **M4 — Content & polish** | Story arc 1 (VN scenes, ~25 matches), 12 characters w/ signatures, VRoid→commission art pass, FTUE, music, **backend decision gate** | New-player hour-one flow tested on 5 humans |
| **M5 — Live-ops & soft launch** | Backend integration (server gacha, receipts), IAP, analytics, rate disclosures, store builds, soft-launch region | Economy dashboards live; soft-launch KPIs defined (D1/D7, session length, rally-abandon rate) |

---

## 7. Risks

1. **The feel doesn't land** (existential) → M0 gate is a hard wall; we iterate there indefinitely before building meta. Juice systems (§2.3) are in M0, not polish.
2. **Grind vs skill tension** → resolved by design §2.7 (auto after A-rank manual, manual bonus).
3. **Anime 3D art cost** → VRoid pipeline defers spend until fun is proven; 12-char launch cast caps commission budget; one great gym, not five arenas.
4. **Scope creep** (real-time PvP, beach modes, 2v2) → non-goals written down (§3.5); ghost PvP is the pressure valve.
5. **Equipment RNG resentment** → deterministic mains + crafting pity (§3.3).
6. **Compliance** (gacha odds, minors, regional loot-box law) → rates published in-UI from M2 onward; legal review pre-soft-launch.
7. **Unity licensing shifts** → low risk at indie revenue; deterministic core is engine-portable insurance.

## 8. Open Questions (non-blocking)

- Title, school/team names, tone (earnest Haikyuu vs. slightly absurd).
- Art budget & whether launch cast is 12 or trimmed to 8.
- Story format ambition: static VN portraits (planned) vs Live2D (cost ↑).
- Target soft-launch region (PH/CA/NZ conventional).
