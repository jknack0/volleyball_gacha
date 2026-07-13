using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Match;

namespace VG.Tests
{
    /// <summary>
    /// Defends the composition layer: replay determinism (data-schemas §5.2), match legality
    /// (win-by-2, §1.1), termination, and §8.3 rally instrumentation across seeds and tiers.
    /// </summary>
    [TestFixture]
    public class MatchSimTests
    {
        private static MatchConfig Config(ulong seed,
            DifficultyTier home = DifficultyTier.Normal,
            DifficultyTier away = DifficultyTier.Normal,
            MatchFormat format = MatchFormat.To11,
            int homeRaw = 100, int awayRaw = 100)
            => new MatchConfig(
                TeamSpec.Uniform("H", homeRaw, home),
                TeamSpec.Uniform("A", awayRaw, away),
                format, seed);

        [Test]
        public void SameSeed_ProducesBitIdenticalMatches()
        {
            // Bug caught: hidden nondeterminism anywhere in the composition (kills replays/ghost PvP).
            var a = new MatchSim(Config(42)).Run();
            var b = new MatchSim(Config(42)).Run();

            Assert.That(b.HomeScore, Is.EqualTo(a.HomeScore));
            Assert.That(b.AwayScore, Is.EqualTo(a.AwayScore));
            Assert.That(b.Rallies.Count, Is.EqualTo(a.Rallies.Count));
            for (int i = 0; i < a.Rallies.Count; i++)
            {
                Assert.That(b.Rallies[i].Contacts, Is.EqualTo(a.Rallies[i].Contacts), $"rally {i} contacts");
                Assert.That(b.Rallies[i].Outcome, Is.EqualTo(a.Rallies[i].Outcome), $"rally {i} outcome");
                Assert.That(b.Rallies[i].DurationTicks, Is.EqualTo(a.Rallies[i].DurationTicks), $"rally {i} ticks");
                Assert.That(b.Rallies[i].Grades, Is.EqualTo(a.Rallies[i].Grades), $"rally {i} grades");
                Assert.That(b.Rallies[i].Qualities, Is.EqualTo(a.Rallies[i].Qualities), $"rally {i} qualities");
            }
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentMatches()
        {
            var a = new MatchSim(Config(1)).Run();
            var b = new MatchSim(Config(2)).Run();
            bool identical = a.Rallies.Count == b.Rallies.Count
                          && a.HomeScore == b.HomeScore && a.AwayScore == b.AwayScore;
            if (identical)
                for (int i = 0; i < a.Rallies.Count && identical; i++)
                    identical = a.Rallies[i].Contacts == b.Rallies[i].Contacts
                             && a.Rallies[i].Outcome == b.Rallies[i].Outcome;
            Assert.That(identical, Is.False, "two seeds played the identical match — RNG not wired through");
        }

        [Test]
        public void Matches_Terminate_AndScoresAreLegal_AcrossSeedsAndTiers()
        {
            // Bug caught: rally soft-locks, illegal machine transitions, win-by-2 violations.
            var tiers = new[] { DifficultyTier.Easy, DifficultyTier.Normal, DifficultyTier.Hard };
            foreach (var home in tiers)
            foreach (var away in tiers)
            {
                for (ulong seed = 0; seed < 12; seed++)
                {
                    var r = new MatchSim(Config(seed * 7919 + (ulong)home * 31 + (ulong)away, home, away)).Run();
                    int hi = System.Math.Max(r.HomeScore, r.AwayScore);
                    int lo = System.Math.Min(r.HomeScore, r.AwayScore);

                    Assert.That(hi, Is.GreaterThanOrEqualTo(11), $"{home}v{away}/{seed}: nobody reached target");
                    Assert.That(hi - lo, Is.GreaterThanOrEqualTo(2), $"{home}v{away}/{seed}: win-by-2 violated");
                    Assert.That(r.HomeWon, Is.EqualTo(r.HomeScore > r.AwayScore), $"{home}v{away}/{seed}");
                    Assert.That(r.Rallies.Count, Is.EqualTo(r.HomeScore + r.AwayScore),
                        $"{home}v{away}/{seed}: every rally awards exactly one point");
                }
            }
        }

        [Test]
        public void RallyLogs_CarryTheSpecInstrumentation()
        {
            // §8.3 [structural]: {contacts, grades[], qualities[], outcome, duration} per rally.
            var r = new MatchSim(Config(7)).Run();
            foreach (var rally in r.Rallies)
            {
                Assert.That(rally.Contacts, Is.GreaterThanOrEqualTo(1), "at minimum the serve");
                Assert.That(rally.Grades.Count, Is.EqualTo(rally.Qualities.Count));
                Assert.That(rally.DurationTicks, Is.GreaterThan(0));
                foreach (float q in rally.Qualities)
                    Assert.That(q, Is.InRange(0f, 1f));
            }
        }

        [Test]
        public void SeedSet_IsCarried_ForReplay()
        {
            var r = new MatchSim(Config(99)).Run();
            Assert.That(r.Seeds.Master, Is.EqualTo(99UL));
            Assert.That(r.Seeds.Version, Is.EqualTo(1));
        }

        [Test]
        public void StrongerTeam_WinsMoreOften()
        {
            // Bug caught: stats not actually flowing into outcomes (the meta layer would be cosmetic).
            int strongWins = 0;
            const int n = 40;
            for (ulong seed = 0; seed < n; seed++)
                if (new MatchSim(Config(seed, homeRaw: 160, awayRaw: 60)).Run().HomeWon) strongWins++;

            Assert.That(strongWins, Is.GreaterThan(n * 3 / 4),
                $"160-raw team only won {strongWins}/{n} vs a 60-raw team — stats aren't biting");
        }

        [Test]
        public void HigherTier_BeatsLowerTier_OnEqualStats()
        {
            // §6: difficulty lives in AI decision/execution quality — it must show up in results.
            int hardWins = 0;
            const int n = 40;
            for (ulong seed = 0; seed < n; seed++)
                if (!new MatchSim(Config(seed, home: DifficultyTier.Easy, away: DifficultyTier.Hard)).Run().HomeWon)
                    hardWins++;

            Assert.That(hardWins, Is.GreaterThan(n * 2 / 3),
                $"Hard AI only won {hardWins}/{n} vs Easy on equal stats — tiers are not distinguishable");
        }
    }
}
