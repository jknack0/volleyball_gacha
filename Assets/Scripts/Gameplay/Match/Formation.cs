using System;
using VG.Data;
using VG.Gameplay.Ball;
using VG.Gameplay.Rally;

namespace VG.Gameplay.Match
{
    /// <summary>
    /// Team-size-aware court formations [tunable v0]. 6v6 is the PLAN §2.4 default; 3v3 is its
    /// named fallback, now a first-class config so the M0 grey-box can A/B them cheaply.
    ///
    /// 6v6 positions: 1 RB(server) · 2 RF · 3 MF · 4 LF · 5 LB · 6 MB (own-team view, x mirrored for Home).
    /// 3v3 positions: 1 back-center (server) · 2 net-right (setter) · 3 net-left (hitter).
    /// </summary>
    public static class Formation
    {
        // Slot tables indexed by court position; [0] unused. Own-team view for the PositiveZ half.
        private static readonly float[] X6 = { 0f, 7.0f, 7.0f, 4.5f, 2.0f, 2.0f, 4.5f };
        private static readonly float[] Z6 = { 0f, 7.5f, 2.0f, 2.0f, 2.0f, 7.5f, 7.5f };
        private static readonly float[] X3 = { 0f, 4.5f, 6.5f, 2.5f };
        private static readonly float[] Z3 = { 0f, 6.5f, 2.0f, 2.0f };

        public static void ValidateSize(int teamSize)
        {
            if (teamSize != 3 && teamSize != 6)
                throw new ArgumentOutOfRangeException(nameof(teamSize), teamSize, "Team size is 3 or 6.");
        }

        /// <summary>Floor standing point for a team's court position (y = 0), Home mirrored to −z.</summary>
        public static Vec3 Slot(int teamSize, TeamSide team, int courtPosition)
        {
            ValidateSize(teamSize);
            float x = teamSize == 6 ? X6[courtPosition] : X3[courtPosition];
            float z = teamSize == 6 ? Z6[courtPosition] : Z3[courtPosition];
            return team == TeamSide.Home
                ? new Vec3(CourtGeometry.CourtWidth - x, 0f, -z)
                : new Vec3(x, 0f, z);
        }

        /// <summary>The setter plays position 2 in both formations [structural: MC is the setter].</summary>
        public static int Setter(int teamSize) => 2;

        /// <summary>Position 1 serves in both formations [structural rotation convention].</summary>
        public static int Server(int teamSize) => 1;

        /// <summary>Attacker court position per set option [tunable v0].</summary>
        public static int AttackerFor(int teamSize, SetOption option)
        {
            if (teamSize == 6)
            {
                switch (option)
                {
                    case SetOption.QuickMiddle: return 3;
                    case SetOption.HighOutside: return 4;
                    case SetOption.BackRowPipe: return 6;
                    default: return 2;
                }
            }
            switch (option) // 3v3: one front hitter carries quick AND high (different arcs)
            {
                case SetOption.QuickMiddle: return 3;
                case SetOption.HighOutside: return 3;
                case SetOption.BackRowPipe: return 1;
                default: return 2;
            }
        }

        /// <summary>Receiver/digger court position by attacker-view landing column [tunable v0].</summary>
        public static int ReceiverForColumn(int teamSize, int column)
        {
            if (teamSize == 6)
                return column == 0 ? 1 : column == 1 ? 6 : 5;
            return column == 0 ? 3 : column == 1 ? 1 : 2;
        }

        /// <summary>Who takes an easy mid-court free ball [tunable v0].</summary>
        public static int FreeBallReceiver(int teamSize) => teamSize == 6 ? 6 : 1;

        /// <summary>Blocking court position for an attack lane column [tunable v0].</summary>
        public static int BlockerFor(int teamSize, int laneColumn)
        {
            if (teamSize == 6) return 3;        // MB fronts every lane in v0
            return laneColumn == 0 ? 2 : 3;     // 3v3: the net player opposite the lane
        }

        /// <summary>(position, deep zone) scan list for the Hard AI's weakest-receiver serve (§6.3) [tunable v0].</summary>
        public static void WeakReceiverScan(int teamSize, Span<int> positions, Span<ZoneId> zones)
        {
            if (teamSize == 6)
            {
                positions[0] = 1; positions[1] = 6; positions[2] = 5;
            }
            else
            {
                positions[0] = 3; positions[1] = 1; positions[2] = 2;
            }
            zones[0] = ZoneId.z_LB; zones[1] = ZoneId.z_CB; zones[2] = ZoneId.z_RB;
        }
    }
}
