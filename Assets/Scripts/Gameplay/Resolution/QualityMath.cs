using System;
using VG.Data;

namespace VG.Gameplay.Resolution
{
    /// <summary>
    /// Contact-quality math, docs/m0-gameplay-spec.md §3.2–3.3. Pure, deterministic,
    /// RNG-free, allocation-free.
    /// </summary>
    public static class QualityMath
    {
        /// <summary>floor(stats) = 0.15 + 0.35 × stat_c (§3.2) — quality at a Miss-graded contact.</summary>
        public static float Floor(ResolutionTunables tunables, float statNormalized)
        {
            return tunables.QualityFloorBase + tunables.QualityFloorPerStat * statNormalized;
        }

        /// <summary>ceiling(stats) = 0.55 + 0.45 × stat_c (§3.2) — quality at a Perfect contact.</summary>
        public static float Ceiling(ResolutionTunables tunables, float statNormalized)
        {
            return tunables.QualityCeilingBase + tunables.QualityCeilingPerStat * statNormalized;
        }

        /// <summary>grade_coefficient: Perfect 1.0 / Great 0.7 / Good 0.4 / Miss 0.0 (§3.2).</summary>
        public static float GradeCoefficient(ResolutionTunables tunables, TimingGrade grade)
        {
            switch (grade)
            {
                case TimingGrade.Perfect: return tunables.GradeCoefficientPerfect;
                case TimingGrade.Great: return tunables.GradeCoefficientGreat;
                case TimingGrade.Good: return tunables.GradeCoefficientGood;
                case TimingGrade.Miss: return tunables.GradeCoefficientMiss;
                default:
                    throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
            }
        }

        /// <summary>
        /// quality = floor + grade_coefficient × (ceiling − floor), clamped to [0,1] (§3.2
        /// "quality ∈ [0,1] always" [structural]). <paramref name="statNormalized"/> is the
        /// contact's stat_c (ContactWindows.GoverningStatC). The clamp is load-bearing:
        /// debuffed tunables must not leak negative quality, buffed ones must not exceed 1.
        /// </summary>
        public static float Quality(ResolutionTunables tunables, TimingGrade grade, float statNormalized)
        {
            float floor = Floor(tunables, statNormalized);
            float ceiling = Ceiling(tunables, statNormalized);
            float q = floor + GradeCoefficient(tunables, grade) * (ceiling - floor);
            if (q <= 0f) return 0f;
            if (q >= 1f) return 1f;
            return q;
        }

        /// <summary>
        /// Receive/dig quality → display grade (§3.3 table). Thresholds are inclusive lower
        /// bounds of the better grade: q ≥ 0.85 ⇒ S, ≥ 0.65 ⇒ A, ≥ 0.45 ⇒ B, ≥ 0.25 ⇒ C,
        /// else Shank — matching the spec's "0.65 ≤ q &lt; 0.85" interval notation exactly.
        /// The playable/unplayable Shank split (q ≥ ShankPlayableMin) is point-resolution
        /// territory (VB-7/8); only the constant lives in tunables.
        /// </summary>
        public static ReceiveGrade ReceiveGradeOf(ResolutionTunables tunables, float quality)
        {
            if (quality >= tunables.ReceiveGradeSMin) return ReceiveGrade.S;
            if (quality >= tunables.ReceiveGradeAMin) return ReceiveGrade.A;
            if (quality >= tunables.ReceiveGradeBMin) return ReceiveGrade.B;
            if (quality >= tunables.ReceiveGradeCMin) return ReceiveGrade.C;
            return ReceiveGrade.Shank;
        }
    }
}
