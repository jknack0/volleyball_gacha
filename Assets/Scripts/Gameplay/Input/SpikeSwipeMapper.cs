using System;
using VG.Data;

namespace VG.Gameplay.Input
{
    /// <summary>
    /// §4.3 swipe→shot compiler [structural] with §7.3 boundary forgiveness: a swipe within
    /// ±10° of the line/cross boundary resolves toward the SAFER shot (cross over line).
    /// Screen convention: +y = "straight ahead" toward the opponent court.
    /// </summary>
    public static class SpikeSwipeMapper
    {
        /// <summary>Compile a classified gesture at AttackContact into a shot (§4.3 table).</summary>
        public static SpikeShot Map(InputTunables t, in Gesture gesture, float swipeTravelPx)
        {
            switch (gesture.Kind)
            {
                case GestureKind.Tap:
                    return SpikeShot.Feint; // tap during spike window = feint/dump (§4.3)

                case GestureKind.ShortSwipeAsTap:
                    return SpikeShot.Cross; // §7.3: no aim change → safe default [tunable: cross]

                case GestureKind.Swipe:
                    break;

                default:
                    return SpikeShot.Cross; // degenerate input → safe shot
            }

            // Roll: short upward swipe (§4.3: < 40% of min swipe length, upward).
            if (gesture.DirY > 0.5f && swipeTravelPx < t.SwipeMinTravelPx * (1f + t.RollLengthFraction))
            {
                // A "short" full-swipe: barely past threshold and mostly vertical.
                if (swipeTravelPx < t.SwipeMinTravelPx + t.SwipeMinTravelPx * t.RollLengthFraction
                    && MathF.Abs(gesture.DirX) < 0.35f)
                    return SpikeShot.Roll;
            }

            // Line vs cross by angle from straight-ahead (+y), with the §7.3 safety band.
            float angleDeg = MathF.Abs(MathF.Atan2(gesture.DirX, MathF.Max(gesture.DirY, 1e-4f))) * (180f / MathF.PI);
            float boundary = t.CrossAngleDegrees;

            if (angleDeg < boundary - t.BoundaryDegrees) return SpikeShot.Line;
            if (angleDeg > boundary + t.BoundaryDegrees) return SpikeShot.Cross;
            return SpikeShot.Cross; // inside the ±10° band → safer shot (§7.3: cross over line)
        }

        /// <summary>Shot → target zone for an attack lane (§4.3 table). Lane is the attacker-view column.</summary>
        public static ZoneId TargetZone(SpikeShot shot, int laneColumn)
        {
            switch (shot)
            {
                case SpikeShot.Line:
                    return (ZoneId)(6 + laneColumn);                       // same-column back edge z_XB
                case SpikeShot.Cross:
                    return laneColumn == 0 ? ZoneId.z_RB : ZoneId.z_LB;    // opposite corner back
                case SpikeShot.Roll:
                    return ZoneId.z_CM;                                    // soft center
                default:
                    return (ZoneId)laneColumn;                             // feint: front zone at the lane
            }
        }
    }
}
