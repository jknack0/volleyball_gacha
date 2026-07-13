using System;
using System.Collections.Generic;
using System.Text;
using VG.Data;
using VG.Gameplay.Ai;
using VG.Gameplay.Match;
using VG.Gameplay.Resolution;

namespace VG.SimRunner
{
    /// <summary>
    /// VB-11 headless runner (docs/tooling-pipeline.md §2). Modes:
    ///   batch      --home-tier X --away-tier Y [--matches N] [--seed0 S] [--home-raw R] [--away-raw R] [--format 11|15|25] [--json path]
    ///   mirror     [--matches N] [--tier X] [--json path]        — suite (c): CI must contain 0.50, |bias| ≤ 3pp
    ///   transcript [--seed S] [--tier X]                          — one match, rally-by-rally demo
    ///   sweep      [--matches N]                                  — ServeReceiveRequirementFactor grid vs ace rate / gate band
    ///   calibrate  [--matches N] [--home-raw R]                   — suite (a): Median proxy vs Easy/Normal/Hard, bands 80–92 / 55–72 / 32–48%
    ///   economy    [--matches N]                                  — suite (b) slice: Skilled proxy at PI−0.08 must keep ≥30% vs Normal
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
                case "sweep": return Sweep(opt);
                case "calibrate": return Calibrate(opt);
                case "economy": return Economy(opt);
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
            public int Size = 6;
            public string JsonPath;
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
                    case "--size": o.Size = int.Parse(v); break;
                    case "--json": o.JsonPath = v; break;
                }
            }
            return o;
        }

        private static MatchResult Play(Options o, ulong seed,
            PointResolutionTunables point = null,
            GradeDistribution? homeProxy = null, GradeDistribution? awayProxy = null)
            => new MatchSim(new MatchConfig(
                TeamSpec.Uniform("H", o.HomeRaw, o.HomeTier, homeProxy, o.Size),
                TeamSpec.Uniform("A", o.AwayRaw, o.AwayTier, awayProxy, o.Size),
                o.Format, seed), point).Run();

        // ---- aggregation --------------------------------------------------------------------

        private sealed class Agg
        {
            public int Matches, HomeWins, Rallies, TwoContact;
            public long Ticks;
            public readonly List<int> Lengths = new List<int>();
            public readonly Dictionary<RallyOutcome, int> Outcomes = new Dictionary<RallyOutcome, int>();

            public void Add(MatchResult r)
            {
                Matches++;
                if (r.HomeWon) HomeWins++;
                foreach (var rally in r.Rallies)
                {
                    Rallies++;
                    Ticks += rally.DurationTicks;
                    Lengths.Add(rally.Contacts);
                    if (rally.Contacts <= 2) TwoContact++;
                    Outcomes.TryGetValue(rally.Outcome, out int c);
                    Outcomes[rally.Outcome] = c + 1;
                }
            }

            public double WinRate => Matches == 0 ? 0 : (double)HomeWins / Matches;
            public double TwoContactShare => Rallies == 0 ? 0 : (double)TwoContact / Rallies;
            public double MedianLength { get { Lengths.Sort(); return Median(Lengths); } }
        }

        private static Agg RunAgg(Options o, PointResolutionTunables point = null,
            GradeDistribution? homeProxy = null, GradeDistribution? awayProxy = null)
        {
            var agg = new Agg();
            for (int i = 0; i < o.Matches; i++)
                agg.Add(Play(o, o.Seed0 + (ulong)i, point, homeProxy, awayProxy));
            return agg;
        }

        // ---- batch --------------------------------------------------------------------------

        private static int Batch(Options o)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var agg = RunAgg(o);
            sw.Stop();

            double median = agg.MedianLength;
            var (lo, hi) = WilsonCi(agg.HomeWins, agg.Matches);

            Console.WriteLine($"# batch  {o.HomeTier}(raw {o.HomeRaw}) vs {o.AwayTier}(raw {o.AwayRaw})  {o.Matches} matches ({o.Format})  [{sw.ElapsedMilliseconds} ms]");
            Console.WriteLine($"home win rate: {100 * agg.WinRate:F1}%   Wilson95 [{100 * lo:F1}%, {100 * hi:F1}%]");
            Console.WriteLine($"rallies: {agg.Rallies}   mean contacts {Mean(agg.Lengths):F2}   median {median:F1}   2-contact {100 * agg.TwoContactShare:F1}%   feel-gate band [4, 9] {(median >= 4 && median <= 9 ? "PASS" : "MISS")}");
            Console.WriteLine($"mean rally sim-time: {agg.Ticks / (double)agg.Rallies / 60.0:F1} s");
            Console.WriteLine("rally-length histogram:");
            Histogram(agg.Lengths);
            Console.WriteLine("outcomes:");
            foreach (var kv in agg.Outcomes)
                Console.WriteLine($"  {kv.Key,-8} {kv.Value,6}  {100.0 * kv.Value / agg.Rallies:F1}%");

            WriteJson(o.JsonPath, agg, o, "batch");
            return 0;
        }

        // ---- mirror (suite c) ----------------------------------------------------------------

        private static int Mirror(Options o)
        {
            o.AwayTier = o.HomeTier;
            o.AwayRaw = o.HomeRaw;
            var agg = RunAgg(o);

            var (lo, hi) = WilsonCi(agg.HomeWins, agg.Matches);
            bool ciContainsHalf = lo <= 0.5 && 0.5 <= hi;
            bool biasOk = Math.Abs(agg.WinRate - 0.5) <= 0.03;

            Console.WriteLine($"# mirror  {o.HomeTier} vs {o.HomeTier}, raw {o.HomeRaw}, {o.Matches} matches");
            Console.WriteLine($"home win rate: {100 * agg.WinRate:F2}%   Wilson95 [{100 * lo:F2}%, {100 * hi:F2}%]");
            Console.WriteLine($"CI contains 50%: {(ciContainsHalf ? "yes" : "NO")}   |bias| <= 3pp: {(biasOk ? "yes" : "NO")}");
            Console.WriteLine(ciContainsHalf && biasOk ? "MIRROR SUITE: PASS" : "MIRROR SUITE: FAIL — structural side/serve bias");

            WriteJson(o.JsonPath, agg, o, "mirror");
            return ciContainsHalf && biasOk ? 0 : 1;
        }

        // ---- sweep: the ace-rate knob -----------------------------------------------------------

        private static int Sweep(Options o)
        {
            Console.WriteLine($"# sweep ServeReceiveRequirementFactor  {o.HomeTier} mirror, {o.Matches} matches/point");
            Console.WriteLine("factor | 2-contact | median | mean | kills | tools | nets | blocks");
            for (float f = 1.00f; f >= 0.549f; f -= 0.05f)
            {
                var point = new PointResolutionTunables { ServeReceiveRequirementFactor = f };
                var agg = RunAgg(o, point);
                agg.Outcomes.TryGetValue(RallyOutcome.Kill, out int k);
                agg.Outcomes.TryGetValue(RallyOutcome.Tooled, out int t);
                agg.Outcomes.TryGetValue(RallyOutcome.Net, out int n);
                agg.Outcomes.TryGetValue(RallyOutcome.Blocked, out int b);
                Console.WriteLine(
                    $"{f,6:F2} | {100 * agg.TwoContactShare,8:F1}% | {agg.MedianLength,6:F1} | {Mean(agg.Lengths),4:F2} | {Pct(k, agg.Rallies),5} | {Pct(t, agg.Rallies),5} | {Pct(n, agg.Rallies),5} | {Pct(b, agg.Rallies),6}");
            }
            return 0;
        }

        // ---- calibrate (suite a) -------------------------------------------------------------------

        private static int Calibrate(Options o)
        {
            // Median proxy executes; decisions run at Normal vocabulary/weights [tunable pairing].
            Console.WriteLine($"# calibrate  Median proxy (Normal decisions, raw {o.HomeRaw}) vs each tier, {o.Matches} matches each");
            (DifficultyTier tier, double lo, double hi)[] bands =
            {
                (DifficultyTier.Easy, 0.80, 0.92),
                (DifficultyTier.Normal, 0.55, 0.72),
                (DifficultyTier.Hard, 0.32, 0.48),
            };
            bool allPass = true;
            double prev = double.MaxValue;
            foreach (var (tier, blo, bhi) in bands)
            {
                var local = new Options
                {
                    HomeTier = DifficultyTier.Normal, AwayTier = tier,
                    Matches = o.Matches, Seed0 = o.Seed0,
                    HomeRaw = o.HomeRaw, AwayRaw = o.AwayRaw, Format = o.Format,
                };
                var agg = RunAgg(local, homeProxy: SkillProxy.Median);
                var (lo, hi) = WilsonCi(agg.HomeWins, agg.Matches);
                bool inBand = agg.WinRate >= blo && agg.WinRate <= bhi;
                bool ordered = agg.WinRate < prev;
                prev = agg.WinRate;
                allPass &= inBand && ordered;
                Console.WriteLine(
                    $"vs {tier,-6}  win {100 * agg.WinRate,5:F1}%  CI [{100 * lo:F1}, {100 * hi:F1}]  band [{100 * blo:F0}, {100 * bhi:F0}]  {(inBand ? "PASS" : "MISS")}{(ordered ? "" : "  ORDERING-VIOLATION")}");
            }
            Console.WriteLine(allPass ? "CALIBRATION: PASS" : "CALIBRATION: MISS — tuning input (§6.2 distributions / proxy defs)");
            return allPass ? 0 : 1;
        }

        // ---- economy §8.2 slice (suite b) ---------------------------------------------------------------

        private static int Economy(Options o)
        {
            // Skilled proxy at PI − 0.08 (raw −16) vs Normal AI at listed PI: must keep ≥ 30% (no hard wall).
            var local = new Options
            {
                HomeTier = DifficultyTier.Normal, AwayTier = DifficultyTier.Normal,
                Matches = o.Matches, Seed0 = o.Seed0,
                HomeRaw = o.HomeRaw - 16, AwayRaw = o.AwayRaw, Format = o.Format,
            };
            var agg = RunAgg(local, homeProxy: SkillProxy.Skilled);
            var (lo, hi) = WilsonCi(agg.HomeWins, agg.Matches);
            bool pass = agg.WinRate >= 0.30;
            Console.WriteLine($"# economy  Skilled proxy raw {local.HomeRaw} (PI −0.08) vs Normal AI raw {o.AwayRaw}, {o.Matches} matches");
            Console.WriteLine($"win rate: {100 * agg.WinRate:F1}%  CI [{100 * lo:F1}, {100 * hi:F1}]  requirement ≥ 30%: {(pass ? "PASS" : "FAIL — hard wall (economy §8.2)")}");
            return pass ? 0 : 1;
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

        // ---- output helpers -----------------------------------------------------------------------------

        private static void WriteJson(string path, Agg agg, Options o, string mode)
        {
            if (string.IsNullOrEmpty(path)) return;
            var sb = new StringBuilder(1024);
            sb.Append('{');
            sb.Append($"\"mode\":\"{mode}\",\"homeTier\":\"{o.HomeTier}\",\"awayTier\":\"{o.AwayTier}\",");
            sb.Append($"\"homeRaw\":{o.HomeRaw},\"awayRaw\":{o.AwayRaw},\"format\":{(int)o.Format},\"matches\":{agg.Matches},\"seed0\":{o.Seed0},");
            sb.Append($"\"homeWins\":{agg.HomeWins},\"winRate\":{agg.WinRate:F4},\"rallies\":{agg.Rallies},");
            sb.Append($"\"medianContacts\":{agg.MedianLength:F1},\"meanContacts\":{Mean(agg.Lengths):F3},\"twoContactShare\":{agg.TwoContactShare:F4},");
            sb.Append("\"outcomes\":{");
            bool first = true;
            foreach (var kv in agg.Outcomes)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append($"\"{kv.Key}\":{kv.Value}");
            }
            sb.Append("}}");
            System.IO.File.WriteAllText(path, sb.ToString());
            Console.WriteLine($"json report → {path}");
        }

        private static string Pct(int n, int total) => total == 0 ? "-" : $"{100.0 * n / total:F1}%";

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
