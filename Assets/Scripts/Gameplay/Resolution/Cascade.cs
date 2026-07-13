using VG.Data;

namespace VG.Gameplay.Resolution
{
    /// <summary>§3.5 set-grade → spike-window ctx constants. Defaults = spec values.</summary>
    public sealed class CascadeTunables
    {
        /// <summary>Perfect set: spike windows ×1.25 — §3.5 [tunable].</summary>
        public float SpikeCtxPerfectSet = 1.25f;

        /// <summary>Great set: ×1.00 — §3.5 [tunable].</summary>
        public float SpikeCtxGreatSet = 1.0f;

        /// <summary>Good set: ×0.75 — §3.5 [tunable].</summary>
        public float SpikeCtxGoodSet = 0.75f;
    }

    /// <summary>
    /// The quality cascade's gating tables — docs/m0-gameplay-spec.md §3.4 (receive grade →
    /// set options) and §3.5 (set grade → spike window ctx). The §3.4 matrix is [structural]:
    /// it is the game's tactical grammar, hard-coded rather than tunable.
    ///
    /// Callers must pre-filter UNPLAYABLE Shanks (§3.3: q &lt; ShankPlayableMin ⇒ T7, point over);
    /// the Shank row here is the PLAYABLE Shank (desperation ball).
    /// </summary>
    public static class Cascade
    {
        /// <summary>§3.4 matrix [structural]: which set options a receive grade lights up.</summary>
        public static bool IsSetOptionAvailable(ReceiveGrade receive, SetOption option)
        {
            switch (receive)
            {
                case ReceiveGrade.S:
                    return true;                                            // ✓ ✓ ✓ ✓
                case ReceiveGrade.A:
                    return option != SetOption.Dump;                        // ✓ ✓ ✓ —
                case ReceiveGrade.B:
                    return option == SetOption.HighOutside
                        || option == SetOption.BackRowPipe;                 // — ✓ ✓ —
                case ReceiveGrade.C:
                    return option == SetOption.HighOutside;                 // — ✓ — —
                case ReceiveGrade.Shank:
                    return option == SetOption.HighOutside;                 // — ✓(cap) — —
                default:
                    return false;
            }
        }

        /// <summary>§3.4 Shank row: the forced high-outside carries a set-grade cap of Good [structural].</summary>
        public static bool CapsSetGradeAtGood(ReceiveGrade receive) => receive == ReceiveGrade.Shank;

        /// <summary>
        /// §3.5: ctx multiplier for ALL spike windows given the set's timing grade.
        /// Returns false for a Miss set — no spike exists; the rally routes a free ball
        /// (§1.2 free-ball rule), which the caller sequences.
        /// </summary>
        public static bool TryGetSpikeWindowCtx(CascadeTunables tunables, TimingGrade setGrade, out float ctx)
        {
            switch (setGrade)
            {
                case TimingGrade.Perfect:
                    ctx = tunables.SpikeCtxPerfectSet;
                    return true;
                case TimingGrade.Great:
                    ctx = tunables.SpikeCtxGreatSet;
                    return true;
                case TimingGrade.Good:
                    ctx = tunables.SpikeCtxGoodSet;
                    return true;
                default:
                    ctx = 0f;
                    return false;
            }
        }
    }
}
