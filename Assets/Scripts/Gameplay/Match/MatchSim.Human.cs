using System;
using System.Collections.Generic;
using VG.Data;
using VG.Gameplay.Ball;
using VG.Gameplay.Hype;
using VG.Gameplay.Input;
using VG.Gameplay.Rally;
using VG.Gameplay.Resolution;

namespace VG.Gameplay.Match
{
    /// <summary>
    /// VB-13: the human-controlled side. One side of the sim consumes quantized
    /// <see cref="PlayerInput"/> events instead of AI decisions — live play and replays feed the
    /// identical stream, so (seed + input log) ⇒ bit-identical MatchResult [structural, §4.3].
    ///
    /// Human contacts are graded through REAL timing windows (ContactWindows §3.1) with the
    /// §3.5 set-grade→spike-window ctx and §7.4 assist applied — everything §6.4 forbids for AI.
    /// v1 scope: serve, receive, set choice, spike, dig. Blocking stays AI-driven
    /// [v0: block UX needs VB-14's visual read].
    ///
    /// No-input fallbacks are the spec's own timeouts: T3 auto-serve (grade-capped Good),
    /// T8 auto high-outside set (capped Good), no receive commit = ace (§3.3),
    /// apex passed without a tap = Miss free ball (T10).
    /// </summary>
    public sealed partial class MatchSim
    {
        private const int ServeMeterPeriodTicks = 90;   // 1.5 s meter cycle [tunable v0]
        private const int ServeMeterSweetTick = 45;     // sweet spot at cycle midpoint [tunable v0]
        private const float MsPerTick = 1000f / 60f;

        /// <summary>Which side is player-controlled; null = AI vs AI.</summary>
        public TeamSide? HumanSide { get; }

        /// <summary>§7.4 assist level 0/1/2 → window widen +0% / +25% / +50%. Human windows only [structural].</summary>
        public int AssistLevel = 0;

        private readonly List<PlayerInput> _inputs = new List<PlayerInput>(64);

        // per-window human state (reset on state entry)
        private int _tick;                    // absolute sim tick
        private int _windowAnchorTick;        // entry tick of the current input-bearing state
        private int _approachApexTick;        // absolute tick of the spike apex
        private bool _receiveCommitted;
        private int _receiveTapTick = -1;
        private bool _spikeTapTaken;
        private TimingGrade _humanSpikeGrade;
        private SpikeShot _humanSpikeShot;
        private TimingGrade _lastSetGrade = TimingGrade.Great; // feeds §3.5 ctx for the HUMAN spike

        /// <summary>Current absolute sim tick — the input layer stamps gestures with this.</summary>
        public int SimTick => _tick;

        /// <summary>Queue a quantized input (live play or replay preload). Order-stable per tick.</summary>
        public void SubmitInput(in PlayerInput input) => _inputs.Add(input);

        public void SubmitInputs(IEnumerable<PlayerInput> inputs)
        {
            foreach (var i in inputs) _inputs.Add(i);
        }

        private bool IsHuman(TeamSide side) => HumanSide.HasValue && HumanSide.Value == side;

        /// <summary>First queued input of a kind with Tick ≤ now — §7.2 "first qualifying gesture wins".</summary>
        private bool TryConsume(PlayerInputKind kind, out PlayerInput input)
        {
            for (int i = 0; i < _inputs.Count; i++)
            {
                if (_inputs[i].Kind != kind || _inputs[i].Tick > _tick) continue;
                input = _inputs[i];
                _inputs.RemoveAt(i);
                return true;
            }
            input = default;
            return false;
        }

        private float AssistMultiplier =>
            AssistLevel <= 0 ? 1f : 1f + (AssistLevel == 1 ? _res.AssistWidenLevel1 : _res.AssistWidenLevel2);

        private void ApplyGradeHype(TimingGrade grade, TeamSide side)
        {
            if (grade == TimingGrade.Perfect) _hype.Apply(HypeEvent.PerfectContact, side); // §3.7
        }

        private TimingGrade ClassifyHuman(ContactType contact, float statC, float deltaMs, float ctx)
        {
            float window = ContactWindows.WindowMs(_res, contact, statC, ctx, AssistMultiplier);
            return ContactWindows.Classify(_res, deltaMs, window);
        }

        // ---- serve (hold-release meter, §4.2) -------------------------------------------------

        /// <summary>Human ServeAim: consume a ServeRelease; grade = meter distance to the sweet tick.</summary>
        private bool HumanTryServe()
        {
            if (!TryConsume(PlayerInputKind.ServeRelease, out var input)) return false;

            var server = AtPosition(_serving, Formation.Server(_teamSize));
            int heldTicks = Math.Max(0, input.Tick - _windowAnchorTick);
            int phase = heldTicks % ServeMeterPeriodTicks;
            float deltaMs = Math.Abs(phase - ServeMeterSweetTick) * MsPerTick;

            float statC = StatC(server, StatId.Serve, StatId.Power);
            // ctx = 1.0 at v0: aim snaps to zone centers, all ≥1 m from lines (§4.2 shrink inert until VB-14's free reticle).
            TimingGrade grade = ClassifyHuman(ContactType.Serve, statC, deltaMs, 1f);
            ApplyGradeHype(grade, _serving);
            float q = QualityMath.Quality(_res, grade, statC);

            ZoneId target = (ZoneId)Math.Clamp(input.B, 0, 8);
            _machine.Fire(RallyTrigger.ServeReleased); // T3 → ServeContact
            AuthorServe(grade, q, target, jump: false); // [v0: human float serve; jump toggle = VB-14 UI]
            return true;
        }

        // ---- receive / dig (timing taps, §3.1/§7.2) --------------------------------------------

        /// <summary>Pump receive-window inputs each tick (commit + first timing tap).</summary>
        private void HumanPumpReceiveInputs()
        {
            if (!_receiveCommitted && TryConsume(PlayerInputKind.ReceiveCommit, out _))
                _receiveCommitted = true;
            if (_receiveTapTick < 0 && TryConsume(PlayerInputKind.TimingTap, out var tap))
                _receiveTapTick = tap.Tick; // one evaluation per window (§7.2)
        }

        /// <summary>Human serve-receive at ball arrival. Returns the receive quality, or −1 for an ace (no commit / unplayable).</summary>
        private float HumanEvaluateReceive(in PlayerSpec receiver, int arrivalTick, out TimingGrade grade)
        {
            if (!_receiveCommitted) // §3.3: no commit at all ⇒ ace
            {
                grade = TimingGrade.Miss;
                return -1f;
            }

            float statC = StatC(receiver, StatId.Receive, StatId.Speed);
            if (_receiveTapTick < 0)
            {
                grade = TimingGrade.Miss; // committed but never tapped ⇒ quality = floor(stats) (§3.3)
            }
            else
            {
                float deltaMs = Math.Abs(_receiveTapTick - arrivalTick) * MsPerTick;
                grade = ClassifyHuman(ContactType.Receive, statC, deltaMs, 1f);
            }
            ApplyGradeHype(grade, HumanSide.Value);
            return QualityMath.Quality(_res, grade, statC);
        }

        /// <summary>Deferred §3.6 step 7 for a human defender (dig quality unknown until the tap).</summary>
        private void HumanResolveDigAtLanding()
        {
            var digger = ReceiverFor(HumanSide.Value, _pendingAim);
            float statC = StatC(digger, StatId.Receive, StatId.Speed);

            TimingGrade grade;
            if (_receiveTapTick < 0)
            {
                grade = TimingGrade.Miss;
            }
            else
            {
                float deltaMs = Math.Abs(_receiveTapTick - _tick) * MsPerTick; // called AT the landing tick
                grade = ClassifyHuman(ContactType.Dig, statC, deltaMs, 1f);
            }
            ApplyGradeHype(grade, HumanSide.Value);
            float qd = QualityMath.Quality(_res, grade, statC);

            float required = PointResolution.DigRequirement(_point, _pendingRes.EffectiveAttack);
            RecordContact(grade, qd, HumanSide.Value);

            if (qd >= required)
            {
                if (_pendingRes.EffectiveAttack >= _hypeT.BigSpikeAThreshold)
                    _hype.Apply(HypeEvent.BigSpikeDug, HumanSide.Value);
                _machine.Fire(RallyTrigger.DigSucceeded); // T14
                _possession = HumanSide.Value;
                _receiveGrade = QualityMath.ReceiveGradeOf(_res, qd - required);
                _plan = FlightPlan.None;
                _ballRest = CourtSlots.Position(_possession, 2, _teamSize);
            }
            else
            {
                _hype.Apply(HypeEvent.Kill, Other(HumanSide.Value));
                _log.Outcome = RallyOutcome.Kill;
                _log.WonByHome = Other(HumanSide.Value) == TeamSide.Home;
                _machine.Fire(RallyTrigger.TerminalOutcome); // T15 from DigWindow
            }
        }

        // ---- set choice (§3.4 gating + decision-speed grade) --------------------------------------

        /// <summary>Human SetSelect: consume a SetChoice; grade by decision speed [tunable v0].</summary>
        private bool HumanTrySetChoice()
        {
            if (!TryConsume(PlayerInputKind.SetChoice, out var input)) return false;

            var option = (SetOption)Math.Clamp(input.A, 0, 3);
            if (!Cascade.IsSetOptionAvailable(_receiveGrade, option))
                option = SetOption.HighOutside; // illegal pick degrades to the always-lit option (§3.4)

            int decisionTicks = Math.Max(0, input.Tick - _windowAnchorTick);
            TimingGrade grade = decisionTicks <= 15 ? TimingGrade.Perfect
                              : decisionTicks <= 30 ? TimingGrade.Great
                              : TimingGrade.Good;   // [tunable v0: court-vision speed = set grade]
            if (Cascade.CapsSetGradeAtGood(_receiveGrade) && grade > TimingGrade.Good)
                grade = TimingGrade.Good;
            ApplyGradeHype(grade, HumanSide.Value);

            AuthorSet(option, grade);
            _machine.Fire(RallyTrigger.LaneChosen); // T8 → BallInFlight
            return true;
        }

        // ---- spike (apex tap + swipe shot, §4.3) ----------------------------------------------------

        /// <summary>Pump the spike tap during AttackApproach; grade vs the apex tick with §3.5 ctx.</summary>
        private void HumanPumpSpikeTap()
        {
            if (_spikeTapTaken) return;
            if (!TryConsume(PlayerInputKind.SpikeTap, out var input)) return;

            _spikeTapTaken = true;
            var attacker = AtPosition(_possession, Formation.AttackerFor(_teamSize, _pendingOption));
            float statC = StatC(attacker, StatId.Power, StatId.Jump);
            Cascade.TryGetSpikeWindowCtx(_cascade, _lastSetGrade, out float ctx); // §3.5 bites for HUMANS
            float deltaMs = Math.Abs(input.Tick - _approachApexTick) * MsPerTick;
            _humanSpikeGrade = ClassifyHuman(ContactType.Spike, statC, deltaMs, ctx <= 0f ? 1f : ctx);
            ApplyGradeHype(_humanSpikeGrade, _possession);
            _humanSpikeShot = (SpikeShot)Math.Clamp(input.A, 0, 3);
        }

        /// <summary>Reset per-window human state on rally-state entry.</summary>
        private void HumanOnStateEntered(RallyState entered)
        {
            _windowAnchorTick = _tick;
            switch (entered)
            {
                case RallyState.ReceiveWindow:
                case RallyState.DigWindow:
                    _receiveCommitted = false;
                    _receiveTapTick = -1;
                    _arcAnchor = _arcTick;
                    break;
                case RallyState.AttackApproach:
                    _spikeTapTaken = false;
                    _approachApexTick = _tick + (_arc.DurationTicks - _arcTick);
                    break;
            }
        }

        private int _arcAnchor; // arc tick at window entry (dig timing anchor)
    }
}
