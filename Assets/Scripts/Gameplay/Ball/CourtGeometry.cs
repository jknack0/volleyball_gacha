using VG.Data;

namespace VG.Gameplay.Ball
{
    /// <summary>Which half of the court a point / team occupies, named by the sign of z.</summary>
    public enum CourtSide
    {
        /// <summary>Half occupying z ∈ [−9, 0].</summary>
        NegativeZ,
        /// <summary>Half occupying z ∈ [0, +9].</summary>
        PositiveZ
    }

    /// <summary>
    /// Court dimensions, net, antennas, and the 3×3 aim-zone grid
    /// (docs/m0-gameplay-spec.md §2.3, §2.4, §4.1).
    ///
    /// COORDINATE FRAME [structural] — defined here once; ALL downstream gameplay code adopts it:
    ///   * Left-handed, Unity-style axes: +y up, +z "forward"; an observer facing +z has +x on their RIGHT.
    ///   * Origin: base of the net post on the x = 0 sideline, on the floor, in the net plane.
    ///   * +x runs along the net; the sidelines are x = 0 and x = 9 (court width, §2.3).
    ///   * +y is height in meters (floor = 0).
    ///   * +z is perpendicular to the net; the NET PLANE IS z = 0.
    ///     <see cref="CourtSide.PositiveZ"/> half is z ∈ [0, +9]; <see cref="CourtSide.NegativeZ"/> half is z ∈ [−9, 0].
    ///   * §2.4's antenna rule "ground track crossing the net plane outside x ∈ [0, 9] m → out" reads verbatim.
    ///
    /// Zone-grid conventions (§4.1): columns L/C/R are labeled from the ATTACKING side's view
    /// (the team on the OTHER half of the landing point); rows F/M/B are distance from the net.
    /// For the PositiveZ half (attacked from NegativeZ, attacker faces +z, right = +x):
    ///   column L = x ∈ [0,3), C = [3,6), R = [6,9]; row F = z ∈ [0,3), M = [3,6), B = [6,9].
    /// The NegativeZ half is the mirror image (attacker faces −z, so column L sits at HIGH x).
    /// Tie-break [structural]: a coordinate exactly on an interior gridline belongs to the
    /// higher-index zone in ATTACKER-LOCAL coordinates (farther from the attacker's left
    /// sideline / farther from the net). Points off the half's 9×9 rectangle are clamped to
    /// the nearest zone — out-of-bounds is judged separately by <see cref="IsInBounds"/>.
    /// </summary>
    public static class CourtGeometry
    {
        /// <summary>Fixed sim timestep is 1/60 s — spec §2.5 [structural].</summary>
        public const int TicksPerSecond = 60;

        /// <summary>Full court length along z, meters — spec §2.3 [structural].</summary>
        public const float CourtLength = 18f;

        /// <summary>Court width along x (net length), meters — spec §2.3 [structural].</summary>
        public const float CourtWidth = 9f;

        /// <summary>One half: 9×9 m — spec §2.4 [structural].</summary>
        public const float HalfLength = 9f;

        /// <summary>Net height, meters — spec §2.3: "net height 2.24 m (co-ed compromise)" [tunable].</summary>
        public const float NetHeight = 2.24f;

        /// <summary>Antenna x positions bound the legal crossing span x ∈ [0, 9] — spec §2.4 [structural].</summary>
        public const float AntennaXMin = 0f;
        public const float AntennaXMax = 9f;

        /// <summary>Zone edge length: 9 m half / 3 zones — spec §4.1 [structural].</summary>
        public const float ZoneSize = 3f;

        /// <summary>Convert an authored duration in seconds to sim ticks (round-to-nearest, min 1).</summary>
        public static int TicksFromSeconds(float seconds)
        {
            int t = (int)System.MathF.Round(seconds * TicksPerSecond);
            return t < 1 ? 1 : t;
        }

        /// <summary>
        /// Map a court-plane point to its 3×3 zone (spec §4.1). The half is chosen by the sign
        /// of z (z == 0 exactly counts as the PositiveZ half [structural tie-break]); columns are
        /// labeled from the attacking side's view. See the class doc for gridline tie-breaks.
        /// </summary>
        public static ZoneId ZoneAt(float x, float z)
        {
            bool positiveHalf = z >= 0f;
            // Attacker-local axes: localX = 0 at the attacker's LEFT sideline, localZ = 0 at the net.
            float localX = positiveHalf ? x : CourtWidth - x;
            float localZ = positiveHalf ? z : -z;
            if (localX < 0f) localX = 0f; else if (localX > CourtWidth) localX = CourtWidth;
            if (localZ < 0f) localZ = 0f; else if (localZ > HalfLength) localZ = HalfLength;

            int col = localX >= 2f * ZoneSize ? 2 : (localX >= ZoneSize ? 1 : 0); // L/C/R
            int row = localZ >= 2f * ZoneSize ? 2 : (localZ >= ZoneSize ? 1 : 0); // F/M/B
            return (ZoneId)(row * 3 + col); // ZoneId order: z_LF..z_RF, z_LM..z_RM, z_LB..z_RB
        }

        /// <summary>
        /// Floor-plane center of <paramref name="zone"/> on <paramref name="side"/> (y = 0).
        /// Inverse of <see cref="ZoneAt"/>: ZoneAt(CenterOf(zone, side)) == zone for every zone.
        /// Used as the aim target before quality-scaled scatter (§4.1 — scatter applied by caller).
        /// </summary>
        public static Vec3 CenterOf(ZoneId zone, CourtSide side)
        {
            int i = (int)zone;
            float localX = (i % 3) * ZoneSize + ZoneSize * 0.5f;
            float localZ = (i / 3) * ZoneSize + ZoneSize * 0.5f;
            return side == CourtSide.PositiveZ
                ? new Vec3(localX, 0f, localZ)
                : new Vec3(CourtWidth - localX, 0f, -localZ);
        }

        /// <summary>
        /// Is a landing point inside the 9×9 half owned by <paramref name="side"/>?
        /// Boundaries are INCLUSIVE: "Landing on a line = in" — spec §2.4 [structural].
        /// </summary>
        public static bool IsInBounds(float x, float z, CourtSide side)
        {
            if (x < 0f || x > CourtWidth) return false;
            return side == CourtSide.PositiveZ
                ? z >= 0f && z <= HalfLength
                : z >= -HalfLength && z <= 0f;
        }
    }
}
