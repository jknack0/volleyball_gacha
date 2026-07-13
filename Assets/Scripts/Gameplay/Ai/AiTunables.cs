using VG.Data;

namespace VG.Gameplay.Ai
{
    /// <summary>Per-tier §6.1 utility weights. Field names mirror the spec's w_* rows.</summary>
    public struct UtilityWeights
    {
        public float Matchup;   // w_matchup
        public float Score;     // w_score
        public float Hype;      // w_hype
        public float Rally;     // w_rally
        public float Lit;       // w_lit
        public float Surprise;  // w_surprise
    }

    /// <summary>Per-tier §6.2 timing-grade probabilities. Must sum to 1 (Miss absorbs CDF remainder).</summary>
    public struct GradeDistribution
    {
        public float Perfect;
        public float Great;
        public float Good;
        public float Miss;
    }

    /// <summary>
    /// Every constant of docs/m0-gameplay-spec.md §6 in one instantiable bag. Defaults ARE the
    /// spec values. Consolidation into ScriptableObjects happens at VB-12+; until then callers
    /// new one up (or mutate a copy for tuning/tests).
    ///
    /// §6.4 HARD RULE [structural]: this module carries ZERO player-side concepts. Difficulty
    /// NEVER adds player input latency, shrinks player windows, or alters player-side math —
    /// it lives exclusively in AI decision quality (§6.1 weights), AI execution (§6.2
    /// distributions), and AI vocabulary (§6.3). Nothing in VG.Gameplay.Ai's public surface
    /// mentions windows, latency, or assist; AiTests enforces this by reflection scan.
    /// </summary>
    public sealed class AiTunables
    {
        // ---- §6.1 weight tables per difficulty tier [all tunable] ----------------------

        /// <summary>Easy: safe, rally-prolonging, lit-chasing; no matchup hunting to speak of. §6.1. // [tunable]</summary>
        public UtilityWeights EasyWeights = new UtilityWeights
        {
            Matchup = 0.2f, Score = 0.1f, Hype = 0.0f, Rally = 0.3f, Lit = 0.4f, Surprise = 0.0f,
        };

        /// <summary>Normal: balanced; starts varying its looks (w_surprise > 0). §6.1. // [tunable]</summary>
        public UtilityWeights NormalWeights = new UtilityWeights
        {
            Matchup = 0.4f, Score = 0.2f, Hype = 0.1f, Rally = 0.2f, Lit = 0.4f, Surprise = 0.2f,
        };

        /// <summary>Hard: matchup-hunting, score-aware, unpredictable. §6.1. // [tunable]</summary>
        public UtilityWeights HardWeights = new UtilityWeights
        {
            Matchup = 0.6f, Score = 0.3f, Hype = 0.2f, Rally = 0.1f, Lit = 0.4f, Surprise = 0.4f,
        };

        // ---- §6.2 grade distributions per tier [all tunable] ---------------------------
        // Rubber-banding (±one column) is M1+; not represented here in M0.

        /// <summary>Easy execution: mostly Good, frequent Miss. §6.2. // [tunable]</summary>
        public GradeDistribution EasyGrades = new GradeDistribution
        {
            Perfect = 0.10f, Great = 0.30f, Good = 0.40f, Miss = 0.20f,
        };

        /// <summary>Normal execution. §6.2. // [tunable]</summary>
        public GradeDistribution NormalGrades = new GradeDistribution
        {
            Perfect = 0.25f, Great = 0.40f, Good = 0.25f, Miss = 0.10f,
        };

        /// <summary>Hard execution: near-pro. §6.2. // [tunable]</summary>
        public GradeDistribution HardGrades = new GradeDistribution
        {
            Perfect = 0.45f, Great = 0.35f, Good = 0.15f, Miss = 0.05f,
        };

        /// <summary>Tier → §6.1 weight row. // [structural: pure lookup]</summary>
        public UtilityWeights WeightsFor(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return EasyWeights;
                case DifficultyTier.Hard: return HardWeights;
                default: return NormalWeights;
            }
        }

        /// <summary>Tier → §6.2 distribution row. // [structural: pure lookup]</summary>
        public GradeDistribution GradesFor(DifficultyTier tier)
        {
            switch (tier)
            {
                case DifficultyTier.Easy: return EasyGrades;
                case DifficultyTier.Hard: return HardGrades;
                default: return NormalGrades;
            }
        }

        // §6.3 tactic vocabulary is [structural] (the game's difficulty grammar), so it is
        // hard-coded in TacticVocabulary rather than carried here as tunable data.
    }
}
