using VG.Gameplay.Ball;
using VG.Gameplay.Rally;

namespace VG.Gameplay.Match
{
    /// <summary>
    /// Side/launch geometry helpers shared by sim and view. Standing-slot tables live in
    /// <see cref="Formation"/> (team-size-aware); this class keeps the size-independent frame
    /// conventions and thin size-aware wrappers.
    /// </summary>
    public static class CourtSlots
    {
        /// <summary>Home occupies the NegativeZ half, Away the PositiveZ half [structural].</summary>
        public static CourtSide SideOf(TeamSide team)
            => team == TeamSide.Home ? CourtSide.NegativeZ : CourtSide.PositiveZ;

        /// <summary>Floor-plane standing point for a team's court position (y = 0).</summary>
        public static Vec3 Position(TeamSide team, int courtPosition, int teamSize)
            => Formation.Slot(teamSize, team, courtPosition);

        /// <summary>Serve launch point: behind the team's own baseline at the server's x [tunable v0].</summary>
        public static Vec3 ServeLaunch(TeamSide team, float contactHeight, int teamSize)
        {
            Vec3 p = Formation.Slot(teamSize, team, Formation.Server(teamSize));
            float z = team == TeamSide.Home ? -(CourtGeometry.HalfLength + 0.2f) : CourtGeometry.HalfLength + 0.2f;
            return new Vec3(p.X, contactHeight, z);
        }

        /// <summary>The OPPONENT's mid-court — the free-ball target (§2.3 "always mid-court") [structural].
        /// The sender is <paramref name="team"/>; Home occupies −z, so Home's free ball lands at +z.</summary>
        public static Vec3 MidCourt(TeamSide team)
            => team == TeamSide.Home ? new Vec3(4.5f, 0f, 4.5f) : new Vec3(4.5f, 0f, -4.5f);
    }
}
