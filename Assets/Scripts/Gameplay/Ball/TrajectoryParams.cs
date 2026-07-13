namespace VG.Gameplay.Ball
{
    /// <summary>
    /// Minimal engine-free float vector in the CourtGeometry frame (x along net, y up, z across net).
    /// Defined here (NOT in VG.Data) so the pure sim never references UnityEngine.
    /// </summary>
    public readonly struct Vec3
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public Vec3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    /// <summary>Arc archetypes — one per row of the spec §2.3 per-contact-type table.</summary>
    public enum ArcKind
    {
        ServeFloat,
        ServeJump,
        Pass,
        SetHigh,
        SetQuick,
        Spike,
        RollShot,
        BlockDeflection,
        FreeBall
    }

    /// <summary>
    /// Authored arc parameters — spec §2.2: "{start, end, h_apex, u_apex, T, ease, wobble_seed?}",
    /// trivially serializable for replays.
    ///
    /// DETERMINISM CONTRACT [structural]: this struct and <see cref="Trajectory"/> consume NO RNG.
    /// Every ⚄-marked scatter in the spec (float-serve wobble phase, block-deflection ±15° jitter,
    /// net-cord landing side) is sampled BY THE CALLER from the injected seeded IRng at author
    /// time and baked into these fields before construction. Playback is a pure function.
    /// </summary>
    public struct TrajectoryParams
    {
        /// <summary>Contact archetype this arc was authored for (drives net-cord eligibility etc.).</summary>
        public ArcKind Kind;

        /// <summary>Contact point; Start.Y = h_start (contact height) — §2.2.</summary>
        public Vec3 Start;

        /// <summary>Landing target; End.Y = h_end (0 for floor) — §2.2.</summary>
        public Vec3 End;

        /// <summary>Authored apex height h_apex in meters — §2.2.</summary>
        public float ApexHeight;

        /// <summary>Apex position u_apex ∈ [0, 1] along the arc; 0 = monotonic descent (spike) — §2.2/§2.3.</summary>
        public float ApexU;

        /// <summary>Duration T in sim ticks (1/60 s) — §2.2; horizontal speed is a consequence of T and distance.</summary>
        public int DurationTicks;

        /// <summary>Float-serve lateral wobble amplitude in meters (0 = none) — §2.3 [tunable via BallTunables].</summary>
        public float WobbleAmplitude;

        /// <summary>Wobble sine phase in radians — sampled by the caller from seeded RNG ⚄ at author time (§2.3).</summary>
        public float WobblePhase;

        /// <summary>Full sine cycles of wobble over the flight — §2.3 leaves frequency open; default from BallTunables [tunable].</summary>
        public float WobbleCycles;
    }
}
