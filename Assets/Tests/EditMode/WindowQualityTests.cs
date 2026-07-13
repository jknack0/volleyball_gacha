using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Resolution;

namespace VG.Tests
{
    /// <summary>
    /// VB-4: timing-window and quality math, docs/m0-gameplay-spec.md §3.1–3.3 + §7.4.
    /// Covers data-schemas.md §5.1 rows TimingGrade_BoundaryMs_Exact,
    /// TimingGrade_StatWidensPerfectWindow_Monotonic, Quality_ClampAtStat0_Floor,
    /// Quality_ClampAtStat1_Ceiling. Deterministic — zero RNG.
    /// </summary>
    [TestFixture]
    public class WindowQualityTests
    {
        private ResolutionTunables _t;

        [SetUp]
        public void SetUp()
        {
            _t = new ResolutionTunables(); // spec defaults
        }

        // ---------------------------------------------------------------- §3.1 window size

        // Contract: window is strictly monotonic in stat_c — stats widen, never remove, the input.
        // Bug caught: widening formula inverted (1 − k·stat) or overflowing at high stats.
        [Test]
        public void WindowMs_MonotonicInStat_AndExactAtEndpoints()
        {
            float prev = -1f;
            foreach (float stat in new[] { 0f, 0.25f, 0.5f, 0.75f, 1f })
            {
                float w = ContactWindows.WindowMs(_t, ContactType.Spike, stat, 1f, 1f);
                Assert.That(w, Is.GreaterThan(prev), $"Window not strictly increasing at stat {stat}");
                prev = w;
            }

            // Endpoints per §3.1 defaults: 150 × (1 + 0.5×stat).
            Assert.That(ContactWindows.WindowMs(_t, ContactType.Spike, 0f, 1f, 1f), Is.EqualTo(150f));
            Assert.That(ContactWindows.WindowMs(_t, ContactType.Spike, 1f, 1f, 1f), Is.EqualTo(225f));
        }

        // Contract (§5.1 TimingGrade_StatWidensPerfectWindow_Monotonic): higher stat ⇒ Perfect band ≥,
        // and widening never degenerates into auto-Perfect (Perfect band stays < full window).
        // Bug caught: band fractions recomputed from a widened base so Perfect swallows Good.
        [Test]
        public void PerfectBand_WidensWithStat_ButNeverSwallowsWindow()
        {
            float wLow = ContactWindows.WindowMs(_t, ContactType.Receive, 0f, 1f, 1f);
            float wHigh = ContactWindows.WindowMs(_t, ContactType.Receive, 1f, 1f, 1f);
            float perfectLow = wLow * _t.PerfectFraction;
            float perfectHigh = wHigh * _t.PerfectFraction;

            Assert.That(perfectHigh, Is.GreaterThan(perfectLow),
                "Stat widening must widen the Perfect band too (uniform scaling).");
            // A tap just inside the widened Perfect edge is Perfect; well outside it is not —
            // widening did not turn the whole window into Perfect.
            Assert.That(ContactWindows.Classify(_t, perfectHigh - 0.5f, wHigh), Is.EqualTo(TimingGrade.Perfect));
            Assert.That(ContactWindows.Classify(_t, perfectHigh + 0.5f, wHigh), Is.EqualTo(TimingGrade.Great));
            Assert.That(ContactWindows.Classify(_t, wHigh - 0.5f, wHigh), Is.EqualTo(TimingGrade.Good));
        }

        // Contract: assist (§7.4) multiplies the window exactly — +0%/+25%/+50% steps.
        // Bug caught: assist added as flat ms, or applied to the Perfect band only.
        [Test]
        public void Assist_MultipliesWindowExactly()
        {
            float baseline = ContactWindows.WindowMs(_t, ContactType.Serve, 0.5f, 1f, _t.AssistMultiplier(0));

            Assert.That(_t.AssistMultiplier(0), Is.EqualTo(1.00f));
            Assert.That(_t.AssistMultiplier(1), Is.EqualTo(1.25f));
            Assert.That(_t.AssistMultiplier(2), Is.EqualTo(1.50f));

            Assert.That(ContactWindows.WindowMs(_t, ContactType.Serve, 0.5f, 1f, _t.AssistMultiplier(1)),
                Is.EqualTo(baseline * 1.25f).Within(1e-4f));
            Assert.That(ContactWindows.WindowMs(_t, ContactType.Serve, 0.5f, 1f, _t.AssistMultiplier(2)),
                Is.EqualTo(baseline * 1.50f).Within(1e-4f));

            // §7.4: exactly three steps — a fourth level is a caller bug, not a silent clamp.
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _t.AssistMultiplier(3));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => _t.AssistMultiplier(-1));
        }

        // Contract: ctx scales the window linearly (§3.1) — the §3.5 set-grade hook and the
        // quick-set ×0.8 arrive through this one multiplier.
        // Bug caught: ctx applied to base_ms before stat widening with a different formula shape,
        // or quick-set shrink hardcoded against a specific contact type.
        [Test]
        public void Ctx_ScalesWindowLinearly()
        {
            float neutral = ContactWindows.WindowMs(_t, ContactType.Spike, 0.5f, _t.DefaultCtx, 1f);

            foreach (float ctx in new[] { 0.75f, _t.QuickSetAttackCtx, 1.25f })
                Assert.That(ContactWindows.WindowMs(_t, ContactType.Spike, 0.5f, ctx, 1f),
                    Is.EqualTo(neutral * ctx).Within(1e-4f),
                    $"ctx {ctx} did not scale the window linearly");
        }

        // ---------------------------------------------------------------- §3.1 classification

        // Contract (§5.1 TimingGrade_BoundaryMs_Exact): with defaults (window 150) the band edges
        // sit at exactly 40/90/150 ms and a boundary value resolves to the BETTER grade.
        // Bug caught: '<' vs '<=' flip demoting a frame-perfect input to Great.
        [Test]
        public void Classify_BoundaryMs_Exact_BothSides()
        {
            const float w = 150f; // stat 0, ctx 1, assist 1

            Assert.That(ContactWindows.Classify(_t, 0f, w), Is.EqualTo(TimingGrade.Perfect));
            Assert.That(ContactWindows.Classify(_t, 40f, w), Is.EqualTo(TimingGrade.Perfect), "boundary → better grade");
            Assert.That(ContactWindows.Classify(_t, 40.01f, w), Is.EqualTo(TimingGrade.Great));
            Assert.That(ContactWindows.Classify(_t, 90f, w), Is.EqualTo(TimingGrade.Great), "boundary → better grade");
            Assert.That(ContactWindows.Classify(_t, 90.01f, w), Is.EqualTo(TimingGrade.Good));
            Assert.That(ContactWindows.Classify(_t, 150f, w), Is.EqualTo(TimingGrade.Good), "boundary → better grade");
            Assert.That(ContactWindows.Classify(_t, 150.01f, w), Is.EqualTo(TimingGrade.Miss));
        }

        // Contract: bands are centered on t* — early (negative) and late (positive) offsets of the
        // same magnitude grade identically (§3.1: Δ = |t − t*|).
        // Bug caught: signed comparison silently making all early taps Perfect or all Miss.
        [Test]
        public void Classify_CenteredBands_SignIrrelevant()
        {
            const float w = 150f;
            Assert.That(ContactWindows.Classify(_t, -40f, w), Is.EqualTo(TimingGrade.Perfect));
            Assert.That(ContactWindows.Classify(_t, -90f, w), Is.EqualTo(TimingGrade.Great));
            Assert.That(ContactWindows.Classify(_t, -150f, w), Is.EqualTo(TimingGrade.Good));
            Assert.That(ContactWindows.Classify(_t, -151f, w), Is.EqualTo(TimingGrade.Miss));
        }

        // Contract: beyond W_Good ⇒ Miss (§3.1), at any widening.
        // Bug caught: Miss branch computed against the unwidened base window.
        [Test]
        public void Classify_OutsideWindow_IsMiss_AtAnyWidening()
        {
            float wide = ContactWindows.WindowMs(_t, ContactType.Spike, 1f, 1.25f, _t.AssistMultiplier(2)); // 421.875
            Assert.That(ContactWindows.Classify(_t, wide + 0.5f, wide), Is.EqualTo(TimingGrade.Miss));
            Assert.That(ContactWindows.Classify(_t, wide - 0.5f, wide), Is.EqualTo(TimingGrade.Good),
                "Just inside the widened outer edge must still be a hit.");
            Assert.That(ContactWindows.Classify(_t, 10_000f, wide), Is.EqualTo(TimingGrade.Miss));
        }

        // Contract: band edges scale in base_ms proportion with the window (§3.1 — every multiplier
        // widens Perfect/Great/Good uniformly). At window 300 (2×) the edges are 80/180/300.
        // Bug caught: Classify comparing Δ against unscaled 40/90 while the outer edge scales.
        [Test]
        public void Classify_BandEdges_ScaleWithWindow()
        {
            const float w = 300f;
            Assert.That(ContactWindows.Classify(_t, 80f, w), Is.EqualTo(TimingGrade.Perfect));
            Assert.That(ContactWindows.Classify(_t, 80.01f, w), Is.EqualTo(TimingGrade.Great));
            Assert.That(ContactWindows.Classify(_t, 180f, w), Is.EqualTo(TimingGrade.Great));
            Assert.That(ContactWindows.Classify(_t, 180.01f, w), Is.EqualTo(TimingGrade.Good));
            Assert.That(ContactWindows.Classify(_t, 300f, w), Is.EqualTo(TimingGrade.Good));
            Assert.That(ContactWindows.Classify(_t, 300.01f, w), Is.EqualTo(TimingGrade.Miss));
        }

        // ---------------------------------------------------------------- §3.1 governing stats

        // Contract: stat_c = mean of the contact's governing stats (§3.1 table [structural]);
        // Receive and Dig share a mapping; Set is Technique alone.
        // Bug caught: a contact reading the wrong stat column (e.g. Spike off Technique),
        // breaking every window and quality downstream.
        [Test]
        public void GoverningStatC_MatchesSpecTable()
        {
            // Distinct normalized values: Power .5, Jump .3, Technique .9, Serve .7, Receive .2, Speed .6
            var stats = new StatBlock { Power = 100, Jump = 60, Technique = 180, Serve = 140, Receive = 40, Speed = 120 };

            Assert.That(ContactWindows.GoverningStatC(ContactType.Serve, in stats), Is.EqualTo(0.6f).Within(1e-5f));   // (Serve+Power)/2
            Assert.That(ContactWindows.GoverningStatC(ContactType.Receive, in stats), Is.EqualTo(0.4f).Within(1e-5f)); // (Receive+Speed)/2
            Assert.That(ContactWindows.GoverningStatC(ContactType.Dig, in stats), Is.EqualTo(0.4f).Within(1e-5f));     // same as Receive
            Assert.That(ContactWindows.GoverningStatC(ContactType.Set, in stats), Is.EqualTo(0.9f).Within(1e-5f));     // Technique
            Assert.That(ContactWindows.GoverningStatC(ContactType.Spike, in stats), Is.EqualTo(0.4f).Within(1e-5f));   // (Power+Jump)/2
            Assert.That(ContactWindows.GoverningStatC(ContactType.Block, in stats), Is.EqualTo(0.6f).Within(1e-5f));   // (Jump+Technique)/2
        }

        // ---------------------------------------------------------------- §3.2 quality

        // Contract (§5.1 Quality_ClampAtStat0_Floor): at stat 0 quality spans exactly [0.15, 0.55]
        // and stays in [0,1] even when a debuffed floor goes negative.
        // Bug caught: negative floor leaking below 0 and corrupting the downstream cascade.
        [Test]
        public void Quality_AtStat0_FloorAndClamp()
        {
            Assert.That(QualityMath.Quality(_t, TimingGrade.Miss, 0f), Is.EqualTo(0.15f).Within(1e-5f),
                "Miss quality must equal floor(stats) (§3.2/§3.3).");
            Assert.That(QualityMath.Quality(_t, TimingGrade.Perfect, 0f), Is.EqualTo(0.55f).Within(1e-5f),
                "Perfect quality must equal ceiling(stats).");

            // Debuff scenario: floor pushed negative must clamp to 0, not go negative.
            var debuffed = new ResolutionTunables { QualityFloorBase = -0.4f };
            Assert.That(QualityMath.Quality(debuffed, TimingGrade.Miss, 0f), Is.EqualTo(0f));
        }

        // Contract (§5.1 Quality_ClampAtStat1_Ceiling): at stat 1 with Perfect (and any stacked
        // buff coefficient) quality caps at exactly 1.
        // Bug caught: buff stacking exceeding 1.0, making spikes literally unreceivable.
        [Test]
        public void Quality_AtStat1_CeilingAndClamp()
        {
            Assert.That(QualityMath.Quality(_t, TimingGrade.Perfect, 1f), Is.EqualTo(1f),
                "ceiling(1) = 0.55 + 0.45 = 1.0 exactly (§3.2).");

            // Buff scenario: an over-1 grade coefficient must clamp at 1, never exceed it.
            var buffed = new ResolutionTunables { GradeCoefficientPerfect = 1.5f };
            Assert.That(QualityMath.Quality(buffed, TimingGrade.Perfect, 1f), Is.EqualTo(1f));
            Assert.That(QualityMath.Quality(buffed, TimingGrade.Perfect, 0.5f), Is.LessThanOrEqualTo(1f));
        }

        // Contract: for a fixed stat, quality is strictly ordered Perfect > Great > Good > Miss
        // (§3.2 grade coefficients 1.0 > 0.7 > 0.4 > 0.0), and floor < ceiling at every stat.
        // Bug caught: coefficient table transposed so a Good outscores a Great.
        [Test]
        public void Quality_GradeOrdering_Monotone()
        {
            foreach (float stat in new[] { 0f, 0.3f, 0.7f, 1f })
            {
                float perfect = QualityMath.Quality(_t, TimingGrade.Perfect, stat);
                float great = QualityMath.Quality(_t, TimingGrade.Great, stat);
                float good = QualityMath.Quality(_t, TimingGrade.Good, stat);
                float miss = QualityMath.Quality(_t, TimingGrade.Miss, stat);

                Assert.That(perfect, Is.GreaterThan(great), $"Perfect ≤ Great at stat {stat}");
                Assert.That(great, Is.GreaterThan(good), $"Great ≤ Good at stat {stat}");
                Assert.That(good, Is.GreaterThan(miss), $"Good ≤ Miss at stat {stat}");
                Assert.That(QualityMath.Floor(_t, stat), Is.LessThan(QualityMath.Ceiling(_t, stat)),
                    $"floor ≥ ceiling at stat {stat} — grade would stop mattering");
            }
        }

        // Contract: stats raise BOTH floor and ceiling (§3.2 — "stats raise floor and ceiling");
        // a maxed whiff (Miss at stat 1) still scores below a zero-stat Perfect: input keeps primacy
        // (PLAN pillar 2 via §3.2 constants).
        // Bug caught: floor slope applied to ceiling only, letting stats replace timing.
        [Test]
        public void Quality_StatsRaiseFloorAndCeiling_InputStillPrimary()
        {
            Assert.That(QualityMath.Quality(_t, TimingGrade.Miss, 1f), Is.EqualTo(0.50f).Within(1e-5f)); // floor(1)
            Assert.That(QualityMath.Quality(_t, TimingGrade.Good, 1f),
                Is.GreaterThan(QualityMath.Quality(_t, TimingGrade.Good, 0f)));
            Assert.That(QualityMath.Quality(_t, TimingGrade.Miss, 1f),
                Is.LessThan(QualityMath.Quality(_t, TimingGrade.Perfect, 0f)),
                "Stat 1 Miss (0.50) must stay below stat 0 Perfect (0.55) — timing must keep primacy.");
        }

        // ---------------------------------------------------------------- §3.3 receive grades

        // Contract: quality → ReceiveGrade thresholds are exhaustive and lower-bound inclusive
        // (S ≥ .85, A ≥ .65, B ≥ .45, C ≥ .25, else Shank), matching §3.3 interval notation.
        // Bug caught: '>' at a threshold demoting an exactly-0.85 receive to A.
        [Test]
        public void ReceiveGradeOf_Thresholds_Exhaustive_EdgesInclusive()
        {
            Assert.That(QualityMath.ReceiveGradeOf(_t, 1.00f), Is.EqualTo(ReceiveGrade.S));
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.85f), Is.EqualTo(ReceiveGrade.S), "exact threshold → S");
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.8499f), Is.EqualTo(ReceiveGrade.A));
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.65f), Is.EqualTo(ReceiveGrade.A), "exact threshold → A");
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.6499f), Is.EqualTo(ReceiveGrade.B));
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.45f), Is.EqualTo(ReceiveGrade.B), "exact threshold → B");
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.4499f), Is.EqualTo(ReceiveGrade.C));
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.25f), Is.EqualTo(ReceiveGrade.C), "exact threshold → C");
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0.2499f), Is.EqualTo(ReceiveGrade.Shank));
            Assert.That(QualityMath.ReceiveGradeOf(_t, 0f), Is.EqualTo(ReceiveGrade.Shank));
        }

        // Contract: the whole quality range [0,1] maps to some grade with no dead gaps, and the
        // mapping is monotone non-decreasing in quality (ReceiveGrade enum orders Shank<C<B<A<S).
        // Bug caught: reordered threshold checks creating an unreachable grade band.
        [Test]
        public void ReceiveGradeOf_MonotoneAcrossFullRange()
        {
            var prev = ReceiveGrade.Shank;
            for (int i = 0; i <= 100; i++)
            {
                float q = i / 100f;
                var g = QualityMath.ReceiveGradeOf(_t, q);
                Assert.That(g, Is.GreaterThanOrEqualTo(prev), $"Grade regressed at q={q}");
                prev = g;
            }
            Assert.That(prev, Is.EqualTo(ReceiveGrade.S), "q=1 must reach S");
        }
    }
}
