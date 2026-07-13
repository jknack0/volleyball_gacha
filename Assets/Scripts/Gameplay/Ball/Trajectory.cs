using System;

namespace VG.Gameplay.Ball
{
    /// <summary>§2.4 net-plane ruling for an authored arc, checked analytically at author time.</summary>
    public enum NetRuling
    {
        /// <summary>Ground track never crosses the net plane (same-side arc: set, dig, …).</summary>
        NoCrossing,
        /// <summary>Crosses with clearance ≥ NetHeight + fault margin.</summary>
        Clears,
        /// <summary>Crossing height below the band — terminal net fault.</summary>
        NetFault,
        /// <summary>Serve/spike inside [NetHeight − 0.04, NetHeight + 0.02) — re-author as cord roll ⚄ (§2.4).</summary>
        NetCord,
        /// <summary>Crosses the net plane outside x ∈ [0, 9] — terminal out (§2.4 antenna rule).</summary>
        AntennaOut,
    }

    /// <summary>Where/whether the ground track crosses the net plane z = 0.</summary>
    public readonly struct NetCrossing
    {
        public readonly bool Crosses;
        /// <summary>Normalized arc position u of the crossing.</summary>
        public readonly float U;
        /// <summary>Ball x at the crossing (wobble included) — the §2.4 antenna check input.</summary>
        public readonly float X;
        /// <summary>Ball height at the crossing.</summary>
        public readonly float Height;

        public NetCrossing(bool crosses, float u, float x, float height)
        {
            Crosses = crosses;
            U = u;
            X = x;
            Height = height;
        }
    }

    /// <summary>
    /// An evaluated authored arc — docs/m0-gameplay-spec.md §2.2: piecewise quadratic Bézier
    /// height + linear ground track, played back over DurationTicks fixed steps (1/60 s).
    ///
    /// Pure and deterministic [structural]: no RNG, no allocation per <see cref="PositionAt"/>,
    /// bit-identical playback for identical params. Every ⚄ input was baked into
    /// <see cref="TrajectoryParams"/> by the caller at author time (§2.5).
    ///
    /// Height construction: two parabola segments joined at (ApexU, ApexHeight) with zero slope
    /// at the join (C1): u ≤ ApexU → h = apex + (start − apex)·(1 − s)², s = u/ApexU;
    /// u &gt; ApexU → h = apex + (end − apex)·t², t = (u − ApexU)/(1 − ApexU).
    /// ApexU = 0 degenerates to the descent segment only (spike: monotonic descent, §2.3).
    ///
    /// Float-serve wobble: lateral offset perpendicular to the ground track,
    /// amplitude · sin(πu) · sin(phase + 2π·cycles·u). The sin(πu) envelope guarantees the
    /// wobble is zero at u = 0 and u = 1 [structural: wobble NEVER moves contact or landing
    /// points — §2.3's "sampled at author time, deterministic playback"].
    ///
    /// Net-crossing simplification [structural, documented]: the crossing u is solved on the
    /// un-wobbled linear track; wobble contributes to the crossing X (antenna check) only.
    /// </summary>
    public sealed class Trajectory
    {
        private readonly TrajectoryParams _p;
        private readonly float _perpX; // unit perpendicular to the ground track, xz plane
        private readonly float _perpZ;

        public Trajectory(TrajectoryParams p)
        {
            if (p.DurationTicks < 1)
                throw new ArgumentException("DurationTicks must be >= 1.", nameof(p));
            if (p.ApexU < 0f || p.ApexU > 1f)
                throw new ArgumentException("ApexU must be in [0, 1].", nameof(p));

            _p = p;

            float dx = p.End.X - p.Start.X;
            float dz = p.End.Z - p.Start.Z;
            float len = MathF.Sqrt(dx * dx + dz * dz);
            if (len > 1e-6f)
            {
                _perpX = -dz / len;
                _perpZ = dx / len;
            }
            else
            {
                _perpX = 0f; // degenerate vertical arc: no defined lateral direction, wobble inert
                _perpZ = 0f;
            }
        }

        public ArcKind Kind => _p.Kind;
        public int DurationTicks => _p.DurationTicks;

        /// <summary>Authored landing point — exact; wobble envelope is zero at u = 1.</summary>
        public Vec3 LandingPoint => _p.End;

        /// <summary>Ball position at a sim tick; ticks clamp to [0, DurationTicks].</summary>
        public Vec3 PositionAt(int tick)
        {
            if (tick < 0) tick = 0;
            else if (tick > _p.DurationTicks) tick = _p.DurationTicks;
            float u = tick / (float)_p.DurationTicks;

            float wob = WobbleOffset(u);
            float x = Lerp(_p.Start.X, _p.End.X, u) + wob * _perpX;
            float z = Lerp(_p.Start.Z, _p.End.Z, u) + wob * _perpZ;
            return new Vec3(x, HeightAt(u), z);
        }

        /// <summary>Bézier height at normalized position u ∈ [0, 1] (see class doc).</summary>
        public float HeightAt(float u)
        {
            if (u <= 0f) return _p.ApexU <= 0f ? _p.ApexHeight : _p.Start.Y;
            if (u >= 1f) return _p.ApexU >= 1f ? _p.ApexHeight : _p.End.Y;

            if (_p.ApexU <= 0f)
            {
                float t = u;
                return _p.ApexHeight + (_p.End.Y - _p.ApexHeight) * t * t;
            }
            if (_p.ApexU >= 1f)
            {
                float s = 1f - u;
                return _p.ApexHeight + (_p.Start.Y - _p.ApexHeight) * s * s;
            }
            if (u <= _p.ApexU)
            {
                float s = 1f - u / _p.ApexU;
                return _p.ApexHeight + (_p.Start.Y - _p.ApexHeight) * s * s;
            }
            float tb = (u - _p.ApexU) / (1f - _p.ApexU);
            return _p.ApexHeight + (_p.End.Y - _p.ApexHeight) * tb * tb;
        }

        /// <summary>
        /// Ground-track crossing of the net plane z = 0. Crossing exists iff Start.Z and End.Z
        /// have strictly opposite signs (an endpoint exactly on the plane is a net-touch contact,
        /// not a crossing) [structural tie-break].
        /// </summary>
        public NetCrossing FindNetCrossing()
        {
            float z0 = _p.Start.Z;
            float z1 = _p.End.Z;
            if (z0 == 0f || z1 == 0f || (z0 > 0f) == (z1 > 0f))
                return new NetCrossing(false, 0f, 0f, 0f);

            float u = z0 / (z0 - z1);
            float x = Lerp(_p.Start.X, _p.End.X, u) + WobbleOffset(u) * _perpX;
            return new NetCrossing(true, u, x, HeightAt(u));
        }

        /// <summary>Classify the arc against §2.4's net/antenna rules. Bounds are ruled separately.</summary>
        public NetRuling RuleNetInteraction(BallTunables tunables)
        {
            var c = FindNetCrossing();
            if (!c.Crosses) return NetRuling.NoCrossing;

            if (c.X < CourtGeometry.AntennaXMin || c.X > CourtGeometry.AntennaXMax)
                return NetRuling.AntennaOut;

            float faultCeiling = CourtGeometry.NetHeight + tunables.NetFaultMargin;
            if (c.Height >= faultCeiling) return NetRuling.Clears;

            bool cordEligible = _p.Kind == ArcKind.ServeFloat
                             || _p.Kind == ArcKind.ServeJump
                             || _p.Kind == ArcKind.Spike;
            float cordFloor = CourtGeometry.NetHeight - tunables.NetCordBandBelow;
            if (cordEligible && c.Height >= cordFloor) return NetRuling.NetCord;

            return NetRuling.NetFault;
        }

        /// <summary>§2.4 bounds rule for the landing point; line = in (CourtGeometry.IsInBounds).</summary>
        public bool LandsInBounds(CourtSide attackedSide)
            => CourtGeometry.IsInBounds(_p.End.X, _p.End.Z, attackedSide);

        private float WobbleOffset(float u)
        {
            if (_p.WobbleAmplitude == 0f) return 0f;
            return _p.WobbleAmplitude
                 * MathF.Sin(MathF.PI * u)
                 * MathF.Sin(_p.WobblePhase + 2f * MathF.PI * _p.WobbleCycles * u);
        }

        /// <summary>Exact-endpoint lerp: returns b bit-exactly at u = 1 (a·(1−u) + b·u).</summary>
        private static float Lerp(float a, float b, float u) => a * (1f - u) + b * u;
    }
}
