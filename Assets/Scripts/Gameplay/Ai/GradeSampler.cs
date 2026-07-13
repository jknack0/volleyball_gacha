using VG.Data;
using VG.Gameplay.Rng;

namespace VG.Gameplay.Ai
{
    /// <summary>
    /// §6.2 AI timing execution: the AI never "taps" — it samples its TimingGrade directly
    /// from the tier's distribution ⚄. The sampled grade then feeds §3.2 exactly like a
    /// player grade; nothing here touches player windows (§6.4).
    /// </summary>
    public static class GradeSampler
    {
        /// <summary>
        /// One grade per call, via exactly one NextFloat01 draw mapped through the tier's
        /// CDF in spec column order Perfect→Great→Good→Miss [structural: exactly one draw
        /// per sample — replay stability]. Miss absorbs any tuning remainder, so the sampler
        /// stays total even if a mutated distribution sums below 1.
        /// </summary>
        public static TimingGrade Sample(AiTunables tunables, DifficultyTier tier, IRng rng)
            => Sample(tunables.GradesFor(tier), rng);

        /// <summary>
        /// Distribution-direct overload — used by player skill proxies (tooling-pipeline §2:
        /// headless has no human, so a proxy IS a grade distribution). Same one-draw contract.
        /// </summary>
        public static TimingGrade Sample(in GradeDistribution d, IRng rng)
        {
            float u = rng.NextFloat01();

            float c = d.Perfect;
            if (u < c) return TimingGrade.Perfect;
            c += d.Great;
            if (u < c) return TimingGrade.Great;
            c += d.Good;
            if (u < c) return TimingGrade.Good;
            return TimingGrade.Miss;
        }
    }
}
