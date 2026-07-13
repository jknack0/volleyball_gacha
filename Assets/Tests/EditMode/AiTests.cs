using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Ai;
using VG.Gameplay.Rng;

namespace VG.Tests
{
    /// <summary>
    /// Defends docs/m0-gameplay-spec.md §6: §6.1 utility weights + argmax/tie-break rng
    /// contract, §6.2 grade distributions + one-draw sampling, §6.3 tactic vocabulary
    /// (including the §3.4 Cascade intersection), §6.4 no-player-side-concepts hard rule.
    /// </summary>
    [TestFixture]
    public class AiTests
    {
        private const ulong Seed = 0xA1D0C0DEBEEFCAFEUL;

        /// <summary>Delegating IRng that counts every draw — proves rng frugality contracts.</summary>
        private sealed class CountingRng : IRng
        {
            private readonly IRng _inner;
            public int Draws;

            public CountingRng(IRng inner) { _inner = inner; }

            public uint NextUInt() { Draws++; return _inner.NextUInt(); }
            public int NextInt(int min, int max) { Draws++; return _inner.NextInt(min, max); }
            public float NextFloat01() { Draws++; return _inner.NextFloat01(); }
        }

        // ---- (a) §6.1 weight table verbatim --------------------------------------------

        // Bug caught: any weight cell drifting from the spec table (silent difficulty retune).
        [TestCase(DifficultyTier.Easy, 0.2f, 0.1f, 0.0f, 0.3f, 0.4f, 0.0f)]
        [TestCase(DifficultyTier.Normal, 0.4f, 0.2f, 0.1f, 0.2f, 0.4f, 0.2f)]
        [TestCase(DifficultyTier.Hard, 0.6f, 0.3f, 0.2f, 0.1f, 0.4f, 0.4f)]
        public void WeightTable_MatchesSpecVerbatim(
            DifficultyTier tier, float matchup, float score, float hype, float rally, float lit, float surprise)
        {
            UtilityWeights w = new AiTunables().WeightsFor(tier);
            Assert.That(w.Matchup, Is.EqualTo(matchup), "w_matchup");
            Assert.That(w.Score, Is.EqualTo(score), "w_score");
            Assert.That(w.Hype, Is.EqualTo(hype), "w_hype");
            Assert.That(w.Rally, Is.EqualTo(rally), "w_rally");
            Assert.That(w.Lit, Is.EqualTo(lit), "w_lit");
            Assert.That(w.Surprise, Is.EqualTo(surprise), "w_surprise");
        }

        // Bug caught: any §6.2 probability cell drifting from spec (AI execution retuned unnoticed).
        [TestCase(DifficultyTier.Easy, 0.10f, 0.30f, 0.40f, 0.20f)]
        [TestCase(DifficultyTier.Normal, 0.25f, 0.40f, 0.25f, 0.10f)]
        [TestCase(DifficultyTier.Hard, 0.45f, 0.35f, 0.15f, 0.05f)]
        public void GradeDistributions_MatchSpecVerbatim(
            DifficultyTier tier, float perfect, float great, float good, float miss)
        {
            GradeDistribution d = new AiTunables().GradesFor(tier);
            Assert.That(d.Perfect, Is.EqualTo(perfect), "P(Perfect)");
            Assert.That(d.Great, Is.EqualTo(great), "P(Great)");
            Assert.That(d.Good, Is.EqualTo(good), "P(Good)");
            Assert.That(d.Miss, Is.EqualTo(miss), "P(Miss)");
            Assert.That(d.Perfect + d.Great + d.Good + d.Miss, Is.EqualTo(1f).Within(1e-6f),
                "distribution must be total");
        }

        // ---- (b) §6.1 scorer + argmax rng contract -------------------------------------

        [Test]
        public void Score_IsWeightedSumOfInputs()
        {
            // Contract: U(a) = Σ w_i·x_i, §6.1. Bug caught: a dropped/duplicated term or crossed wiring
            // (e.g. w_hype applied to x_rally) — distinct prime-ish values make any mix-up detectable.
            var w = new UtilityWeights { Matchup = 1f, Score = 2f, Hype = 4f, Rally = 8f, Lit = 16f, Surprise = 32f };
            var x = new UtilityInputs { Matchup = 0.5f, Score = 0.25f, Hype = 0.125f, Rally = 1f, Lit = 0f, Surprise = 0.5f };
            // 0.5 + 0.5 + 0.5 + 8 + 0 + 16 = 25.5
            Assert.That(UtilityScorer.Score(in w, in x), Is.EqualTo(25.5f));
        }

        [Test]
        public void PickArgmax_UniqueMax_PicksBest_AndConsumesZeroRng()
        {
            // Contract: unique max ⇒ argmax, NO rng draws [structural: replay stability].
            // Bug caught: an unconditional tie-break draw shifting the Ai stream every decision.
            var rng = Xoshiro128StarStar.FromSeed(Seed);
            RngState before = rng.State;

            var candidates = new[]
            {
                new UtilityInputs { Matchup = 0.2f },
                new UtilityInputs { Matchup = 0.9f, Lit = 1f },  // strictly best under every tier's weights
                new UtilityInputs { Matchup = 0.5f },
            };

            foreach (DifficultyTier tier in new[] { DifficultyTier.Easy, DifficultyTier.Normal, DifficultyTier.Hard })
                Assert.That(UtilityScorer.PickArgmax(new AiTunables(), tier, candidates, rng), Is.EqualTo(1), tier.ToString());

            Assert.That(rng.State, Is.EqualTo(before), "unique max must consume zero rng draws");
        }

        [Test]
        public void PickArgmax_ExactTies_ConsumeExactlyOneDraw_AndStayInTieSet()
        {
            // Contract: exact ties broken by ONE seeded draw ⚄ (§6.1). Bug caught: multi-draw
            // tie-breaks (stream drift) or the pick escaping the tied set.
            var tied = new UtilityInputs { Lit = 1f, Rally = 0.5f };
            var candidates = new[]
            {
                new UtilityInputs { Rally = 0.1f },  // loser
                tied, tied,                          // indices 1, 2 tied at max
                new UtilityInputs(),                 // loser
                tied,                                // index 4 tied at max
            };

            var counting = new CountingRng(Xoshiro128StarStar.FromSeed(Seed));
            int pick = UtilityScorer.PickArgmax(new AiTunables(), DifficultyTier.Normal, candidates, counting);

            Assert.That(counting.Draws, Is.EqualTo(1), "exactly one draw per tie-break");
            Assert.That(pick, Is.EqualTo(1).Or.EqualTo(2).Or.EqualTo(4), "pick must be a tied candidate");
        }

        [Test]
        public void PickArgmax_TieBreak_IsDeterministicPerSeed()
        {
            // Contract: seeded tie-break ⇒ same seed, same pick (§6.1 ⚄ + replay determinism).
            // Bug caught: any non-IRng entropy (time, hash order) leaking into the tie-break.
            var tied = new UtilityInputs { Lit = 1f };
            var candidates = new[] { tied, tied, tied, tied };

            for (ulong seed = 1; seed <= 5; seed++)
            {
                int first = UtilityScorer.PickArgmax(
                    new AiTunables(), DifficultyTier.Hard, candidates, Xoshiro128StarStar.FromSeed(seed));
                int second = UtilityScorer.PickArgmax(
                    new AiTunables(), DifficultyTier.Hard, candidates, Xoshiro128StarStar.FromSeed(seed));
                Assert.That(second, Is.EqualTo(first), $"seed {seed} must reproduce its tie-break");
            }
        }

        [Test]
        public void PickArgmax_EmptyCandidates_Throws()
        {
            // Contract: no legal actions is a caller bug, surfaced loudly. Bug caught: a silent
            // sentinel index (-1/0) flowing into the rally as a phantom action.
            Assert.That(
                () => UtilityScorer.PickArgmax(
                    new AiTunables(), DifficultyTier.Easy, ReadOnlySpan<UtilityInputs>.Empty, Xoshiro128StarStar.FromSeed(Seed)),
                Throws.ArgumentException);
        }

        // ---- (c)+(d) §6.2 sampler ------------------------------------------------------

        // Contract: empirical grade frequencies match the §6.2 tier row. Bug caught: CDF walked in
        // the wrong order, a boundary off-by-one, or a distribution row swapped between tiers.
        // ±2% absolute bound [tunable]: ~6σ headroom at n=10k, so a correct sampler never trips it.
        [TestCase(DifficultyTier.Easy)]
        [TestCase(DifficultyTier.Normal)]
        [TestCase(DifficultyTier.Hard)]
        public void Sample_EmpiricalFrequencies_MatchSpecWithin2Percent(DifficultyTier tier)
        {
            const int N = 10_000;
            var tunables = new AiTunables();
            var rng = Xoshiro128StarStar.FromSeed(Seed);

            var counts = new int[4];
            for (int i = 0; i < N; i++)
                counts[(int)GradeSampler.Sample(tunables, tier, rng)]++;

            GradeDistribution d = tunables.GradesFor(tier);
            Assert.That(counts[(int)TimingGrade.Perfect] / (float)N, Is.EqualTo(d.Perfect).Within(0.02f), "P(Perfect)");
            Assert.That(counts[(int)TimingGrade.Great] / (float)N, Is.EqualTo(d.Great).Within(0.02f), "P(Great)");
            Assert.That(counts[(int)TimingGrade.Good] / (float)N, Is.EqualTo(d.Good).Within(0.02f), "P(Good)");
            Assert.That(counts[(int)TimingGrade.Miss] / (float)N, Is.EqualTo(d.Miss).Within(0.02f), "P(Miss)");
        }

        [Test]
        public void Sample_ConsumesExactlyOneDrawPerCall()
        {
            // Contract: one NextFloat01 per sample [structural: replay stability]. Bug caught:
            // rejection sampling or per-bucket draws silently multiplying stream consumption.
            var counting = new CountingRng(Xoshiro128StarStar.FromSeed(Seed));
            var tunables = new AiTunables();

            for (int i = 1; i <= 100; i++)
            {
                GradeSampler.Sample(tunables, DifficultyTier.Normal, counting);
                Assert.That(counting.Draws, Is.EqualTo(i), $"call {i} must consume exactly one draw");
            }
        }

        // ---- (e) §6.3 tactic vocabulary ------------------------------------------------

        // §6.3 serve column, all 12 cells. Bug caught: a tier gaining/losing a serve kind
        // (e.g. Easy jump-serving, Hard losing its floats — supersets must stay supersets).
        [TestCase(DifficultyTier.Easy, ServeTactic.CenterFloat, true)]
        [TestCase(DifficultyTier.Easy, ServeTactic.LineFloat, false)]
        [TestCase(DifficultyTier.Easy, ServeTactic.CornerFloat, false)]
        [TestCase(DifficultyTier.Easy, ServeTactic.JumpServe, false)]
        [TestCase(DifficultyTier.Normal, ServeTactic.CenterFloat, true)]
        [TestCase(DifficultyTier.Normal, ServeTactic.LineFloat, true)]
        [TestCase(DifficultyTier.Normal, ServeTactic.CornerFloat, true)]
        [TestCase(DifficultyTier.Normal, ServeTactic.JumpServe, false)]
        [TestCase(DifficultyTier.Hard, ServeTactic.CenterFloat, true)]
        [TestCase(DifficultyTier.Hard, ServeTactic.LineFloat, true)]
        [TestCase(DifficultyTier.Hard, ServeTactic.CornerFloat, true)]
        [TestCase(DifficultyTier.Hard, ServeTactic.JumpServe, true)]
        public void ServeVocabulary_MatchesSpec(DifficultyTier tier, ServeTactic serve, bool expected)
        {
            Assert.That(TacticVocabulary.AllowsServe(tier, serve), Is.EqualTo(expected));
        }

        // §6.3 set-option column, all 12 cells. Bug caught: any vocabulary cell flipped
        // (Easy quicking, Normal dumping — difficulty identity corrupted).
        [TestCase(DifficultyTier.Easy, SetOption.HighOutside, true)]
        [TestCase(DifficultyTier.Easy, SetOption.QuickMiddle, false)]
        [TestCase(DifficultyTier.Easy, SetOption.BackRowPipe, false)]
        [TestCase(DifficultyTier.Easy, SetOption.Dump, false)]
        [TestCase(DifficultyTier.Normal, SetOption.HighOutside, true)]
        [TestCase(DifficultyTier.Normal, SetOption.QuickMiddle, true)]
        [TestCase(DifficultyTier.Normal, SetOption.BackRowPipe, false)]
        [TestCase(DifficultyTier.Normal, SetOption.Dump, false)]
        [TestCase(DifficultyTier.Hard, SetOption.HighOutside, true)]
        [TestCase(DifficultyTier.Hard, SetOption.QuickMiddle, true)]
        [TestCase(DifficultyTier.Hard, SetOption.BackRowPipe, true)]
        [TestCase(DifficultyTier.Hard, SetOption.Dump, true)]
        public void SetOptionVocabulary_MatchesSpec(DifficultyTier tier, SetOption option, bool expected)
        {
            Assert.That(TacticVocabulary.AllowsSetOption(tier, option), Is.EqualTo(expected));
        }

        [Test]
        public void MayChooseSetOption_IntersectsVocabularyWithCascadeLitMatrix()
        {
            // Contract: BOTH the §6.3 vocabulary AND the §3.4 lit matrix must allow the option.
            // Bug caught: either gate dropped — Normal AI quicking off a B receive (vocab-only),
            // or Easy AI quicking off an S receive (Cascade-only).
            // Normal + B receive: vocabulary allows Quick, but B does not light it.
            Assert.That(TacticVocabulary.MayChooseSetOption(
                DifficultyTier.Normal, ReceiveGrade.B, SetOption.QuickMiddle), Is.False,
                "B receive must veto Quick despite Normal vocabulary");

            // Easy + S receive: everything is lit, but Easy's vocabulary is high-outside only.
            Assert.That(TacticVocabulary.MayChooseSetOption(
                DifficultyTier.Easy, ReceiveGrade.S, SetOption.QuickMiddle), Is.False,
                "Easy vocabulary must veto Quick despite S receive");

            // Hard + C receive: full vocabulary, but only high-outside is lit.
            Assert.That(TacticVocabulary.MayChooseSetOption(
                DifficultyTier.Hard, ReceiveGrade.C, SetOption.BackRowPipe), Is.False,
                "C receive must veto pipe despite Hard vocabulary");

            // Both gates open.
            Assert.That(TacticVocabulary.MayChooseSetOption(
                DifficultyTier.Normal, ReceiveGrade.A, SetOption.QuickMiddle), Is.True,
                "A receive + Normal vocabulary must allow Quick");
            Assert.That(TacticVocabulary.MayChooseSetOption(
                DifficultyTier.Easy, ReceiveGrade.Shank, SetOption.HighOutside), Is.True,
                "the forced high-outside stays choosable at every tier");
        }

        [Test]
        public void BlockBehavior_MatchesSpecPerTier()
        {
            // §6.3 block column. Bug caught: tiers swapped (Easy soft-committing adjacent columns).
            Assert.That(TacticVocabulary.BlockBehaviorFor(DifficultyTier.Easy),
                Is.EqualTo(BlockBehavior.ReadOnlyCenter));
            Assert.That(TacticVocabulary.BlockBehaviorFor(DifficultyTier.Normal),
                Is.EqualTo(BlockBehavior.ReadWithOccasionalCommit));
            Assert.That(TacticVocabulary.BlockBehaviorFor(DifficultyTier.Hard),
                Is.EqualTo(BlockBehavior.FullMixAdjacentSoftCommit));
        }

        // ---- (f) determinism -----------------------------------------------------------

        [Test]
        public void SameSeed_ProducesIdenticalGradeAndDecisionSequences()
        {
            // Contract: the whole module is a pure function of (tunables, tier, rng) — same seed,
            // same sequences. Bug caught: hidden state (statics, caches) breaking replays.
            var tunables = new AiTunables();
            var tied = new UtilityInputs { Lit = 1f };
            var candidates = new[] { tied, tied, tied };

            var rngA = Xoshiro128StarStar.FromSeed(Seed);
            var rngB = Xoshiro128StarStar.FromSeed(Seed);

            for (int i = 0; i < 200; i++)
            {
                if ((i & 1) == 0)
                    Assert.That(GradeSampler.Sample(tunables, DifficultyTier.Hard, rngB),
                        Is.EqualTo(GradeSampler.Sample(tunables, DifficultyTier.Hard, rngA)),
                        $"grade sequence diverged at step {i}");
                else
                    Assert.That(UtilityScorer.PickArgmax(tunables, DifficultyTier.Hard, candidates, rngB),
                        Is.EqualTo(UtilityScorer.PickArgmax(tunables, DifficultyTier.Hard, candidates, rngA)),
                        $"decision sequence diverged at step {i}");
            }
        }

        // ---- §6.4 hard rule ------------------------------------------------------------

        [Test]
        public void PublicSurface_CarriesNoPlayerSideConcepts()
        {
            // Contract §6.4 [structural]: difficulty NEVER touches player windows, input latency,
            // or assist — those live in VG.Gameplay.Resolution behind player-only paths. Bug
            // caught: someone threading a window/latency/assist knob through the Ai module.
            string[] forbidden = { "window", "latency", "assist" };
            var offenders = new List<string>();

            foreach (Type type in typeof(AiTunables).Assembly.GetTypes())
            {
                if (type.Namespace != "VG.Gameplay.Ai" || !type.IsPublic)
                    continue;

                CheckName(type.Name, $"type {type.Name}", forbidden, offenders);
                foreach (MemberInfo member in type.GetMembers(
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    CheckName(member.Name, $"{type.Name}.{member.Name}", forbidden, offenders);
                }
            }

            Assert.That(offenders, Is.Empty,
                "§6.4: the Ai public surface must not name player-side concepts");
        }

        private static void CheckName(string name, string display, string[] forbidden, List<string> offenders)
        {
            string lower = name.ToLowerInvariant();
            for (int i = 0; i < forbidden.Length; i++)
            {
                if (lower.Contains(forbidden[i]))
                    offenders.Add($"{display} (contains '{forbidden[i]}')");
            }
        }
    }
}
