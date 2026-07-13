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
    /// The composition layer as a TICK-DRIVEN engine (§2.5: RallySim.Tick advances exactly one
    /// fixed 1/60 s step). One Tick = one sim step; the ball flies its authored arcs in real
    /// ticks and every state transition happens on the tick the spec assigns it. The grey-box
    /// view (VB-12) renders <see cref="BallPosition"/>/<see cref="PlayerPosition"/>; VB-13's
    /// input layer replaces the AI decision ticks with player windows.
    ///
    /// Wires: RallyStateMachine (§1) · GradeSampler/UtilityScorer/TacticVocabulary (§6) ·
    /// QualityMath/Cascade/PointResolution (§3) · BallTunables/Trajectory (§2) · HypeMeters (§3.7).
    ///
    /// Determinism: all randomness on the injected RngSet (Ai stream: decisions + grade samples;
    /// Rally stream: coin flip, float-serve wobble, net-cord side). Same MatchConfig ⇒
    /// bit-identical MatchResult. Attack outcomes are PRECOMPUTED at author time (§3.6 is pure)
    /// and revealed at the physically-correct tick (block stuffs at the net, kills at the floor).
    ///
    /// [v0 simplifications, documented at the site]: AI-vs-AI (player input path = VB-13);
    /// no signature spends; §3.5 ctx bypass for AI (spec §6.2 note); slot-standing players
    /// (no movement sim in M0); AI decision ticks are fixed constants [tunable].
    /// </summary>
    public sealed class MatchSim
    {
        private const int MaxContactsPerRally = 200;  // runaway-rally guard [structural]
        private const int ServeAimDecisionTick = 30;  // AI "release" [tunable v0]
        private const int SetSelectDecisionTick = 20; // AI lane pick inside the 45-tick window [tunable v0]
        private const float BlockDecisionFraction = 0.5f; // commit mid-approach [tunable v0]

        private enum FlightPlan { None, Serve, SetToAttack, FreeBall, Spike }

        private readonly MatchConfig _config;
        private readonly RngSet _rng;
        private readonly ResolutionTunables _res = new ResolutionTunables();
        private readonly CascadeTunables _cascade = new CascadeTunables();
        private readonly PointResolutionTunables _point;
        private readonly BallTunables _ball = new BallTunables();
        private readonly HypeTunables _hypeT = new HypeTunables();
        private readonly AiTunables _ai = new AiTunables();
        private readonly RallyTunables _rallyT = new RallyTunables();

        private readonly HypeMeters _hype;
        private readonly RallyStateMachine _machine;
        private readonly MatchResult _result;

        private readonly int[] _rotation = new int[2];
        private readonly int _teamSize;
        private readonly int[] _setOptionUses = new int[8];
        private int _decisionsPerSide;

        // per-rally flow state
        private TeamSide _serving;
        private TeamSide _possession;
        private ReceiveGrade _receiveGrade;
        private RallyLog _log;
        private bool _logClosed;

        // flight state
        private FlightPlan _plan = FlightPlan.None;
        private Trajectory _arc;
        private int _arcTick;
        private int _crossingTick;
        private NetRuling _ruling;
        private Vec3 _ballRest; // ball position when not in flight

        // pending (precomputed) attack outcome
        private AttackResolution _pendingRes;
        private TimingGrade _pendingDigGrade;
        private float _pendingDigQuality;
        private SetOption _pendingOption;
        private BlockState _blockState;
        private bool _blockDecided;

        public MatchSim(MatchConfig config, PointResolutionTunables pointTunables = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _point = pointTunables ?? new PointResolutionTunables();
            _teamSize = config.TeamSize;
            _rng = RngSet.FromMaster(config.MasterSeed);
            _hype = new HypeMeters(_hypeT);
            _result = new MatchResult { Format = config.Format, Seeds = _rng.Seeds };
            _machine = new RallyStateMachine(_rallyT);
            _machine.OnEnter += OnStateEntered;

            // §1.1 MatchStart: coin flip ⚄ (Rally stream), then lineups validate trivially in M0.
            _serving = RallyRng.NextFloat01() < 0.5f ? TeamSide.Home : TeamSide.Away;
            _machine.Fire(RallyTrigger.LineupsValid); // T1 → PreServe (starts the first rally log)
        }

        // ---- public surface (consumed by VB-12 view / VB-13 input / SimRunner) --------------------

        public bool Done { get; private set; }
        public MatchResult Result => _result;
        public RallyState CurrentState => _machine.CurrentState;
        public int HomeScore => _result.HomeScore;
        public int AwayScore => _result.AwayScore;
        public TeamSide ServingSide => _serving;
        public HypeMeters Hype => _hype;

        /// <summary>(entered, previous) — presentation hook passthrough.</summary>
        public event Action<RallyState, RallyState> OnStateChanged;

        /// <summary>Fires when a rally's outcome is revealed, before PointResolved presentation ends.</summary>
        public event Action<RallyLog> OnRallyEnded;

        /// <summary>Ball world position this tick (CourtGeometry frame).</summary>
        public Vec3 BallPosition => _plan != FlightPlan.None ? _arc.PositionAt(_arcTick) : _ballRest;

        /// <summary>Capsule standing point for a team's court position 1..6 (M0: slot-static).</summary>
        public Vec3 PlayerPosition(TeamSide side, int courtPosition)
            => CourtSlots.Position(side, courtPosition, _teamSize);

        public string PlayerNameAt(TeamSide side, int courtPosition)
            => AtPosition(side, courtPosition).Name;

        /// <summary>Players per side (3 or 6) — the view builds this many capsules.</summary>
        public int PlayerCount => _teamSize;

        /// <summary>Play the whole match headlessly (SimRunner path): loop Tick until done.</summary>
        public MatchResult Run()
        {
            int guard = 0;
            while (!Done)
            {
                Tick();
                if (++guard > 2_000_000)
                    throw new InvalidOperationException("Match failed to terminate within 2M ticks.");
            }
            return _result;
        }

        /// <summary>Advance exactly one fixed sim step (1/60 s) [structural §2.5].</summary>
        public void Tick()
        {
            if (Done) return;

            // Match-over intercept: must preempt Rotation's auto-timer (T18 over T17).
            if (_machine.CurrentState == RallyState.Rotation && IsMatchOver())
            {
                _machine.Fire(RallyTrigger.MatchPointReached);
                return; // OnStateEntered(MatchEnd) set Done
            }

            _machine.Tick(); // drives PreServe/PointResolved/Rotation timers (§1.2 T2/T16/T17)
            if (Done) return;

            if (_log != null && !_logClosed) _log.DurationTicks++;

            switch (_machine.CurrentState)
            {
                case RallyState.ServeAim:
                    if (_machine.TicksInState >= ServeAimDecisionTick) DoServeContact();
                    break;

                case RallyState.SetSelect:
                    if (_machine.TicksInState >= SetSelectDecisionTick) DoSetChoice();
                    break;

                case RallyState.BallInFlight:
                    if (_plan == FlightPlan.SetToAttack)
                        _machine.Fire(RallyTrigger.SetArcAscending); // T9 — approach runs the set arc
                    else
                        AdvanceFlight();
                    break;

                case RallyState.ReceiveWindow:
                case RallyState.DigWindow:
                    AdvanceFlight(); // window is open while the ball completes its arc (T5 note)
                    break;

                case RallyState.AttackApproach:
                    AdvanceApproach();
                    break;

                // PreServe / PointResolved / Rotation / MatchEnd: machine timers / intercepts.
            }
        }

        // ---- state-entry bookkeeping ----------------------------------------------------------------

        private void OnStateEntered(RallyState entered, RallyState previous)
        {
            OnStateChanged?.Invoke(entered, previous);
            switch (entered)
            {
                case RallyState.PreServe:
                    _log = new RallyLog { ServedByHome = _serving == TeamSide.Home };
                    _logClosed = false;
                    _plan = FlightPlan.None;
                    _blockDecided = false;
                    _ballRest = CourtSlots.ServeLaunch(_serving, 2.0f, _teamSize);
                    break;

                case RallyState.PointResolved:
                    CloseRally();
                    break;

                case RallyState.MatchEnd:
                    _result.HomeWon = _result.HomeScore > _result.AwayScore;
                    Done = true;
                    break;
            }
        }

        private void CloseRally()
        {
            if (_logClosed) return;
            _logClosed = true;
            _plan = FlightPlan.None;

            if (_log.WonByHome) _result.HomeScore++; else _result.AwayScore++;
            _result.Rallies.Add(_log);
            OnRallyEnded?.Invoke(_log);

            // Standard sideout: winner serves next; a team rotates when it GAINS serve.
            TeamSide winner = _log.WonByHome ? TeamSide.Home : TeamSide.Away;
            if (winner != _serving)
            {
                _rotation[(int)winner] = (_rotation[(int)winner] + 1) % _teamSize;
                _serving = winner;
            }
        }

        private bool IsMatchOver()
        {
            int target = (int)_config.Format;
            int hi = Math.Max(_result.HomeScore, _result.AwayScore);
            return hi >= target && Math.Abs(_result.HomeScore - _result.AwayScore) >= 2; // win by 2 [structural §1.1]
        }

        // ---- serve ---------------------------------------------------------------------------------

        private void DoServeContact()
        {
            var server = AtPosition(_serving, 1);
            var tier = Team(_serving).Tier;
            bool jumpServe = tier == DifficultyTier.Hard; // §6.3 Hard row [tunable v0]
            ZoneId target = ChooseServeTarget(_serving);

            _machine.Fire(RallyTrigger.ServeReleased); // T3 → ServeContact (instantaneous)

            TimingGrade grade = SampleGrade(_serving);
            float statC = StatC(server, StatId.Serve, StatId.Power);
            float q = QualityMath.Quality(_res, grade, statC);
            if (jumpServe) q = Math.Min(1f, q + 0.15f); // §4.2 [tunable]
            RecordContact(grade, q, _serving);

            Vec3 launch = CourtSlots.ServeLaunch(_serving, jumpServe ? 2.1f : 2.0f, _teamSize);
            Vec3 end = AimPoint(Other(_serving), target, q);
            TrajectoryParams p = jumpServe
                ? _ball.ServeJump(launch, end)
                : _ball.ServeFloat(launch, end, RallyRng.NextFloat01() * 6.2831853f); // §2.3 ⚄

            BeginFlight(FlightPlan.Serve, p);
            _pendingOption = default;
            _pendingServeTarget = target;
            _pendingServeQuality = q;
            _pendingServeGrade = grade;

            _machine.Fire(RallyTrigger.TrajectoryAuthored); // T4 → BallInFlight
        }

        private ZoneId _pendingServeTarget;
        private float _pendingServeQuality;
        private TimingGrade _pendingServeGrade;

        // ---- set → attack ----------------------------------------------------------------------------

        private void DoSetChoice()
        {
            SetOption option = ChooseSetOption(_possession, _receiveGrade, _log.Contacts);
            var setter = AtPosition(_possession, 2); // [tunable v0: setter plays right-front]
            TimingGrade grade = SampleGrade(_possession);
            if (Cascade.CapsSetGradeAtGood(_receiveGrade) && grade > TimingGrade.Good)
                grade = TimingGrade.Good; // §3.4 Shank cap
            float q = QualityMath.Quality(_res, grade, setter.Stats.Normalized(StatId.Technique));
            RecordContact(grade, q, _possession);

            // §3.5: a Miss set is a free ball over — author it and flip possession at landing.
            if (!Cascade.TryGetSpikeWindowCtx(_cascade, grade, out _))
            {
                _machine.Fire(RallyTrigger.LaneChosen); // T8 → BallInFlight
                BeginFreeBall(_possession, CourtSlots.Position(_possession, 2, _teamSize));
                return;
            }

            _pendingOption = option;
            Vec3 setterPos = CourtSlots.Position(_possession, 2, _teamSize);
            var attackerSlot = AttackerSlot(_possession, option);
            Vec3 start = new Vec3(setterPos.X, 1.8f, setterPos.Z);
            Vec3 end = new Vec3(attackerSlot.X, 2.0f, attackerSlot.Z);
            TrajectoryParams p = option == SetOption.QuickMiddle ? _ball.SetQuick(start, end) : _ball.SetHigh(start, end);

            _machine.Fire(RallyTrigger.LaneChosen); // T8 → BallInFlight; T9 fires next tick
            BeginFlight(FlightPlan.SetToAttack, p);
        }

        private void AdvanceApproach()
        {
            _arcTick++;

            if (!_blockDecided && _arcTick >= (int)(_arc.DurationTicks * BlockDecisionFraction))
            {
                _blockDecided = true;
                _machine.Fire(RallyTrigger.DefenseCommitted); // T11 (parallel)
                _blockState = ChooseBlock(Other(_possession), _pendingOption);
            }

            if (_arcTick >= _arc.DurationTicks)
                DoAttackContact(); // apex reached
        }

        private void DoAttackContact()
        {
            _machine.Fire(RallyTrigger.AttackTapped); // T10 → AttackContact (instantaneous)

            var attacker = AtPosition(_possession, Formation.AttackerFor(_teamSize, _pendingOption));
            TimingGrade grade = SampleGrade(_possession);
            float q = QualityMath.Quality(_res, grade, StatC(attacker, StatId.Power, StatId.Jump));
            RecordContact(grade, q, _possession);

            if (grade == TimingGrade.Miss)
            {
                // §3.6 step 1: whiffed apex — free ball over.
                BeginFreeBall(_possession, AttackerSlot(_possession, _pendingOption));
                _machine.Fire(RallyTrigger.TrajectoryAuthored); // T12 → BallInFlight
                return;
            }

            ZoneId aim = ChooseSpikeAim(_possession, _pendingOption, _log.Contacts);
            var slot = AttackerSlot(_possession, _pendingOption);
            Vec3 start = new Vec3(slot.X, _ball.SpikeContactHeight(attacker.Stats.Normalized(StatId.Jump)), slot.Z);
            Vec3 end = AimPoint(Other(_possession), aim, q);

            // Pre-evaluate §3.6 (pure, deterministic); reveal at the physically-correct tick.
            var digger = ReceiverFor(Other(_possession), aim);
            _pendingDigGrade = SampleGrade(Other(_possession));
            _pendingDigQuality = QualityMath.Quality(_res, _pendingDigGrade, StatC(digger, StatId.Receive, StatId.Speed));
            _pendingRes = PointResolution.ResolveAttack(_point, _res, q, grade, aim, _blockState, _pendingDigQuality);

            BeginFlight(FlightPlan.Spike, _ball.Spike(start, end, q));
            _machine.Fire(RallyTrigger.TrajectoryAuthored); // T12 → BallInFlight (block resolves with it)
        }

        // ---- flight ------------------------------------------------------------------------------------

        private void BeginFlight(FlightPlan plan, TrajectoryParams p)
        {
            _plan = plan;
            _arc = new Trajectory(p);
            _arcTick = 0;
            _ruling = _arc.RuleNetInteraction(_ball);
            var crossing = _arc.FindNetCrossing();
            _crossingTick = crossing.Crosses
                ? Math.Max(1, (int)(crossing.U * _arc.DurationTicks))
                : -1;
        }

        private void BeginFreeBall(TeamSide from, Vec3 fromSlot)
        {
            var p = _ball.FreeBall(new Vec3(fromSlot.X, 1.8f, fromSlot.Z), CourtSlots.MidCourt(from));
            BeginFlight(FlightPlan.FreeBall, p);
        }

        private void AdvanceFlight()
        {
            if (_plan == FlightPlan.None) return;
            _arcTick++;

            if (_crossingTick >= 0 && _arcTick == _crossingTick)
            {
                OnNetCross();
                if (_logClosed || _plan == FlightPlan.None) return;
            }

            if (_arcTick >= _arc.DurationTicks)
                OnLanding();
        }

        private void OnNetCross()
        {
            switch (_plan)
            {
                case FlightPlan.Serve:
                case FlightPlan.FreeBall:
                    if (_ruling == NetRuling.NetFault || _ruling == NetRuling.AntennaOut
                        || (_ruling == NetRuling.NetCord && RallyRng.NextFloat01() < 0.5f)) // §2.4 drama ⚄
                    {
                        TerminalFromFlight(_ruling == NetRuling.AntennaOut ? RallyOutcome.Out : RallyOutcome.Net,
                            winner: Other(ThrowerOf(_plan)), errorBy: ThrowerOf(_plan));
                        return;
                    }
                    _machine.Fire(RallyTrigger.BallCrossedNet); // T5 → ReceiveWindow (ball keeps flying)
                    break;

                case FlightPlan.Spike:
                    // Arc-level faults first (§2.4), then the precomputed §3.6 reveals that live at the net.
                    if (_ruling == NetRuling.NetFault || _ruling == NetRuling.AntennaOut
                        || (_ruling == NetRuling.NetCord && RallyRng.NextFloat01() < 0.5f)) // ⚄
                    {
                        TerminalFromFlight(_ruling == NetRuling.AntennaOut ? RallyOutcome.Out : RallyOutcome.Net,
                            winner: Other(_possession), errorBy: _possession);
                        return;
                    }
                    switch (_pendingRes.Outcome)
                    {
                        case AttackOutcome.Net:
                            TerminalFromFlight(RallyOutcome.Net, winner: Other(_possession), errorBy: _possession);
                            break;
                        case AttackOutcome.Blocked:
                            _hype.Apply(HypeEvent.StuffBlock, Other(_possession));
                            TerminalFromFlight(RallyOutcome.Blocked, winner: Other(_possession));
                            break;
                        case AttackOutcome.Tooled:
                            _hype.Apply(HypeEvent.Kill, _possession);
                            TerminalFromFlight(RallyOutcome.Tooled, winner: _possession);
                            break;
                        case AttackOutcome.Dug:
                        case AttackOutcome.Kill:
                            _machine.Fire(RallyTrigger.SpikePlayable); // T13 → DigWindow
                            break;
                            // AttackOutcome.Out reveals at landing (the ball lands out).
                    }
                    break;
            }
        }

        private void OnLanding()
        {
            switch (_plan)
            {
                case FlightPlan.Serve:
                    EvaluateServeReceive();
                    break;

                case FlightPlan.FreeBall:
                    EvaluateFreeBallReceive();
                    break;

                case FlightPlan.Spike:
                    switch (_pendingRes.Outcome)
                    {
                        case AttackOutcome.Out:
                            TerminalFromFlight(RallyOutcome.Out, winner: Other(_possession), errorBy: _possession);
                            break;

                        case AttackOutcome.Dug:
                            RecordContact(_pendingDigGrade, _pendingDigQuality, Other(_possession));
                            if (_pendingRes.EffectiveAttack >= _hypeT.BigSpikeAThreshold)
                                _hype.Apply(HypeEvent.BigSpikeDug, Other(_possession)); // §3.7 [v0: judged on A_eff]
                            _machine.Fire(RallyTrigger.DigSucceeded); // T14 → SetSelect (cascade repeats)
                            _possession = Other(_possession);
                            _receiveGrade = _pendingRes.DigDisplayGrade;
                            _plan = FlightPlan.None;
                            _ballRest = CourtSlots.Position(_possession, 2, _teamSize);
                            break;

                        case AttackOutcome.Kill:
                        default:
                            RecordContact(_pendingDigGrade, _pendingDigQuality, Other(_possession));
                            _hype.Apply(HypeEvent.Kill, _possession);
                            _log.Outcome = RallyOutcome.Kill;
                            _log.WonByHome = _possession == TeamSide.Home;
                            _machine.Fire(RallyTrigger.TerminalOutcome); // T15 from DigWindow
                            break;
                    }
                    break;
            }
        }

        private void EvaluateServeReceive()
        {
            TeamSide receiving = Other(_serving);
            var receiver = ReceiverFor(receiving, _pendingServeTarget);
            TimingGrade grade = SampleGrade(receiving);
            float q = QualityMath.Quality(_res, grade, StatC(receiver, StatId.Receive, StatId.Speed));
            RecordContact(grade, q, receiving);

            var res = PointResolution.ResolveServe(_point, _res, _pendingServeQuality, _pendingServeGrade, _pendingServeTarget, q);
            var display = QualityMath.ReceiveGradeOf(_res, q);
            bool unplayable = display == ReceiveGrade.Shank && q < _res.ShankPlayableMin;

            if (res.Outcome == AttackOutcome.Kill || unplayable)
            {
                _hype.Apply(HypeEvent.Ace, _serving);
                _log.Outcome = RallyOutcome.Kill;
                _log.WonByHome = _serving == TeamSide.Home;
                _machine.Fire(RallyTrigger.ReceiveFailed); // T7 — ace
                return;
            }
            if (res.Outcome == AttackOutcome.Net || res.Outcome == AttackOutcome.Out)
            {
                _hype.Apply(HypeEvent.OwnError, _serving);
                _log.Outcome = res.Outcome == AttackOutcome.Net ? RallyOutcome.Net : RallyOutcome.Out;
                _log.WonByHome = receiving == TeamSide.Home;
                _machine.Fire(RallyTrigger.ReceiveFailed); // service error revealed on receipt
                return;
            }

            _machine.Fire(RallyTrigger.ReceiveSucceeded); // T6 → SetSelect
            _possession = receiving;
            _receiveGrade = display;
            _plan = FlightPlan.None;
            _ballRest = CourtSlots.Position(receiving, 2, _teamSize);
        }

        private void EvaluateFreeBallReceive()
        {
            TeamSide receiving = Other(ThrowerOf(FlightPlan.FreeBall));
            var receiver = AtPosition(receiving, Formation.FreeBallReceiver(_teamSize)); // free balls target mid-court (§2.3)
            TimingGrade grade = SampleGrade(receiving);
            float q = QualityMath.Quality(_res, grade, StatC(receiver, StatId.Receive, StatId.Speed));
            RecordContact(grade, q, receiving);

            var display = QualityMath.ReceiveGradeOf(_res, q);
            if (display == ReceiveGrade.Shank && q < _res.ShankPlayableMin)
            {
                _hype.Apply(HypeEvent.Kill, Other(receiving));
                _log.Outcome = RallyOutcome.Kill;
                _log.WonByHome = Other(receiving) == TeamSide.Home;
                _machine.Fire(RallyTrigger.ReceiveFailed); // T7
                return;
            }
            if (display < ReceiveGrade.S) display = (ReceiveGrade)((int)display + 1); // +1 bonus [structural §1.2]

            _machine.Fire(RallyTrigger.ReceiveSucceeded);
            _possession = receiving;
            _receiveGrade = display;
            _plan = FlightPlan.None;
            _ballRest = CourtSlots.Position(receiving, 2, _teamSize);
        }

        private void TerminalFromFlight(RallyOutcome outcome, TeamSide winner, TeamSide? errorBy = null)
        {
            if (errorBy.HasValue) _hype.Apply(HypeEvent.OwnError, errorBy.Value); // §3.7
            _log.Outcome = outcome;
            _log.WonByHome = winner == TeamSide.Home;
            _machine.Fire(RallyTrigger.TerminalOutcome); // T15
        }

        /// <summary>Which team sent the current serve/free-ball flight.</summary>
        private TeamSide ThrowerOf(FlightPlan plan)
            => plan == FlightPlan.Serve ? _serving : _possession;

        // ---- AI decision helpers (unchanged logic from the headless engine) ---------------------------

        private TeamSpec Team(TeamSide side) => side == TeamSide.Home ? _config.Home : _config.Away;
        private static TeamSide Other(TeamSide s) => s == TeamSide.Home ? TeamSide.Away : TeamSide.Home;
        private IRng AiRng => _rng.Get(RngStream.Ai);
        private IRng RallyRng => _rng.Get(RngStream.Rally);

        private PlayerSpec AtPosition(TeamSide side, int courtPosition)
        {
            int idx = (courtPosition - 1 + _rotation[(int)side]) % _teamSize;
            return Team(side).Players[idx];
        }

        private float StatC(in PlayerSpec p, StatId a, StatId b)
            => 0.5f * (p.Stats.Normalized(a) + p.Stats.Normalized(b));

        private TimingGrade SampleGrade(TeamSide side)
        {
            var team = Team(side);
            var grade = team.GradeOverride.HasValue
                ? GradeSampler.Sample(team.GradeOverride.Value, AiRng) // skill proxy (tooling §2) ⚄
                : GradeSampler.Sample(_ai, team.Tier, AiRng);          // §6.2 ⚄
            if (grade == TimingGrade.Perfect) _hype.Apply(HypeEvent.PerfectContact, side); // §3.7
            return grade;
        }

        private void RecordContact(TimingGrade grade, float quality, TeamSide side)
        {
            _log.Contacts++;
            if (_log.Contacts > MaxContactsPerRally)
                throw new InvalidOperationException("Runaway rally — resolution math cannot terminate.");
            _log.Grades.Add(grade);
            _log.Qualities.Add(quality);
            if (_log.Contacts > _hypeT.LongRallyThreshold) // §3.7: per extra contact, both teams
            {
                _hype.Apply(HypeEvent.LongRallyContact, TeamSide.Home);
                _hype.Apply(HypeEvent.LongRallyContact, TeamSide.Away);
            }
            _decisionsPerSide++;
            _ = side;
        }

        private Vec3 AimPoint(TeamSide targetTeam, ZoneId zone, float quality)
        {
            var side = CourtSlots.SideOf(targetTeam);
            Vec3 c = CourtGeometry.CenterOf(zone, side);
            // §4.1: bad contacts drift toward court center, never out [tunable].
            float drift = Math.Min(1f, (1f - quality) * 1.5f / 4.5f);
            float cz = side == CourtSide.PositiveZ ? 4.5f : -4.5f;
            return new Vec3(c.X + (4.5f - c.X) * drift, 0f, c.Z + (cz - c.Z) * drift);
        }

        private ZoneId ChooseServeTarget(TeamSide serving)
        {
            var tier = Team(serving).Tier;
            if (tier == DifficultyTier.Easy) return ZoneId.z_CM; // §6.3

            if (tier == DifficultyTier.Hard)
            {
                TeamSide recv = Other(serving);
                ZoneId best = ZoneId.z_CB;
                int weakest = int.MaxValue;
                Span<int> positions = stackalloc int[3];
                Span<ZoneId> zones = stackalloc ZoneId[3];
                Formation.WeakReceiverScan(_teamSize, positions, zones);
                for (int i = 0; i < 3; i++)
                {
                    int r = AtPosition(recv, positions[i]).Stats.Raw(StatId.Receive);
                    if (r < weakest) { weakest = r; best = zones[i]; }
                }
                return best;
            }

            Span<ZoneId> candidates = stackalloc ZoneId[] { ZoneId.z_CM, ZoneId.z_LB, ZoneId.z_RB, ZoneId.z_CB };
            var inputs = new UtilityInputs[4];
            for (int i = 0; i < 4; i++)
                inputs[i] = new UtilityInputs
                {
                    Matchup = _point.RiskOf(candidates[i]),
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
                    Matchup = 0.5f,
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


        private Vec3 AttackerSlot(TeamSide side, SetOption option)
            => CourtSlots.Position(side, Formation.AttackerFor(_teamSize, option), _teamSize);

        private PlayerSpec ReceiverFor(TeamSide side, ZoneId landing)
        {
            // [tunable v0: coverage by attacker-view landing column, per formation]
            return AtPosition(side, Formation.ReceiverForColumn(_teamSize, PointResolution.ZoneColumn(landing)));
        }

        private BlockState ChooseBlock(TeamSide defending, SetOption option)
        {
            int lane = option == SetOption.HighOutside ? 0 : 1; // [tunable v0]
            var tier = Team(defending).Tier;
            var blocker = AtPosition(defending, Formation.BlockerFor(_teamSize, lane));
            TimingGrade g = SampleGrade(defending);
            float qb = QualityMath.Quality(_res, g, StatC(blocker, StatId.Jump, StatId.Technique));

            switch (TacticVocabulary.BlockBehaviorFor(tier)) // §6.3
            {
                case BlockBehavior.ReadOnlyCenter:
                    return new BlockState(BlockCommit.Read, 1, qb);
                case BlockBehavior.ReadWithOccasionalCommit:
                    if (AiRng.NextFloat01() < 0.2f) // ⚄ [tunable v0]
                        return new BlockState(BlockCommit.Early, lane, qb);
                    return new BlockState(BlockCommit.Read, lane, qb);
                default:
                    float roll = AiRng.NextFloat01(); // ⚄
                    if (roll < 0.4f) return new BlockState(BlockCommit.Early, lane, qb);
                    if (roll < 0.55f) return new BlockState(BlockCommit.Early, Math.Min(2, lane + 1), qb);
                    return new BlockState(BlockCommit.Read, lane, qb);
            }
        }

        private ZoneId ChooseSpikeAim(TeamSide side, SetOption option, int rallyContacts)
        {
            int lane = option == SetOption.HighOutside ? 0 : 1;
            Span<ZoneId> shots = stackalloc ZoneId[] // §4.3 [structural]
            {
                (ZoneId)(6 + lane),
                lane == 0 ? ZoneId.z_RB : ZoneId.z_LB,
                ZoneId.z_CM,
                (ZoneId)lane,
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
    }
}
