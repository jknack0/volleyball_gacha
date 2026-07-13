# Compliance & Localization

Source of truth: `PLAN.md` (§4 economy compliance, §7 risk 6). Scope: localization architecture + posture, age rating, store obligations, JP gacha self-regulation, privacy, save/soft-launch data policy, title clearance. One line of legal reality: this doc is engineering/planning input, not legal advice — counsel reviews everything here pre-soft-launch (PLAN §7.6).

---

## 1. Localization posture

**Recommendation: EN-first launch; JP fast-follow evaluated at soft-launch KPIs [tunable].**

Core argument: the game is comedy-forward. Comedy does not translate — it requires **transcreation** (jokes rebuilt per-locale by a writer, not a translator), which carries a cost multiplier over rate-card translation. Cost ownership: `docs/art-budget.md` (Localization subsection: JP transcreation line + comedy-VO cost note). This doc owns posture and scope only; no prices here.

The consequence that matters NOW: shipping EN-only is a content decision, but **the architecture must be loc-ready from M2** (first UI-heavy milestone) [structural]. Retrofitting string keys into a shipped meta UI is the classic expensive mistake; doing it from day one is nearly free.

### 1.1 Loc-ready architecture rules [structural]

| Rule | Detail |
|---|---|
| All user-facing strings behind keys | `LocKey` alias in `docs/data-schemas.md` (conventions block + `displayName` fields). No hardcoded UI text in prefabs/code — editor lint from M2 |
| String tables | **Unity Localization package** (`com.unity.localization`): string tables + locale assets, Addressables-backed (fits PLAN §5.1 live-content pipeline — a JP table ships without a store build) |
| Dev-time fallback | Missing table entry renders the key text itself → EN-only authoring stays frictionless; EN copy IS the key value through launch [structural, per data-schemas conventions] |
| Smart strings for plurals/params | Unity Localization Smart Format for counts ("3 pulls until pity") — never string-concatenate sentence fragments, JP word order breaks it |
| Font fallback plan (JP) | TextMeshPro fallback chain: primary EN display font → **Noto Sans JP** SDF. JP needs kana + Jōyō kanji (~2,136) + JIS X 0208 coverage; use TMP *dynamic* SDF atlas for JP (baked atlases only for the small fixed HUD set) [structural]. Comedy display type (impact text, cut-in copy) needs a JP-capable display weight chosen at JP go-decision — flag to art-budget |
| Text-expansion headroom | Every text container is authored with **~+35% width headroom** over EN copy [tunable]; DE/FR are the usual worst offenders, JP is shorter but taller (line-height). UI review checklist item from M2 (`docs/ui-screens.md`) |
| Orientation-agnostic | Portrait-vs-landscape is unmade (PLAN §2.5, m0 spec §8). Both layouts inherit the same headroom rule; nothing above depends on orientation |
| No text in art | Signage, jersey text, VFX copy: either loc-neutral (numbers, fictional-league marks) or rendered from string tables — never baked into textures |

What is NOT localized pre-JP-decision: VO (EN or non-verbal grunts/exclamations only — JP VO is a separate art-budget line), story scene *content* (transcreation scope), store listing (EN + machine-assisted JP listing acceptable for visibility, marked as such).

---

## 2. Schema integration (done this wave)

`docs/data-schemas.md` patched: `LocKey` defined in the conventions block; `displayName` on `CharacterDef`, `SignatureMoveDef`, `EquipmentSetDef`, `DailyDrillDef`, `OpponentTeamDef`, `BondGroupDef` → `LocKey`; `StoryChapterDef.sceneRefs` noted as carrying `LocKey`-based dialogue. Internal `id` fields stay plain strings. YAML examples keep EN text — consistent with the dev-fallback rule.

---

## 3. Age rating & store compliance

### 3.1 IARC questionnaire — expected answers and outcome

Both stores rate via the IARC questionnaire ([ESRB ratings process](https://www.esrb.org/ratings/ratings-process/)). Our honest answers:

| IARC question area | Our answer | Why |
|---|---|---|
| Violence | Mild/cartoon — sports contact, comedic pratfalls, no injury depiction | Spike-to-the-face is a gag, not violence |
| Simulated gambling (casino-style) | **No** | Gacha is asked separately; we have no casino/betting imagery |
| Paid random items / loot boxes | **Yes** | Banner gacha (PLAN §3.1). Triggers the universal interactive element, not (on ESRB) an age bump |
| In-game purchases | Yes | Gems, battle pass |
| User interaction / UGC | No at launch (V1 has no chat/UGC); ghost PvP (V1.x) shows lineup names only — re-answer at V1.x | PLAN §3.5 |
| Sexual content / language / drugs | None / mild comedic language / none | Co-ed cast is presence, not content — IARC has no question it touches |

**Expected bands [estimate — verify at submission; IARC output is per-questionnaire]:**

| Board | Estimate | Basis |
|---|---|---|
| ESRB | **E10+ or T**, + interactive element **"In-Game Purchases (Includes Random Items)"** (label is universal, assigned to all gacha regardless of age category — [ESRB](https://www.esrb.org/blog/in-game-purchases-includes-random-items/)) | Content alone is E10+-ish; comedic mischief may tip T |
| PEGI | **PEGI 16 by default** under the March 2026 overhaul: any game selling paid random items gets minimum 16 for submissions from June 2026, with experimental mitigation paths down to 7/12 ([Reed Smith summary](https://www.reedsmith.com/articles/pegi-launches-interactive-risk-categories-overhauls-age-ratings-for-loot-boxes-in-game-spending-and-communication-features/), [PEGI paid-random-items notice](https://pegi.info/news/pegi-introduces-feature-notice)) | We launch after June 2026 → **plan for PEGI 16; investigate mitigation criteria (spend caps, disclosure quality) before EU listing** — this is the single biggest rating surprise in the estimate |
| CERO | **B (12+)** band-equivalent — mild comedic violence, no antisocial content | CERO covers boxed/console releases ([CERO rating system](https://www.cero.gr.jp/en/publics/index/17/)); mobile storefronts in JP show IARC/Apple ratings instead, so CERO only matters if a console port ever happens. Listed because the task asks for the band |

EU watch item: the Digital Fairness Act process (IMCO vote Oct 2025) may restrict loot boxes for minors; PEGI 16 default partially insulates us, monitor at M5.

### 3.2 Store obligations — exact and already covered

| Store | Obligation (verbatim policy) | Our compliance |
|---|---|---|
| Apple | App Review 3.1.1: "Apps offering 'loot boxes' or other mechanisms that provide randomized virtual items for purchase must disclose the odds of receiving each type of item to customers **prior to purchase**" ([App Review Guidelines](https://developer.apple.com/app-store/review/guidelines/)) | Rates published in-UI on the banner screen *before* the pull button, per PLAN §4 ("published gacha rates, pity counters visible in-UI") and `docs/ui-screens.md` §5 (Pull Ceremony & Gacha Screens: always-visible pity counter + 50/50 state, odds page one tap from the banner screen, spend confirm). `BannerDef` carries the published rates as data (data-schemas §1.8); `Pity_Statistical_10kSeededRolls` test defends published-rate truthfulness (data-schemas §5.1) — a wrong published rate is a store violation, not just a bug |
| Google Play | Monetization policy: "Apps offering mechanisms to receive randomized virtual items from a purchase (i.e. 'loot boxes') must clearly disclose the odds of receiving those items **in advance of purchase**" ([Game Developer coverage](https://www.gamedeveloper.com/business/games-on-the-google-play-store-now-required-to-disclose-loot-box-odds), policy live since May 2019) | Same mechanism satisfies both. Per-item disclosure granularity: per-rarity rates + per-character within-rarity odds (uniform within rarity except featured) shown on the rates detail panel |

Disclosure format rule [structural]: odds shown per rarity **and** per obtainable character (featured vs off-banner split), plus current pity counter (`PityState.lifetimePulls` / `pullsSinceSsr`) — exceeds both stores' floor and matches JOGA display norms (§3.3).

### 3.3 Japan market — kompu-gacha analysis

Background: "complete gacha" (kompu gacha) — granting a prize for assembling a **specific set** of items obtained from paid random draws — is illegal in Japan as "card matching" under the Prize Display Act (Keihyōhyō/景表法), per the Consumer Affairs Agency's July 2012 operation standards ([Monolith Law analysis](https://monolith.law/en/general-corporate/game-random-complete-illegal), [Lexology/aplaw](https://www.lexology.com/library/detail.aspx?g=9207df10-a8a2-4f67-81c3-6a148a6100e2)). Ordinary gacha remains legal, self-regulated by [JOGA random-item guidelines](https://monolith.law/en/general-corporate/online-game-payment-regulation-unjustifiable-premiums-act) (rate display, spend-expectation norms).

**Do bond groups (PLAN §3.1 bond links; data-schemas §1.14) constitute kompu-gacha? Honest analysis:**

The uncomfortable part first: a bond group DOES reward owning multiple *specific* characters, most of whom come from paid gacha. That is structurally adjacent to card matching — "presenting a specific combination of different types of tokens" for a benefit. Anyone claiming zero resemblance is not reading the CAA definition.

Why it nonetheless does **not** fall under the prohibition as designed:

| Card-matching element | Our design | Verdict |
|---|---|---|
| A *prize* (keihin) — a new, separable item/benefit granted on completion | Bond bonus is a **conditional stat modifier, active only while ≥2 members are fielded in the lineup** (data-schemas §2.7); it deactivates on lineup change, is never a granted item, currency, or character, and is capped ≤+3% per group (data-schemas §1.14 invariant) | Not a separable prize; an attribute of using the items together — the pattern JP industry treats as synergy, not kompu (ubiquitous in JP-published gacha: team-composition bonuses) |
| *Complete* a specific enumerated set | Threshold is **any 2+ of a group** (default 2), not the full member list; groups MAY include the MC, who is free and not in gacha (data-schemas §1.3/§1.14) | No "complete the set" chase for the last missing piece — the escalation mechanic the CAA targeted |
| Set members obtainable only via paid draws | Story/free characters and the MC can be bond members; gems are earnable F2P (PLAN §4) | Weakens the "paid lottery combination" element further |

**Finding: bond groups as specified are not kompu-gacha, with three design guardrails that MUST hold [structural]:** (1) bond activation never grants an item/currency/character — modifier only; (2) no bond group may require its complete member list (threshold < member count, or count = 2); (3) bonuses stay within the ≤3% per-group / lineup-cap budget so no combination is presented as a chase-worthy prize. Any future "collect all members of X school → reward" event idea is the prohibited pattern — write that down as PROHIBITED now. JP counsel reviews this analysis before any JP listing [gate at JP go-decision].

Also confirmed: no other kompu pattern exists in the design — equipment sets (PLAN §3.3) are farmable/craftable, not gacha; crafting pity is deterministic.

---

## 4. Privacy & data

### 4.1 Pre-backend (M0–M4): no-PII posture [structural]

| Item | Posture |
|---|---|
| Accounts | None. Local save + platform cloud save (PLAN §5.3); `playerId` is a locally generated GUID, never transmitted |
| Telemetry | Local JSONL only, on-device (`docs/m0-hardening.md` §2.3: no vendor SDKs, no PII sinks); nothing leaves the device |
| Third-party SDKs | **None before M4** [structural]. Crash reporting from M1 (PLAN §5.4) is the one exception — self-hosted GlitchTip preferred over Sentry SaaS to keep the no-third-party posture honest; crash payloads scrubbed of paths/device identifiers |
| Ads / tracking / ATT | No ads, no tracking, no IDFA use → Apple ATT prompt not required pre-M5 (and target: never — no ad SDKs planned) |

### 4.2 M5+ (backend, IAP, analytics live): GDPR/CCPA basics

| Requirement | Plan |
|---|---|
| Lawful basis (GDPR) | Contract performance for account/save/purchases; legitimate interest for anti-fraud + crash; **consent** for analytics where required (EU) — consent toggle at first run in EU locales [tunable] |
| Data inventory | Account id, platform store id, purchase receipts, save state, telemetry events (design events + device model/OS — no contacts, no location, no advertising ID) |
| Deletion path | Support-ticket flow at M5 (email → verified deletion incl. backend save + analytics user id), in-app self-serve deletion evaluated post-launch [tunable]. Apple requires account deletion in-app if accounts exist — check at M5 gate |
| Access/export | Same support path; JSON export of save + purchase history |
| Processors | PlayFab (or Nakama host), analytics vendor, receipt validation — DPA with each at M5; list in privacy policy |
| Retention | Telemetry raw ≤ 24 months, receipts per tax law, deleted accounts purged ≤ 30 days [tunable] |

### 4.3 COPPA / age gate

- Marketing + store category target **13+** [tunable]; not "directed to children" (comedy parody register, gacha monetization, teen-and-up genre norms all point away from child-directed).
- No social features at launch → **no age screen needed in V1**. If/when V1.x ghost-PvP leaderboards add display names or any social surface: add a **neutral age screen** (free-entry date, no nudging) and gate accordingly [tunable].
- Do not use "for kids"/child-appeal store metadata; Google Play target-audience declaration set to 13+.

### 4.4 Store privacy label draft rows

Apple "nutrition label" / Google Data safety, drafted for **M5 state** (pre-M5 truthfully declares "data not collected"):

| Data type | Collected? | Linked to identity? | Purpose |
|---|---|---|---|
| Purchase history | Yes (M5+) | Yes (account) | App functionality (entitlements), fraud |
| Gameplay content / progress | Yes (M5+) | Yes (account) | App functionality (cloud save) |
| Product interaction (analytics) | Yes (M5+) | No (pseudonymous analytics id) | Analytics |
| Crash/diagnostics | Yes (M1+, self-hosted) | No | App functionality |
| Identifiers (advertising) | **No** | — | No ad SDKs |
| Location, contacts, browsing, health, financial info | **No** | — | Never collected |

---

## 5. Save & soft-launch data policy

**Recommendation: soft-launch progress is KEPT at the M5 backend cutover.** `SaveGame` is versioned with single-step migrations and backup-before-migrate (data-schemas §2.1), so cutover is a **server import of the local save** (validate → ingest → server becomes authoritative), not a wipe. Publish "progress may be reset during soft launch" in the soft-launch ToS **as legal cover only** [tunable] — wiping paying users' pity/pulls is reputational damage the genre punishes hard, and our migration machinery makes keeping progress cheap.

Local-save tamper posture (pre-revenue there is nothing to steal but balance-test integrity):

| Phase | Posture | Rationale |
|---|---|---|
| M0–M1 | Plain local JSON, zero protection — **accept risk** [structural] | No economy exists; friction only hurts iteration |
| M2–M4 | Add **checksum + light obfuscation** (HMAC over canonical JSON with an app-embedded key, file XOR/AES) — cheap, one-day task at M2 | Deters casual editing while currencies/gacha are local; NOT security (key ships in the binary), just an honesty speed bump for playtests |
| M5 | **Server-authoritative wallet + gacha rolls** close the hole for real (PLAN §5.4: deterministic core moves server-side as a transport change) | Real money ⇒ real authority. Local save keeps only non-monetary state cache |

Import fraud note for M5: tampered pre-cutover saves could import inflated wallets — sanity-cap imported currencies/pity against telemetry-plausible maxima at ingest [structural]; whales flagged, not auto-trusted.

---

## 6. Title & identity clearance checklist

Title is TBD (PLAN §8). No proposals here — process only. Run the full list per candidate BEFORE attachment forms:

1. **Trademark search** — Nice classes **9** (downloadable software/games) and **41** (entertainment services/online games): USPTO TESS, EUIPO, J-PlatPat (JP). Knockout search first (identical + phonetic), full search via counsel only for the shortlist survivor.
2. **Store-name collision** — search App Store + Google Play for the exact name and close variants; store search is the discovery surface, a collision is fatal even if legally clear. Check major gacha/sports titles' sub-brands too.
3. **Domain + socials** — .com/.gg/.jp availability; handle availability on X, YouTube, TikTok, Discord vanity. Uniform handle > perfect name.
4. **JP katakana rendering check** — transliterate the candidate to katakana: does it collapse into an existing brand's katakana? Is it pronounceable/short (≤6 morae preferred)? Does it accidentally mean something? Native-speaker check, one hour, before shortlisting [structural for JP fast-follow credibility].
5. **General-language screen** — slang/profanity scan in EN/JP/DE/FR/ES/PT/KO (the usual expansion set); comedy title candidates are unusually prone to double meanings — that's partly the point, so screen for *unintended* ones.
6. **Volleyball-world conflicts** — real leagues, teams, federations (FIVB etc.), and existing volleyball media (Haikyu!! trademarks) — parody register does not license proximity.
7. Working title used in builds/repos stays uncleared-safe: keep `volleyball_gacha` internally until clearance passes.

---

Cross-doc: loc cost ownership → `docs/art-budget.md` (Localization subsection); odds-disclosure UI → `docs/ui-screens.md` §5; telemetry posture → `docs/m0-hardening.md` §2.3; `LocKey` schema rules → `docs/data-schemas.md`; economy compliance intents → `PLAN.md` §4.
