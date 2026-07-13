using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Resolution;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §3.4 (receive → set options, exhaustively) and
    /// §3.5 (set grade → spike window ctx) — data-schemas §5.1 CascadeMatrix rows.
    /// </summary>
    [TestFixture]
    public class CascadeTests
    {
        // §3.4, all 20 cells. Bug caught: any matrix cell flipped (tactical grammar corrupted).
        [TestCase(ReceiveGrade.S, SetOption.QuickMiddle, true)]
        [TestCase(ReceiveGrade.S, SetOption.HighOutside, true)]
        [TestCase(ReceiveGrade.S, SetOption.BackRowPipe, true)]
        [TestCase(ReceiveGrade.S, SetOption.Dump, true)]
        [TestCase(ReceiveGrade.A, SetOption.QuickMiddle, true)]
        [TestCase(ReceiveGrade.A, SetOption.HighOutside, true)]
        [TestCase(ReceiveGrade.A, SetOption.BackRowPipe, true)]
        [TestCase(ReceiveGrade.A, SetOption.Dump, false)]
        [TestCase(ReceiveGrade.B, SetOption.QuickMiddle, false)]
        [TestCase(ReceiveGrade.B, SetOption.HighOutside, true)]
        [TestCase(ReceiveGrade.B, SetOption.BackRowPipe, true)]
        [TestCase(ReceiveGrade.B, SetOption.Dump, false)]
        [TestCase(ReceiveGrade.C, SetOption.QuickMiddle, false)]
        [TestCase(ReceiveGrade.C, SetOption.HighOutside, true)]
        [TestCase(ReceiveGrade.C, SetOption.BackRowPipe, false)]
        [TestCase(ReceiveGrade.C, SetOption.Dump, false)]
        [TestCase(ReceiveGrade.Shank, SetOption.QuickMiddle, false)]
        [TestCase(ReceiveGrade.Shank, SetOption.HighOutside, true)]
        [TestCase(ReceiveGrade.Shank, SetOption.BackRowPipe, false)]
        [TestCase(ReceiveGrade.Shank, SetOption.Dump, false)]
        public void SetOptionMatrix_MatchesSpec(ReceiveGrade receive, SetOption option, bool expected)
        {
            Assert.That(Cascade.IsSetOptionAvailable(receive, option), Is.EqualTo(expected));
        }

        [Test]
        public void OnlyPlayableShank_CapsSetGradeAtGood()
        {
            // Bug caught: the cap leaking onto clean receives (punishes good play) or missing on Shank.
            Assert.Multiple(() =>
            {
                Assert.That(Cascade.CapsSetGradeAtGood(ReceiveGrade.Shank), Is.True);
                Assert.That(Cascade.CapsSetGradeAtGood(ReceiveGrade.C), Is.False);
                Assert.That(Cascade.CapsSetGradeAtGood(ReceiveGrade.B), Is.False);
                Assert.That(Cascade.CapsSetGradeAtGood(ReceiveGrade.A), Is.False);
                Assert.That(Cascade.CapsSetGradeAtGood(ReceiveGrade.S), Is.False);
            });
        }

        [Test]
        public void SpikeWindowCtx_MatchesSpecTable_AndMissMeansNoSpike()
        {
            // Bug caught: §3.5 ctx inverted (bad sets widening spike windows) or Miss producing a spike.
            var t = new CascadeTunables();
            Assert.Multiple(() =>
            {
                Assert.That(Cascade.TryGetSpikeWindowCtx(t, TimingGrade.Perfect, out float p), Is.True);
                Assert.That(p, Is.EqualTo(1.25f));
                Assert.That(Cascade.TryGetSpikeWindowCtx(t, TimingGrade.Great, out float g), Is.True);
                Assert.That(g, Is.EqualTo(1.0f));
                Assert.That(Cascade.TryGetSpikeWindowCtx(t, TimingGrade.Good, out float gd), Is.True);
                Assert.That(gd, Is.EqualTo(0.75f));
                Assert.That(Cascade.TryGetSpikeWindowCtx(t, TimingGrade.Miss, out _), Is.False,
                    "Miss set must route a free ball, never a spike");
            });
        }
    }
}
