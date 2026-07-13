using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Hype;
using VG.Gameplay.Rally;
using VG.Gameplay.Signatures;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §3.7 (Hype accrual/clamps/Ignition) and the contract
    /// primitives (a)–(f) — data-schemas §5.1 rows Hype_Accrual, Ignition_Threshold, SigPrimitive_a..f.
    /// </summary>
    [TestFixture]
    public class HypeSignatureTests
    {
        private HypeTunables _t;
        private HypeMeters _hype;
        private SignatureEngine _sig;

        [SetUp]
        public void SetUp()
        {
            _t = new HypeTunables();
            _hype = new HypeMeters(_t);
            _sig = new SignatureEngine();
        }

        // ---- §3.7 accrual ---------------------------------------------------------------------

        [TestCase(HypeEvent.PerfectContact, 4)]
        [TestCase(HypeEvent.Kill, 10)]
        [TestCase(HypeEvent.StuffBlock, 14)]
        [TestCase(HypeEvent.Ace, 14)]
        [TestCase(HypeEvent.LongRallyContact, 2)]
        [TestCase(HypeEvent.BigSpikeDug, 8)]
        [TestCase(HypeEvent.OwnError, -6)]
        public void AccrualTable_MatchesSpec(HypeEvent e, int expected)
        {
            // Bug caught: any §3.7 row drifting from spec.
            Assert.That(_hype.DeltaFor(e), Is.EqualTo(expected));
        }

        [Test]
        public void Hype_ClampsAtZero_AndAtMax()
        {
            // Bug caught: negative meters (error spiral) or overflow past 100.
            _hype.Apply(HypeEvent.OwnError, TeamSide.Home);
            Assert.That(_hype.Hype(TeamSide.Home), Is.EqualTo(0), "floor clamp");

            for (int i = 0; i < 30; i++) _hype.Apply(HypeEvent.StuffBlock, TeamSide.Home);
            Assert.That(_hype.Hype(TeamSide.Home), Is.EqualTo(100), "ceiling clamp");
        }

        [Test]
        public void Meters_AreIndependentPerTeam()
        {
            _hype.Apply(HypeEvent.Kill, TeamSide.Home);
            Assert.That(_hype.Hype(TeamSide.Home), Is.EqualTo(10));
            Assert.That(_hype.Hype(TeamSide.Away), Is.EqualTo(0));
        }

        // ---- Ignition ---------------------------------------------------------------------------

        [Test]
        public void Ignition_LatchesAtThreshold_AndReportsExactlyOnce()
        {
            // Bug caught: C12 presentation hook firing repeatedly, or never.
            bool ignitedYet = false;
            for (int i = 0; i < 30 && !ignitedYet; i++)
                ignitedYet = _hype.Apply(HypeEvent.Kill, TeamSide.Home);

            Assert.That(ignitedYet, Is.True);
            Assert.That(_hype.IsIgnited(TeamSide.Home), Is.True);
            Assert.That(_hype.Apply(HypeEvent.Kill, TeamSide.Home), Is.False, "already latched — no re-trigger");
        }

        [Test]
        public void Spending_BelowThreshold_NeverUnlatchesIgnition()
        {
            // Bug caught: revoking Ignition on signature spend (punishes using the celebrated moves).
            while (!_hype.IsIgnited(TeamSide.Home)) _hype.Apply(HypeEvent.StuffBlock, TeamSide.Home);

            Assert.That(_hype.TrySpend(TeamSide.Home, 60), Is.True);
            Assert.That(_hype.Hype(TeamSide.Home), Is.EqualTo(40));
            Assert.That(_hype.IsIgnited(TeamSide.Home), Is.True, "Ignition latches [structural]");
        }

        [Test]
        public void TrySpend_FailsWithoutFunds_AndLeavesTheMeterUntouched()
        {
            // §1.3 gate: activation requires Hype ≥ cost.
            _hype.Apply(HypeEvent.Kill, TeamSide.Away); // 10
            Assert.That(_hype.TrySpend(TeamSide.Away, 30), Is.False);
            Assert.That(_hype.Hype(TeamSide.Away), Is.EqualTo(10));
            Assert.That(_hype.TrySpend(TeamSide.Away, 10), Is.True);
            Assert.That(_hype.Hype(TeamSide.Away), Is.EqualTo(0));
        }

        // ---- primitives (a)–(f) --------------------------------------------------------------------

        [Test]
        public void PrimitiveA_GuaranteedGrade_IsOneShot()
        {
            var effect = new SigEffect { Primitive = SigPrimitive.GuaranteedTimingGrade, Grade = TimingGrade.Perfect };
            _sig.Apply(effect, TeamSide.Home, _hype);

            Assert.That(_sig.TryConsumeGuaranteedGrade(TeamSide.Home, out var grade), Is.True);
            Assert.That(grade, Is.EqualTo(TimingGrade.Perfect));
            Assert.That(_sig.TryConsumeGuaranteedGrade(TeamSide.Home, out _), Is.False, "one contact only (§1.3 stub a)");
            Assert.That(_sig.TryConsumeGuaranteedGrade(TeamSide.Away, out _), Is.False, "never leaks to the other side");
        }

        [Test]
        public void PrimitiveB_WindowAdjust_LastsExactlyNContacts_AndSupportsShrink()
        {
            var widen = new SigEffect { Primitive = SigPrimitive.TimingWindowAdjust, Percent = 20f, Contacts = 2 };
            _sig.Apply(widen, TeamSide.Home, _hype);

            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Home), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Home), Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Home), Is.EqualTo(1f), "expired after N contacts");

            var shrink = new SigEffect { Primitive = SigPrimitive.TimingWindowAdjust, Percent = -25f, Contacts = 1 };
            _sig.Apply(shrink, TeamSide.Away, _hype);
            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Away), Is.EqualTo(0.75f).Within(1e-5f), "±X% means shrink is legal");
        }

        [Test]
        public void PrimitiveC_TrajectoryOverride_IsOneShot()
        {
            var effect = new SigEffect { Primitive = SigPrimitive.TrajectoryOverride, TrajectoryOverrideId = "arc.sig_drift_serve" };
            _sig.Apply(effect, TeamSide.Home, _hype);

            Assert.That(_sig.TryConsumeTrajectoryOverride(TeamSide.Home, out var id), Is.True);
            Assert.That(id, Is.EqualTo("arc.sig_drift_serve"));
            Assert.That(_sig.TryConsumeTrajectoryOverride(TeamSide.Home, out _), Is.False);
        }

        [Test]
        public void PrimitiveD_DebuffsTheOpponent_NotTheCaster()
        {
            // Bug caught: debuff keyed to the caster instead of the target.
            var effect = new SigEffect { Primitive = SigPrimitive.OpponentContactDebuff, Percent = 30f, Contacts = 1 };
            _sig.Apply(effect, TeamSide.Home, _hype);

            Assert.That(_sig.ConsumeBlockDigDebuffMultiplier(TeamSide.Home), Is.EqualTo(1f), "caster unaffected");
            Assert.That(_sig.ConsumeBlockDigDebuffMultiplier(TeamSide.Away), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(_sig.ConsumeBlockDigDebuffMultiplier(TeamSide.Away), Is.EqualTo(1f), "expired");
        }

        [Test]
        public void PrimitiveE_HypeDelta_ResolvesImmediately_BothTargets()
        {
            var gain = new SigEffect { Primitive = SigPrimitive.HypeDelta, HypeAmount = 15 };
            _sig.Apply(gain, TeamSide.Home, _hype);
            Assert.That(_hype.Hype(TeamSide.Home), Is.EqualTo(15));

            _hype.Add(TeamSide.Away, 20);
            var drain = new SigEffect { Primitive = SigPrimitive.HypeDelta, HypeAmount = -8, HypeTargetsOpponent = true };
            _sig.Apply(drain, TeamSide.Home, _hype);
            Assert.That(_hype.Hype(TeamSide.Away), Is.EqualTo(12), "opponent drain (story bible's Thesis Defense)");
        }

        [Test]
        public void PrimitiveF_QualityBuff_CountsDown_AndCallerClampsQuality()
        {
            var effect = new SigEffect { Primitive = SigPrimitive.TeamQualityBuff, Percent = 15f, Contacts = 1 };
            _sig.Apply(effect, TeamSide.Home, _hype);

            float multiplier = _sig.ConsumeQualityMultiplier(TeamSide.Home);
            Assert.That(multiplier, Is.EqualTo(1.15f).Within(1e-5f));

            // §3.2: "(f) multiplies the final quality, clamped to 1" — the clamp is the caller's:
            float quality = 0.95f * multiplier;
            Assert.That(System.Math.Min(quality, 1f), Is.EqualTo(1f).Within(1e-5f));

            Assert.That(_sig.ConsumeQualityMultiplier(TeamSide.Home), Is.EqualTo(1f), "expired after N contacts");
        }

        [Test]
        public void SamePrimitive_Reapplied_Replaces()
        {
            // [structural M0 simplification]: latest instance wins, no stacking.
            _sig.Apply(new SigEffect { Primitive = SigPrimitive.TimingWindowAdjust, Percent = 10f, Contacts = 5 }, TeamSide.Home, _hype);
            _sig.Apply(new SigEffect { Primitive = SigPrimitive.TimingWindowAdjust, Percent = 40f, Contacts = 1 }, TeamSide.Home, _hype);

            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Home), Is.EqualTo(1.4f).Within(1e-5f));
            Assert.That(_sig.ConsumeWindowMultiplier(TeamSide.Home), Is.EqualTo(1f), "replaced instance's count, not the sum");
        }
    }
}
