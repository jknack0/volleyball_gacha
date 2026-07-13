using System;

namespace VG.Data
{
    /// <summary>
    /// The closed set of signature-move mechanical primitives (a)–(f) — contract vocabulary,
    /// docs/data-schemas.md §0. Every signature, passive, set bonus, and bond effect compiles
    /// to these; the sim resolves nothing else. [structural]
    /// </summary>
    public enum SigPrimitive
    {
        /// <summary>(a) Guaranteed timing grade for one contact.</summary>
        GuaranteedTimingGrade,
        /// <summary>(b) Timing-window widen/shrink ±X% for N contacts.</summary>
        TimingWindowAdjust,
        /// <summary>(c) Trajectory override — special authored arc, referenced by id.</summary>
        TrajectoryOverride,
        /// <summary>(d) Opponent block/dig quality debuff for N contacts.</summary>
        OpponentContactDebuff,
        /// <summary>(e) Hype gain/drain.</summary>
        HypeDelta,
        /// <summary>(f) Team quality buff +X% for N contacts.</summary>
        TeamQualityBuff,
    }

    /// <summary>
    /// One primitive instance with its parameters (a SignatureMoveDef carries 1–2 of these —
    /// docs/data-schemas.md §1.4 invariant). Flat struct: unused fields are ignored per primitive.
    /// </summary>
    [Serializable]
    public struct SigEffect
    {
        public SigPrimitive Primitive;

        /// <summary>(a): the granted grade.</summary>
        public TimingGrade Grade;

        /// <summary>(b)/(d)/(f): percent magnitude. Signed for (b) — negative shrinks (opponent-targeted shrink is modeled as (b) on the opponent by the applier).</summary>
        public float Percent;

        /// <summary>(b)/(d)/(f): contact count N the modifier lasts.</summary>
        public int Contacts;

        /// <summary>(e): signed Hype amount.</summary>
        public int HypeAmount;

        /// <summary>(e): true → applies to the opponent's meter (drain); false → own team.</summary>
        public bool HypeTargetsOpponent;

        /// <summary>(c): authored-arc id resolved by the trajectory layer at author time.</summary>
        public string TrajectoryOverrideId;
    }
}
