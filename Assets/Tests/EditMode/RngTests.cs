using System;
using NUnit.Framework;
using VG.Gameplay.Rng;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/data-schemas.md §4.1 (seeded RNG, named streams) and §5.1 rows
    /// Rng_SameSeedSameSequence / Rng_StreamIsolation / Rng_StateRoundTrip.
    /// </summary>
    [TestFixture]
    public class RngTests
    {
        private const ulong Seed = 0xDEADBEEFCAFEBABEUL;

        [Test]
        public void SameMaster_ProducesIdenticalSequences_PerStream()
        {
            var a = RngSet.FromMaster(Seed);
            var b = RngSet.FromMaster(Seed);

            foreach (RngStream stream in Enum.GetValues(typeof(RngStream)))
            {
                var ra = a.Get(stream);
                var rb = b.Get(stream);
                for (int i = 0; i < 1000; i++)
                    Assert.That(rb.NextUInt(), Is.EqualTo(ra.NextUInt()),
                        $"Divergence in stream {stream} at draw {i}");
            }
        }

        [Test]
        public void DifferentMasters_Diverge()
        {
            var a = RngSet.FromMaster(1).Get(RngStream.Rally);
            var b = RngSet.FromMaster(2).Get(RngStream.Rally);

            bool anyDifferent = false;
            for (int i = 0; i < 16 && !anyDifferent; i++)
                anyDifferent = a.NextUInt() != b.NextUInt();
            Assert.That(anyDifferent, Is.True, "Distinct masters produced identical Rally prefixes.");
        }

        [Test]
        public void Streams_AreDistinctSequences()
        {
            var set = RngSet.FromMaster(Seed);
            uint gacha = set.Get(RngStream.Gacha).NextUInt();
            uint rally = set.Get(RngStream.Rally).NextUInt();
            uint ai = set.Get(RngStream.Ai).NextUInt();
            uint substats = set.Get(RngStream.Substats).NextUInt();

            Assert.That(new[] { gacha, rally, ai, substats }, Is.Unique,
                "First draws across the four streams collided — per-stream derivation broken.");
        }

        [Test]
        public void StreamIsolation_ConsumingOneStream_NeverShiftsAnother()
        {
            var touched = RngSet.FromMaster(Seed);
            var control = RngSet.FromMaster(Seed);

            // Burn 500 Rally draws + 250 Ai draws on one set only.
            for (int i = 0; i < 500; i++) touched.Get(RngStream.Rally).NextUInt();
            for (int i = 0; i < 250; i++) touched.Get(RngStream.Ai).NextUInt();

            // Gacha and Substats must be untouched: identical to a virgin set.
            for (int i = 0; i < 100; i++)
            {
                Assert.That(touched.Get(RngStream.Gacha).NextUInt(),
                    Is.EqualTo(control.Get(RngStream.Gacha).NextUInt()), $"Gacha shifted at draw {i}");
                Assert.That(touched.Get(RngStream.Substats).NextUInt(),
                    Is.EqualTo(control.Get(RngStream.Substats).NextUInt()), $"Substats shifted at draw {i}");
            }
        }

        [Test]
        public void State_RoundTrips_MidSequence()
        {
            var original = Xoshiro128StarStar.FromSeed(Seed);
            for (int i = 0; i < 137; i++) original.NextUInt();

            var restored = new Xoshiro128StarStar(original.State);
            for (int i = 0; i < 100; i++)
                Assert.That(restored.NextUInt(), Is.EqualTo(original.NextUInt()),
                    $"Restored stream diverged at draw {i}");
        }

        [Test]
        public void RngSet_CaptureAndRestore_ContinuesAllStreams()
        {
            var live = RngSet.FromMaster(Seed);
            for (int i = 0; i < 313; i++) live.Get(RngStream.Rally).NextUInt();
            for (int i = 0; i < 71; i++) live.Get(RngStream.Gacha).NextUInt();

            var restored = RngSet.Restore(live.Seeds, live.CaptureStates());

            foreach (RngStream stream in Enum.GetValues(typeof(RngStream)))
                for (int i = 0; i < 50; i++)
                    Assert.That(restored.Get(stream).NextUInt(),
                        Is.EqualTo(live.Get(stream).NextUInt()),
                        $"Stream {stream} diverged after restore at draw {i}");
        }

        [Test]
        public void NextInt_StaysInRange_AndCoversEndpoints()
        {
            var rng = Xoshiro128StarStar.FromSeed(Seed);
            bool sawMin = false, sawMaxMinusOne = false;

            for (int i = 0; i < 10_000; i++)
            {
                int v = rng.NextInt(3, 7);
                Assert.That(v, Is.InRange(3, 6));
                if (v == 3) sawMin = true;
                if (v == 6) sawMaxMinusOne = true;
            }

            Assert.That(sawMin, Is.True, "Never drew the minimum of the range.");
            Assert.That(sawMaxMinusOne, Is.True, "Never drew the maximum of the range.");
        }

        [Test]
        public void NextInt_NegativeRanges_Work()
        {
            var rng = Xoshiro128StarStar.FromSeed(Seed);
            for (int i = 0; i < 1_000; i++)
                Assert.That(rng.NextInt(-10, -5), Is.InRange(-10, -6));
        }

        [Test]
        public void NextInt_EmptyRange_Throws()
        {
            var rng = Xoshiro128StarStar.FromSeed(Seed);
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(5, 4));
        }

        [Test]
        public void NextFloat01_StaysInUnitInterval()
        {
            var rng = Xoshiro128StarStar.FromSeed(Seed);
            for (int i = 0; i < 10_000; i++)
            {
                float f = rng.NextFloat01();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }

        [Test]
        public void AllZeroState_IsGuarded()
        {
            var rng = new Xoshiro128StarStar(new RngState());
            bool anyNonZero = false;
            for (int i = 0; i < 8 && !anyNonZero; i++)
                anyNonZero = rng.NextUInt() != 0;
            Assert.That(anyNonZero, Is.True, "All-zero state produced the degenerate constant-zero stream.");
        }

        [Test]
        public void SeedSet_Derivation_IsStableAcrossVersions_GoldenVector()
        {
            // Golden regression pin: these values were captured from the first verified run.
            // If this test fails, replay/save compatibility is broken — bump SeedSet.CurrentVersion
            // and migrate, never silently change derivation. [structural]
            var seeds = SeedSet.FromMaster(0x123456789ABCDEF0UL);
            var firstDraws = new uint[4];
            var set = new RngSet(seeds);
            for (int i = 0; i < 4; i++) firstDraws[i] = set.Get((RngStream)i).NextUInt();

            Assert.Multiple(() =>
            {
                Assert.That(seeds.Version, Is.EqualTo(1));
                Assert.That(seeds.Master, Is.EqualTo(0x123456789ABCDEF0UL));
                Assert.That(seeds.Gacha, Is.EqualTo(GoldenVectors.Gacha), "Gacha");
                Assert.That(seeds.Rally, Is.EqualTo(GoldenVectors.Rally), "Rally");
                Assert.That(seeds.Ai, Is.EqualTo(GoldenVectors.Ai), "Ai");
                Assert.That(seeds.Substats, Is.EqualTo(GoldenVectors.Substats), "Substats");
                Assert.That(firstDraws[0], Is.EqualTo(GoldenVectors.FirstDraws[0]), "Draw.Gacha");
                Assert.That(firstDraws[1], Is.EqualTo(GoldenVectors.FirstDraws[1]), "Draw.Rally");
                Assert.That(firstDraws[2], Is.EqualTo(GoldenVectors.FirstDraws[2]), "Draw.Ai");
                Assert.That(firstDraws[3], Is.EqualTo(GoldenVectors.FirstDraws[3]), "Draw.Substats");
            });
        }
    }
}
