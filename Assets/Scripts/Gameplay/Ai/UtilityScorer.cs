using System;
using VG.Data;
using VG.Gameplay.Rng;

namespace VG.Gameplay.Ai
{
    /// <summary>
    /// Per-action §6.1 inputs, all normalized 0..1. Computed by the CALLER from sim state
    /// (matchup remap, score pressure, hype, rally length, §3.4 lit status via
    /// Cascade.IsSetOptionAvailable, and usage counts for surprise) — the scorer only combines.
    /// </summary>
    public struct UtilityInputs
    {
        public float Matchup;   // x_matchup: attacker stat_c − best defender block stat_c, remapped 0..1
        public float Score;     // x_score: (opp − own + 5) / 10 clamped
        public float Hype;      // x_hype: own team Hype / 100
        public float Rally;     // x_rally: min(contacts / 10, 1)
        public float Lit;       // x_lit: 1 if lit at full grade potential, else penalized by the cap
        public float Surprise;  // x_surprise: 1 − (uses of action ÷ actions so far)
    }

    /// <summary>
    /// §6.1 utility scoring: U(a) = Σ w_i × x_i(a), argmax over legal candidates, exact ties
    /// broken by ONE seeded RNG draw ⚄. Draw only via the injected IRng — the Ai stream is
    /// the caller's choice (RngStream.Ai by convention).
    /// </summary>
    public static class UtilityScorer
    {
        /// <summary>U(a) = Σ w_i × x_i(a). §6.1. // [structural: the formula, not a knob]</summary>
        public static float Score(in UtilityWeights w, in UtilityInputs x)
        {
            return w.Matchup * x.Matchup
                 + w.Score * x.Score
                 + w.Hype * x.Hype
                 + w.Rally * x.Rally
                 + w.Lit * x.Lit
                 + w.Surprise * x.Surprise;
        }

        /// <summary>
        /// Index of the argmax-U candidate. A unique maximum consumes NO rng [structural:
        /// rng frugality keeps replays stable — an inserted no-tie decision must not shift
        /// the Ai stream]. Exact float ties (§6.1 ⚄) consume exactly one NextInt draw,
        /// uniform over the tied candidates.
        /// </summary>
        public static int PickArgmax(
            AiTunables tunables, DifficultyTier tier, ReadOnlySpan<UtilityInputs> candidates, IRng rng)
        {
            if (candidates.Length == 0)
                throw new ArgumentException("At least one legal candidate is required.", nameof(candidates));

            UtilityWeights w = tunables.WeightsFor(tier);

            // Pass 1: max value + count of exact ties at max.
            float best = Score(in w, in candidates[0]);
            int bestIndex = 0;
            int tieCount = 1;
            for (int i = 1; i < candidates.Length; i++)
            {
                float u = Score(in w, in candidates[i]);
                if (u > best)
                {
                    best = u;
                    bestIndex = i;
                    tieCount = 1;
                }
                else if (u == best)
                {
                    tieCount++;
                }
            }

            if (tieCount == 1)
                return bestIndex; // unique max: zero draws.

            // Pass 2: exactly one draw picks uniformly among the tied candidates.
            int pick = rng.NextInt(0, tieCount);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (Score(in w, in candidates[i]) == best && pick-- == 0)
                    return i;
            }

            return bestIndex; // unreachable: pass 2 revisits the same values as pass 1.
        }
    }
}
