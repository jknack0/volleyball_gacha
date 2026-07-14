using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Input;

namespace VG.Tests
{
    /// <summary>
    /// Defends m0-gameplay-spec §7.1 (gesture grammar), §7.3 (forgiveness), §4.3 (swipe→shot).
    /// All timings in ticks (60/s): 150 ms = 9 ticks, 250 ms = 15, 80 ms ≈ 4.8.
    /// </summary>
    [TestFixture]
    public class InputTests
    {
        private InputTunables _t;
        private GestureClassifier _c;

        [SetUp]
        public void SetUp()
        {
            _t = new InputTunables();
            _c = new GestureClassifier(_t);
        }

        private Gesture Stroke(int downTick, float dx, float dy, int ticks)
        {
            _c.Down(new PointerSample(downTick, 100f, 100f));
            _c.Move(new PointerSample(downTick + ticks / 2, 100f + dx / 2f, 100f + dy / 2f));
            return _c.Up(new PointerSample(downTick + ticks, 100f + dx, 100f + dy));
        }

        [Test]
        public void QuickStillRelease_IsATap_TimestampedAtTouchDown()
        {
            // Bug caught: taps stamped at release (breaks §7.2's buffer semantics).
            var g = Stroke(1000, dx: 4f, dy: 3f, ticks: 5); // 83 ms, 5 px
            Assert.That(g.Kind, Is.EqualTo(GestureKind.Tap));
            Assert.That(g.DownTick, Is.EqualTo(1000));
        }

        [Test]
        public void LongHold_IsHoldRelease()
        {
            var g = Stroke(0, dx: 2f, dy: 0f, ticks: 30); // 500 ms held, still
            Assert.That(g.Kind, Is.EqualTo(GestureKind.HoldRelease));
            Assert.That(g.UpTick, Is.EqualTo(30), "power evaluates at release");
        }

        [Test]
        public void FastLongTravel_IsASwipe_WithUnitDirection()
        {
            var g = Stroke(0, dx: 0f, dy: 80f, ticks: 8); // 80 px in 133 ms
            Assert.That(g.Kind, Is.EqualTo(GestureKind.Swipe));
            Assert.That(g.DirY, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(g.DirX, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void MidTravel_DegradesToTapForTiming_NoAim()
        {
            // §7.3: 24–60 px = tap for timing, no aim change.
            var g = Stroke(0, dx: 40f, dy: 0f, ticks: 8);
            Assert.That(g.Kind, Is.EqualTo(GestureKind.ShortSwipeAsTap));
        }

        [Test]
        public void SecondTapWithin80ms_IsDiscarded()
        {
            // Bug caught: double-tap double-evaluating a window (§7.3).
            var first = Stroke(0, 2f, 2f, ticks: 4);
            Assert.That(first.Kind, Is.EqualTo(GestureKind.Tap));
            var second = Stroke(6, 2f, 2f, ticks: 4); // 6 ticks = 100 ms after down(0)? 6*16.7=100ms — inside 80? 100>80 → kept.
            Assert.That(second.Kind, Is.EqualTo(GestureKind.Tap), "100 ms apart is a legitimate re-tap");
            var third = Stroke(14, 2f, 2f, ticks: 4); // 14-10... third down at 14, prev tap down at 6 → 8 ticks = 133 ms → kept
            Assert.That(third.Kind, Is.EqualTo(GestureKind.Tap));
            var burst = Stroke(20, 2f, 2f, ticks: 2);
            Assert.That(burst.Kind, Is.EqualTo(GestureKind.Tap));
            var ghost = Stroke(23, 2f, 2f, ticks: 2); // 3 ticks = 50 ms after 'burst' → discarded
            Assert.That(ghost.Kind, Is.EqualTo(GestureKind.None));
        }

        [Test]
        public void SlowLongTravel_IsNotASwipe()
        {
            // 80 px over 400 ms: too slow for a swipe (§7.1: within 250 ms) → hold-release.
            var g = Stroke(0, dx: 80f, dy: 0f, ticks: 24);
            Assert.That(g.Kind, Is.EqualTo(GestureKind.HoldRelease));
        }

        [Test]
        public void HeldTicks_ReadsLive_ForThePowerMeter()
        {
            _c.Down(new PointerSample(100, 50f, 50f));
            Assert.That(_c.HeldTicks(130), Is.EqualTo(30));
            Assert.That(_c.IsDown, Is.True);
        }

        // ---- §4.3 swipe → shot ------------------------------------------------------------------

        private Gesture Swipe(float dirX, float dirY) => new Gesture(GestureKind.Swipe, 0, 8, dirX, dirY);

        [Test]
        public void StraightAhead_IsLine_DiagonalIsCross()
        {
            Assert.That(SpikeSwipeMapper.Map(_t, Swipe(0f, 1f), 100f), Is.EqualTo(SpikeShot.Line));
            Assert.That(SpikeSwipeMapper.Map(_t, Swipe(0.7f, 0.7f), 100f), Is.EqualTo(SpikeShot.Cross), "45° is a cross");
        }

        [Test]
        public void BoundaryBand_ResolvesToTheSaferShot()
        {
            // §7.3: within ±10° of the 25° line/cross boundary → cross (safer).
            float rad = 30f * (3.14159265f / 180f); // 30° — inside [15°, 35°] band
            var g = Swipe((float)System.Math.Sin(rad), (float)System.Math.Cos(rad));
            Assert.That(SpikeSwipeMapper.Map(_t, g, 100f), Is.EqualTo(SpikeShot.Cross));

            rad = 10f * (3.14159265f / 180f);       // 10° — below the band → line
            g = Swipe((float)System.Math.Sin(rad), (float)System.Math.Cos(rad));
            Assert.That(SpikeSwipeMapper.Map(_t, g, 100f), Is.EqualTo(SpikeShot.Line));
        }

        [Test]
        public void TapDuringSpikeWindow_IsAFeint_ShortSwipeIsSafeCross()
        {
            Assert.That(SpikeSwipeMapper.Map(_t, new Gesture(GestureKind.Tap, 0, 3), 0f), Is.EqualTo(SpikeShot.Feint));
            Assert.That(SpikeSwipeMapper.Map(_t, new Gesture(GestureKind.ShortSwipeAsTap, 0, 3), 40f), Is.EqualTo(SpikeShot.Cross));
        }

        [Test]
        public void ShortUpwardSwipe_IsARoll()
        {
            // Spec ambiguity resolved [documented in mapper]: §4.3's "short upward swipe" vs §7.3's
            // short-swipe-as-tap — the roll band is a LEGAL swipe (≥60 px) that is barely past
            // threshold (< 84 px) and mostly vertical.
            Assert.That(SpikeSwipeMapper.Map(_t, Swipe(0.1f, 0.99f), 70f), Is.EqualTo(SpikeShot.Roll));
            Assert.That(SpikeSwipeMapper.Map(_t, Swipe(0.0f, 1.0f), 100f), Is.EqualTo(SpikeShot.Line), "long straight swipe stays a line");
        }

        [Test]
        public void ShotToZone_FollowsTheSpecTable()
        {
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Line, 0), Is.EqualTo(ZoneId.z_LB));
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Line, 1), Is.EqualTo(ZoneId.z_CB));
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Cross, 0), Is.EqualTo(ZoneId.z_RB));
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Cross, 1), Is.EqualTo(ZoneId.z_LB));
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Roll, 0), Is.EqualTo(ZoneId.z_CM));
            Assert.That(SpikeSwipeMapper.TargetZone(SpikeShot.Feint, 1), Is.EqualTo(ZoneId.z_CF));
        }
    }
}
