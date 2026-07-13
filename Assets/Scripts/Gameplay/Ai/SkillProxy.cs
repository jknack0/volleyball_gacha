namespace VG.Gameplay.Ai
{
    /// <summary>
    /// Player skill proxies — docs/tooling-pipeline.md §2: the headless simulator has no human,
    /// so a proxy models a player as a timing-grade distribution (decisions still run through
    /// the AI utility layer at a paired tier). Used by the tier-calibration and economy-§8.2
    /// validation suites. All distributions [tunable] — first-guess values, the calibration
    /// suite exists to move them.
    /// </summary>
    public static class SkillProxy
    {
        /// <summary>New/struggling player: worse than Easy AI execution [tunable].</summary>
        public static GradeDistribution Casual => new GradeDistribution
        { Perfect = 0.08f, Great = 0.25f, Good = 0.40f, Miss = 0.27f };

        /// <summary>The reference player for tier calibration (tooling §2a bands) [tunable].</summary>
        public static GradeDistribution Median => new GradeDistribution
        { Perfect = 0.18f, Great = 0.35f, Good = 0.32f, Miss = 0.15f };

        /// <summary>Skilled player — implements economy §8.2's "PI − 0.08 must not hard-wall" clause [tunable].</summary>
        public static GradeDistribution Skilled => new GradeDistribution
        { Perfect = 0.40f, Great = 0.35f, Good = 0.18f, Miss = 0.07f };
    }
}
