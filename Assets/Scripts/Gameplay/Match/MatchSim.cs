using System;
using VG.Data;
using VG.Gameplay.Ai;
using VG.Gameplay.Ball;
using VG.Gameplay.Hype;
using VG.Gameplay.Rally;
using VG.Gameplay.Resolution;
using VG.Gameplay.Rng;

namespace VG.Gameplay.Match
{
    /// <summary>
    /// The composition layer: drives one full headless match by wiring every tested module
    /// together — RallyStateMachine (§1) for legality, GradeSampler/UtilityScorer (§6) for AI
    /// execution and decisions, QualityMath/Cascade/PointResolution (§3) for contact math,
    /// BallTunables/Trajectory (§2) for serve/spike arcs and net rulings, HypeMeters (§3.7).
    ///
    /// Deterministic: all randomness on the injected RngSet's Ai stream (decisions, grade
    /// samples) and Rally stream (float-serve wobble, net-cord side, coin flip) — same
    /// MatchConfig ⇒ bit-identical MatchResult.
    ///
    /// [v0 simplifications, all documented at the site]:
    /// - AI-vs-AI only (player input path arrives with VB-13); no signature spends (Hype
    ///   accrues and Ignition latches; stubs wire in at VB-12's player demo).
    /// - §3.5 set→spike-window ctx does not affect AI (AI samples grades, has no windows —
    ///   symmetric across both sides, so mirror-match validity is unaffected; M1 revisits
    ///   grade-shift-by-set-grade).
    /// - Court-position role mapping and block-commit heuristics are simple deterministic
    ///   tables marked [tunable v0].
    /// - Rally duration = authored arc ticks + fixed presentation ticks (machine timers are
    ///   driven by explicit triggers headlessly, not per-tick).
    /// </summary>
    public sealed class MatchSim
    {
        private const int MaxContactsPerRally = 200; // runaway-rally guard [structural test invariant]

        private readonly MatchConfig _config;
        private readonly RngSet _rng;
        private readonly ResolutionTunables _res = new ResolutionTunables();
        private readonly CascadeTunables _cascade = new CascadeTunables();
        private readonly PointResolutionTunables _point;
        private readonly BallTunables _ball = new BallTunables();
        private readonly HypeTunables _hypeT = new HypeTunables();
        private readonly AiTunables _ai = new AiTunables();
        private readonly RallyTunables _rally = new RallyTunables();

        private readonly HypeMeters _hype;
        private readonly int[] _rotation = new int[2];      // rotation offset per side
        private readonly int[] _setOptionUses = new int[8]; // surprise tracking: 4 options × 2 sides
        private int _decisionsPerSide;                       // coarse action counter for x_surprise

        public MatchSim(MatchConfig config, PointResolutionTunables pointTunables = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _point = pointTunables ?? new PointResolutionTunables();
            _rng = RngSet.FromMaster(config.MasterSeed);
            _hype = new HypeMeters(_hypeT);
        }

        private TeamSpec Team(TeamSide side) => side == TeamSide.Home ? _config.Home : _config.Away;
        private static TeamSide Other(TeamSide s) => s == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        private IRng AiRng => _rng.Get(RngStream.Ai);
        private IRng RallyRng => _rng.Get(RngStream.Rally);

        /// <summary>Court position 1..6 → player, honoring the side's current rotation.</summary>
        private PlayerSpec AtPosition(TeamSide side, int courtPosition)
        {
            int idx = (courtPosition - 1 + _rotation[(int)side]) % 6;
            return Team(side).Players[idx];
        }

        private float StatC(in PlayerSpec p, StatId a, StatId b)
            => 0.5f * (p.Stats.Normalized(a) + p.Stats.Normalized(b));

        public MatchResult Run()
        {
            var result = new MatchResult { Format = _config.Format, Seeds = _rng.Seeds };
            var machine = new RallyStateMachine(_rally);

            machine.Fire(RallyTrigger.LineupsValid); // T1; lineup legality is trivially true in M0

            // §1.1 MatchStart: coin flip for first serve ⚄ (Rally stream).
            TeamSide serving = RallyRng.NextFloat01() < 0.5f ? TeamSide.Home : TeamSide.Away;

            int target = (int)_config.Format;
            while (true)
            {
                var log = PlayRally(machine, serving);
                result.Rallies.Add(log);
                if (log.WonByHome) result.HomeScore++; else result.AwayScore++;

                machine.Fire(RallyTrigger.ScoreApplied); // T16 → Rotation

                bool over = (result.HomeScore >= target || result.AwayScore >= target)
                         && Math.Abs(result.HomeScore - result.AwayScore) >= 2; // win by 2 [structural §1.1]
                if (over)
                {
                    machine.Fire(RallyTrigger.MatchPointReached); // T18
                    result.HomeWon = result.HomeScore > result.AwayScore;
                    return result;
                }

                // Standard sideout: the point winner serves next; a team rotates when it GAINS serve.
                TeamSide winner = log.WonByHome ? TeamSide.Home : TeamSide.Away;
                if (winner != serving)
                {
                    _rotation[(int)winner] = (_rotation[(int)winner] + 1) % 6;
                    serving = winner;
                }
                machine.Fire(RallyTrigger.RotationApplied); // T17 → PreServe
            }
        }

        // ---- one rally -----------------------------------------------------------------------

        private RallyLog PlayRally(RallyStateMachine machine, TeamSide serving)
        {
            var log = new RallyLog { ServedByHome = serving == TeamSide.Home };
            log.DurationTicks += _rally.PreServePresentationTicks + _rally.PointResolvedPresentationTicks;

            machine.Fire(RallyTrigger.PresentationDone); // T2 → ServeAim

            // ---- serve (§4.2 + §6.3 vocabulary) ----
            var server = AtPosition(serving, 1);
            var tier = Team(serving).Tier;
            bool jumpServe = tier == DifficultyTier.Hard; // §6.3 Hard row [tunable v0]
            ZoneId serveTarget = ChooseServeTarget(serving);

            machine.Fire(RallyTrigger.ServeReleased);     // T3 → ServeContact
            TimingGrade serveGrade = SampleGrade(serving);
            float serveStatC = StatC(server, StatId.Serve, StatId.Power); // §3.1 governing stats
            float qServe = QualityMath.Quality(_res, serveGrade, serveStatC);
            if (jumpServe) qServe = Math.Min(1f, qServe + 0.15f); // §4.2 [tunable]
            RecordContact(log, serving, serveGrade, qServe);

            var serveRuling = AuthorAndRuleArc(isSpike: false, jumpServe, serving, serveTarget, qServe, log);
            machine.Fire(RallyTrigger.TrajectoryAuthored); // T4 → BallInFlight

            if (serveRuling == NetRuling.NetFault)
                return Terminal(machine, log, RallyOutcome.Net, winner: Other(serving), errorBy: serving, fromFlight: true);
            if (serveRuling == NetRuling.AntennaOut)
                return Terminal(machine, log, RallyOutcome.Out, winner: Other(serving), errorBy: serving, fromFlight: true);
            if (serveRuling == NetRuling.NetCord && RallyRng.NextFloat01() < 0.5f) // §2.4 drama RNG ⚄
                return Terminal(machine, log, RallyOutcome.Net, winner: Other(serving), errorBy: serving, fromFlight: true);
            // Cord falling on the receiving side plays on as a normal (ugly) serve.

            machine.Fire(RallyTrigger.BallCrossedNet); // T5 → ReceiveWindow

            // ---- serve receive: §3.6 tail — pipeline with B = 0, receive as the dig ----
            TeamSide receiving = Other(serving);
            var receiver = ReceiverFor(receiving, serveTarget);
            TimingGrade recGrade = SampleGrade(receiving);
            float qReceive = QualityMath.Quality(_res, recGrade, StatC(receiver, StatId.Receive, StatId.Speed));
            RecordContact(log, receiving, recGrade, qReceive);

            var serveRes = PointResolution.ResolveServe(_point, _res, qServe, serveGrade, serveTarget, qReceive);
            if (serveRes.Outcome == AttackOutcome.Kill) // ace
            {
                machine.Fire(RallyTrigger.ReceiveFailed); // T7
                _hype.Apply(HypeEvent.Ace, serving);
                log.Outcome = RallyOutcome.Kill;
                log.WonByHome = serving == TeamSide.Home;
                return log;
            }
            if (serveRes.Outcome == AttackOutcome.Net || serveRes.Outcome == AttackOutcome.Out)
                return Terminal(machine, log, serveRes.Outcome == AttackOutcome.Net ? RallyOutcome.Net : RallyOutcome.Out,
                    winner: receiving, errorBy: serving, fromFlight: false, viaReceiveWindow: machine);

            // Received: §3.3 unplayable-Shank check, then the cascade loop.
            var recDisplay = QualityMath.ReceiveGradeOf(_res, qReceive);
            if (recDisplay == ReceiveGrade.Shank && qReceive < _res.ShankPlayableMin)
            {
                machine.Fire(RallyTrigger.ReceiveFailed); // T7 unplayable
                _hype.Apply(HypeEvent.Ace, serving);
                log.Outcome = RallyOutcome.Kill;
                log.WonByHome = serving == TeamSide.Home;
                return log;
            }
            machine.Fire(RallyTrigger.ReceiveSucceeded); // T6 → SetSelect

            return CascadeLoop(machine, log, possession: receiving, recDisplay);
        }

        /// <summary>SetSelect → attack → block/dig, repeating on digs and free balls until terminal.</summary>
        private RallyLog CascadeLoop(RallyStateMachine machine, RallyLog log, TeamSide possession, ReceiveGrade receiveGrade)
        {
            while (true)
            {
                if (log.Contacts > MaxContactsPerRally)
                    throw new InvalidOperationException("Runaway rally — resolution math cannot terminate.");

                // ---- set (§3.4 lit ∧ §6.3 vocabulary, utility pick) ----
                SetOption option = ChooseSetOption(possession, receiveGrade, log.Contacts);
                var setter = AtPosition(possession, 2); // [tunable v0: setter plays right-front]
                TimingGrade setGrade = SampleGrade(possession);
                if (Cascade.CapsSetGradeAtGood(receiveGrade) && setGrade > TimingGrade.Good)
                    setGrade = TimingGrade.Good; // §3.4 Shank row cap
                float qSet = QualityMath.Quality(_res, setGrade, setter.Stats.Normalized(StatId.Technique));
                RecordContact(log, possession, setGrade, qSet);
                machine.Fire(RallyTrigger.LaneChosen); // T8 → BallInFlight
                log.DurationTicks += SetArcTicks(option);

                // §3.5: a Miss set is a free ball over — possession flips, cascade repeats.
                if (!Cascade.TryGetSpikeWindowCtx(_cascade, setGrade, out _))
                {
                    machine.Fire(RallyTrigger.BallCrossedNet); // free-ball rule §1.2
                    var freshGrade = FreeBallReceive(machine, log, receivingSide: Other(possession), out bool unplayable);
                    if (unplayable)
                    {
                        _hype.Apply(HypeEvent.Kill, possession);
                        log.Outcome = RallyOutcome.Kill;
                        log.WonByHome = possession == TeamSide.Home;
                        return log;
                    }
                    possession = Other(possession);
                    receiveGrade = freshGrade;
                    continue;
                }

                machine.Fire(RallyTrigger.SetArcAscending); // T9 → AttackApproach

                // ---- block commit (§6.3 behavior, during AttackApproach) ----
                TeamSide defending = Other(possession);
                var block = ChooseBlock(machine, defending, option);

                // ---- spike (§4.3 aim, §3.6 resolution) ----
                var attacker = AttackerFor(possession, option);
                machine.Fire(RallyTrigger.AttackTapped);   // T10 → AttackContact
                TimingGrade spikeGrade = SampleGrade(possession);
                float qSpike = QualityMath.Quality(_res, spikeGrade, StatC(attacker, StatId.Power, StatId.Jump));
                RecordContact(log, possession, spikeGrade, qSpike);

                ZoneId aim = ChooseSpikeAim(possession, option, log.Contacts);
                var ruling = AuthorAndRuleArc(isSpike: true, jump: false, possession, aim, qSpike, log);
                machine.Fire(RallyTrigger.TrajectoryAuthored); // T12 → BallInFlight (block resolves with it)

                if (spikeGrade != TimingGrade.Miss) // a Miss never reaches the net-ruling (free ball §3.6 step 1)
                {
                    if (ruling == NetRuling.NetFault)
                        return Terminal(machine, log, RallyOutcome.Net, winner: defending, errorBy: possession, fromFlight: true);
                    if (ruling == NetRuling.AntennaOut)
                        return Terminal(machine, log, RallyOutcome.Out, winner: defending, errorBy: possession, fromFlight: true);
                    if (ruling == NetRuling.NetCord && RallyRng.NextFloat01() < 0.5f) // ⚄
                        return Terminal(machine, log, RallyOutcome.Net, winner: defending, errorBy: possession, fromFlight: true);
                }

                var digger = ReceiverFor(defending, aim);
                TimingGrade digGrade = SampleGrade(defending);
                float qDigRaw = QualityMath.Quality(_res, digGrade, StatC(digger, StatId.Receive, StatId.Speed));
                var resolution = PointResolution.ResolveAttack(_point, _res, qSpike, spikeGrade, aim, block, qDigRaw);

                switch (resolution.Outcome)
                {
                    case AttackOutcome.FreeBall: // §3.6 step 1 — over it goes
                        machine.Fire(RallyTrigger.BallCrossedNet);
                        var fbGrade = FreeBallReceive(machine, log, defending, out bool fbUnplayable);
                        if (fbUnplayable)
                        {
                            _hype.Apply(HypeEvent.Kill, possession);
                            log.Outcome = RallyOutcome.Kill;
                            log.WonByHome = possession == TeamSide.Home;
                            return log;
                        }
                        possession = defending;
                        receiveGrade = fbGrade;
                        continue;

                    case AttackOutcome.Net:
                        return Terminal(machine, log, RallyOutcome.Net, winner: defending, errorBy: possession, fromFlight: true);
                    case AttackOutcome.Out:
                        return Terminal(machine, log, RallyOutcome.Out, winner: defending, errorBy: possession, fromFlight: true);

                    case AttackOutcome.Blocked:
                        machine.Fire(RallyTrigger.TerminalOutcome); // T15
                        _hype.Apply(HypeEvent.StuffBlock, defending);
                        log.Outcome = RallyOutcome.Blocked;
                        log.WonByHome = defending == TeamSide.Home;
                        return log;

                    case AttackOutcome.Tooled:
                        machine.Fire(RallyTrigger.TerminalOutcome); // T15
                        _hype.Apply(HypeEvent.Kill, possession);
                        log.Outcome = RallyOutcome.Tooled;
                        log.WonByHome = possession == TeamSide.Home;
                        return log;

                    case AttackOutcome.Dug:
                        machine.Fire(RallyTrigger.SpikePlayable);  // T13 → DigWindow
                        RecordContact(log, defending, digGrade, qDigRaw);
                        if (resolution.EffectiveAttack >= _hypeT.BigSpikeAThreshold)
                            _hype.Apply(HypeEvent.BigSpikeDug, defending); // §3.7 [v0: judged on A_eff]
                        if (resolution.BlockTouched)
                            log.DurationTicks += DeflectionTicks(resolution.EffectiveAttack);
                        machine.Fire(RallyTrigger.DigSucceeded);   // T14 → SetSelect, cascade repeats
                        possession = defending;
                        receiveGrade = resolution.DigDisplayGrade;
                        continue;

                    case AttackOutcome.Kill:
                    default:
                        machine.Fire(RallyTrigger.SpikePlayable);  // the dig was attempted…
                        RecordContact(log, defending, digGrade, qDigRaw);
                        machine.Fire(RallyTrigger.TerminalOutcome); // …and failed (T15 from DigWindow)
                        _hype.Apply(HypeEvent.Kill, possession);
                        log.Outcome = RallyOutcome.Kill;
                        log.WonByHome = possession == TeamSide.Home;
                        return log;
                }
            }
        }

        // ---- AI decision helpers -----------------------------------------------------------------

        private TimingGrade SampleGrade(TeamSide side)
        {
            var team = Team(side);
            var grade = team.GradeOverride.HasValue
                ? GradeSampler.Sample(team.GradeOverride.Value, AiRng) // skill proxy (tooling §2) ⚄
                : GradeSampler.Sample(_ai, team.Tier, AiRng);          // §6.2 ⚄
            if (grade == TimingGrade.Perfect) _hype.Apply(HypeEvent.PerfectContact, side); // §3.7
            return grade;
        }

        private void RecordContact(RallyLog log, TeamSide side, TimingGrade grade, float quality)
        {
            log.Contacts++;
            log.Grades.Add(grade);
            log.Qualities.Add(quality);
            if (log.Contacts > _hypeT.LongRallyThreshold) // §3.7: per extra contact, both teams
            {
                _hype.Apply(HypeEvent.LongRallyContact, TeamSide.Home);
                _hype.Apply(HypeEvent.LongRallyContact, TeamSide.Away);
            }
            _decisionsPerSide++;
        }

        private ZoneId ChooseServeTarget(TeamSide serving)
        {
            var tier = Team(serving).Tier;
            if (tier == DifficultyTier.Easy) return ZoneId.z_CM; // §6.3 Easy: center float only

            if (tier == DifficultyTier.Hard)
            {
                // §6.3 Hard: targets weakest receiver (min Receive stat among back row) [v0 mapping].
                TeamSide recv = Other(serving);
                ZoneId best = ZoneId.z_CB;
                int weakest = int.MaxValue;
                Span<int> positions = stackalloc int[] { 1, 6, 5 }; // right/middle/left back
                Span<ZoneId> zones = stackalloc ZoneId[] { ZoneId.z_LB, ZoneId.z_CB, ZoneId.z_RB };
                for (int i = 0; i < 3; i++)
                {
                    int r = AtPosition(recv, positions[i]).Stats.Raw(StatId.Receive);
                    if (r < weakest) { weakest = r; best = zones[i]; }
                }
                return best;
            }

            // Normal: float serves incl. line/corner targets — utility over candidates (§6.1).
            Span<ZoneId> candidates = stackalloc ZoneId[] { ZoneId.z_CM, ZoneId.z_LB, ZoneId.z_RB, ZoneId.z_CB };
            var inputs = new UtilityInputs[4];
            for (int i = 0; i < 4; i++)
                inputs[i] = new UtilityInputs
                {
                    Matchup = _point.RiskOf(candidates[i]), // line serves reward through risk(z) §4.2
                    Lit = 1f,
                    Hype = _hype.Hype(serving) / 100f,
                };
            int pick = UtilityScorer.PickArgmax(_ai, Team(serving).Tier, inputs, AiRng);
            return candidates[pick];
        }

        private SetOption ChooseSetOption(TeamSide side, ReceiveGrade receive, int rallyContacts)
        {
            var tier = Team(side).Tier;
            Span<SetOption> all = stackalloc SetOption[]
                { SetOption.QuickMiddle, SetOption.HighOutside, SetOption.BackRowPipe, SetOption.Dump };

            var candidates = new SetOption[4];
            var inputs = new UtilityInputs[4];
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                if (!TacticVocabulary.MayChooseSetOption(tier, receive, all[i])) continue; // §6.3 ∧ §3.4
                int useIdx = (int)all[i] + (side == TeamSide.Home ? 0 : 4);
                candidates[n] = all[i];
                inputs[n] = new UtilityInputs
                {
                    Matchup = 0.5f, // uniform teams v0; real lineups feed attacker-vs-blocker statc here
                    Lit = Cascade.CapsSetGradeAtGood(receive) ? 0.4f : 1f, // §6.1 x_lit cap penalty [tunable]
                    Rally = Math.Min(rallyContacts / 10f, 1f),
                    Hype = _hype.Hype(side) / 100f,
                    Surprise = _decisionsPerSide == 0 ? 1f : 1f - (float)_setOptionUses[useIdx] / _decisionsPerSide,
                };
                n++;
            }
            int pick = UtilityScorer.PickArgmax(_ai, tier, new ReadOnlySpan<UtilityInputs>(inputs, 0, n), AiRng);
            _setOptionUses[(int)candidates[pick] + (side == TeamSide.Home ? 0 : 4)]++;
            return candidates[pick];
        }

        private PlayerSpec AttackerFor(TeamSide side, SetOption option)
        {
            switch (option) // [tunable v0 role mapping]
            {
                case SetOption.QuickMiddle: return AtPosition(side, 3);  // middle-front
                case SetOption.HighOutside: return AtPosition(side, 4);  // left-front
                case SetOption.BackRowPipe: return AtPosition(side, 6);  // middle-back
                default: return AtPosition(side, 2);                     // dump: the setter
            }
        }

        private PlayerSpec ReceiverFor(TeamSide side, ZoneId landing)
        {
            switch (PointResolution.ZoneColumn(landing)) // [tunable v0: back row by column]
            {
                case 0: return AtPosition(side, 1);
                case 1: return AtPosition(side, 6);
                default: return AtPosition(side, 5);
            }
        }

        private BlockState ChooseBlock(RallyStateMachine machine, TeamSide defending, SetOption option)
        {
            int lane = option == SetOption.HighOutside ? 0 : 1; // attacker-view lane column [tunable v0]
            var tier = Team(defending).Tier;
            var blocker = AtPosition(defending, 3); // middle blocker fronts every lane [tunable v0]
            TimingGrade g = SampleGrade(defending);
            float qb = QualityMath.Quality(_res, g, StatC(blocker, StatId.Jump, StatId.Technique));

            switch (TacticVocabulary.BlockBehaviorFor(tier)) // §6.3
            {
                case BlockBehavior.ReadOnlyCenter:
                    machine.Fire(RallyTrigger.DefenseCommitted);
                    return new BlockState(BlockCommit.Read, 1, qb);

                case BlockBehavior.ReadWithOccasionalCommit:
                    machine.Fire(RallyTrigger.DefenseCommitted);
                    if (AiRng.NextFloat01() < 0.2f) // ⚄ occasional commit [tunable v0]
                        return new BlockState(BlockCommit.Early, lane, qb);
                    return new BlockState(BlockCommit.Read, lane, qb);

                default: // FullMixAdjacentSoftCommit
                    machine.Fire(RallyTrigger.DefenseCommitted);
                    float roll = AiRng.NextFloat01(); // ⚄
                    if (roll < 0.4f) return new BlockState(BlockCommit.Early, lane, qb);
                    if (roll < 0.55f) return new BlockState(BlockCommit.Early, Math.Min(2, lane + 1), qb); // soft adjacent
                    return new BlockState(BlockCommit.Read, lane, qb);
            }
        }

        private ZoneId ChooseSpikeAim(TeamSide side, SetOption option, int rallyContacts)
        {
            int lane = option == SetOption.HighOutside ? 0 : 1;
            Span<ZoneId> shots = stackalloc ZoneId[] // §4.3 shot table [structural]
            {
                (ZoneId)(6 + lane),                        // line: same-column back edge (z_XB)
                lane == 0 ? ZoneId.z_RB : ZoneId.z_LB,     // cross: opposite corner back
                ZoneId.z_CM,                               // roll: safe center
                (ZoneId)lane,                              // feint: front zone at the lane
            };
            var inputs = new UtilityInputs[4];
            for (int i = 0; i < 4; i++)
                inputs[i] = new UtilityInputs
                {
                    Matchup = _point.RiskOf(shots[i]),
                    Lit = 1f,
                    Rally = Math.Min(rallyContacts / 10f, 1f),
                    Hype = _hype.Hype(side) / 100f,
                };
            int pick = UtilityScorer.PickArgmax(_ai, Team(side).Tier, inputs, AiRng);
            return shots[pick];
        }

        // ---- arcs, rulings, bookkeeping ------------------------------------------------------------

        /// <summary>Author the serve/spike arc (§2.3), apply §4.1 quality scatter, return the §2.4 ruling.</summary>
        private NetRuling AuthorAndRuleArc(bool isSpike, bool jump, TeamSide from, ZoneId target, float quality, RallyLog log)
        {
            var side = from == TeamSide.Home ? CourtSide.NegativeZ : CourtSide.PositiveZ;
            var targetSide = from == TeamSide.Home ? CourtSide.PositiveZ : CourtSide.NegativeZ;
            float zSign = from == TeamSide.Home ? -1f : 1f;

            Vec3 end = CourtGeometry.CenterOf(target, targetSide);
            // §4.1: bad contacts drift toward court center, never out — offset = (1 − q) × 1.5 m [tunable].
            float drift = (1f - quality) * 1.5f;
            end = new Vec3(end.X + (4.5f - end.X) * Math.Min(1f, drift / 4.5f), 0f,
                           end.Z + (targetSide == CourtSide.PositiveZ ? 4.5f - end.Z : -4.5f - end.Z) * Math.Min(1f, drift / 4.5f));

            TrajectoryParams p;
            if (isSpike)
            {
                var start = new Vec3(4.5f, _ball.SpikeContactHeight(quality), zSign * 2.5f); // [v0 launch point]
                p = _ball.Spike(start, end, quality);
            }
            else if (jump)
            {
                p = _ball.ServeJump(new Vec3(6.5f, 2.1f, zSign * 9.2f), end);
            }
            else
            {
                p = _ball.ServeFloat(new Vec3(6.5f, 2.0f, zSign * 9.2f), end,
                    wobblePhase: RallyRng.NextFloat01() * 6.2831853f); // §2.3 ⚄
            }

            var arc = new Trajectory(p);
            log.DurationTicks += arc.DurationTicks;
            return arc.RuleNetInteraction(_ball);
        }

        /// <summary>§1.2 free-ball rule: easy arc over, +1 display grade to the receiver.</summary>
        private ReceiveGrade FreeBallReceive(RallyStateMachine machine, RallyLog log, TeamSide receivingSide, out bool unplayable)
        {
            log.DurationTicks += CourtGeometry.TicksFromSeconds(_ball.FreeBallDurationSeconds);
            var receiver = AtPosition(receivingSide, 6); // free balls target mid-court (§2.3)
            TimingGrade g = SampleGrade(receivingSide);
            float q = QualityMath.Quality(_res, g, StatC(receiver, StatId.Receive, StatId.Speed));
            RecordContact(log, receivingSide, g, q);

            var display = QualityMath.ReceiveGradeOf(_res, q);
            unplayable = display == ReceiveGrade.Shank && q < _res.ShankPlayableMin;
            if (unplayable)
            {
                machine.Fire(RallyTrigger.ReceiveFailed); // T7
                return display;
            }
            if (display < ReceiveGrade.S) display = (ReceiveGrade)((int)display + 1); // +1 display-grade bonus [structural §1.2]
            machine.Fire(RallyTrigger.ReceiveSucceeded);
            return display;
        }

        /// <summary>Terminal outcome bookkeeping: hype, machine path, log.</summary>
        private RallyLog Terminal(
            RallyStateMachine machine, RallyLog log, RallyOutcome outcome,
            TeamSide winner, TeamSide errorBy, bool fromFlight, RallyStateMachine viaReceiveWindow = null)
        {
            if (viaReceiveWindow != null)
                machine.Fire(RallyTrigger.ReceiveFailed); // already in ReceiveWindow (serve error detected post-receive)
            else if (fromFlight)
                machine.Fire(RallyTrigger.TerminalOutcome); // T15 from BallInFlight

            if (outcome == RallyOutcome.Net || outcome == RallyOutcome.Out)
                _hype.Apply(HypeEvent.OwnError, errorBy); // §3.7
            log.Outcome = outcome;
            log.WonByHome = winner == TeamSide.Home;
            return log;
        }

        private int SetArcTicks(SetOption option)
            => CourtGeometry.TicksFromSeconds(option == SetOption.QuickMiddle ? _ball.SetQuickDurationSeconds : _ball.SetHighDurationSeconds);

        private int DeflectionTicks(float remainingEnergy)
            => CourtGeometry.TicksFromSeconds(
                _ball.BlockDeflectDurationMaxSeconds
                + (_ball.BlockDeflectDurationMinSeconds - _ball.BlockDeflectDurationMaxSeconds) * Math.Min(1f, remainingEnergy));
    }
}
