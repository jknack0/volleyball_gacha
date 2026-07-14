using System.Collections.Generic;
using NUnit.Framework;
using VG.Data;
using VG.Gameplay.Input;
using VG.Gameplay.Match;
using VG.Gameplay.Rally;

namespace VG.Tests
{
    /// <summary>
    /// VB-13 acceptance: the human-controlled side. Defends replay identity
    /// ((seed + input log) ⇒ bit-identical MatchResult — data-schemas §4.3), the spec's
    /// no-input fallbacks, and human timing windows actually grading contacts.
    /// </summary>
    [TestFixture]
    public class HumanMatchTests
    {
        private static MatchConfig Config(ulong seed, int size = 3)
            => new MatchConfig(
                TeamSpec.Uniform("H", 100, DifficultyTier.Normal, teamSize: size),
                TeamSpec.Uniform("A", 100, DifficultyTier.Normal, teamSize: size),
                MatchFormat.To11, seed);

        /// <summary>A blind "plausible player" script: one of each input kind every 20 ticks.</summary>
        private static List<PlayerInput> SpamScript(int horizonTicks)
        {
            var inputs = new List<PlayerInput>();
            for (int t = 20; t < horizonTicks; t += 20)
            {
                inputs.Add(new PlayerInput(t, PlayerInputKind.ServeRelease, 0, (int)ZoneId.z_CB));
                inputs.Add(new PlayerInput(t, PlayerInputKind.ReceiveCommit));
                inputs.Add(new PlayerInput(t, PlayerInputKind.TimingTap));
                inputs.Add(new PlayerInput(t, PlayerInputKind.SetChoice, (int)SetOption.HighOutside));
                inputs.Add(new PlayerInput(t, PlayerInputKind.SpikeTap, (int)SpikeShot.Cross));
            }
            return inputs;
        }

        private static MatchResult RunHuman(ulong seed, List<PlayerInput> script)
        {
            var sim = new MatchSim(Config(seed), humanSide: TeamSide.Home);
            sim.SubmitInputs(script);
            return sim.Run();
        }

        [Test]
        public void SameSeedAndInputLog_ReplaysBitIdentically()
        {
            // THE VB-13 acceptance row: a recorded input log replays into the identical outcome.
            var script = SpamScript(60_000);
            var a = RunHuman(77, script);
            var b = RunHuman(77, new List<PlayerInput>(script));

            Assert.That(b.HomeScore, Is.EqualTo(a.HomeScore));
            Assert.That(b.AwayScore, Is.EqualTo(a.AwayScore));
            Assert.That(b.Rallies.Count, Is.EqualTo(a.Rallies.Count));
            for (int i = 0; i < a.Rallies.Count; i++)
            {
                Assert.That(b.Rallies[i].Outcome, Is.EqualTo(a.Rallies[i].Outcome), $"rally {i}");
                Assert.That(b.Rallies[i].Grades, Is.EqualTo(a.Rallies[i].Grades), $"rally {i} grades");
                Assert.That(b.Rallies[i].Qualities, Is.EqualTo(a.Rallies[i].Qualities), $"rally {i} qualities");
                Assert.That(b.Rallies[i].DurationTicks, Is.EqualTo(a.Rallies[i].DurationTicks), $"rally {i} ticks");
            }
        }

        [Test]
        public void DifferentInputLogs_ProduceDifferentMatches()
        {
            // Bug caught: human inputs silently ignored (the "human" side secretly playing itself).
            var a = RunHuman(77, SpamScript(60_000));
            var b = RunHuman(77, new List<PlayerInput>()); // same seed, no inputs at all
            bool identical = a.HomeScore == b.HomeScore && a.AwayScore == b.AwayScore
                          && a.Rallies.Count == b.Rallies.Count;
            if (identical)
                for (int i = 0; i < a.Rallies.Count && identical; i++)
                {
                    identical = a.Rallies[i].Contacts == b.Rallies[i].Contacts;
                    if (identical)
                        for (int gIdx = 0; gIdx < a.Rallies[i].Grades.Count && identical; gIdx++)
                            identical = a.Rallies[i].Grades[gIdx] == b.Rallies[i].Grades[gIdx];
                }
            Assert.That(identical, Is.False, "input log had no effect on the match");
        }

        [Test]
        public void NoInputAtAll_StillTerminates_ViaSpecFallbacks()
        {
            // T3 auto-serve, T8 auto-set, no-commit ace — the spec's own safety nets.
            var r = RunHuman(5, new List<PlayerInput>());
            int hi = System.Math.Max(r.HomeScore, r.AwayScore);
            Assert.That(hi, Is.GreaterThanOrEqualTo(11));
            Assert.That(System.Math.Abs(r.HomeScore - r.AwayScore), Is.GreaterThanOrEqualTo(2));
            Assert.That(r.AwayScore, Is.GreaterThan(r.HomeScore), "an inert player must lose");
        }

        [Test]
        public void SweetSpotServeRelease_OutgradesAWildOne()
        {
            // Human timing windows must actually bite: release at the meter's sweet tick vs 20 ticks off.
            float SweetQuality(int offsetTicks, ulong seed)
            {
                var sim = new MatchSim(Config(seed), humanSide: TeamSide.Home);
                int guard = 0;
                while (sim.CurrentState != RallyState.ServeAim && !sim.Done)
                {
                    sim.Tick();
                    if (++guard > 10_000) Assert.Fail("never reached ServeAim");
                }
                if (sim.ServingSide != TeamSide.Home) return -1f; // coin flip gave Away the serve — skip seed
                int anchor = sim.SimTick;
                sim.SubmitInput(new PlayerInput(anchor + 45 + offsetTicks, PlayerInputKind.ServeRelease, 0, (int)ZoneId.z_CB));
                for (int i = 0; i < 5000 && sim.Result.Rallies.Count == 0 && !sim.Done; i++) sim.Tick();
                // First recorded contact of the first rally is the serve.
                var log = sim.Result.Rallies.Count > 0 ? sim.Result.Rallies[0] : null;
                if (log == null)
                {
                    // rally still running — the serve quality is the first grade entry once present
                    Assert.Fail("rally did not progress far enough to record the serve");
                }
                return log.Qualities[0];
            }

            // Find a seed where Home serves first.
            for (ulong seed = 1; seed < 12; seed++)
            {
                float sweet = SweetQuality(0, seed);
                if (sweet < 0f) continue;
                float wild = SweetQuality(22, seed); // 22 ticks ≈ 367 ms off — a Miss
                Assert.That(sweet, Is.GreaterThan(wild),
                    $"seed {seed}: sweet-tick release ({sweet:F3}) must outgrade a wild one ({wild:F3})");
                return;
            }
            Assert.Inconclusive("no seed in range gave Home the first serve");
        }

        [Test]
        public void AssistLevel_WidensHumanWindows()
        {
            // §7.4: assist turns a near-miss release into a graded one. Same seed, same input, only assist differs.
            float QualityAt(int assist, ulong seed)
            {
                var sim = new MatchSim(Config(seed), humanSide: TeamSide.Home) { AssistLevel = assist };
                int guard = 0;
                while (sim.CurrentState != RallyState.ServeAim && !sim.Done)
                {
                    sim.Tick();
                    if (++guard > 10_000) Assert.Fail("never reached ServeAim");
                }
                if (sim.ServingSide != TeamSide.Home) return -1f;
                int anchor = sim.SimTick;
                sim.SubmitInput(new PlayerInput(anchor + 45 + 13, PlayerInputKind.ServeRelease, 0, (int)ZoneId.z_CB)); // ~217 ms off: Miss bare, Good with +50%
                for (int i = 0; i < 5000 && sim.Result.Rallies.Count == 0 && !sim.Done; i++) sim.Tick();
                return sim.Result.Rallies.Count > 0 ? sim.Result.Rallies[0].Qualities[0] : float.NaN;
            }

            for (ulong seed = 1; seed < 12; seed++)
            {
                float without = QualityAt(0, seed);
                if (without < 0f) continue;
                float with50 = QualityAt(2, seed);
                Assert.That(with50, Is.GreaterThanOrEqualTo(without),
                    $"seed {seed}: assist must never worsen a human contact");
                Assert.That(with50, Is.GreaterThan(without),
                    $"seed {seed}: a ~183 ms miss should be rescued by +50% windows");
                return;
            }
            Assert.Inconclusive("no seed in range gave Home the first serve");
        }
    }
}
