namespace VG.Gameplay.Resolution
{
    /// <summary>
    /// Every constant of docs/m0-gameplay-spec.md §3.1–3.3 + §7.4 in one instantiable bag.
    /// Defaults ARE the spec values. Consolidation into ScriptableObjects happens at VB-12+;
    /// until then callers new one up (or mutate a copy for tuning/tests).
    /// </summary>
    public sealed class ResolutionTunables
    {
        // ---- §3.1 Timing windows -------------------------------------------------------
        // W_grade = base_ms[grade] × (1 + k_stat × stat_c) × ctx × assist.
        // base_ms is per GRADE, not per contact type: contact identity enters the formula
        // only through stat_c (governing stats, ContactWindows.GoverningStatC) and ctx.

        /// <summary>Perfect band half-width at stat 0, ms. §3.1 base_ms. // [tunable]</summary>
        public float BasePerfectMs = 40f;

        /// <summary>Great band half-width at stat 0, ms. §3.1 base_ms. // [tunable]</summary>
        public float BaseGreatMs = 90f;

        /// <summary>Good band half-width at stat 0, ms — the outer edge; beyond it is Miss. §3.1 base_ms. // [tunable]</summary>
        public float BaseGoodMs = 150f;

        /// <summary>Window widening per stat point: ×(1 + KStat × stat_c). §3.1 k_stat. // [tunable]</summary>
        public float KStat = 0.5f;

        /// <summary>Perfect share of the full (Good) window: 40/150 with defaults. Derived from base_ms. // [tunable via base_ms]</summary>
        public float PerfectFraction { get { return BasePerfectMs / BaseGoodMs; } }

        /// <summary>Great share of the full (Good) window: 90/150 with defaults. Derived from base_ms. // [tunable via base_ms]</summary>
        public float GreatFraction { get { return BaseGreatMs / BaseGoodMs; } }

        // ---- §3.1 ctx (context multiplier) ---------------------------------------------
        // ctx is COMPUTED UPSTREAM and passed into ContactWindows.WindowMs:
        //   spike  ← set-grade table §3.5 (VB-7 owns the cascade; value arrives as ctxMultiplier),
        //   serve  ← aim-risk §4.2 (aim model owns it),
        //   quick set attack ← QuickSetAttackCtx below,
        //   everything else ← DefaultCtx.

        /// <summary>Quick-set attack window shrink, §3.1 ctx row. // [tunable]</summary>
        public float QuickSetAttackCtx = 0.8f;

        /// <summary>Neutral ctx ("else 1.0"), §3.1 ctx row. // [structural]</summary>
        public float DefaultCtx = 1.0f;

        // ---- §7.4 Accessibility assist (player windows only, never AI) -----------------
        // Widen steps +0% / +25% / +50%; applied as the assist multiplier in §3.1.

        /// <summary>Assist level 0 widen (off). §7.4. // [tunable]</summary>
        public float AssistWidenLevel0 = 0.00f;

        /// <summary>Assist level 1 widen (+25%). §7.4. // [tunable]</summary>
        public float AssistWidenLevel1 = 0.25f;

        /// <summary>Assist level 2 widen (+50%). §7.4. // [tunable]</summary>
        public float AssistWidenLevel2 = 0.50f;

        /// <summary>Assist widen level → window multiplier (1 + widen). §7.4 / §3.1 assist.</summary>
        public float AssistMultiplier(int level)
        {
            switch (level)
            {
                case 0: return 1f + AssistWidenLevel0;
                case 1: return 1f + AssistWidenLevel1;
                case 2: return 1f + AssistWidenLevel2;
                default:
                    throw new System.ArgumentOutOfRangeException(
                        nameof(level), level, "Assist has exactly three steps (§7.4).");
            }
        }

        // ---- §3.2 Quality formula [structural shape, tunable constants] ----------------
        // quality = floor(stat_c) + grade_coefficient × (ceiling(stat_c) − floor(stat_c)), clamped [0,1].

        /// <summary>floor(stats) intercept: floor = 0.15 + 0.35 × stat_c. §3.2. // [tunable]</summary>
        public float QualityFloorBase = 0.15f;

        /// <summary>floor(stats) slope per stat_c. §3.2. // [tunable]</summary>
        public float QualityFloorPerStat = 0.35f;

        /// <summary>ceiling(stats) intercept: ceiling = 0.55 + 0.45 × stat_c. §3.2. // [tunable]</summary>
        public float QualityCeilingBase = 0.55f;

        /// <summary>ceiling(stats) slope per stat_c. §3.2. // [tunable]</summary>
        public float QualityCeilingPerStat = 0.45f;

        /// <summary>grade_coefficient, Perfect. §3.2. // [tunable]</summary>
        public float GradeCoefficientPerfect = 1.0f;

        /// <summary>grade_coefficient, Great. §3.2. // [tunable]</summary>
        public float GradeCoefficientGreat = 0.7f;

        /// <summary>grade_coefficient, Good. §3.2. // [tunable]</summary>
        public float GradeCoefficientGood = 0.4f;

        /// <summary>grade_coefficient, Miss — quality collapses to floor(stats). §3.2 / §3.3. // [tunable]</summary>
        public float GradeCoefficientMiss = 0.0f;

        // ---- §3.3 Receive display-grade thresholds (q ≥ threshold ⇒ grade) -------------

        /// <summary>S: q ≥ 0.85. §3.3. // [tunable]</summary>
        public float ReceiveGradeSMin = 0.85f;

        /// <summary>A: q ≥ 0.65. §3.3. // [tunable]</summary>
        public float ReceiveGradeAMin = 0.65f;

        /// <summary>B: q ≥ 0.45. §3.3. // [tunable]</summary>
        public float ReceiveGradeBMin = 0.45f;

        /// <summary>C: q ≥ 0.25; below is Shank. §3.3. // [tunable]</summary>
        public float ReceiveGradeCMin = 0.25f;

        /// <summary>Shank sub-rule: q ≥ 0.10 ⇒ playable Shank, below ⇒ point over (T7).
        /// Constant lives here with its grade table; the T7 decision itself is VB-7/8. §3.3. // [structural]</summary>
        public float ShankPlayableMin = 0.10f;
    }
}
