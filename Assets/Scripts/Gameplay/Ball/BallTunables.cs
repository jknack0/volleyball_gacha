using System;

namespace VG.Gameplay.Ball
{
    /// <summary>
    /// Per-contact-type default arc parameters and trajectory-rule constants —
    /// docs/m0-gameplay-spec.md §2.2, §2.3, §2.4. One instance per sim; defaults = spec values.
    /// Consolidation into ScriptableObjects happens at VB-12+, not here.
    ///
    /// The factory methods author <see cref="TrajectoryParams"/> from this table. They consume
    /// NO RNG: ⚄ values (wobble phase, deflection jitter direction, net-cord side) are sampled
    /// by the caller and passed in / applied to start–end before authoring (§2.5).
    /// </summary>
    public sealed class BallTunables
    {
        // ---- §2.2 global arc rules -------------------------------------------------------

        /// <summary>Horizontal speed clamp v_max = 30 m/s; too-fast arcs get T raised to the clamp — §2.2 [tunable].</summary>
        public float MaxHorizontalSpeed = 30f;

        // ---- §2.4 net interaction --------------------------------------------------------

        /// <summary>Net fault margin: crossing height &lt; NetHeight + 0.02 m → net — §2.4 [tunable].</summary>
        public float NetFaultMargin = 0.02f;

        /// <summary>Net-cord band lower reach: serve/spike within [NetHeight − 0.04, NetHeight + 0.02] → cord roll — §2.4 [tunable].</summary>
        public float NetCordBandBelow = 0.04f;

        // ---- §2.3 per-contact-type parameter table ----------------------------------------

        /// <summary>Serve (float) T = 1.3 s — §2.3 [tunable].</summary>
        public float ServeFloatDurationSeconds = 1.3f;
        /// <summary>Serve (float) h_apex = 3.4 m — §2.3 [tunable].</summary>
        public float ServeFloatApexHeight = 3.4f;
        /// <summary>Serve (float) u_apex = 0.45 — §2.3 [tunable].</summary>
        public float ServeFloatApexU = 0.45f;
        /// <summary>Float-serve lateral wobble amplitude cap 0.5 m — §2.3 [tunable].</summary>
        public float ServeFloatWobbleAmplitude = 0.5f;
        /// <summary>Wobble sine cycles over the flight — §2.3 gives no frequency; 2 reads as a float S-curve [tunable].</summary>
        public float ServeFloatWobbleCycles = 2f;

        /// <summary>Serve (jump) T = 0.9 s; flatter, faster — §2.3 [tunable].</summary>
        public float ServeJumpDurationSeconds = 0.9f;
        /// <summary>Serve (jump) h_apex = 3.0 m — §2.3 [tunable].</summary>
        public float ServeJumpApexHeight = 3.0f;
        /// <summary>Serve (jump) u_apex = 0.35 — §2.3 [tunable].</summary>
        public float ServeJumpApexU = 0.35f;

        /// <summary>Pass/receive T = 1.6 s — §2.3 [tunable].</summary>
        public float PassDurationSeconds = 1.6f;
        /// <summary>Pass/receive h_apex = 4.5 m — §2.3 [tunable].</summary>
        public float PassApexHeight = 4.5f;
        /// <summary>Pass/receive u_apex = 0.5 — §2.3 [tunable].</summary>
        public float PassApexU = 0.5f;

        /// <summary>Set (high) T = 1.8 s — §2.3 [tunable]; long hang is [structural: readability].</summary>
        public float SetHighDurationSeconds = 1.8f;
        /// <summary>Set (high) h_apex = 6.0 m — §2.3 [tunable].</summary>
        public float SetHighApexHeight = 6.0f;
        /// <summary>Set (high) u_apex = 0.5 — §2.3 [tunable].</summary>
        public float SetHighApexU = 0.5f;

        /// <summary>Set (quick) T = 0.7 s; tight window by design — §2.3 [tunable].</summary>
        public float SetQuickDurationSeconds = 0.7f;
        /// <summary>Set (quick) h_apex = 3.2 m — §2.3 [tunable].</summary>
        public float SetQuickApexHeight = 3.2f;
        /// <summary>Set (quick) u_apex = 0.5 — §2.3 [tunable].</summary>
        public float SetQuickApexU = 0.5f;

        /// <summary>Spike speed floor: v = lerp(14, 26 m/s, quality) — §2.3 [tunable].</summary>
        public float SpikeSpeedMin = 14f;
        /// <summary>Spike speed ceiling — §2.3 [tunable].</summary>
        public float SpikeSpeedMax = 26f;
        /// <summary>Spike contact height base: 2.6 m — §2.3 [tunable].</summary>
        public float SpikeContactHeightBase = 2.6f;
        /// <summary>Spike contact height jump scale: + 0.9 × Jump(normalized) m — §2.3 [tunable].</summary>
        public float SpikeContactHeightJumpScale = 0.9f;

        /// <summary>Roll shot T = 1.1 s — §2.3 [tunable].</summary>
        public float RollShotDurationSeconds = 1.1f;
        /// <summary>Roll shot h_apex = 3.6 m; soft arc over block — §2.3 [tunable].</summary>
        public float RollShotApexHeight = 3.6f;
        /// <summary>Roll shot u_apex = 0.4 — §2.3 [tunable].</summary>
        public float RollShotApexU = 0.4f;

        /// <summary>Block deflection shortest T = 0.5 s (full remaining energy) — §2.3 [tunable].</summary>
        public float BlockDeflectDurationMinSeconds = 0.5f;
        /// <summary>Block deflection longest T = 0.9 s (no remaining energy) — §2.3 [tunable].</summary>
        public float BlockDeflectDurationMaxSeconds = 0.9f;
        /// <summary>Block deflection h_apex = 2.8 m — §2.3 [tunable].</summary>
        public float BlockDeflectApexHeight = 2.8f;
        /// <summary>Block deflection u_apex = 0.3 — §2.3 [tunable].</summary>
        public float BlockDeflectApexU = 0.3f;
        /// <summary>Block deflection direction jitter: reflected incoming ±15° ⚄ — §2.3 [tunable].
        /// The jitter is applied BY THE CALLER (seeded RNG) to the end point before authoring.</summary>
        public float BlockDeflectJitterDegrees = 15f;

        /// <summary>Free ball T = 2.0 s — §2.3 [tunable].</summary>
        public float FreeBallDurationSeconds = 2.0f;
        /// <summary>Free ball h_apex = 5.0 m — §2.3 [tunable].</summary>
        public float FreeBallApexHeight = 5.0f;
        /// <summary>Free ball u_apex = 0.5; always mid-court target — §2.3 [tunable].</summary>
        public float FreeBallApexU = 0.5f;

        // ---- Authoring factories (pure; RNG-free) -----------------------------------------

        /// <summary>Float serve — caller supplies the wobble phase from seeded RNG ⚄ (§2.3).</summary>
        public TrajectoryParams ServeFloat(Vec3 start, Vec3 end, float wobblePhase)
        {
            var p = Author(ArcKind.ServeFloat, start, end,
                ServeFloatApexHeight, ServeFloatApexU, ServeFloatDurationSeconds);
            p.WobbleAmplitude = ServeFloatWobbleAmplitude;
            p.WobblePhase = wobblePhase;
            p.WobbleCycles = ServeFloatWobbleCycles;
            return p;
        }

        public TrajectoryParams ServeJump(Vec3 start, Vec3 end)
            => Author(ArcKind.ServeJump, start, end, ServeJumpApexHeight, ServeJumpApexU, ServeJumpDurationSeconds);

        public TrajectoryParams Pass(Vec3 start, Vec3 end)
            => Author(ArcKind.Pass, start, end, PassApexHeight, PassApexU, PassDurationSeconds);

        public TrajectoryParams SetHigh(Vec3 start, Vec3 end)
            => Author(ArcKind.SetHigh, start, end, SetHighApexHeight, SetHighApexU, SetHighDurationSeconds);

        public TrajectoryParams SetQuick(Vec3 start, Vec3 end)
            => Author(ArcKind.SetQuick, start, end, SetQuickApexHeight, SetQuickApexU, SetQuickDurationSeconds);

        /// <summary>Spike contact height = 2.6 + 0.9 × Jump(normalized 0..1) m — §2.3 [tunable].</summary>
        public float SpikeContactHeight(float jumpNormalized)
            => SpikeContactHeightBase + SpikeContactHeightJumpScale * jumpNormalized;

        /// <summary>
        /// Spike: shallow monotonic descent — h_apex = contact height (Start.Y), u_apex = 0;
        /// T = dist / v where v = lerp(SpikeSpeedMin, SpikeSpeedMax, quality) — §2.3.
        /// </summary>
        public TrajectoryParams Spike(Vec3 start, Vec3 end, float quality)
        {
            if (quality < 0f) quality = 0f; else if (quality > 1f) quality = 1f;
            float v = SpikeSpeedMin + (SpikeSpeedMax - SpikeSpeedMin) * quality;
            float seconds = HorizontalDistance(start, end) / v;
            return Author(ArcKind.Spike, start, end, start.Y, 0f, seconds);
        }

        public TrajectoryParams RollShot(Vec3 start, Vec3 end)
            => Author(ArcKind.RollShot, start, end, RollShotApexHeight, RollShotApexU, RollShotDurationSeconds);

        /// <summary>
        /// Block deflection: T from remaining energy (1 − B) — §2.3/§3.6. Full remaining energy
        /// (1 − B = 1) → fastest arc (T min); no energy → slowest (T max). The ±15° direction
        /// jitter ⚄ is applied BY THE CALLER to <paramref name="end"/> before this call.
        /// </summary>
        public TrajectoryParams BlockDeflection(Vec3 start, Vec3 end, float remainingEnergy)
        {
            if (remainingEnergy < 0f) remainingEnergy = 0f; else if (remainingEnergy > 1f) remainingEnergy = 1f;
            float seconds = BlockDeflectDurationMaxSeconds
                + (BlockDeflectDurationMinSeconds - BlockDeflectDurationMaxSeconds) * remainingEnergy;
            return Author(ArcKind.BlockDeflection, start, end, BlockDeflectApexHeight, BlockDeflectApexU, seconds);
        }

        public TrajectoryParams FreeBall(Vec3 start, Vec3 end)
            => Author(ArcKind.FreeBall, start, end, FreeBallApexHeight, FreeBallApexU, FreeBallDurationSeconds);

        /// <summary>
        /// Shared authoring: converts T to ticks and enforces §2.2's v_max clamp — if the authored
        /// duration implies horizontal speed &gt; MaxHorizontalSpeed, T is raised to dist / v_max.
        /// </summary>
        private TrajectoryParams Author(ArcKind kind, Vec3 start, Vec3 end, float apexHeight, float apexU, float seconds)
        {
            float dist = HorizontalDistance(start, end);
            if (seconds > 0f && dist / seconds > MaxHorizontalSpeed)
                seconds = dist / MaxHorizontalSpeed;
            return new TrajectoryParams
            {
                Kind = kind,
                Start = start,
                End = end,
                ApexHeight = apexHeight,
                ApexU = apexU,
                DurationTicks = CourtGeometry.TicksFromSeconds(seconds)
            };
        }

        private static float HorizontalDistance(Vec3 a, Vec3 b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            return MathF.Sqrt(dx * dx + dz * dz);
        }
    }
}
