namespace VG.Gameplay.Hype
{
    /// <summary>
    /// Hype accrual table — docs/m0-gameplay-spec.md §3.7. Defaults = spec values.
    /// Per-team meter 0–100, threshold 100 → Ignition [structural per contract].
    /// </summary>
    public sealed class HypeTunables
    {
        /// <summary>Meter bounds [structural per contract: per-team 0–100].</summary>
        public int HypeMax = 100;

        /// <summary>Ignition threshold — §1.3/§3.7 [structural].</summary>
        public int IgnitionThreshold = 100;

        /// <summary>Perfect contact +4 — §3.7 [tunable].</summary>
        public int PerfectContact = 4;

        /// <summary>Kill +10 — §3.7 [tunable].</summary>
        public int Kill = 10;

        /// <summary>Stuff block +14 — §3.7 [tunable].</summary>
        public int StuffBlock = 14;

        /// <summary>Ace +14 — §3.7 [tunable].</summary>
        public int Ace = 14;

        /// <summary>Rally ≥ 6 contacts: per extra contact, BOTH teams +2 — §3.7 [tunable].</summary>
        public int LongRallyPerContact = 2;

        /// <summary>The rally length at which the both-teams accrual starts — §3.7 [tunable].</summary>
        public int LongRallyThreshold = 6;

        /// <summary>Dug an A ≥ 0.8 spike: digging team +8 — §3.7 [tunable].</summary>
        public int BigSpikeDug = 8;

        /// <summary>The A (attack power) threshold that makes a dig "big" — §3.7 [tunable].</summary>
        public float BigSpikeAThreshold = 0.8f;

        /// <summary>Own error (net/out) −6 — §3.7 [tunable].</summary>
        public int OwnError = -6;
    }
}
