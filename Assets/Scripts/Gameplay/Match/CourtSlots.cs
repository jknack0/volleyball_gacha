using VG.Gameplay.Ball;
using VG.Gameplay.Rally;

namespace VG.Gameplay.Match
{
    /// <summary>
    /// Canonical capsule standing positions per court position 1..6, in the CourtGeometry frame
    /// [tunable v0]. M0 has no movement sim (contact-based control, PLAN §2.1) — players stand
    /// at slots; the ball is the star. Shared by the sim (arc launch points) and the grey-box
    /// view (capsule placement) so they can never disagree.
    ///
    /// Volleyball convention: 1 right-back (server), 2 right-front, 3 middle-front,
    /// 4 left-front, 5 left-back, 6 middle-back — "right/left" from each team's own view,
    /// so the Away half mirrors x.
    /// </summary>
    public static class CourtSlots
    {
        // Local coordinates for the PositiveZ half viewed by its own team (x from their right).
        private static readonly float[] SlotX = { 0f, 7.0f, 7.0f, 4.5f, 2.0f, 2.0f, 4.5f }; // [pos]
        private static readonly float[] SlotZ = { 0f, 7.5f, 2.0f, 2.0f, 2.0f, 7.5f, 7.5f };

        /// <summary>Home occupies the NegativeZ half, Away the PositiveZ half [structural].</summary>
        public static CourtSide SideOf(TeamSide team)
            => team == TeamSide.Home ? CourtSide.NegativeZ : CourtSide.PositiveZ;

        /// <summary>Floor-plane standing point for a team's court position (y = 0).</summary>
        public static Vec3 Position(TeamSide team, int courtPosition)
        {
            float x = SlotX[courtPosition];
            float z = SlotZ[courtPosition];
            return team == TeamSide.Home
                ? new Vec3(CourtGeometry.CourtWidth - x, 0f, -z) // mirror x so pos 4 is stage-left of the net for both
                : new Vec3(x, 0f, z);
        }

        /// <summary>Serve launch point: behind the team's own baseline at the server's x [tunable v0].</summary>
        public static Vec3 ServeLaunch(TeamSide team, float contactHeight)
        {
            Vec3 p = Position(team, 1);
            float z = team == TeamSide.Home ? -(CourtGeometry.HalfLength + 0.2f) : CourtGeometry.HalfLength + 0.2f;
            return new Vec3(p.X, contactHeight, z);
        }

        /// <summary>The OPPONENT's mid-court — the free-ball target (§2.3 "always mid-court") [structural].
        /// The sender is <paramref name="team"/>; Home occupies −z, so Home's free ball lands at +z.</summary>
        public static Vec3 MidCourt(TeamSide team)
            => team == TeamSide.Home ? new Vec3(4.5f, 0f, 4.5f) : new Vec3(4.5f, 0f, -4.5f);
    }
}
