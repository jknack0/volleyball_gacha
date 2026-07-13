using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Resolution;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §3.6 (ordered resolution pipeline, no RNG) and §4.1 risk —
    /// including data-schemas §5.1's mandated edge cases (Shank-adjacent flows, Perfect-everything,
    /// aim into a committed block).
    /// </summary>
    [TestFixture]
    public class PointResolutionTests
    {
        private PointResolutionTunables _t;
        private ResolutionTunables _r;

        private const float NoDig = -1f;

        [SetUp]
        public void SetUp()
        {
            _t = new PointResolutionTunables();
            _r = new ResolutionTunables();
        }

        private AttackResolution Resolve(
            float q, TimingGrade timing, ZoneId zone, BlockState block, float dig = NoDig)
            => PointResolution.ResolveAttack(_t, _r, q, timing, zone, block, dig);

        // ---- step order -----------------------------------------------------------------------

        [Test]
        public void Step1_TimingMiss_IsFreeBall_EvenAgainstAMonsterBlock()
        {
            // Bug caught: pipeline evaluated out of order (§3.6 "first terminal wins").
            var block = new BlockState(BlockCommit.Early, PointResolution.ZoneColumn(ZoneId.z_CM), 1f);
            var res = Resolve(0.9f, TimingGrade.Miss, ZoneId.z_CM, block);
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.FreeBall));
        }

        [Test]
        public void Step2_QualityUnderNetFloor_IsNet()
        {
            var res = Resolve(0.05f, TimingGrade.Good, ZoneId.z_CM, BlockState.None);
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.Net));
        }

        [Test]
        public void Step3_WeakEdgeAim_IsOut_ButPerfectTimingDiscountsTheRisk()
        {
            // Bug caught: §3.6's risk_discount dropped — Perfect line shots punished as errors.
            var weak = Resolve(0.25f, TimingGrade.Good, ZoneId.z_LB, BlockState.None);
            Assert.That(weak.Outcome, Is.EqualTo(AttackOutcome.Out), "0.25 < 0.30 on an edge zone");

            var perfect = Resolve(0.25f, TimingGrade.Perfect, ZoneId.z_LB, BlockState.None);
            Assert.That(perfect.Outcome, Is.Not.EqualTo(AttackOutcome.Out), "0.25 ≥ 0.30 − 0.10 with Perfect timing");
        }

        [Test]
        public void Step3_CenterZone_NeverSailsOut()
        {
            // §4.1: center aim is the safe ball — outs only come from edge-zone risk.
            var res = Resolve(0.09f, TimingGrade.Good, ZoneId.z_CM, BlockState.None);
            Assert.That(res.Outcome, Is.Not.EqualTo(AttackOutcome.Out));
        }

        // ---- block interactions (schema §5.1: "aim into committed block") ------------------------

        [Test]
        public void Step4_AimingIntoAStrongCommittedBlock_IsStuffed()
        {
            // A = 0.4 × (0.75 + 0.25 × 0.2) = 0.32; B = 0.6 × 1.15 (early correct) = 0.69; B − A = 0.37 ≥ 0.15.
            var block = new BlockState(BlockCommit.Early, PointResolution.ZoneColumn(ZoneId.z_CM), 0.6f);
            var res = Resolve(0.4f, TimingGrade.Good, ZoneId.z_CM, block);
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.Blocked));
        }

        [Test]
        public void Step5_BigSwing_OffTheAdjacentBlock_IsTooled_OnEdgeZonesOnly()
        {
            // A = 0.9 × (0.75 + 0.25 × 1.0) = 0.9; adjacent read block: B = 0.5 × 0.85 × 0.35 ≈ 0.149.
            int adjacentColumn = PointResolution.ZoneColumn(ZoneId.z_LB) + 1;
            var block = new BlockState(BlockCommit.Read, adjacentColumn, 0.5f);

            var edge = Resolve(0.9f, TimingGrade.Perfect, ZoneId.z_LB, block);
            Assert.That(edge.Outcome, Is.EqualTo(AttackOutcome.Tooled), "edge zone, touch, margin ≥ 0.25");

            var center = Resolve(0.9f, TimingGrade.Perfect, ZoneId.z_CM, new BlockState(BlockCommit.Read, 0, 0.5f));
            Assert.That(center.Outcome, Is.Not.EqualTo(AttackOutcome.Tooled), "tools require an edge zone (§3.6 step 5)");
        }

        [Test]
        public void EarlyWrongColumn_IsNoBlockAtAll()
        {
            // Bug caught: early-wrong still granting match — §3.6: "early wrong → match = 0".
            int wrongColumn = (PointResolution.ZoneColumn(ZoneId.z_LB) + 2) % 3;
            var block = new BlockState(BlockCommit.Early, wrongColumn, 1f);
            var res = Resolve(0.9f, TimingGrade.Perfect, ZoneId.z_LB, block);
            Assert.That(res.BlockTouched, Is.False, "B must be exactly 0 — no touch, no tool, no damping");
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.Kill));
        }

        [Test]
        public void Step6_NonTerminalTouch_DampsTheAttack()
        {
            // Bug caught: A_eff = A × (1 − 0.5 × B) not applied after a touch.
            int col = PointResolution.ZoneColumn(ZoneId.z_CB);
            // Near-even matchup so neither stuff (step 4) nor tool (step 5) triggers:
            // A = 0.72, B = 0.8 × 0.85 = 0.68 → step 6 damping path.
            var block = new BlockState(BlockCommit.Read, col, 0.8f);
            var touched = Resolve(0.8f, TimingGrade.Great, ZoneId.z_CB, block);
            var clean = Resolve(0.8f, TimingGrade.Great, ZoneId.z_CB, BlockState.None);

            Assert.That(touched.BlockTouched, Is.True);
            Assert.That(touched.EffectiveAttack, Is.LessThan(clean.EffectiveAttack));
        }

        // ---- dig (step 7/8) -------------------------------------------------------------------

        [Test]
        public void Step7_DigThreshold_IsInclusive_AndMarginMapsToDisplayGrade()
        {
            // Bug caught: exclusive comparison at the exact threshold (§3.6: q_d ≥ 0.25 + 0.55 × A_eff).
            var clean = Resolve(0.8f, TimingGrade.Great, ZoneId.z_CB, BlockState.None, dig: NoDig);
            float required = _t.DigBase + _t.DigPerAEff * clean.EffectiveAttack;

            var dugExact = Resolve(0.8f, TimingGrade.Great, ZoneId.z_CB, BlockState.None, dig: required);
            Assert.That(dugExact.Outcome, Is.EqualTo(AttackOutcome.Dug), "exact threshold must dig");
            Assert.That(dugExact.DigDisplayGrade, Is.EqualTo(ReceiveGrade.Shank), "zero margin maps to the lowest display grade");

            var dugBig = Resolve(0.3f, TimingGrade.Great, ZoneId.z_CM, BlockState.None, dig: 0.99f);
            Assert.That(dugBig.Outcome, Is.EqualTo(AttackOutcome.Dug));
            Assert.That(dugBig.DigDisplayGrade, Is.GreaterThan(ReceiveGrade.Shank), "big margin must earn a real grade");

            var missed = Resolve(0.8f, TimingGrade.Great, ZoneId.z_CB, BlockState.None, dig: required - 0.001f);
            Assert.That(missed.Outcome, Is.EqualTo(AttackOutcome.Kill));
        }

        [Test]
        public void PerfectEverything_StrongDigStillAnswersAPerfectSpike()
        {
            // Schema §5.1 "Perfect everything": max attack vs a great dig — defense CAN answer
            // (stats assist, skill decides — PLAN pillar 2). Bug caught: dig math making perfect attacks unanswerable.
            float required = _t.DigBase + _t.DigPerAEff * (1f * (_t.AttackPowerBase + _t.AttackPowerRiskScale * _t.RiskCorner));
            Assert.That(required, Is.LessThanOrEqualTo(0.85f), "a ceiling dig must remain possible");

            var res = Resolve(1f, TimingGrade.Perfect, ZoneId.z_RB, BlockState.None, dig: 0.9f);
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.Dug));

            var floorDig = Resolve(1f, TimingGrade.Perfect, ZoneId.z_RB, BlockState.None, dig: 0.7f);
            Assert.That(floorDig.Outcome, Is.EqualTo(AttackOutcome.Kill));
        }

        [Test]
        public void NoDigAttempt_IsAKill()
        {
            var res = Resolve(0.6f, TimingGrade.Great, ZoneId.z_CB, BlockState.None);
            Assert.That(res.Outcome, Is.EqualTo(AttackOutcome.Kill));
        }

        // ---- serve wrapper ----------------------------------------------------------------------

        [Test]
        public void Serve_ReusesThePipeline_WithNoBlock()
        {
            // §3.6 tail: ace = step 8, service error = steps 2–3.
            var ace = PointResolution.ResolveServe(_t, _r, 0.9f, TimingGrade.Perfect, ZoneId.z_LB, receiveQuality: 0.2f);
            Assert.That(ace.Outcome, Is.EqualTo(AttackOutcome.Kill), "unreturned serve = ace");

            var error = PointResolution.ResolveServe(_t, _r, 0.05f, TimingGrade.Good, ZoneId.z_CM, receiveQuality: 0.5f);
            Assert.That(error.Outcome, Is.EqualTo(AttackOutcome.Net), "service error via step 2");

            var received = PointResolution.ResolveServe(_t, _r, 0.7f, TimingGrade.Great, ZoneId.z_CB, receiveQuality: 0.9f);
            Assert.That(received.Outcome, Is.EqualTo(AttackOutcome.Dug), "clean receive continues the rally");
        }

        [Test]
        public void Serve_TimingMissNeverWhiffs_ItDegradesInstead()
        {
            // §1.2 T3: serve timeout/miss is grade-capped, not a whiff — step 1 must not fire for serves.
            var res = PointResolution.ResolveServe(_t, _r, 0.5f, TimingGrade.Miss, ZoneId.z_CM, receiveQuality: 0.1f);
            Assert.That(res.Outcome, Is.Not.EqualTo(AttackOutcome.FreeBall));
        }

        // ---- risk table ---------------------------------------------------------------------------

        [Test]
        public void RiskTable_MatchesSpec()
        {
            Assert.That(_t.RiskOf(ZoneId.z_LF), Is.EqualTo(1.0f));
            Assert.That(_t.RiskOf(ZoneId.z_RF), Is.EqualTo(1.0f));
            Assert.That(_t.RiskOf(ZoneId.z_LB), Is.EqualTo(1.0f));
            Assert.That(_t.RiskOf(ZoneId.z_RB), Is.EqualTo(1.0f));
            Assert.That(_t.RiskOf(ZoneId.z_CM), Is.EqualTo(0.2f));
            Assert.That(_t.RiskOf(ZoneId.z_CF), Is.EqualTo(0.6f));
            Assert.That(_t.RiskOf(ZoneId.z_LM), Is.EqualTo(0.6f));
            Assert.That(_t.RiskOf(ZoneId.z_RM), Is.EqualTo(0.6f));
            Assert.That(_t.RiskOf(ZoneId.z_CB), Is.EqualTo(0.6f));
        }

        [Test]
        public void Determinism_SameInputs_SameOutcome()
        {
            // §3.6 header: "No RNG." Bug caught: hidden randomness in resolution.
            var block = new BlockState(BlockCommit.Read, 1, 0.55f);
            var a = Resolve(0.73f, TimingGrade.Great, ZoneId.z_CB, block, dig: 0.61f);
            var b = Resolve(0.73f, TimingGrade.Great, ZoneId.z_CB, block, dig: 0.61f);
            Assert.That(b.Outcome, Is.EqualTo(a.Outcome));
            Assert.That(b.EffectiveAttack, Is.EqualTo(a.EffectiveAttack));
            Assert.That(b.BlockTouched, Is.EqualTo(a.BlockTouched));
            Assert.That(b.DigDisplayGrade, Is.EqualTo(a.DigDisplayGrade));
        }
    }
}
