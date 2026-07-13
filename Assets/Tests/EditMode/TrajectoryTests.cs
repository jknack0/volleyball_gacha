using System;
using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Ball;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §2 (authored kinematic arcs) and §4.1 (zone grid):
    /// determinism, endpoint/apex fidelity, the §2.3 param table, §2.4 net/antenna/bounds rules.
    /// </summary>
    [TestFixture]
    public class TrajectoryTests
    {
        private BallTunables _t;

        [SetUp]
        public void SetUp() => _t = new BallTunables();

        // ---- determinism -----------------------------------------------------------------

        [Test]
        public void SameParams_ProduceBitIdenticalPlayback()
        {
            // Bug caught: hidden state / RNG inside playback (would desync replays, §2.5).
            var p = _t.ServeFloat(new Vec3(4.5f, 2.0f, -8.5f), new Vec3(6.5f, 0f, 6.0f), wobblePhase: 1.234f);
            var a = new Trajectory(p);
            var b = new Trajectory(p);

            for (int tick = 0; tick <= a.DurationTicks; tick++)
            {
                Vec3 va = a.PositionAt(tick);
                Vec3 vb = b.PositionAt(tick);
                Assert.That(vb.X, Is.EqualTo(va.X), $"X diverged at tick {tick}");
                Assert.That(vb.Y, Is.EqualTo(va.Y), $"Y diverged at tick {tick}");
                Assert.That(vb.Z, Is.EqualTo(va.Z), $"Z diverged at tick {tick}");
            }
        }

        // ---- endpoints & apex --------------------------------------------------------------

        [Test]
        public void Endpoints_AreHonoredExactly_EvenWithWobble()
        {
            // Bug caught: wobble or lerp form shifting contact/landing points (§2.3 wobble rule).
            var start = new Vec3(4.5f, 2.0f, -8.5f);
            var end = new Vec3(6.5f, 0f, 6.0f);
            var arc = new Trajectory(_t.ServeFloat(start, end, wobblePhase: 0.777f));

            Vec3 p0 = arc.PositionAt(0);
            Assert.That(p0.X, Is.EqualTo(start.X));
            Assert.That(p0.Y, Is.EqualTo(start.Y));
            Assert.That(p0.Z, Is.EqualTo(start.Z));

            Vec3 pT = arc.PositionAt(arc.DurationTicks);
            Assert.That(pT.X, Is.EqualTo(end.X), "Wobble envelope must be zero at u = 1.");
            Assert.That(pT.Y, Is.EqualTo(end.Y));
            Assert.That(pT.Z, Is.EqualTo(end.Z));
        }

        [Test]
        public void ApexHeight_IsReached_AtAuthoredApexU()
        {
            // Bug caught: bezier segments not joining at the authored apex (§2.2 C1 join).
            var p = _t.SetHigh(new Vec3(4.5f, 1.8f, -3f), new Vec3(1.5f, 2.0f, -1f));
            var arc = new Trajectory(p);

            Assert.That(arc.HeightAt(p.ApexU), Is.EqualTo(p.ApexHeight).Within(1e-5f));

            // Apex is the maximum over the whole flight.
            float max = float.MinValue;
            for (int tick = 0; tick <= arc.DurationTicks; tick++)
                max = MathF.Max(max, arc.PositionAt(tick).Y);
            Assert.That(max, Is.LessThanOrEqualTo(p.ApexHeight + 1e-4f));
        }

        [Test]
        public void Spike_IsMonotonicDescent()
        {
            // Bug caught: ApexU = 0 special case regrowing height mid-flight (§2.3 spike row).
            var p = _t.Spike(new Vec3(2f, _t.SpikeContactHeight(0.6f), -3f), new Vec3(7f, 0f, 7f), quality: 0.8f);
            var arc = new Trajectory(p);

            float prev = float.MaxValue;
            for (int tick = 0; tick <= arc.DurationTicks; tick++)
            {
                float y = arc.PositionAt(tick).Y;
                Assert.That(y, Is.LessThanOrEqualTo(prev + 1e-5f), $"Height rose at tick {tick}");
                prev = y;
            }
        }

        // ---- §2.3 param table / §2.2 clamp --------------------------------------------------

        [Test]
        public void EveryFactory_AuthorsAValidArc()
        {
            // Bug caught: a table row authoring a degenerate arc (0 ticks, broken endpoints).
            var start = new Vec3(4.5f, 2.2f, -6f);
            var end = new Vec3(4.5f, 0f, 5f);
            var all = new[]
            {
                _t.ServeFloat(start, end, 0.5f),
                _t.ServeJump(start, end),
                _t.Pass(start, end),
                _t.SetHigh(start, new Vec3(2f, 2f, -4f)),
                _t.SetQuick(start, new Vec3(3f, 2f, -5f)),
                _t.Spike(new Vec3(4f, _t.SpikeContactHeight(1f), -2f), end, 1f),
                _t.RollShot(start, end),
                _t.BlockDeflection(new Vec3(4f, 2.4f, 0.2f), new Vec3(3f, 0f, 4f), 0.5f),
                _t.FreeBall(start, end),
            };

            foreach (var p in all)
            {
                var arc = new Trajectory(p);
                Assert.That(arc.DurationTicks, Is.GreaterThanOrEqualTo(1), p.Kind.ToString());
                Assert.That(arc.PositionAt(arc.DurationTicks).Y, Is.EqualTo(p.End.Y).Within(1e-5f), p.Kind.ToString());
            }
        }

        [Test]
        public void MaxHorizontalSpeed_ClampRaisesDuration()
        {
            // Bug caught: §2.2 v_max clamp missing — physically absurd arcs on long quick sets.
            var start = new Vec3(0f, 2f, -9f);
            var end = new Vec3(0f, 2f, 9f); // 18 m; authored 0.7 s → 25.7 m/s < 30 OK; force longer:
            var far = new Vec3(9f, 2f, 9f);  // sqrt(9² + 18²) ≈ 20.12 m; 0.7 s → 28.7 m/s < 30; farther:
            // Use a custom stretch: 27 m along z is off-court but the clamp is pure math.
            var stretched = _t.SetQuick(new Vec3(0f, 2f, -13.5f), new Vec3(0f, 2f, 13.5f)); // 27 m / 0.7 s = 38.6 m/s
            int expected = CourtGeometry.TicksFromSeconds(27f / _t.MaxHorizontalSpeed);
            Assert.That(stretched.DurationTicks, Is.EqualTo(expected));

            var unclamped = _t.SetQuick(start, far);
            Assert.That(unclamped.DurationTicks, Is.EqualTo(CourtGeometry.TicksFromSeconds(_t.SetQuickDurationSeconds)));
        }

        [Test]
        public void BlockDeflection_DurationScalesWithRemainingEnergy()
        {
            // Bug caught: §2.3 "T from remaining energy (1 − B)" inverted.
            var start = new Vec3(4f, 2.4f, 0.2f);
            var end = new Vec3(3f, 0f, 4f);
            var full = _t.BlockDeflection(start, end, remainingEnergy: 1f);
            var none = _t.BlockDeflection(start, end, remainingEnergy: 0f);
            Assert.That(full.DurationTicks, Is.LessThan(none.DurationTicks),
                "Full remaining energy must produce the FASTER (shorter-T) deflection.");
        }

        // ---- §2.4 net / antenna / bounds ----------------------------------------------------

        [Test]
        public void NetRuling_Clears_ForALegalSpike()
        {
            var p = _t.Spike(new Vec3(4.5f, 3.2f, -3f), new Vec3(4.5f, 0f, 6f), quality: 0.9f);
            Assert.That(new Trajectory(p).RuleNetInteraction(_t), Is.EqualTo(NetRuling.Clears));
        }

        [Test]
        public void NetRuling_NetFault_ForALowNonServeSpikeArc()
        {
            // Pass crossing at 1.5 m — under the band, not cord-eligible (§2.4 "except serve/spike").
            var p = _t.Pass(new Vec3(4.5f, 1f, -4f), new Vec3(4.5f, 0f, 4f));
            p.ApexHeight = 1.5f; // low shank arc
            var arc = new Trajectory(p);
            var crossing = arc.FindNetCrossing();
            Assume.That(crossing.Height, Is.LessThan(CourtGeometry.NetHeight - _t.NetCordBandBelow),
                "fixture must cross under the cord band");
            Assert.That(arc.RuleNetInteraction(_t), Is.EqualTo(NetRuling.NetFault));
        }

        [Test]
        public void NetRuling_NetCord_ForServeInsideTheBand()
        {
            // Bug caught: cord band edges misclassified (§2.4's one "drama" RNG entry point).
            var p = _t.ServeJump(new Vec3(4.5f, 2.1f, -8f), new Vec3(4.5f, 0f, 5f));
            // Tune apex so the crossing height lands inside [net − 0.04, net + 0.02).
            var probe = new Trajectory(p);
            float u = probe.FindNetCrossing().U;
            // Solve apex for crossing height ≈ net height: h(u) = apex + (end − apex)·t², t = (u−u_a)/(1−u_a).
            float t = (u - p.ApexU) / (1f - p.ApexU);
            float target = CourtGeometry.NetHeight; // mid-band
            p.ApexHeight = (target - 0f * t * t) / (1f - t * t);
            var arc = new Trajectory(p);
            var crossing = arc.FindNetCrossing();
            Assume.That(crossing.Height,
                Is.GreaterThanOrEqualTo(CourtGeometry.NetHeight - _t.NetCordBandBelow)
                  .And.LessThan(CourtGeometry.NetHeight + _t.NetFaultMargin),
                "fixture must cross inside the cord band");
            Assert.That(arc.RuleNetInteraction(_t), Is.EqualTo(NetRuling.NetCord));
        }

        [Test]
        public void NetRuling_AntennaOut_WhenCrossingOutsideNetSpan()
        {
            var p = _t.Spike(new Vec3(9.5f, 3.2f, -3f), new Vec3(10.5f, 0f, 6f), quality: 0.9f);
            Assert.That(new Trajectory(p).RuleNetInteraction(_t), Is.EqualTo(NetRuling.AntennaOut));
        }

        [Test]
        public void NetRuling_NoCrossing_ForSameSideArc()
        {
            var p = _t.SetHigh(new Vec3(4.5f, 1.8f, -4f), new Vec3(2f, 2f, -1f));
            Assert.That(new Trajectory(p).RuleNetInteraction(_t), Is.EqualTo(NetRuling.NoCrossing));
        }

        [Test]
        public void Bounds_LandingOnALine_IsIn()
        {
            // Bug caught: exclusive boundary comparison — §2.4 "Landing on a line = in" [structural].
            Assert.That(CourtGeometry.IsInBounds(0f, 0f, CourtSide.PositiveZ), Is.True);
            Assert.That(CourtGeometry.IsInBounds(9f, 9f, CourtSide.PositiveZ), Is.True);
            Assert.That(CourtGeometry.IsInBounds(4.5f, -9f, CourtSide.NegativeZ), Is.True);
            Assert.That(CourtGeometry.IsInBounds(9.0001f, 4f, CourtSide.PositiveZ), Is.False);
            Assert.That(CourtGeometry.IsInBounds(4.5f, 9.0001f, CourtSide.PositiveZ), Is.False);
            Assert.That(CourtGeometry.IsInBounds(4.5f, 0.0001f, CourtSide.NegativeZ), Is.False);
        }

        // ---- §4.1 zone grid -----------------------------------------------------------------

        [Test]
        public void ZoneGrid_CenterOf_RoundTrips_ForAllZonesBothSides()
        {
            // Bug caught: mirrored-half column flip broken (attacker-view labeling, §4.1).
            foreach (CourtSide side in Enum.GetValues(typeof(CourtSide)))
            {
                foreach (ZoneId zone in Enum.GetValues(typeof(ZoneId)))
                {
                    Vec3 c = CourtGeometry.CenterOf(zone, side);
                    Assert.That(CourtGeometry.ZoneAt(c.X, c.Z), Is.EqualTo(zone), $"{side}/{zone}");
                }
            }
        }

        [Test]
        public void ZoneGrid_GridlineTieBreak_GoesToHigherIndexZone()
        {
            // Bug caught: ambiguous boundary ownership (documented tie-break in CourtGeometry).
            Assert.That(CourtGeometry.ZoneAt(3f, 3f), Is.EqualTo(ZoneId.z_CM));
            Assert.That(CourtGeometry.ZoneAt(6f, 6f), Is.EqualTo(ZoneId.z_RB));
            Assert.That(CourtGeometry.ZoneAt(0f, 0f), Is.EqualTo(ZoneId.z_LF));
            // Mirrored half: attacker-local column L sits at HIGH x.
            Assert.That(CourtGeometry.ZoneAt(6f, -3f), Is.EqualTo(ZoneId.z_CM).Or.EqualTo(ZoneId.z_CM));
            Assert.That(CourtGeometry.ZoneAt(9f, -0.5f), Is.EqualTo(ZoneId.z_LF));
        }

        [Test]
        public void Wobble_MovesMidFlight_NeverTheLanding()
        {
            var start = new Vec3(4.5f, 2.0f, -8.5f);
            var end = new Vec3(4.5f, 0f, 6.0f);
            var straight = new Trajectory(_t.ServeJump(start, end));
            var pWobble = _t.ServeFloat(start, end, wobblePhase: 0.9f);
            var wobbly = new Trajectory(pWobble);

            bool deviated = false;
            for (int tick = 1; tick < wobbly.DurationTicks && !deviated; tick++)
            {
                float uStraightX = start.X; // straight track has constant x here
                deviated = MathF.Abs(wobbly.PositionAt(tick).X - uStraightX) > 1e-3f;
            }
            Assert.That(deviated, Is.True, "Float serve never wobbled laterally.");
            Assert.That(wobbly.PositionAt(wobbly.DurationTicks).X, Is.EqualTo(end.X), "Wobble moved the landing point.");
            _ = straight;
        }
    }
}
