using System;
using VG.Data;

namespace VG.Gameplay.Resolution
{
    /// <summary>
    /// Timing-window math, docs/m0-gameplay-spec.md §3.1. Pure, deterministic, RNG-free,
    /// allocation-free. All widths are half-widths in ms around the ideal tick t*
    /// (bands are centered: an offset of ±x ms classifies identically).
    /// </summary>
    public static class ContactWindows
    {
        /// <summary>
        /// stat_c for a contact = mean of its governing stats, normalized 0..1
        /// (§3.1 governing-stats table [structural]):
        /// Serve ← (Serve, Power); Receive/Dig ← (Receive, Speed); Set ← Technique;
        /// Spike ← (Power, Jump); Block ← (Jump, Technique).
        /// </summary>
        public static float GoverningStatC(ContactType contact, in StatBlock stats)
        {
            switch (contact)
            {
                case ContactType.Serve:
                    return 0.5f * (stats.Normalized(StatId.Serve) + stats.Normalized(StatId.Power));
                case ContactType.Receive:
                case ContactType.Dig:
                    return 0.5f * (stats.Normalized(StatId.Receive) + stats.Normalized(StatId.Speed));
                case ContactType.Set:
                    return stats.Normalized(StatId.Technique);
                case ContactType.Spike:
                    return 0.5f * (stats.Normalized(StatId.Power) + stats.Normalized(StatId.Jump));
                case ContactType.Block:
                    return 0.5f * (stats.Normalized(StatId.Jump) + stats.Normalized(StatId.Technique));
                default:
                    throw new ArgumentOutOfRangeException(nameof(contact), contact, null);
            }
        }

        /// <summary>
        /// Full timing window (the Good outer edge, ms): base_ms × (1 + k_stat × stat_c) × ctx × assist (§3.1).
        /// <paramref name="statNormalized"/> is stat_c (see <see cref="GoverningStatC"/>), 0..1.
        /// <paramref name="ctxMultiplier"/> is computed upstream (§3.5 spike←set-grade via VB-7,
        /// §4.2 serve aim-risk, tunables.QuickSetAttackCtx, else tunables.DefaultCtx).
        /// <paramref name="assistMultiplier"/> from tunables.AssistMultiplier (§7.4), player windows only.
        /// <paramref name="contact"/> does not alter base_ms — per §3.1 the base is per grade, not per
        /// contact; contact identity acts through stat_c and ctx. The parameter pins the call shape
        /// for VB-7/8 composition.
        /// </summary>
        public static float WindowMs(
            ResolutionTunables tunables,
            ContactType contact,
            float statNormalized,
            float ctxMultiplier,
            float assistMultiplier)
        {
            return tunables.BaseGoodMs
                   * (1f + tunables.KStat * statNormalized)
                   * ctxMultiplier
                   * assistMultiplier;
        }

        /// <summary>
        /// Grade = smallest band containing |Δ| (§3.1). Bands are centered on t*; the sign of
        /// <paramref name="tapOffsetMs"/> (early/late) is irrelevant. Band edges scale off
        /// <paramref name="windowMs"/> in the base_ms proportions (40/90/150 by default), so all
        /// multipliers in §3.1 widen every band uniformly.
        /// Edge rule [structural, data-schemas §5.1 TimingGrade_BoundaryMs_Exact]: a boundary
        /// value resolves to the BETTER grade (inclusive ≤) — a frame-perfect tap is never demoted.
        /// Beyond windowMs ⇒ Miss.
        /// </summary>
        public static TimingGrade Classify(ResolutionTunables tunables, float tapOffsetMs, float windowMs)
        {
            float delta = Math.Abs(tapOffsetMs);
            if (delta > windowMs)
                return TimingGrade.Miss;

            // scale == the §3.1 multiplier product; == 1 exactly when windowMs == BaseGoodMs,
            // which keeps the default band edges at exactly 40/90/150 ms.
            float scale = windowMs / tunables.BaseGoodMs;
            if (delta <= tunables.BasePerfectMs * scale)
                return TimingGrade.Perfect;
            if (delta <= tunables.BaseGreatMs * scale)
                return TimingGrade.Great;
            return TimingGrade.Good;
        }
    }
}
