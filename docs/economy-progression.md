# Economy & Progression Workbook

Numeric companion to PLAN.md §3–4. Every number is either **derived on this page** or labeled **[tunable]** (prototype will move it) / **[structural]** (load-bearing design). Gameplay timing values are out of scope (see m0-gameplay-spec). Stat consumption is normalized 0..1 by the gameplay layer; this doc owns raw values and % modifiers (raw→normalized mapping: `raw / 200`, cap 200 **[tunable]**).

---

## 1. Currencies

| ID | Role | Faucets | Sinks |
|---|---|---|---|
| `gems_free` | Earned premium; pulls | Dailies, story first-clears, weekly tournament, events, achievements, login calendar, battle-pass free track, mail/compensation | Gacha pulls (spent **before** `gems_paid`) **[structural]** |
| `gems_paid` | IAP premium; pulls + paid-only offers | IAP packs, monthly card, BP premium rebate | Gacha pulls, paid-only cosmetic bundles |
| `coins` | Universal upgrade currency | Match rewards (manual +20%), dailies, gear-stage tokens, weekly tournament, BP free track, events, salvage | Character XP application (0.25/XP), equipment upgrades, crafting |
| `training_points` | Gacha-unit growth currency (MC grows via drills directly) | Daily drills (beyond MC cap), weekly missions, BP free track | Signature levels 2–10, gacha-unit stat training |

Pull costs (contract): **single 160 / multi 1600** (multi = 10 pulls at no discount; the 1600 price IS 10×160 — "discount" messaging is a UI choice, not an economy one) **[structural]**.

---

## 2. Faucet Audit — `gems_free`, F2P

Target (PLAN §4): **~1 multi (1,600 gems) per week F2P**.

| Activity | Cadence | Gems | Per week |
|---|---|---|---|
| Daily missions (4 drills + 1 match, ≤5 min) | daily | 100 | 700 |
| First manual match win of day | daily | 20 | 140 |
| Weekly tournament (AI ladder; placement 200–300, table = mid placement) | weekly | 300 | 300 |
| Weekly mission chest | weekly | 150 | 150 |
| Login calendar (600/month) | monthly | 600 | 138 |
| Monthly event (~2-week runtime, full clear) | monthly | 900 | 207 |
| Story first-clears + achievements, amortized first 3 months¹ | monthly | 500 | 115 |
| **Total** | | | **1,750** |

Sustained (no story/achv line): **1,635/week**.

¹ Launch pools: 25 story matches × 60 first-clear + 3★ bonuses ≈ 2,300; achievement pool ≈ 5,000; beginner 7-day missions 2,400 → ~9,700 one-time gems ÷ 3 months ≈ 500/month. All values **[tunable]**.

| Horizon | Formula | Gems | Pulls |
|---|---|---|---|
| Daily (recurring only) | 100 + 20 | 120 | 0.75 |
| Weekly | table above | 1,750 (1,635 sustained) | 10.9 / 10.2 |
| Monthly (30.4 d) | 120×30.4 + 450×4.345 + 600 + 900 + 500 | **6,993** | **43.7** |

**Audit vs target:** 1,600/wk × 4.345 = 6,952/month required; faucets give 6,993 → within **0.6%**. On target; the daily-mission 100 is the tuning knob.

---

## 3. Pull Math

### 3.1 Rates & soft-pity ramp [tunable]

Base SSR **0.8%**, SR 8.0%, R 91.2% (published in-UI per PLAN §4). Pity per contract: hard 80, soft ramp from 65, 50/50 featured with guarantee after loss.

Per-pull SSR probability `p(n)` (n = pulls since last SSR; counter resets on SSR):

```
p(n) = 0.008                      n ≤ 64
p(n) = 0.008 + 0.05·(n − 64)      65 ≤ n ≤ 79    (+5.0 pp per pull)
p(80) = 1.0                       hard pity
```

| n | 64 | 65 | 70 | 75 | 79 | 80 |
|---|---|---|---|---|---|---|
| p(n) | 0.8% | 5.8% | 30.8% | 55.8% | 75.8% | 100% |
| P(SSR by n), cumulative | 40.2% | 43.7% | 82.8% | 99.2% | 99.99% | 100% |

### 3.2 Expected pulls per SSR

Renewal expectation over the first-success distribution:

$$E[\text{pulls/SSR}] = \sum_{n=1}^{80} n \cdot p(n) \prod_{k=1}^{n-1}\bigl(1-p(k)\bigr) = \mathbf{53.33}$$

(Computed exactly from the ramp above; distribution sums to 1.0. Median = **67** pulls.)

### 3.3 Expected pulls per FEATURED SSR

50/50 with guarantee: each SSR event yields the featured unit with p = ½; a loss makes the next SSR guaranteed. Expected featured per SSR event pair: win path 1 SSR/featured (p=½), loss path 2 SSRs/featured (p=½) → **1.5 SSRs per featured** on average.

$$E[\text{pulls/featured}] = 1.5 \times 53.33 = \mathbf{80.0 \text{ pulls}} = 12{,}798 \text{ gems} \approx \mathbf{12{,}800}$$

Worst case (double hard pity): 160 pulls = 25,600 gems. Per-SSR gem cost: 53.33 × 160 = **8,532**.

### 3.4 Persona table

Banner window = **6 weeks** (synced to story arc per PLAN §3.1) **[structural]**. Pricing assumptions **[tunable]**: monthly card $4.99 → 300 instant + 90/day = 3,000/mo; BP premium $9.99/season → 680 gems rebate (≈493/mo); direct packs ≈ 80 gems/$ at large-pack bonus tier.

| Persona | Gems/month | Pulls/mo | Featured SSR/mo (÷80.0) | Featured / 6-wk window |
|---|---|---|---|---|
| F2P | 6,993 | 43.7 | **0.55** | **0.82** |
| Light ($15/mo: card + BP) | 6,993 + 3,000 + 493 = 10,486 | 65.5 | **0.82** | **1.23** |
| Whale ($300/mo: card + BP + $285 packs) | 10,486 + 22,800 = 33,286 | 208.0 | **2.60** | **3.90** |

Readings: F2P **guarantees** (hard-pity worst case, 160 pulls = 25,600 gems) a featured SSR every ~2.4 windows by saving, and *expects* one every ~1.2 windows. Whale can take a featured to ~LB2–3 within its window; LB5 in one window ≈ 6 copies × 80 pulls = 480 pulls ≈ 76,800 gems ≈ **$960** — the deep sink.

---

## 4. Character Progression (gacha units)

### 4.1 XP curve, levels 1–60

Per-level step (quadratic — early levels feel fast, cap is a project) **[tunable coefficients, structural shape]**:

$$XP(L \to L{+}1) = 50L^2 + 100L \qquad \text{cumulative } C(L) = \sum_{k=1}^{L-1} XP(k)$$

| Level | 1 | 10 | 20 | 30 | 40 | 50 | 60 |
|---|---|---|---|---|---|---|---|
| Cumulative XP | 0 | 18,750 | 142,500 | 471,250 | 1,105,000 | 2,143,750 | **3,687,500** |
| Coins to reach (0.25/XP) | 0 | 4,688 | 35,625 | 117,813 | 276,250 | 535,938 | **921,875** |

### 4.2 XP items [tunable]

| Item | XP | Prime source per band |
|---|---|---|
| Small Whistle | 100 | dailies (lv 1–20 band: 142.5k XP ≈ cheap) |
| Drill Tape | 500 | gear/farm stages (lv 20–40 band: 962.5k XP) |
| Game Film | 2,000 | weekly tournament, events (lv 40–60 band: 2,582.5k XP) |
| Championship Reel | 10,000 | BP, event shops |

Coin cost is charged on application (0.25 coins/XP), so item rarity is a logistics knob, not a price knob.

### 4.3 Limit Break (0–5 stars, contract)

Shards are **character-specific**, from dupes only (event shops may stock shards for older characters later **[tunable]**):

| Rarity | Shards per dupe |
|---|---|
| R | 15 |
| SR | 40 |
| SSR | 125 |

LB star costs: **75 / 100 / 125 / 150 / 175** = 625 total → exactly **5 SSR dupes = LB5** (6 copies owned). Per star: **+3% all stats** (LB5 = +15%) **[tunable]**; stars 1/3/5 also raise the signature-level cap checkpoint (cap 4 → 7 → 10) **[tunable]**.

### 4.4 Signature levels 1–10

Paid in `training_points` (levels 5+ also need Signature Manuals ×1/level from weekly tournament **[tunable]**):

| To level | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | Total |
|---|---|---|---|---|---|---|---|---|---|---|
| TP | 100 | 200 | 400 | 700 | 1,100 | 1,600 | 2,200 | 2,900 | 3,700 | **12,900** |

### 4.5 One SSR, pull → max (summary)

| Resource | Amount | Derivation |
|---|---|---|
| `gems` (acquire, EV) | 12,798 | §3.3 |
| XP | 3,687,500 | §4.1 |
| `coins` (leveling) | 921,875 | 3,687,500 × 0.25 |
| `coins` (gear ×4 to +12) | 312,000 | §6.4 |
| `training_points` (sig 10) | 12,900 | §4.4 |
| Dupes for LB5 | 5 (625 shards) | §4.3 — EV +400 pulls ≈ 64,000 gems (whale scope) |
| Farm time (4pc set) | ~19 days | §6.5 |

F2P pacing: coins income ≈ 25k/day (§8 faucets) → first SSR to 60 in ~5 weeks alongside gear spend. Intended: level cap is a **campaign**, not a week-one checkbox.

---

### 4.6 Bond-link bonuses (lineup synergy)

Bond groups (fiction: story bible bond graph; schema: `BondGroupDef`, data-schemas §1.14) activate when ≥2 members are fielded among slots+libero. Power budget:

| Rule | Value |
|---|---|
| Per-group bonus | +2% raw stats total, split across 1–2 stats; SSR-anchored groups may carry +3% **[tunable]** |
| Active groups counted | up to 3 per lineup; excess stay lit in UI, grant nothing **[tunable]** |
| Lineup-wide cap | ≤ +6% PI from bonds — deliberately under one timing grade (±0.08 PI, §8.2): bonds shape team identity, never replace skill **[structural]** |

Zero acquisition cost — bonds unlock by owning the characters. They are the gacha's team-building hook (PLAN §3.1), not a sink; their EV is priced into pull value, not into coins/TP.

## 5. MC Daily Training

MC (the team's setter, not in gacha — contract) grows **only** via drills + match play. Raw stat scale 0–200; MC starts all stats at **40**.

### 5.1 Per-drill income [tunable]

| Drill (PLAN §3.2) | Feeds | Base stat XP/run | Grade multiplier | Rewarded runs/day |
|---|---|---|---|---|
| Jump Training | Jump | 24 | ×0.5 (C) … ×1.5 (S), avg ≈ ×1.25 | 1 |
| Serve Practice | Serve | 24 | same | 1 |
| Spike Drill | Power | 24 | same | 1 |
| Receive Drill | Receive | 24 | same | 1 |
| Manual match win | Technique, Speed | 5 each | — | cap 6/day (30 XP) |

Expected daily per drilled stat: 24 × 1.25 = **30 stat XP**. Extra drill runs: no stat XP, 10 TP each (cap 5) — practice stays worth something, never required **[structural]**. Each rewarded run also grants 25 TP (daily TP ≈ 150 with mission bonus).

Technique/Speed feeding from *matches* is deliberate: MC's court-vision stat grows by playing volleyball, not by menuing (diegetic per contract).

### 5.2 Soft-cap schedule (diminishing returns) [tunable]

| Stat band | 40–60 | 60–75 | 75–90 | 90–100 | 100–120 |
|---|---|---|---|---|---|
| Stat XP per +1 point | 100 | 200 | 400 | 800 | 1,600 |

### 5.3 Trajectory (30 XP/day/stat, derived from the schedule)

| Day | D1 | D7 | D30 | D90 | D180 |
|---|---|---|---|---|---|
| Drilled stat (raw) | 40.3 | 42.1 | 49.0 | 63.5 | 76.0 |

### 5.4 Anti-FOMO rule [structural]

1. **Missing a day never reduces existing stats** — no decay, no rust mechanic, ever.
2. Streak calendar pays **additive gems**, never stat multipliers (PLAN §3.2: missed days never punish beyond the missed gain).
3. Caps are small: a lapsed week forfeits 210 stat XP/stat ≈ ≤2.1 points at band 1 — and the soft-cap schedule means the lapsed player pays *fewer* XP per point than the player ahead of them, so relative gaps self-heal. A lapsed week is caught in relative-power terms without any catch-up mechanic.

---

## 6. Equipment Economy

### 6.1 Tiers & deterministic main stats

Tiers reuse rarity vocabulary: R / SR / SSR. Main stat is **fixed by slot** (Accessory: fixed by *set*), value fixed by tier+upgrade — zero main-stat RNG **[structural]**:

| Slot | Main stat | R (+0→+12) | SR | SSR |
|---|---|---|---|---|
| Shoes | Speed % | 4→8% | 8→16% | 12→24% |
| Kneepads | Receive % | 4→8% | 8→16% | 12→24% |
| Jersey | Power % | 4→8% | 8→16% | 12→24% |
| Accessory | set-defined (e.g. Featherlight = Jump %) | 4→8% | 8→16% | 12→24% |

Main stat grows **+1%/upgrade level** at SSR (linear, +0…+12); R/SR scale proportionally. Values **[tunable]**.

### 6.2 Substats (light RNG — the *only* RNG)

| Tier | Substat count | Pool | Roll range |
|---|---|---|---|
| R | 1 | 5 stats (6 minus main) | +2–4% each, uniform in 0.5% steps |
| SR | 2 | same | same |
| SSR | 3 | same | same |

Substats are **rolled once at drop/craft and never change** — no upgrade-level substat lottery (PLAN §3.3: gearing, not slot machines on slot machines) **[structural]**.

### 6.3 Set bonuses

2pc/4pc per contract (e.g. Featherlight 2pc +8% Jump, 4pc quick attacks +15% power). Bonus magnitudes live in data-schemas/gameplay spec; this doc prices acquisition only.

### 6.4 Upgrade costs [tunable]

Cost to +L: 1,000 × L coins per level → cumulative to +12 = 1,000 × Σ1..12 = **78,000 coins/piece**, ×4 slots = **312,000/set**. Milestones +4/+8/+12 are the felt power steps (UI framing).

### 6.5 Farm stages, drops, and days-to-4pc (the derivation)

Endgame gear stage (auto-eligible after A-rank manual, per PLAN §2.7) **[tunable rates]**:

- 1 piece/run; tier: R 55% / SR 35% / **SSR 10%**; featured set 50% (each stage features one set); slot uniform ¼.
- Rewarded runs/day: **5** (further runs pay token coins only — soft cap, **not** a stamina gate).
- Salvage: any unwanted piece → 10 Set Fabric. Craft = 100 Fabric → chosen **slot**, set 50% desired, tier SSR 20%.
- **Crafting pity [tunable]:** every **10th craft** is a guaranteed *desired-set SSR of a chosen slot*. Counter persists across sessions, visible in UI.

Expected desired-set SSR drops: 5 × 0.10 × 0.5 = **0.25/day** (random slot). Pure-drop coupon collector over 4 slots needs 4·H₄ = 4(1+½+⅓+¼) = **8.33 pieces** → 8.33 / 0.25 = **33.3 days**. Crafting (≈4.75 salvaged pieces/day → 47.5 Fabric → 0.475 crafts/day; pity every ~21 days; each craft 10% desired-SSR at a *chosen* slot) kills the last-slot tail. Monte Carlo (20k trials, seeded):

| mean | median | p90 |
|---|---|---|
| **19.1 days** | 21 | 26 |

**≈3 weeks of daily play for a 4pc SSR set, hard-bounded by pity** — substat quality, not slot coverage, is the long-tail chase.

---

## 7. Battle Pass & Misc

Season = banner window (6 weeks), 50 levels, BP XP from dailies/weeklies only (no grind spike) **[structural]**. Cosmetic + QoL lean per PLAN §4.

| Track | Contents (per season) [tunable] |
|---|---|
| Free | 300 gems, 200,000 coins, 2,000 TP, XP items (~400k XP), 10 skip tickets |
| Premium $9.99 | +680 gems, jersey skin + serve trail + court decal (cosmetic), 30 skip tickets, inventory expansion, Championship Reels ×5 |

**Never in BP:** characters, LB shards, equipment power **[structural]**.

First-time bonuses **[tunable]**: first-purchase double on each gem pack; beginner banner: discounted multi 1,280 gems, guaranteed SSR within 30 pulls, one purchase; 7-day beginner missions 2,400 gems (≈1.5 multis week one).

Achievement gem pool at launch: **~5,000 gems** lifetime (amortized in §2 footnote).

---

## 8. Sanity Checks

### 8.1 Coin & TP throughput (supporting faucets) [tunable]

| Faucet | Coins/day | TP/day |
|---|---|---|
| Daily missions | 6,000 | 50 |
| 10 manual match wins (600 ea; auto = 500, manual +20% per PLAN §2.7) | 6,000 | — |
| Gear-stage token coins | 3,000 | — |
| Weekly tournament (÷7) | 3,600 | 43 |
| BP free track (÷42) | 4,800 | 48 |
| Drills (4 rewarded + extras) | — | 150 |
| Events (÷30) | 1,600 | — |
| **Total** | **≈25,000** | **≈290** |

Checks: level 1→60 = 921,875 coins ≈ 37 faucet-days (≈5 weeks with gear spend) ✔. Sig 1→10 = 12,900 TP ≈ 45 days ✔ — both slower than gem cadence, so pulls stay the headline decision.

### 8.2 Time-to-wall: power vs story curve

Power Index (PI) = lineup mean normalized stat (raw/200), gear/set/LB multipliers applied. Story tuning targets **[tunable]**; skill swings ≈ ±0.08 PI equivalent (one timing grade — stats assist, skill decides, PLAN §1):

| Day | Player state | PI | Story gate at that point | Margin |
|---|---|---|---|---|
| D1 | MC 40 + starter R/SR lv 1–10 | ~0.23 | Arc intro tuned 0.20 | comfortable |
| D7 | Levels ~20, first R/SR gear | ~0.30 | Training camp 0.28 | skill-optional |
| D30 | Lv 40–45, 2pc set, one SSR owned (43.7 pulls → 1−0.992⁴³ ≈ 29% by base rate alone; the beginner banner guarantees one) | ~0.42 | Mid-bracket 0.38 | on curve |
| D45–60 | Lv 50+, 4pc set (§6.5: ~19d farm started D30) | ~0.50 | Arc finale 0.45 | finale beatable with skill from ~D40 |

No hard wall: every gate is clearable at listed PI − 0.08 with Perfect-heavy play **[structural]** (rubber-banding per PLAN §2.6 is drama-only, disclosed here).

### 8.3 No-stamina audit (PLAN §4 rule)

- Dailies: **no entry currency, no stamina, ever** — rewards are once/day, entry is unlimited **[structural]** ✔
- Gear farm: soft cap = diminishing rewards after 5 runs; entry never blocked ✔
- Story/events: skip tickets are a convenience item, not an entry gate ✔

### 8.4 Gem-income audit vs pull target

Required: 1,600/wk (PLAN §4) → 6,952/month. Delivered: **6,993/month** (§2), +0.6%. Sustained floor after one-time pools dry (month 4+): 6,493/month = 0.93 multi/wk → backfilled by the next arc's story first-clears (content cadence = 1 arc/season keeps the amortized line alive) **[structural dependency: content cadence]**.

### 8.5 Assumptions register

| # | Assumption | Label |
|---|---|---|
| 1 | Base SSR 0.8%; soft ramp +5 pp/pull from 65 | [tunable] |
| 2 | Banner window 6 weeks = story arc cadence | [structural] |
| 3 | Gem pricing ≈80 gems/$ at bonus tier; card/BP prices §3.4 | [tunable] |
| 4 | XP curve coefficients (50L²+100L), coin rate 0.25/XP | [tunable] |
| 5 | LB shard table & 625-total star costs (5 SSR dupes = LB5) | [tunable] |
| 6 | MC soft-cap bands & 30 XP/day rate | [tunable] |
| 7 | Gear rates (10% SSR, 50% set, 5 runs/day, pity 10 crafts) | [tunable] |
| 8 | No stat decay; additive-only streaks; free-first gem spend | [structural] |
| 9 | Substats immutable after drop | [structural] |
| 10 | Raw stat scale 0–200, normalized = raw/200 (schema owned by data-schemas doc) | [tunable] |
