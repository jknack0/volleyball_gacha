using System;
using System.Collections.Generic;
using VG.Data;
using VG.Gameplay.Match;

namespace VG.SimRunner
{
    /// <summary>
    /// VB-11 headless runner. Modes:
    ///   batch  --home-tier X --away-tier Y [--matches N] [--seed0 S] [--home-raw R] [--away-raw R] [--format 11|15|25]
    ///   mirror [--matches N] [--tier X]         — identical teams; §tooling-2c bias suite (CI must contain 0.50)
    ///   transcript [--seed S] [--tier X]        — one match, rally-by-rally demo output
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            string mode = args.Length > 0 ? args[0] : "batch";
            var opt = ParseOptions(args);

            switch (mode)
            {
                case "mirror": return Mirror(opt);
                case "transcript": return Transcript(opt);
                default: return Batch(opt);
            }
        }

        private sealed class Options
        {
            public DifficultyTier HomeTier = DifficultyTier.Normal;
            public DifficultyTier AwayTier = DifficultyTier.Normal;
            public int Matches = 200;
            public ulong Seed0 = 1;
            public int HomeRaw = 100;
            public int AwayRaw = 100;
            public MatchFormat Format = MatchFormat.To11;
        }

        private static Options ParseOptions(string[] args)
        {
            var o = new Options();
            for (int i = 0; i < args.Length - 1; i++)
            {
                string v = args[i + 1];
                switch (args[i])
                {
                    case "--home-tier": o.HomeTier = Enum.Parse<DifficultyTier>(v, true); break;
                    case "--away-tier": o.AwayTier = Enum.Parse<DifficultyTier>(v, true); break;
                    case "--tier": o.HomeTier = o.AwayTier = Enum.Parse<DifficultyTier>(v, true); break;
                    case "--matches": o.Matches = int.Parse(v); break;
                    case "--seed0": case "--seed": o.Seed0 = ulong.Parse(v); break;
                    case "--home-raw": o.HomeRaw = int.Parse(v); break;
                    case "--away-raw": o.AwayRaw = int.Parse(v); break;
                    case "--format": o.Format = (MatchFormat)int.Parse(v); break;
                }
            }
            return o;
        }

        private static MatchResult Play(Options o, ulong seed)
            => new MatchSim(new MatchConfig(
                TeamSpec.Uniform("H", o.HomeRaw, o.HomeTier),
                TeamSpec.Uniform("A", o.AwayRaw, o.AwayTier),
                o.Format, seed)).Run();

        // ---- batch --------------------------------------------------------------------------

        private static int Batch(Options o)
        {
            int homeWins = 0, rallies = 0;
            long ticks = 0;
            var lengths = new List<int>(o.Matches * 40);
            var outcomes = new Dictionary<RallyOutcome, int>();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < o.Matches; i++)
            {
                var r = Play(o, o.Seed0 + (ulong)i);
                if (r.HomeWon) homeWins++;
                foreach (var rally in r.Rallies)
                {
                    rallies++;
                    ticks += rally.DurationTicks;
                    lengths.Add(rally.Contacts);
                    outcomes.TryGetValue(rally.Outcome, out int c);
                    outcomes[rally.Outcome] = c + 1;
                }
            }
            sw.Stop();

            lengths.Sort();
            double median = Median(lengths);
            var (lo, hi) = WilsonCi(homeWins, o.Matches);

            Console.WriteLine($"# batch  {o.HomeTier}(raw {o.HomeRaw}) vs {o.AwayTier}(raw {o.AwayRaw})  {o.Matches} matches ({o.Format})  [{sw.ElapsedMilliseconds} ms]");
            Console.WriteLine($"home win rate: {100.0 * homeWins / o.Matches:F1}%   Wilson95 [{100 * lo:F1}%, {100 * hi:F1}%]");
            Console.WriteLine($"rallies: {rallies}   mean contacts {Mean(lengths):F2}   median {median:F1}   feel-gate band [4, 9] {(median >= 4 && median <= 9 ? "PASS" : "MISS")}");
            Console.WriteLine($"mean rally sim-time: {ticks / (double)rallies / 60.0:F1} s");
            Console.WriteLine("rally-length histogram:");
            Histogram(lengths);
            Console.WriteLine("outcomes:");
            foreach (var kv in outcomes)
                Console.WriteLine($"  {kv.Key,-8} {kv.Value,6}  {100.0 * kv.Value / rallies:F1}%");
            return 0;
        }

        // ---- mirror (tooling-pipeline §2 suite c) ------------------------------------------------

        private static int Mirror(Options o)
        {
            o.AwayTier = o.HomeTier;
            o.AwayRaw = o.HomeRaw;
            int homeWins = 0;
            for (int i = 0; i < o.Matches; i++)
                if (Play(o, o.Seed0 + (ulong)i).HomeWon) homeWins++;

            double rate = (double)homeWins / o.Matches;
            var (lo, hi) = WilsonCi(homeWins, o.Matches);
            bool ciContainsHalf = lo <= 0.5 && 0.5 <= hi;
            bool biasOk = Math.Abs(rate - 0.5) <= 0.03;

            Console.WriteLine($"# mirror  {o.HomeTier} vs {o.HomeTier}, raw {o.HomeRaw}, {o.Matches} matches");
            Console.WriteLine($"home win rate: {100 * rate:F2}%   Wilson95 [{100 * lo:F2}%, {100 * hi:F2}%]");
            Console.WriteLine($"CI contains 50%: {(ciContainsHalf ? "yes" : "NO")}   |bias| <= 3pp: {(biasOk ? "yes" : "NO")}");
            Console.WriteLine(ciContainsHalf && biasOk ? "MIRROR SUITE: PASS" : "MIRROR SUITE: FAIL — structural side/serve bias");
            return ciContainsHalf && biasOk ? 0 : 1;
        }

        // ---- transcript (the terminal demo) ---------------------------------------------------------

        private static int Transcript(Options o)
        {
            var r = Play(o, o.Seed0);
            Console.WriteLine($"# {o.HomeTier} (Home) vs {o.AwayTier} (Away) — first to {(int)o.Format}, seed {o.Seed0}");
            Console.WriteLine();
            int h = 0, a = 0;
            for (int i = 0; i < r.Rallies.Count; i++)
            {
                var rally = r.Rallies[i];
                if (rally.WonByHome) h++; else a++;
                string serve = rally.ServedByHome ? "H serve" : "A serve";
                string grades = string.Join("", rally.Grades.ConvertAll(GradeChar));
                Console.WriteLine(
                    $"R{i + 1,2}  {serve}  {rally.Contacts,2} contacts  [{grades}]  {rally.Outcome,-7} → {(rally.WonByHome ? "HOME" : "AWAY")}   {h,2}–{a,-2}  ({rally.DurationTicks / 60.0:F1}s)");
            }
            Console.WriteLine();
            Console.WriteLine($"FINAL: {r.HomeScore}–{r.AwayScore}  {(r.HomeWon ? "HOME" : "AWAY")} wins  ({r.Rallies.Count} rallies)");
            return 0;
        }

        private static string GradeChar(TimingGrade g)
        {
            switch (g)
            {
                case TimingGrade.Perfect: return "P";
                case TimingGrade.Great: return "G";
                case TimingGrade.Good: return "g";
                default: return "·";
            }
        }

        // ---- stats helpers -----------------------------------------------------------------------------

        private static double Mean(List<int> xs)
        {
            long sum = 0;
            foreach (int x in xs) sum += x;
            return xs.Count == 0 ? 0 : (double)sum / xs.Count;
        }

        private static double Median(List<int> sorted)
        {
            if (sorted.Count == 0) return 0;
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : 0.5 * (sorted[mid - 1] + sorted[mid]);
        }

        private static (double lo, double hi) WilsonCi(int successes, int n)
        {
            if (n == 0) return (0, 1);
            const double z = 1.959963985; // 95%
            double p = (double)successes / n;
            double z2 = z * z;
            double denom = 1 + z2 / n;
            double center = (p + z2 / (2 * n)) / denom;
            double half = z * Math.Sqrt(p * (1 - p) / n + z2 / (4.0 * n * n)) / denom;
            return (center - half, center + half);
        }

        private static void Histogram(List<int> sorted)
        {
            if (sorted.Count == 0) return;
            var counts = new SortedDictionary<int, int>();
            foreach (int x in sorted)
            {
                counts.TryGetValue(x, out int c);
                counts[x] = c + 1;
            }
            int max = 0;
            foreach (var kv in counts) max = Math.Max(max, kv.Value);
            foreach (var kv in counts)
            {
                int bar = (int)Math.Round(46.0 * kv.Value / max);
                Console.WriteLine($"  {kv.Key,3} | {new string('#', bar)} {kv.Value} ({100.0 * kv.Value / sorted.Count:F1}%)");
            }
        }
    }
}
