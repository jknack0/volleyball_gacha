using VG.Gameplay.Ai;
using System;
using System.Collections.Generic;
using VG.Data;
using VG.Gameplay.Rng;

namespace VG.Gameplay.Match
{
    /// <summary>One capsule on the court. M0 grey-box: name + stats, nothing else.</summary>
    public readonly struct PlayerSpec
    {
        public readonly string Name;
        public readonly StatBlock Stats;

        public PlayerSpec(string name, StatBlock stats)
        {
            Name = name;
            Stats = stats;
        }
    }

    /// <summary>Six players in initial rotation order (court positions 1..6) + the AI tier driving them.</summary>
    public sealed class TeamSpec
    {
        public readonly PlayerSpec[] Players;
        public readonly DifficultyTier Tier;

        /// <summary>When set, grade sampling uses this distribution instead of the tier's §6.2 row — the skill-proxy hook (tooling-pipeline §2).</summary>
        public readonly GradeDistribution? GradeOverride;

        public TeamSpec(PlayerSpec[] players, DifficultyTier tier, GradeDistribution? gradeOverride = null)
        {
            if (players == null || players.Length != 6)
                throw new ArgumentException("A team is exactly 6 players (M0: no libero/bench — meta layer, M2).", nameof(players));
            Players = players;
            Tier = tier;
            GradeOverride = gradeOverride;
        }

        /// <summary>A uniform team where every player has every stat at <paramref name="raw"/> (0–200).</summary>
        public static TeamSpec Uniform(string prefix, int raw, DifficultyTier tier, GradeDistribution? gradeOverride = null)
        {
            var players = new PlayerSpec[6];
            for (int i = 0; i < 6; i++)
            {
                players[i] = new PlayerSpec($"{prefix}{i + 1}", new StatBlock
                {
                    Power = raw, Jump = raw, Technique = raw, Serve = raw, Receive = raw, Speed = raw,
                });
            }
            return new TeamSpec(players, tier, gradeOverride);
        }
    }

    public sealed class MatchConfig
    {
        public readonly TeamSpec Home;
        public readonly TeamSpec Away;
        public readonly MatchFormat Format;
        public readonly ulong MasterSeed;

        public MatchConfig(TeamSpec home, TeamSpec away, MatchFormat format, ulong masterSeed)
        {
            Home = home ?? throw new ArgumentNullException(nameof(home));
            Away = away ?? throw new ArgumentNullException(nameof(away));
            Format = format;
            MasterSeed = masterSeed;
        }
    }

    /// <summary>
    /// §8.3 instrumentation [structural]: per-rally log
    /// "{seed, contacts, grades[], qualities[], outcome, duration}" — the seed is match-level
    /// (SeedSet in MatchResult); contacts counts ball touches (serve/receive/set/spike/dig;
    /// block touches excluded per volleyball convention [structural]).
    /// </summary>
    public sealed class RallyLog
    {
        public int Contacts;
        public readonly List<TimingGrade> Grades = new List<TimingGrade>(12);
        public readonly List<float> Qualities = new List<float>(12);
        public RallyOutcome Outcome;
        public int DurationTicks;
        public bool ServedByHome;
        public bool WonByHome;
    }

    /// <summary>Headless match output — the M0 slice of data-schemas §2.8 (carries SeedSet for replay).</summary>
    public sealed class MatchResult
    {
        public MatchFormat Format;
        public SeedSet Seeds;
        public int HomeScore;
        public int AwayScore;
        public bool HomeWon;
        public readonly List<RallyLog> Rallies = new List<RallyLog>(64);
    }
}
